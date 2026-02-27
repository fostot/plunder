using System;
using System.Reflection;
using TerrariaModder.Core.Events;
using TerrariaModder.Core.Logging;

namespace Plunder
{
    /// <summary>
    /// Environment Control module — time (pause/set/fast-forward), weather (rain),
    /// events (blood moon, eclipse). All Terraria access via reflection.
    /// Uses FrameEvents.OnPostUpdate for time pause (re-sets Main.time each frame).
    /// </summary>
    public static class EnvironmentControl
    {
        private static ILogger _log;
        // ---- State ----
        private static bool _timePaused;
        private static double _pausedTime;
        private static bool _pausedDayTime;

        // ---- Properties ----
        public static bool TimePaused => _timePaused;

        // ---- Reflection cache ----
        private static Type _mainType;
        private static FieldInfo _timeField;         // Main.time (double)
        private static FieldInfo _dayTimeField;      // Main.dayTime (bool)
        private static FieldInfo _dayRateField;      // Main.dayRate (int)
        private static FieldInfo _bloodMoonField;    // Main.bloodMoon (bool)
        private static FieldInfo _eclipseField;      // Main.eclipse (bool)
        private static FieldInfo _rainingField;      // Main.raining (bool)
        private static FieldInfo _gameMenuField;     // Main.gameMenu (bool)
        private static FieldInfo _fastForwardField;  // Main.fastForwardTimeToDawn (bool)
        private static FieldInfo _sundialCdField;    // Main.sundialCooldown (int)
        private static MethodInfo _startRainMethod;  // Main.StartRain()
        private static MethodInfo _stopRainMethod;   // Main.StopRain()
        private static bool _reflectionReady;

        public static void Initialize(ILogger log)
        {
            _log = log;
            FrameEvents.OnPostUpdate += OnPostUpdate;
            _log.Info("EnvironmentControl initialized");
        }

        public static void Unload()
        {
            FrameEvents.OnPostUpdate -= OnPostUpdate;
            _timePaused = false;
            _log?.Info("EnvironmentControl unloaded");
        }

        // ============================================================
        //  TIME CONTROL
        // ============================================================

        public static void ToggleTimePause()
        {
            if (!EnsureReflection()) return;

            _timePaused = !_timePaused;
            if (_timePaused)
            {
                // Capture current time
                _pausedTime = (double)_timeField.GetValue(null);
                _pausedDayTime = (bool)_dayTimeField.GetValue(null);
            }
            ShowMsg("Time " + (_timePaused ? "Paused" : "Resumed"), _timePaused);
        }

        public static void SetTimePaused(bool v)
        {
            if (_timePaused != v) ToggleTimePause();
        }

        public static void SetDawn()
        {
            if (!EnsureReflection()) return;
            _dayTimeField.SetValue(null, true);
            _timeField.SetValue(null, 0.0);
            if (_timePaused)
            {
                _pausedTime = 0.0;
                _pausedDayTime = true;
            }
            ShowMsg("Time set to Dawn", true);
        }

        public static void SetNoon()
        {
            if (!EnsureReflection()) return;
            _dayTimeField.SetValue(null, true);
            _timeField.SetValue(null, 27000.0);
            if (_timePaused)
            {
                _pausedTime = 27000.0;
                _pausedDayTime = true;
            }
            ShowMsg("Time set to Noon", true);
        }

        public static void SetDusk()
        {
            if (!EnsureReflection()) return;
            _dayTimeField.SetValue(null, false);
            _timeField.SetValue(null, 0.0);
            if (_timePaused)
            {
                _pausedTime = 0.0;
                _pausedDayTime = false;
            }
            ShowMsg("Time set to Dusk", true);
        }

        public static void SetMidnight()
        {
            if (!EnsureReflection()) return;
            _dayTimeField.SetValue(null, false);
            _timeField.SetValue(null, 16200.0);
            if (_timePaused)
            {
                _pausedTime = 16200.0;
                _pausedDayTime = false;
            }
            ShowMsg("Time set to Midnight", true);
        }

        public static void FastForwardToDawn()
        {
            if (!EnsureReflection()) return;
            try
            {
                if (_fastForwardField != null)
                    _fastForwardField.SetValue(null, true);
                if (_sundialCdField != null)
                    _sundialCdField.SetValue(null, 0);
                ShowMsg("Fast-forwarding to Dawn...", true);
            }
            catch (Exception ex)
            {
                _log.Error($"EnvironmentControl: Fast forward failed - {ex.Message}");
            }
        }

        // ============================================================
        //  WEATHER / EVENTS
        // ============================================================

        public static void ToggleRain()
        {
            if (!EnsureReflection()) return;
            try
            {
                bool raining = _rainingField != null && (bool)_rainingField.GetValue(null);
                if (raining)
                {
                    // Try StopRain() — handle overloads with empty args if needed
                    if (_stopRainMethod != null)
                    {
                        var parms = _stopRainMethod.GetParameters();
                        _stopRainMethod.Invoke(null, parms.Length == 0 ? null : new object[parms.Length]);
                    }
                    else
                    {
                        _rainingField.SetValue(null, false);
                    }
                    ShowMsg("Rain Stopped", false);
                }
                else
                {
                    if (_startRainMethod != null)
                    {
                        var parms = _startRainMethod.GetParameters();
                        _startRainMethod.Invoke(null, parms.Length == 0 ? null : new object[parms.Length]);
                    }
                    else
                    {
                        _rainingField.SetValue(null, true);
                    }
                    ShowMsg("Rain Started", true);
                }
            }
            catch (Exception ex)
            {
                _log.Error($"EnvironmentControl: Toggle rain failed - {ex.Message}");
            }
        }

        public static bool IsRaining()
        {
            if (!EnsureReflection() || _rainingField == null) return false;
            try { return (bool)_rainingField.GetValue(null); }
            catch { return false; }
        }

        public static void ToggleBloodMoon()
        {
            if (!EnsureReflection() || _bloodMoonField == null) return;
            try
            {
                bool current = (bool)_bloodMoonField.GetValue(null);
                if (!current)
                {
                    // Blood moon requires nighttime
                    bool isDay = (bool)_dayTimeField.GetValue(null);
                    if (isDay)
                    {
                        _dayTimeField.SetValue(null, false);
                        _timeField.SetValue(null, 0.0);
                    }
                    _bloodMoonField.SetValue(null, true);
                    ShowMsg("Blood Moon Rising!", true);
                }
                else
                {
                    _bloodMoonField.SetValue(null, false);
                    ShowMsg("Blood Moon Ended", false);
                }
            }
            catch (Exception ex)
            {
                _log.Error($"EnvironmentControl: Toggle blood moon failed - {ex.Message}");
            }
        }

        public static bool IsBloodMoon()
        {
            if (!EnsureReflection() || _bloodMoonField == null) return false;
            try { return (bool)_bloodMoonField.GetValue(null); }
            catch { return false; }
        }

        public static void ToggleEclipse()
        {
            if (!EnsureReflection() || _eclipseField == null) return;
            try
            {
                bool current = (bool)_eclipseField.GetValue(null);
                if (!current)
                {
                    // Eclipse requires daytime
                    bool isDay = (bool)_dayTimeField.GetValue(null);
                    if (!isDay)
                    {
                        _dayTimeField.SetValue(null, true);
                        _timeField.SetValue(null, 0.0);
                    }
                    _eclipseField.SetValue(null, true);
                    ShowMsg("Solar Eclipse!", true);
                }
                else
                {
                    _eclipseField.SetValue(null, false);
                    ShowMsg("Eclipse Ended", false);
                }
            }
            catch (Exception ex)
            {
                _log.Error($"EnvironmentControl: Toggle eclipse failed - {ex.Message}");
            }
        }

        public static bool IsEclipse()
        {
            if (!EnsureReflection() || _eclipseField == null) return false;
            try { return (bool)_eclipseField.GetValue(null); }
            catch { return false; }
        }

        // ============================================================
        //  FRAME HOOK (time pause)
        // ============================================================

        private static void OnPostUpdate()
        {
            if (!_timePaused || !_reflectionReady) return;
            if (_gameMenuField != null && (bool)_gameMenuField.GetValue(null)) return;

            try
            {
                _dayTimeField.SetValue(null, _pausedDayTime);
                _timeField.SetValue(null, _pausedTime);
            }
            catch { }
        }

        // ============================================================
        //  REFLECTION
        // ============================================================

        private static bool EnsureReflection()
        {
            if (_reflectionReady) return true;

            try
            {
                var asm = Assembly.Load("Terraria");
                _mainType = Type.GetType("Terraria.Main, Terraria") ?? asm.GetType("Terraria.Main");

                if (_mainType == null)
                {
                    _log.Error("EnvironmentControl: Main type not found");
                    return false;
                }

                var pubStatic = BindingFlags.Public | BindingFlags.Static;
                var allStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

                _timeField = _mainType.GetField("time", pubStatic);
                _dayTimeField = _mainType.GetField("dayTime", pubStatic);
                _dayRateField = _mainType.GetField("dayRate", pubStatic);
                _bloodMoonField = _mainType.GetField("bloodMoon", pubStatic);
                _eclipseField = _mainType.GetField("eclipse", pubStatic);
                _rainingField = _mainType.GetField("raining", pubStatic);
                _gameMenuField = _mainType.GetField("gameMenu", pubStatic);
                _fastForwardField = _mainType.GetField("fastForwardTimeToDawn", pubStatic)
                    ?? _mainType.GetField("fastForwardTime", pubStatic);
                _sundialCdField = _mainType.GetField("sundialCooldown", pubStatic);

                _startRainMethod = _mainType.GetMethod("StartRain", allStatic);
                _stopRainMethod = _mainType.GetMethod("StopRain", allStatic);

                if (_timeField == null || _dayTimeField == null)
                {
                    _log.Error("EnvironmentControl: Main.time or Main.dayTime not found");
                    return false;
                }

                _reflectionReady = true;
                _log.Info("EnvironmentControl: Reflection initialized");
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"EnvironmentControl: Reflection error - {ex.Message}");
                return false;
            }
        }

        // ============================================================
        //  CHAT MESSAGE
        // ============================================================

        private static void ShowMsg(string msg, bool enabled)
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
