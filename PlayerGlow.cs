using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using TerrariaModder.Core.Logging;

namespace Plunder
{
    /// <summary>
    /// PlayerGlow module - makes the player emit light as if they were a torch/light source.
    /// Calls Lighting.AddLight at the player's center position each frame when active.
    /// </summary>
    public static class PlayerGlow
    {
        private static ILogger _log;
        private static Harmony _harmony;
        private static Timer _patchTimer;
        private static readonly object _patchLock = new object();
        private static bool _patchesApplied;

        private static bool _active;
        public static bool IsActive => _active;

        // Light color (warm white, similar to a torch)
        private const float LightR = 1.0f;
        private const float LightG = 0.95f;
        private const float LightB = 0.8f;

        // Reflection cache
        private static Type _mainType;
        private static Type _playerType;
        private static Type _lightingType;
        private static FieldInfo _myPlayerField;
        private static FieldInfo _gameMenuField;
        private static FieldInfo _positionField;
        private static FieldInfo _widthField;
        private static FieldInfo _heightField;
        private static FieldInfo _vec2XField;
        private static FieldInfo _vec2YField;
        private static MethodInfo _addLightMethod;

        private static bool _reflectionReady;

        public static void Initialize(ILogger log, bool defaultState)
        {
            _log = log;
            _active = defaultState;
            _harmony = new Harmony("com.plunder.playerglow");
            _patchTimer = new Timer(ApplyPatches, null, 5000, Timeout.Infinite);
            _log.Info($"PlayerGlow initialized (default: {(_active ? "ON" : "OFF")})");
        }

        public static void Toggle()
        {
            _active = !_active;
            _log.Info($"PlayerGlow: {(_active ? "ON" : "OFF")}");
            ShowMessage("Player Glow " + (_active ? "Enabled" : "Disabled"), _active);
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
            _harmony?.UnpatchAll("com.plunder.playerglow");
            _patchesApplied = false;
            _active = false;
            _log?.Info("PlayerGlow unloaded");
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
                    _log.Error("PlayerGlow: Reflection init failed");
                    return;
                }

                var updateMethod = _playerType.GetMethod("Update",
                    BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(int) }, null);

                if (updateMethod != null)
                {
                    var postfix = typeof(PlayerGlow).GetMethod(nameof(PlayerUpdate_Postfix),
                        BindingFlags.NonPublic | BindingFlags.Static);
                    _harmony.Patch(updateMethod, postfix: new HarmonyMethod(postfix));
                    _log.Info("PlayerGlow: Patched Player.Update (light emission)");
                }
                else
                {
                    _log.Warn("PlayerGlow: Could not find Player.Update(int)");
                }
            }
            catch (Exception ex)
            {
                _log.Error($"PlayerGlow: Patch error - {ex.Message}");
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
                _lightingType = Type.GetType("Terraria.Lighting, Terraria")
                    ?? asm.GetType("Terraria.Lighting");

                var entityType = Type.GetType("Terraria.Entity, Terraria")
                    ?? asm.GetType("Terraria.Entity");

                if (_mainType == null || _playerType == null || _lightingType == null)
                {
                    _log.Error("PlayerGlow: Core types not found" +
                        $" (Main={_mainType != null}, Player={_playerType != null}, Lighting={_lightingType != null})");
                    return;
                }

                var pubStatic = BindingFlags.Public | BindingFlags.Static;
                var pubInst = BindingFlags.Public | BindingFlags.Instance;

                _myPlayerField = _mainType.GetField("myPlayer", pubStatic);
                _gameMenuField = _mainType.GetField("gameMenu", pubStatic);

                // Entity fields
                var searchType = entityType ?? _playerType;
                _positionField = searchType.GetField("position", pubInst);
                _widthField = searchType.GetField("width", pubInst);
                _heightField = searchType.GetField("height", pubInst);

                // Vector2 fields
                var vec2Type = _positionField?.FieldType;
                if (vec2Type != null)
                {
                    _vec2XField = vec2Type.GetField("X", pubInst);
                    _vec2YField = vec2Type.GetField("Y", pubInst);
                }

                // Lighting.AddLight(int tileX, int tileY, float r, float g, float b)
                _addLightMethod = _lightingType.GetMethod("AddLight",
                    pubStatic, null,
                    new[] { typeof(int), typeof(int), typeof(float), typeof(float), typeof(float) },
                    null);

                if (_addLightMethod == null)
                {
                    _log.Error("PlayerGlow: Lighting.AddLight(int,int,float,float,float) not found");

                    // List available AddLight overloads for debugging
                    foreach (var m in _lightingType.GetMethods(pubStatic))
                    {
                        if (m.Name == "AddLight")
                        {
                            var parms = m.GetParameters();
                            string sig = string.Join(", ", Array.ConvertAll(parms, p => p.ParameterType.Name));
                            _log.Info($"  Found: Lighting.AddLight({sig})");
                        }
                    }
                    return;
                }

                _reflectionReady = true;
                _log.Info("PlayerGlow: Reflection initialized");
            }
            catch (Exception ex)
            {
                _log.Error($"PlayerGlow: Reflection error - {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix on Player.Update(int i). Emits light at the player's center
        /// position each frame, making them glow like a torch.
        /// </summary>
        private static void PlayerUpdate_Postfix(object __instance, int i)
        {
            if (!_active || !_reflectionReady) return;

            try
            {
                // Only apply to local player
                if (_gameMenuField != null && (bool)_gameMenuField.GetValue(null)) return;
                int myPlayer = (int)_myPlayerField.GetValue(null);
                if (i != myPlayer) return;

                // Get player center in tile coordinates
                object position = _positionField.GetValue(__instance);
                float posX = (float)_vec2XField.GetValue(position);
                float posY = (float)_vec2YField.GetValue(position);
                int pw = (int)_widthField.GetValue(__instance);
                int ph = (int)_heightField.GetValue(__instance);

                int tileX = (int)((posX + pw * 0.5f) / 16f);
                int tileY = (int)((posY + ph * 0.5f) / 16f);

                // Emit light at player's center
                _addLightMethod.Invoke(null, new object[]
                {
                    tileX, tileY, LightR, LightG, LightB
                });
            }
            catch { }
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
                    byte r = (byte)(enabled ? 255 : 200);
                    byte g = (byte)(enabled ? 230 : 200);
                    byte b = (byte)(enabled ? 150 : 200);
                    newTextMethod.Invoke(null, new object[] { msg, r, g, b });
                }
            }
            catch { }
        }
    }
}
