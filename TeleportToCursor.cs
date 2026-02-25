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
        private static FieldInfo _mouseWorldField; // Main.MouseWorld (Vector2)
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

                // Get mouse world position
                object mouseWorld = _mouseWorldProp?.GetValue(null)
                    ?? _mouseWorldField?.GetValue(null);

                if (mouseWorld == null)
                {
                    _log.Warn("TeleportToCursor: Could not get mouse world position");
                    return;
                }

                float mouseX = (float)_vec2XField.GetValue(mouseWorld);
                float mouseY = (float)_vec2YField.GetValue(mouseWorld);

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

                _myPlayerField = _mainType.GetField("myPlayer",
                    BindingFlags.Public | BindingFlags.Static);
                _playerArrayField = _mainType.GetField("player",
                    BindingFlags.Public | BindingFlags.Static);
                _gameMenuField = _mainType.GetField("gameMenu",
                    BindingFlags.Public | BindingFlags.Static);

                // Main.MouseWorld - could be property or field
                _mouseWorldProp = _mainType.GetProperty("MouseWorld",
                    BindingFlags.Public | BindingFlags.Static);
                if (_mouseWorldProp == null)
                    _mouseWorldField = _mainType.GetField("MouseWorld",
                        BindingFlags.Public | BindingFlags.Static);

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
