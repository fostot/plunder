using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using TerrariaModder.Core.Logging;

namespace Plunder
{
    /// <summary>
    /// MapReveal module - reveals the entire world map by removing fog of war.
    /// Uses WorldMap.Update(x, y, 255) — the same proven approach as HEROsMod.
    /// Update() internally reads Main.tile[x,y], creates a proper MapTile with
    /// type/wall/color data, AND sets it into the WorldMap. One call does it all.
    /// Chunked reveal (columns per frame) avoids freezes.
    /// </summary>
    public static class MapReveal
    {
        private static ILogger _log;
        private static Harmony _harmony;
        private static Timer _patchTimer;
        private static readonly object _patchLock = new object();
        private static bool _patchesApplied;

        private static bool _active;
        public static bool IsActive => _active;

        // Chunked reveal state
        private static int _revealColumn;
        private static bool _revealComplete;
        private static int _frameCounter;
        private const int ColumnsPerFrame = 100;
        private const int RefreshInterval = 600;   // Re-reveal every ~10 sec

        // Reflection cache — core
        private static Type _mainType;
        private static Type _playerType;
        private static FieldInfo _myPlayerField;
        private static FieldInfo _gameMenuField;
        private static FieldInfo _maxTilesXField;
        private static FieldInfo _maxTilesYField;
        private static FieldInfo _refreshMapField;

        // WorldMap access
        private static FieldInfo _mapField;
        private static PropertyInfo _mapProperty;
        private static MethodInfo _mapUpdateMethod;

        // Fast delegate for WorldMap.Update — avoids per-tile reflection overhead
        private delegate void MapUpdateDelegate(int x, int y, byte light);
        private static MapUpdateDelegate _mapUpdate;
        private static object _mapObject;

        private static bool _reflectionReady;

        public static void Initialize(ILogger log, bool defaultState)
        {
            _log = log;
            _active = defaultState;
            _harmony = new Harmony("com.plunder.mapreveal");
            _patchTimer = new Timer(ApplyPatches, null, 5000, Timeout.Infinite);
            _log.Info($"MapReveal initialized (default: {(_active ? "ON" : "OFF")})");
        }

        public static void Toggle()
        {
            _active = !_active;
            if (_active)
            {
                _revealColumn = 0;
                _revealComplete = false;
                _mapObject = null;
                _mapUpdate = null;
            }
            _log.Info($"MapReveal: {(_active ? "ON" : "OFF")}");
            ShowMessage("Full Map Reveal " + (_active ? "Enabled" : "Disabled"), _active);
        }

        public static void SetActive(bool state)
        {
            if (_active != state) Toggle();
        }

        public static void EnsurePatched()
        {
            if (!_patchesApplied)
                ApplyPatches(null);
        }

        public static void Unload()
        {
            _patchTimer?.Dispose();
            _patchTimer = null;
            _harmony?.UnpatchAll("com.plunder.mapreveal");
            _patchesApplied = false;
            _active = false;
            _revealComplete = false;
            _mapObject = null;
            _mapUpdate = null;
            _log?.Info("MapReveal unloaded");
        }

        private static void ApplyPatches(object state)
        {
            lock (_patchLock)
            {
                if (_patchesApplied) return;
                _patchesApplied = true;
            }

            if (_harmony == null) return;

            try
            {
                InitReflection();
                if (!_reflectionReady)
                {
                    _log.Error("MapReveal: Reflection init failed");
                    return;
                }

                var updateMethod = _playerType.GetMethod("Update",
                    BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(int) }, null);

                if (updateMethod != null)
                {
                    var postfix = typeof(MapReveal).GetMethod(nameof(PlayerUpdate_Postfix),
                        BindingFlags.NonPublic | BindingFlags.Static);
                    _harmony.Patch(updateMethod, postfix: new HarmonyMethod(postfix));
                    _log.Info("MapReveal: Patched Player.Update");
                }
                else
                {
                    _log.Warn("MapReveal: Could not find Player.Update(int)");
                }
            }
            catch (Exception ex)
            {
                _log.Error($"MapReveal: Patch error - {ex.Message}");
            }
        }

        private static void InitReflection()
        {
            if (_reflectionReady) return;

            try
            {
                var asm = Assembly.Load("Terraria");

                _mainType = Type.GetType("Terraria.Main, Terraria")
                    ?? asm.GetType("Terraria.Main");
                _playerType = Type.GetType("Terraria.Player, Terraria")
                    ?? asm.GetType("Terraria.Player");

                if (_mainType == null || _playerType == null)
                {
                    _log.Error("MapReveal: Core types not found");
                    return;
                }

                var pubStatic = BindingFlags.Public | BindingFlags.Static;

                _myPlayerField = _mainType.GetField("myPlayer", pubStatic);
                _gameMenuField = _mainType.GetField("gameMenu", pubStatic);
                _maxTilesXField = _mainType.GetField("maxTilesX", pubStatic);
                _maxTilesYField = _mainType.GetField("maxTilesY", pubStatic);
                _refreshMapField = _mainType.GetField("refreshMap", pubStatic);

                // Get Main.Map (WorldMap)
                _mapField = _mainType.GetField("Map", pubStatic);
                if (_mapField == null)
                    _mapProperty = _mainType.GetProperty("Map", pubStatic);

                // Find WorldMap.Update(int, int, byte) — this is what HEROsMod uses
                var mapType = asm.GetType("Terraria.Map.WorldMap");
                if (mapType != null)
                {
                    _mapUpdateMethod = mapType.GetMethod("Update",
                        BindingFlags.Public | BindingFlags.Instance,
                        null, new[] { typeof(int), typeof(int), typeof(byte) }, null);

                    if (_mapUpdateMethod != null)
                        _log.Info("MapReveal: Found WorldMap.Update(int, int, byte)");
                    else
                        _log.Error("MapReveal: WorldMap.Update method not found");
                }
                else
                {
                    _log.Error("MapReveal: WorldMap type not found");
                }

                if (_mapUpdateMethod == null) return;

                _reflectionReady = true;
                _log.Info("MapReveal: Reflection ready (HERO approach)");
            }
            catch (Exception ex)
            {
                _log.Error($"MapReveal: Reflection error - {ex.Message}");
            }
        }

        /// <summary>
        /// Get the WorldMap object and create a fast delegate for Update calls.
        /// Returns true if ready to reveal.
        /// </summary>
        private static bool EnsureDelegate()
        {
            if (_mapUpdate != null) return true;

            try
            {
                _mapObject = _mapField != null
                    ? _mapField.GetValue(null)
                    : _mapProperty?.GetValue(null);

                if (_mapObject == null)
                {
                    _log.Error("MapReveal: Main.Map is null");
                    return false;
                }

                // Create a bound delegate — as fast as a direct call, no reflection overhead
                _mapUpdate = (MapUpdateDelegate)Delegate.CreateDelegate(
                    typeof(MapUpdateDelegate), _mapObject, _mapUpdateMethod);

                _log.Info($"MapReveal: Delegate bound to WorldMap.Update ({_mapObject.GetType().Name})");
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"MapReveal: Delegate creation failed: {ex.Message}");
                _log.Error($"MapReveal: Stack: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Postfix on Player.Update(int i). Reveals a chunk of the map each frame.
        /// </summary>
        private static void PlayerUpdate_Postfix(object __instance, int i)
        {
            if (!_active || !_reflectionReady) return;

            try
            {
                if (_gameMenuField != null && (bool)_gameMenuField.GetValue(null)) return;
                int myPlayer = (int)_myPlayerField.GetValue(null);
                if (i != myPlayer) return;

                _frameCounter++;

                // After full reveal, re-reveal periodically to catch newly explored areas
                if (_revealComplete)
                {
                    if (_frameCounter % RefreshInterval != 0) return;
                    _revealColumn = 0;
                    _revealComplete = false;
                }

                if (!EnsureDelegate()) return;

                int maxX = (int)_maxTilesXField.GetValue(null);
                int maxY = (int)_maxTilesYField.GetValue(null);

                // Safety margin: GetBackgroundType reads neighboring tiles (i±1, j±1)
                // so we must stay away from world edges to avoid IndexOutOfRangeException
                const int margin = 5;
                int safeMaxX = maxX - margin;
                int safeMaxY = maxY - margin;

                int startX = Math.Max(_revealColumn, margin);
                int endCol = Math.Min(startX + ColumnsPerFrame, safeMaxX);

                // WorldMap.Update(x, y, 255) internally calls CreateMapTile which calls
                // GetBackgroundType — that method reads neighbor tiles so we wrap
                // each column in try/catch to silently skip any problem tiles.
                for (int x = startX; x < endCol; x++)
                {
                    try
                    {
                        for (int y = margin; y < safeMaxY; y++)
                        {
                            _mapUpdate(x, y, 255);
                        }
                    }
                    catch
                    {
                        // Skip this column — edge tile or corrupted area
                    }
                }

                _revealColumn = endCol;

                if (_revealColumn >= safeMaxX && !_revealComplete)
                {
                    _revealComplete = true;
                    _refreshMapField?.SetValue(null, true);
                    _log.Info("MapReveal: Full map revealed");
                }
            }
            catch (Exception ex)
            {
                var inner = (ex is TargetInvocationException tie) ? tie.InnerException : ex;
                _log.Error($"MapReveal: Frame error - {inner?.GetType().Name}: {inner?.Message ?? ex.Message}");
                _log.Error($"MapReveal: Stack: {inner?.StackTrace ?? ex.StackTrace}");
            }
        }

        private static void ShowMessage(string msg, bool enabled)
        {
            try
            {
                if (_mainType == null)
                {
                    var asm = Assembly.Load("Terraria");
                    _mainType = asm.GetType("Terraria.Main");
                }

                var newTextMethod = _mainType?.GetMethod("NewText",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(byte), typeof(byte), typeof(byte) },
                    null);

                if (newTextMethod != null)
                {
                    byte r = (byte)(enabled ? 100 : 200);
                    byte g = (byte)(enabled ? 255 : 200);
                    byte b = (byte)(enabled ? 100 : 200);
                    newTextMethod.Invoke(null, new object[] { msg, r, g, b });
                }
            }
            catch { }
        }
    }
}
