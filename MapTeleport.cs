using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using TerrariaModder.Core.Logging;

namespace Plunder
{
    /// <summary>
    /// MapTeleport module - right-click on the fullscreen world map to teleport there.
    /// Uses Player.Update postfix to detect right-click while map is open,
    /// converts map screen coordinates to world position, and teleports.
    /// </summary>
    public static class MapTeleport
    {
        private static ILogger _log;
        private static Harmony _harmony;
        private static Timer _patchTimer;
        private static readonly object _patchLock = new object();
        private static bool _patchesApplied;

        private static bool _active;
        public static bool IsActive => _active;

        // Right-click tracking (detect "just pressed")
        private static bool _prevRightMouse;

        // Reflection cache
        private static Type _mainType;
        private static Type _playerType;
        private static FieldInfo _myPlayerField;
        private static FieldInfo _playerArrayField;
        private static FieldInfo _gameMenuField;

        // Map state
        private static FieldInfo _mapFullscreenField;       // Main.mapFullscreen (bool)
        private static FieldInfo _mapFullscreenPosField;    // Main.mapFullscreenPos (Vector2)
        private static FieldInfo _mapFullscreenScaleField;  // Main.mapFullscreenScale (float)

        // Screen / mouse
        private static FieldInfo _screenWidthField;         // Main.screenWidth (int)
        private static FieldInfo _screenHeightField;        // Main.screenHeight (int)
        private static FieldInfo _mouseXField;              // Main.mouseX (int)
        private static FieldInfo _mouseYField;              // Main.mouseY (int)
        private static FieldInfo _mouseRightField;          // Main.mouseRight (bool)

        // Vector2
        private static FieldInfo _vec2XField;
        private static FieldInfo _vec2YField;
        private static Type _vector2Type;

        // Player teleport
        private static MethodInfo _teleportMethod;
        private static FieldInfo _positionField;
        private static FieldInfo _velocityField;
        private static FieldInfo _fallStartField;
        private static FieldInfo _fallStart2Field;
        private static FieldInfo _widthField;
        private static FieldInfo _heightField;

        private static bool _reflectionReady;

        public static void Initialize(ILogger log, bool defaultState)
        {
            _log = log;
            _active = defaultState;
            _harmony = new Harmony("com.plunder.mapteleport");
            _patchTimer = new Timer(ApplyPatches, null, 5000, Timeout.Infinite);
            _log.Info($"MapTeleport initialized (default: {(_active ? "ON" : "OFF")})");
        }

        public static void Toggle()
        {
            _active = !_active;
            _log.Info($"MapTeleport: {(_active ? "ON" : "OFF")}");
            ShowMessage("Map Click Teleport " + (_active ? "Enabled" : "Disabled"), _active);
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
            _harmony?.UnpatchAll("com.plunder.mapteleport");
            _patchesApplied = false;
            _active = false;
            _log?.Info("MapTeleport unloaded");
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
                    _log.Error("MapTeleport: Reflection init failed");
                    return;
                }

                // Patch Player.Update(int) — same proven pattern
                var updateMethod = _playerType.GetMethod("Update",
                    BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(int) }, null);

                if (updateMethod != null)
                {
                    var postfix = typeof(MapTeleport).GetMethod(nameof(PlayerUpdate_Postfix),
                        BindingFlags.NonPublic | BindingFlags.Static);
                    _harmony.Patch(updateMethod, postfix: new HarmonyMethod(postfix));
                    _log.Info("MapTeleport: Patched Player.Update (map right-click teleport)");
                }
                else
                {
                    _log.Warn("MapTeleport: Could not find Player.Update(int)");
                }
            }
            catch (Exception ex)
            {
                _log.Error($"MapTeleport: Patch error - {ex.Message}");
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

                var entityType = Type.GetType("Terraria.Entity, Terraria")
                    ?? asm.GetType("Terraria.Entity");

                if (_mainType == null || _playerType == null)
                {
                    _log.Error("MapTeleport: Core types not found");
                    return;
                }

                var pubStatic = BindingFlags.Public | BindingFlags.Static;
                var pubInst = BindingFlags.Public | BindingFlags.Instance;

                // Main static fields
                _myPlayerField = _mainType.GetField("myPlayer", pubStatic);
                _playerArrayField = _mainType.GetField("player", pubStatic);
                _gameMenuField = _mainType.GetField("gameMenu", pubStatic);

                // Map fields
                _mapFullscreenField = _mainType.GetField("mapFullscreen", pubStatic);
                _mapFullscreenPosField = _mainType.GetField("mapFullscreenPos", pubStatic);
                _mapFullscreenScaleField = _mainType.GetField("mapFullscreenScale", pubStatic);

                // Screen / mouse
                _screenWidthField = _mainType.GetField("screenWidth", pubStatic);
                _screenHeightField = _mainType.GetField("screenHeight", pubStatic);
                _mouseXField = _mainType.GetField("mouseX", pubStatic);
                _mouseYField = _mainType.GetField("mouseY", pubStatic);
                _mouseRightField = _mainType.GetField("mouseRight", pubStatic);

                if (_mapFullscreenField == null || _mapFullscreenPosField == null ||
                    _mapFullscreenScaleField == null)
                {
                    _log.Error("MapTeleport: Map fields not found");
                    return;
                }

                // Entity.position, Entity.velocity
                var searchType = entityType ?? _playerType;
                _positionField = searchType.GetField("position", pubInst);
                _velocityField = searchType.GetField("velocity", pubInst);

                // Player.fallStart, fallStart2
                _fallStartField = _playerType.GetField("fallStart", pubInst);
                _fallStart2Field = _playerType.GetField("fallStart2", pubInst);

                // Entity.width, Entity.height
                _widthField = searchType.GetField("width", pubInst);
                _heightField = searchType.GetField("height", pubInst);

                // Vector2
                var vec2Type = _positionField?.FieldType;
                _vector2Type = vec2Type;
                if (vec2Type != null)
                {
                    _vec2XField = vec2Type.GetField("X", pubInst);
                    _vec2YField = vec2Type.GetField("Y", pubInst);
                }

                if (_vec2XField == null || _vec2YField == null)
                {
                    _log.Error("MapTeleport: Vector2 fields not found");
                    return;
                }

                // Player.Teleport(Vector2, int, int)
                if (vec2Type != null)
                {
                    _teleportMethod = _playerType.GetMethod("Teleport",
                        pubInst, null, new[] { vec2Type, typeof(int), typeof(int) }, null);

                    if (_teleportMethod == null)
                        _teleportMethod = _playerType.GetMethod("Teleport", pubInst);
                }

                _reflectionReady = true;
                _log.Info("MapTeleport: Reflection initialized");
            }
            catch (Exception ex)
            {
                _log.Error($"MapTeleport: Reflection error - {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix on Player.Update(int i). Detects right-click on the fullscreen
        /// world map and teleports the player to the clicked location.
        /// </summary>
        private static void PlayerUpdate_Postfix(object __instance, int i)
        {
            if (!_active || !_reflectionReady) return;

            try
            {
                // Only run for local player
                if (_gameMenuField != null && (bool)_gameMenuField.GetValue(null)) return;
                int myPlayer = (int)_myPlayerField.GetValue(null);
                if (i != myPlayer) return;

                // Check if fullscreen map is open
                bool mapOpen = (bool)_mapFullscreenField.GetValue(null);
                if (!mapOpen)
                {
                    _prevRightMouse = false;
                    return;
                }

                // Detect right-click "just pressed" (was up, now down)
                bool rightMouse = (bool)_mouseRightField.GetValue(null);
                bool justPressed = rightMouse && !_prevRightMouse;
                _prevRightMouse = rightMouse;

                if (!justPressed) return;

                // Read map transform: center position (tile coords) and zoom scale
                object mapPos = _mapFullscreenPosField.GetValue(null);
                float mapCenterX = (float)_vec2XField.GetValue(mapPos);
                float mapCenterY = (float)_vec2YField.GetValue(mapPos);
                float mapScale = (float)_mapFullscreenScaleField.GetValue(null);

                if (mapScale <= 0f) return; // Safety

                // Read screen and mouse
                int screenW = (int)_screenWidthField.GetValue(null);
                int screenH = (int)_screenHeightField.GetValue(null);
                int mouseX = (int)_mouseXField.GetValue(null);
                int mouseY = (int)_mouseYField.GetValue(null);

                // Convert screen mouse position to tile coordinates
                // Map renders: screenX = (tileX - mapCenterX) * scale + screenW/2
                // Reverse: tileX = mapCenterX + (mouseX - screenW/2) / scale
                float tileX = mapCenterX + (mouseX - screenW / 2f) / mapScale;
                float tileY = mapCenterY + (mouseY - screenH / 2f) / mapScale;

                // Convert tiles to world pixels (each tile = 16x16 pixels)
                float worldX = tileX * 16f;
                float worldY = tileY * 16f;

                // Get player for dimensions
                var players = (Array)_playerArrayField.GetValue(null);
                var player = players.GetValue(myPlayer);

                int pw = 20, ph = 42;
                if (_widthField != null) pw = (int)_widthField.GetValue(player);
                if (_heightField != null) ph = (int)_heightField.GetValue(player);

                // Center player on the clicked world position
                float targetX = worldX - (pw * 0.5f);
                float targetY = worldY - (ph * 0.5f);

                // Close the fullscreen map
                _mapFullscreenField.SetValue(null, false);

                // Direct position set — same approach as TeleportToCursor (proven to work).
                // Player.Teleport() applies unwanted style adjustments, so we skip it.
                object position = _positionField.GetValue(player);
                _vec2XField.SetValue(position, targetX);
                _vec2YField.SetValue(position, targetY);
                _positionField.SetValue(player, position);

                // Zero velocity
                object velocity = _velocityField.GetValue(player);
                _vec2XField.SetValue(velocity, 0f);
                _vec2YField.SetValue(velocity, 0f);
                _velocityField.SetValue(player, velocity);

                // Reset fall damage
                int tileYPos = (int)(targetY / 16f);
                if (_fallStartField != null) _fallStartField.SetValue(player, tileYPos);
                if (_fallStart2Field != null) _fallStart2Field.SetValue(player, tileYPos);

                _log.Info($"MapTeleport: Teleported to tile ({tileX:F1}, {tileY:F1}) world ({targetX:F0}, {targetY:F0})");
            }
            catch (Exception ex)
            {
                _log.Error($"MapTeleport: Frame error - {ex.Message}");
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
                    byte b = (byte)(enabled ? 200 : 200);
                    newTextMethod.Invoke(null, new object[] { msg, r, g, b });
                }
            }
            catch { }
        }
    }
}
