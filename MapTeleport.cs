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
    ///
    /// Coordinate math matches HEROsMod's Teleporter.cs exactly:
    ///   offset = (rawMouse - rawScreenCenter) / mapFullscreenScale
    ///   tilePos = mapFullscreenPos + offset
    ///   worldPos = tilePos * 16
    /// Uses PlayerInput raw values to bypass SetZoom_World transforms.
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

        /// <summary>Toggle verbose debug logging via in-game chat + log file.</summary>
        public static bool DebugMode { get; set; }

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

        // Raw mouse/screen from PlayerInput — bypasses SetZoom_World transforms
        private static Type _playerInputType;
        private static PropertyInfo _piMouseXProp;          // PlayerInput.MouseX
        private static PropertyInfo _piMouseYProp;          // PlayerInput.MouseY
        private static FieldInfo _piMouseXField;            // fallback: field
        private static FieldInfo _piMouseYField;
        // Original screen dimensions (before zoom transforms)
        private static PropertyInfo _piOrigScreenWProp;
        private static PropertyInfo _piOrigScreenHProp;
        private static FieldInfo _piOrigScreenWField;
        private static FieldInfo _piOrigScreenHField;

        // World bounds
        private static FieldInfo _maxTilesXField;           // Main.maxTilesX (int)
        private static FieldInfo _maxTilesYField;           // Main.maxTilesY (int)

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
            DebugMode = false;
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

        public static void ToggleDebug()
        {
            DebugMode = !DebugMode;
            _log.Info($"MapTeleport debug: {(DebugMode ? "ON" : "OFF")}");
            ShowMessage("MapTeleport Debug " + (DebugMode ? "ON" : "OFF"), DebugMode);
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
                var allStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

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

                // PlayerInput — raw mouse & screen values (bypasses SetZoom_World)
                // HEROsMod calls SetZoom_Unscaled() which sets Main.mouseX = PlayerInput.MouseX
                // and Main.screenWidth = OriginalScreenWidth. We read these directly since
                // we're in Update phase where SetZoom_World has already transformed Main.mouseX.
                _playerInputType = Type.GetType("Terraria.GameInput.PlayerInput, Terraria")
                    ?? asm.GetType("Terraria.GameInput.PlayerInput");

                if (_playerInputType != null)
                {
                    _piMouseXProp = _playerInputType.GetProperty("MouseX", allStatic);
                    _piMouseYProp = _playerInputType.GetProperty("MouseY", allStatic);
                    if (_piMouseXProp == null)
                        _piMouseXField = _playerInputType.GetField("MouseX", allStatic);
                    if (_piMouseYProp == null)
                        _piMouseYField = _playerInputType.GetField("MouseY", allStatic);

                    _piOrigScreenWProp = _playerInputType.GetProperty("OriginalScreenWidth", allStatic);
                    _piOrigScreenHProp = _playerInputType.GetProperty("OriginalScreenHeight", allStatic);
                    if (_piOrigScreenWProp == null)
                        _piOrigScreenWField = _playerInputType.GetField("OriginalScreenWidth", allStatic);
                    if (_piOrigScreenHProp == null)
                        _piOrigScreenHField = _playerInputType.GetField("OriginalScreenHeight", allStatic);

                    bool hasRawMouse = _piMouseXProp != null || _piMouseXField != null;
                    bool hasRawScreen = _piOrigScreenWProp != null || _piOrigScreenWField != null;
                    _log.Info($"MapTeleport: PlayerInput found — rawMouse={hasRawMouse}, rawScreen={hasRawScreen}");
                }
                else
                {
                    _log.Warn("MapTeleport: PlayerInput type not found — will use Main.mouseX/screenWidth (may be zoomed)");
                }

                // World bounds
                _maxTilesXField = _mainType.GetField("maxTilesX", pubStatic);
                _maxTilesYField = _mainType.GetField("maxTilesY", pubStatic);

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

        /// <summary>Read raw mouse X from PlayerInput (bypasses SetZoom_World).</summary>
        private static int GetRawMouseX(int fallback)
        {
            try
            {
                if (_piMouseXProp != null) return (int)_piMouseXProp.GetValue(null, null);
                if (_piMouseXField != null) return (int)_piMouseXField.GetValue(null);
            }
            catch { }
            return fallback;
        }

        /// <summary>Read raw mouse Y from PlayerInput (bypasses SetZoom_World).</summary>
        private static int GetRawMouseY(int fallback)
        {
            try
            {
                if (_piMouseYProp != null) return (int)_piMouseYProp.GetValue(null, null);
                if (_piMouseYField != null) return (int)_piMouseYField.GetValue(null);
            }
            catch { }
            return fallback;
        }

        /// <summary>Read original screen width from PlayerInput (before zoom transforms).</summary>
        private static int GetOrigScreenWidth(int fallback)
        {
            try
            {
                if (_piOrigScreenWProp != null) return (int)_piOrigScreenWProp.GetValue(null, null);
                if (_piOrigScreenWField != null) return (int)_piOrigScreenWField.GetValue(null);
            }
            catch { }
            return fallback;
        }

        /// <summary>Read original screen height from PlayerInput (before zoom transforms).</summary>
        private static int GetOrigScreenHeight(int fallback)
        {
            try
            {
                if (_piOrigScreenHProp != null) return (int)_piOrigScreenHProp.GetValue(null, null);
                if (_piOrigScreenHField != null) return (int)_piOrigScreenHField.GetValue(null);
            }
            catch { }
            return fallback;
        }

        /// <summary>
        /// Postfix on Player.Update(int i). Detects right-click on the fullscreen
        /// world map and teleports the player to the clicked location.
        ///
        /// Coordinate conversion matches HEROsMod Teleporter.cs exactly.
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

                // ─── Read all inputs ───────────────────────────────────
                object mapPos = _mapFullscreenPosField.GetValue(null);
                float mapCenterX = (float)_vec2XField.GetValue(mapPos);
                float mapCenterY = (float)_vec2YField.GetValue(mapPos);
                float mapScale = (float)_mapFullscreenScaleField.GetValue(null);

                if (mapScale <= 0f) return;

                // During Update phase, Main.mouseX/screenWidth have been
                // transformed by SetZoom_World. HEROsMod avoids this by
                // running in PostDrawFullscreenMap and calling SetZoom_Unscaled()
                // which resets to raw values. We read the raw values directly
                // from PlayerInput to match HEROsMod's coordinate space.
                int mainMouseX = (int)_mouseXField.GetValue(null);
                int mainMouseY = (int)_mouseYField.GetValue(null);
                int mainScreenW = (int)_screenWidthField.GetValue(null);
                int mainScreenH = (int)_screenHeightField.GetValue(null);

                int rawMouseX = GetRawMouseX(mainMouseX);
                int rawMouseY = GetRawMouseY(mainMouseY);
                int rawScreenW = GetOrigScreenWidth(mainScreenW);
                int rawScreenH = GetOrigScreenHeight(mainScreenH);

                // World bounds (for clamping)
                int maxTilesX = _maxTilesXField != null ? (int)_maxTilesXField.GetValue(null) : 8400;
                int maxTilesY = _maxTilesYField != null ? (int)_maxTilesYField.GetValue(null) : 2400;
                int mapWidth = maxTilesX * 16;
                int mapHeight = maxTilesY * 16;

                // Player dimensions
                var players = (Array)_playerArrayField.GetValue(null);
                var player = players.GetValue(myPlayer);
                int pw = 20, ph = 42;
                if (_widthField != null) pw = (int)_widthField.GetValue(player);
                if (_heightField != null) ph = (int)_heightField.GetValue(player);

                // Current player position (for debug)
                object curPos = _positionField.GetValue(player);
                float curPosX = (float)_vec2XField.GetValue(curPos);
                float curPosY = (float)_vec2YField.GetValue(curPos);

                // ─── Coordinate conversion ─────────────────────────────
                // EXACT HEROsMod formula (Teleporter.cs lines 104-117):
                //   cursorPosition = (rawMouse - rawScreenCenter)
                //   cursorPosition /= 16
                //   cursorPosition *= 16 / mapFullscreenScale
                //   worldPos = (mapFullscreenPos + cursorPosition) * 16
                //
                // Simplified: tileOffset = (rawMouse - rawScreenCenter) / mapScale
                //             worldPos = (mapCenter + tileOffset) * 16

                float offsetX = rawMouseX - rawScreenW / 2f;
                float offsetY = rawMouseY - rawScreenH / 2f;

                float tileOffsetX = offsetX / mapScale;
                float tileOffsetY = offsetY / mapScale;

                float tileX = mapCenterX + tileOffsetX;
                float tileY = mapCenterY + tileOffsetY;

                // Convert tiles to world pixels
                float worldX = tileX * 16f;
                float worldY = tileY * 16f;

                // Position player: feet at click point (matching HEROsMod)
                float targetX = worldX;
                float targetY = worldY - ph;

                // Clamp to world bounds (matching HEROsMod)
                if (targetX < 0) targetX = 0;
                else if (targetX + pw > mapWidth) targetX = mapWidth - pw;
                if (targetY < 0) targetY = 0;
                else if (targetY + ph > mapHeight) targetY = mapHeight - ph;

                // ─── Debug output ──────────────────────────────────────
                if (DebugMode)
                {
                    _log.Info($"[MapTP Debug] rawMouse({rawMouseX},{rawMouseY}) mainMouse({mainMouseX},{mainMouseY}) rawScreen({rawScreenW},{rawScreenH}) mainScreen({mainScreenW},{mainScreenH})");
                    _log.Info($"[MapTP Debug] mapCenter({mapCenterX:F1},{mapCenterY:F1}) mapScale={mapScale:F3}");
                    _log.Info($"[MapTP Debug] offset({offsetX:F1},{offsetY:F1}) tileOff({tileOffsetX:F1},{tileOffsetY:F1})");
                    _log.Info($"[MapTP Debug] tile({tileX:F1},{tileY:F1}) target({targetX:F0},{targetY:F0}) was({curPosX:F0},{curPosY:F0}) delta({targetX - curPosX:F0},{targetY - curPosY:F0})");

                    // In-game chat — compare raw vs Main to show zoom transform effect
                    ShowDebug($"MapTP: rawMouse({rawMouseX},{rawMouseY}) mainMouse({mainMouseX},{mainMouseY})");
                    ShowDebug($"MapTP: rawScreen({rawScreenW},{rawScreenH}) mainScreen({mainScreenW},{mainScreenH})");
                    ShowDebug($"MapTP: mapScale={mapScale:F3} offset({offsetX:F1},{offsetY:F1})");
                    ShowDebug($"MapTP: tileOff({tileOffsetX:F2},{tileOffsetY:F2}) tile({tileX:F1},{tileY:F1})");
                    ShowDebug($"MapTP: was({curPosX:F0},{curPosY:F0}) delta({targetX - curPosX:F0},{targetY - curPosY:F0})");
                }

                // ─── Execute teleport ──────────────────────────────────
                // Close the fullscreen map first
                _mapFullscreenField.SetValue(null, false);

                // Direct position set (same approach as TeleportToCursor, proven to work)
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

                if (!DebugMode)
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

        private static void ShowDebug(string msg)
        {
            try
            {
                var newTextMethod = _mainType?.GetMethod("NewText",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(byte), typeof(byte), typeof(byte) },
                    null);

                newTextMethod?.Invoke(null, new object[] { msg, (byte)200, (byte)200, (byte)255 });
            }
            catch { }
        }
    }
}
