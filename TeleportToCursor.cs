using System;
using System.Reflection;
using TerrariaModder.Core.Logging;

namespace Plunder
{
    /// <summary>
    /// Teleport To Cursor module - teleports the player to the mouse cursor position.
    /// Toggle on/off and use keybind to teleport when enabled.
    /// </summary>
    public static class TeleportToCursor
    {
        private static ILogger _log;
        private static bool _active;
        public static bool IsActive => _active;

        // Reflection cache
        private static Type _mainType;
        private static Type _playerType;
        private static FieldInfo _myPlayerField;
        private static FieldInfo _playerArrayField;
        private static FieldInfo _gameMenuField;
        private static FieldInfo _mouseWorldField; // Main.MouseWorld (Vector2) — fallback only
        private static FieldInfo _positionField;   // Entity.position (Vector2)
        private static FieldInfo _velocityField;   // Entity.velocity (Vector2)
        private static FieldInfo _fallStartField;  // Player.fallStart
        private static FieldInfo _fallStart2Field; // Player.fallStart2
        private static FieldInfo _widthField;      // Entity.width (int)
        private static FieldInfo _heightField;     // Entity.height (int)
        private static FieldInfo _vec2XField;
        private static FieldInfo _vec2YField;
        private static PropertyInfo _mouseWorldProp;
        private static MethodInfo _teleportMethod; // Player.Teleport(Vector2, int, int)
        private static Type _vector2Type;

        // Screen / mouse — raw values bypassing SetZoom_World (same fix as MapTeleport)
        private static FieldInfo _screenPositionField;    // Main.screenPosition (Vector2)
        private static FieldInfo _screenWidthField;       // Main.screenWidth (int)
        private static FieldInfo _screenHeightField;      // Main.screenHeight (int)
        private static Type _playerInputType;
        private static PropertyInfo _piMouseXProp;        // PlayerInput.MouseX
        private static PropertyInfo _piMouseYProp;        // PlayerInput.MouseY
        private static FieldInfo _piMouseXField;
        private static FieldInfo _piMouseYField;
        private static PropertyInfo _piOrigScreenWProp;   // PlayerInput.OriginalScreenWidth
        private static PropertyInfo _piOrigScreenHProp;   // PlayerInput.OriginalScreenHeight
        private static FieldInfo _piOrigScreenWField;
        private static FieldInfo _piOrigScreenHField;

        // Zoom — needed when keybind fires before SetZoom_World adjusts Main fields
        private static FieldInfo _gameZoomTargetField;    // Main.GameZoomTarget (float)
        private static FieldInfo _forcedMinZoomField;     // Main.ForcedMinimumZoom (float)

        private static bool _reflectionReady;

        public static void Initialize(ILogger log, bool defaultState)
        {
            _log = log;
            _active = defaultState;
            _log.Info($"TeleportToCursor initialized (default: {(_active ? "ON" : "OFF")})");
        }

        public static void Toggle()
        {
            _active = !_active;
            _log.Info($"TeleportToCursor: {(_active ? "ON" : "OFF")}");
            ShowMessage("Teleport To Cursor " + (_active ? "Enabled" : "Disabled"), _active);
        }

        public static void SetActive(bool state)
        {
            if (_active != state) Toggle();
        }

        /// <summary>
        /// Actually perform the teleport (called by keybind).
        /// Only works when the feature is toggled on.
        /// Uses Rod of Discord style positioning: player's feet land at cursor.
        /// </summary>
        public static void Teleport()
        {
            if (!_active) return;

            if (!EnsureReflection())
            {
                _log.Error("TeleportToCursor: Reflection not ready");
                return;
            }

            try
            {
                // Check not on menu
                if (_gameMenuField != null && (bool)_gameMenuField.GetValue(null))
                    return;

                int myPlayer = (int)_myPlayerField.GetValue(null);
                var players = (Array)_playerArrayField.GetValue(null);
                var player = players.GetValue(myPlayer);

                // Compute mouse world position from raw PlayerInput values
                // to bypass SetZoom_World transforms.
                //
                // Two cases depending on when the keybind fires in the frame:
                // 1) AFTER SetZoom_World: curScreenW < origScreenW, screenPos is zoom-adjusted
                //    → ratio formula works: worldPos = screenPos + rawMouse * (curScreenW / origScreenW)
                // 2) BEFORE SetZoom_World: curScreenW == origScreenW, screenPos is NOT zoom-adjusted
                //    → must manually account for game zoom to get correct world position
                float mouseX, mouseY;
                if (_screenPositionField != null && _screenWidthField != null)
                {
                    object screenPos = _screenPositionField.GetValue(null);
                    float screenPosX = (float)_vec2XField.GetValue(screenPos);
                    float screenPosY = (float)_vec2YField.GetValue(screenPos);

                    int curScreenW = (int)_screenWidthField.GetValue(null);
                    int curScreenH = (int)_screenHeightField.GetValue(null);
                    int rawMouseX = GetRawMouseX(0);
                    int rawMouseY = GetRawMouseY(0);
                    int origScreenW = GetOrigScreenWidth(curScreenW);
                    int origScreenH = GetOrigScreenHeight(curScreenH);

                    if (curScreenW < origScreenW)
                    {
                        // SetZoom_World already applied — screenPos and screenWidth are zoom-adjusted.
                        // Standard formula: worldPos = screenPos + rawMouse * (screenW / origScreenW)
                        mouseX = screenPosX + rawMouseX * ((float)curScreenW / origScreenW);
                        mouseY = screenPosY + rawMouseY * ((float)curScreenH / origScreenH);
                    }
                    else
                    {
                        // SetZoom_World NOT applied — screenWidth == origScreenWidth.
                        // Must manually account for game zoom.
                        float zoom = GetGameZoom();
                        if (zoom <= 0f) zoom = 1f;

                        // Visible world area with zoom (zoom > 1 means you see less)
                        float visibleW = origScreenW / zoom;
                        float visibleH = origScreenH / zoom;

                        // Zoom centers the view — adjust screenPosition accordingly
                        float adjPosX = screenPosX + (origScreenW - visibleW) * 0.5f;
                        float adjPosY = screenPosY + (origScreenH - visibleH) * 0.5f;

                        // Map raw mouse pixel to world position within the visible area
                        mouseX = adjPosX + rawMouseX * (visibleW / origScreenW);
                        mouseY = adjPosY + rawMouseY * (visibleH / origScreenH);
                    }
                }
                else
                {
                    // Fallback to Main.MouseWorld if raw values unavailable
                    object mouseWorld = _mouseWorldProp?.GetValue(null)
                        ?? _mouseWorldField?.GetValue(null);
                    if (mouseWorld == null)
                    {
                        _log.Warn("TeleportToCursor: Could not get mouse world position");
                        return;
                    }
                    mouseX = (float)_vec2XField.GetValue(mouseWorld);
                    mouseY = (float)_vec2YField.GetValue(mouseWorld);
                }

                // Get actual player dimensions (default 20x42 if reflection fails)
                int pw = 20, ph = 42;
                if (_widthField != null) pw = (int)_widthField.GetValue(player);
                if (_heightField != null) ph = (int)_heightField.GetValue(player);

                // Rod of Discord style: center horizontally, feet at cursor Y
                // This places the player ON TOP of blocks, not embedded inside them
                float targetX = mouseX - (pw * 0.5f);
                float targetY = mouseY - ph;

                // Use Player.Teleport() for proper handling (same as AdminPanel mod)
                if (_teleportMethod != null && _vector2Type != null)
                {
                    var targetPos = Activator.CreateInstance(_vector2Type,
                        new object[] { targetX, targetY });
                    var parms = _teleportMethod.GetParameters();

                    if (parms.Length == 1)
                        _teleportMethod.Invoke(player, new[] { targetPos });
                    else if (parms.Length == 2)
                        _teleportMethod.Invoke(player, new object[] { targetPos, 0 });
                    else if (parms.Length >= 3)
                        _teleportMethod.Invoke(player, new object[] { targetPos, 0, 0 });
                }
                else
                {
                    // Fallback: direct position set
                    object position = _positionField.GetValue(player);
                    _vec2XField.SetValue(position, targetX);
                    _vec2YField.SetValue(position, targetY);
                    _positionField.SetValue(player, position);
                }

                // Zero velocity to prevent weird physics after teleport
                object velocity = _velocityField.GetValue(player);
                _vec2XField.SetValue(velocity, 0f);
                _vec2YField.SetValue(velocity, 0f);
                _velocityField.SetValue(player, velocity);

                // Reset fall damage tracking
                int tileY = (int)(targetY / 16f);
                if (_fallStartField != null)
                    _fallStartField.SetValue(player, tileY);
                if (_fallStart2Field != null)
                    _fallStart2Field.SetValue(player, tileY);

                _log.Info($"Teleported to ({mouseX:F0}, {mouseY:F0})");
            }
            catch (Exception ex)
            {
                _log.Error($"TeleportToCursor: Teleport failed - {ex.Message}");
            }
        }

        public static void Unload()
        {
            _active = false;
            _log?.Info("TeleportToCursor unloaded");
        }

        private static bool EnsureReflection()
        {
            if (_reflectionReady) return true;

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
                    _log.Error("TeleportToCursor: Core types not found");
                    return false;
                }

                var pubStatic = BindingFlags.Public | BindingFlags.Static;
                var allStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

                _myPlayerField = _mainType.GetField("myPlayer", pubStatic);
                _playerArrayField = _mainType.GetField("player", pubStatic);
                _gameMenuField = _mainType.GetField("gameMenu", pubStatic);

                // Main.screenPosition, screenWidth, screenHeight — for raw coordinate math
                _screenPositionField = _mainType.GetField("screenPosition", pubStatic);
                _screenWidthField = _mainType.GetField("screenWidth", pubStatic);
                _screenHeightField = _mainType.GetField("screenHeight", pubStatic);

                // PlayerInput — raw mouse & screen values (bypasses SetZoom_World)
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
                    _log.Info($"TeleportToCursor: PlayerInput found — rawMouse={hasRawMouse}, rawScreen={hasRawScreen}");
                }
                else
                {
                    _log.Warn("TeleportToCursor: PlayerInput type not found — will use Main.MouseWorld (may be zoomed)");
                }

                // Main.GameZoomTarget / ForcedMinimumZoom — needed when keybind fires
                // before SetZoom_World adjusts Main.screenWidth/screenPosition
                _gameZoomTargetField = _mainType.GetField("GameZoomTarget", pubStatic);
                _forcedMinZoomField = _mainType.GetField("ForcedMinimumZoom", pubStatic);
                _log.Info($"TeleportToCursor: Zoom fields — GameZoomTarget={_gameZoomTargetField != null}, ForcedMinimumZoom={_forcedMinZoomField != null}");

                // Main.MouseWorld — fallback only if PlayerInput unavailable
                _mouseWorldProp = _mainType.GetProperty("MouseWorld", pubStatic);
                if (_mouseWorldProp == null)
                    _mouseWorldField = _mainType.GetField("MouseWorld", pubStatic);

                // Entity.position, Entity.velocity (Vector2 structs)
                var searchType = entityType ?? _playerType;
                _positionField = searchType.GetField("position",
                    BindingFlags.Public | BindingFlags.Instance);
                _velocityField = searchType.GetField("velocity",
                    BindingFlags.Public | BindingFlags.Instance);

                // Player.fallStart, fallStart2
                _fallStartField = _playerType.GetField("fallStart",
                    BindingFlags.Public | BindingFlags.Instance);
                _fallStart2Field = _playerType.GetField("fallStart2",
                    BindingFlags.Public | BindingFlags.Instance);

                // Entity.width, Entity.height (for proper teleport offset)
                _widthField = searchType.GetField("width",
                    BindingFlags.Public | BindingFlags.Instance);
                _heightField = searchType.GetField("height",
                    BindingFlags.Public | BindingFlags.Instance);

                // Vector2.X, Vector2.Y
                var vec2Type = _positionField?.FieldType;
                _vector2Type = vec2Type;
                if (vec2Type != null)
                {
                    _vec2XField = vec2Type.GetField("X",
                        BindingFlags.Public | BindingFlags.Instance);
                    _vec2YField = vec2Type.GetField("Y",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                if (_vec2XField == null || _vec2YField == null)
                {
                    _log.Error("TeleportToCursor: Vector2 fields not found");
                    return false;
                }

                // Player.Teleport(Vector2, int, int) — proper teleport with sync
                if (vec2Type != null)
                {
                    _teleportMethod = _playerType.GetMethod("Teleport",
                        BindingFlags.Public | BindingFlags.Instance,
                        null, new[] { vec2Type, typeof(int), typeof(int) }, null);

                    // Fallback: try any Teleport overload
                    if (_teleportMethod == null)
                        _teleportMethod = _playerType.GetMethod("Teleport",
                            BindingFlags.Public | BindingFlags.Instance);
                }

                _reflectionReady = true;
                _log.Info("TeleportToCursor: Reflection initialized");
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"TeleportToCursor: Reflection error - {ex.Message}");
                return false;
            }
        }

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
        /// Get the effective game zoom factor. Uses max(GameZoomTarget, ForcedMinimumZoom)
        /// to match what SetZoom_World applies.
        /// </summary>
        private static float GetGameZoom()
        {
            float zoom = 1f;
            float forced = 1f;
            try
            {
                if (_gameZoomTargetField != null)
                    zoom = (float)_gameZoomTargetField.GetValue(null);
                if (_forcedMinZoomField != null)
                    forced = (float)_forcedMinZoomField.GetValue(null);
            }
            catch { }
            return Math.Max(zoom, forced);
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
