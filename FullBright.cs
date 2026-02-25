using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using Microsoft.Xna.Framework;
using TerrariaModder.Core.Logging;

namespace Plunder
{
    /// <summary>
    /// FullBright module - removes darkness by patching Terraria.Lighting.GetColor
    /// to return Color.White for every tile when active.
    ///
    /// Uses Microsoft.Xna.Framework.Color directly (from Terraria.exe reference)
    /// because Harmony requires the exact value type for struct return prefixes.
    /// </summary>
    public static class FullBright
    {
        private static ILogger _log;
        private static Harmony _harmony;
        private static Timer _patchTimer;
        private static readonly object _patchLock = new object();
        private static bool _patchesApplied;

        // The actual toggle state
        private static bool _active;
        public static bool IsActive => _active;

        public static void Initialize(ILogger log, bool defaultState)
        {
            _log = log;
            _active = defaultState;
            _harmony = new Harmony("com.plunder.fullbright");

            // Delay patching to let Terraria types fully load
            _patchTimer = new Timer(ApplyPatches, null, 5000, Timeout.Infinite);

            _log.Info($"FullBright initialized (default: {(_active ? "ON" : "OFF")})");
        }

        public static void Toggle()
        {
            _active = !_active;
            _log.Info($"FullBright: {(_active ? "ON" : "OFF")}");

            // Show in-game chat message
            try
            {
                var mainType = Type.GetType("Terraria.Main, Terraria")
                    ?? Assembly.Load("Terraria").GetType("Terraria.Main");

                if (mainType != null)
                {
                    var newTextMethod = mainType.GetMethod("NewText",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(string), typeof(byte), typeof(byte), typeof(byte) },
                        null);

                    if (newTextMethod != null)
                    {
                        string msg = "Full Bright " + (_active ? "Enabled" : "Disabled");
                        byte r = (byte)(_active ? 100 : 200);
                        byte g = (byte)(_active ? 255 : 200);
                        byte b = (byte)(_active ? 100 : 200);
                        newTextMethod.Invoke(null, new object[] { msg, r, g, b });
                    }
                }
            }
            catch { }
        }

        public static void SetActive(bool state)
        {
            if (_active != state)
                Toggle();
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
            _harmony?.UnpatchAll("com.plunder.fullbright");
            _patchesApplied = false;
            _active = false;
            _log?.Info("FullBright unloaded");
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
                var lightingType = Type.GetType("Terraria.Lighting, Terraria")
                    ?? Assembly.Load("Terraria").GetType("Terraria.Lighting");

                if (lightingType == null)
                {
                    _log.Error("FullBright: Could not find Terraria.Lighting type");
                    return;
                }

                // Patch GetColor(int, int) - the primary lighting call for tile rendering
                var getColor2 = lightingType.GetMethod("GetColor",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(int), typeof(int) },
                    null);

                if (getColor2 != null)
                {
                    var prefix2 = typeof(FullBright).GetMethod(nameof(GetColor2_Prefix),
                        BindingFlags.NonPublic | BindingFlags.Static);
                    _harmony.Patch(getColor2, prefix: new HarmonyMethod(prefix2));
                    _log.Info("FullBright: Patched Lighting.GetColor(int, int)");
                }
                else
                {
                    _log.Warn("FullBright: Could not find Lighting.GetColor(int, int)");
                }

                // Patch GetColor(int, int, Color) - the blended variant
                var getColor3 = lightingType.GetMethod("GetColor",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(int), typeof(int), typeof(Color) },
                    null);

                if (getColor3 != null)
                {
                    var prefix3 = typeof(FullBright).GetMethod(nameof(GetColor3_Prefix),
                        BindingFlags.NonPublic | BindingFlags.Static);
                    _harmony.Patch(getColor3, prefix: new HarmonyMethod(prefix3));
                    _log.Info("FullBright: Patched Lighting.GetColor(int, int, Color)");
                }

                _log.Info("FullBright: Harmony patches applied");
            }
            catch (Exception ex)
            {
                _log.Error($"FullBright: Patch error - {ex.Message}");
            }
        }

        /// <summary>
        /// Prefix for Lighting.GetColor(int x, int y).
        /// Harmony requires the exact return type (Color is a struct) for __result.
        /// Returns false to skip original when active, setting result to white.
        /// </summary>
        private static bool GetColor2_Prefix(ref Color __result)
        {
            if (!_active) return true;
            __result = Color.White;
            return false;
        }

        /// <summary>
        /// Prefix for Lighting.GetColor(int x, int y, Color oldColor).
        /// Same approach for the blended overload.
        /// </summary>
        private static bool GetColor3_Prefix(ref Color __result)
        {
            if (!_active) return true;
            __result = Color.White;
            return false;
        }
    }
}
