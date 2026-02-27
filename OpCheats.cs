using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using TerrariaModder.Core.Logging;

namespace Plunder
{
    /// <summary>
    /// OP Cheats module — god mode, infinite mana/minions/flight/ammo/breath,
    /// damage multiplier, spawn rate, run speed, etc.
    /// All Terraria access via reflection. Harmony patches delayed 5s for type loading.
    /// </summary>
    public static class OpCheats
    {
        private static ILogger _log;
        private static Harmony _harmony;
        private static Timer _patchTimer;
        private static readonly object _patchLock = new object();
        private static bool _patchesApplied;

        // ---- State ----
        private static bool _godMode;
        private static bool _infiniteMana;
        private static bool _minionsEnabled;     // checkbox toggle
        private static int _minionCount;         // 0 = infinite, 1-20 = cap
        private static bool _infiniteFlight;
        private static bool _infiniteAmmo;
        private static bool _infiniteBreath;
        private static bool _noKnockback;
        private static bool _damageEnabled;      // checkbox toggle
        private static int _damageMult;           // 0 = one hit kill, 1 = normal, 2-20 = multiplier
        private static bool _noFallDamage;
        private static bool _noTreeBombs;
        private static int _spawnRateMult = 1;
        private static int _runSpeedMult = 1;
        private static bool _toolRangeEnabled;
        private static int _toolRangeMult = 1;

        // ---- Properties ----
        public static bool GodMode => _godMode;
        public static bool InfiniteMana => _infiniteMana;
        public static bool MinionsEnabled => _minionsEnabled;
        public static int MinionCount => _minionCount;
        public static bool InfiniteFlight => _infiniteFlight;
        public static bool InfiniteAmmo => _infiniteAmmo;
        public static bool InfiniteBreath => _infiniteBreath;
        public static bool NoKnockback => _noKnockback;
        public static bool DamageEnabled => _damageEnabled;
        public static int DamageMult => _damageMult;
        public static bool NoFallDamage => _noFallDamage;
        public static bool NoTreeBombs => _noTreeBombs;
        public static int SpawnRateMult => _spawnRateMult;
        public static int RunSpeedMult => _runSpeedMult;
        public static bool ToolRangeEnabled => _toolRangeEnabled;
        public static int ToolRangeMult => _toolRangeMult;

        // ---- Toggle methods (UI clicks — flip + chat message) ----

        public static void ToggleGodMode()
        {
            _godMode = !_godMode;
            ShowMsg("God Mode " + (_godMode ? "ON" : "OFF"), _godMode);
        }

        public static void ToggleInfiniteMana()
        {
            _infiniteMana = !_infiniteMana;
            ShowMsg("Infinite Mana " + (_infiniteMana ? "ON" : "OFF"), _infiniteMana);
        }

        public static void ToggleMinions()
        {
            _minionsEnabled = !_minionsEnabled;
            string label = _minionsEnabled
                ? (_minionCount == 0 ? "Minions: Infinite" : $"Minions: {_minionCount} max")
                : "Minions Override OFF";
            ShowMsg(label, _minionsEnabled);
        }

        public static void ToggleInfiniteFlight()
        {
            _infiniteFlight = !_infiniteFlight;
            ShowMsg("Infinite Flight " + (_infiniteFlight ? "ON" : "OFF"), _infiniteFlight);
        }

        public static void ToggleInfiniteAmmo()
        {
            _infiniteAmmo = !_infiniteAmmo;
            ShowMsg("Infinite Ammo " + (_infiniteAmmo ? "ON" : "OFF"), _infiniteAmmo);
        }

        public static void ToggleInfiniteBreath()
        {
            _infiniteBreath = !_infiniteBreath;
            ShowMsg("Infinite Breath " + (_infiniteBreath ? "ON" : "OFF"), _infiniteBreath);
        }

        public static void ToggleNoKnockback()
        {
            _noKnockback = !_noKnockback;
            ShowMsg("No Knockback " + (_noKnockback ? "ON" : "OFF"), _noKnockback);
        }

        public static void ToggleDamage()
        {
            _damageEnabled = !_damageEnabled;
            string label = _damageEnabled
                ? (_damageMult == 0 ? "Damage: One Hit Kill" : $"Damage: {_damageMult}x")
                : "Damage Override OFF";
            ShowMsg(label, _damageEnabled);
        }

        public static void ToggleNoFallDamage()
        {
            _noFallDamage = !_noFallDamage;
            ShowMsg("No Fall Damage " + (_noFallDamage ? "ON" : "OFF"), _noFallDamage);
        }

        public static void ToggleNoTreeBombs()
        {
            _noTreeBombs = !_noTreeBombs;
            ShowMsg("No Tree Bombs " + (_noTreeBombs ? "ON" : "OFF"), _noTreeBombs);
        }

        public static void ToggleToolRange()
        {
            _toolRangeEnabled = !_toolRangeEnabled;
            string label = _toolRangeEnabled
                ? (_toolRangeMult <= 1 ? "Tool Range: Normal" : $"Tool Range: {_toolRangeMult}x")
                : "Tool Range Override OFF";
            ShowMsg(label, _toolRangeEnabled);
        }

        // ---- Setter methods (sliders / config restore) ----

        public static void SetGodMode(bool v) { _godMode = v; }
        public static void SetInfiniteMana(bool v) { _infiniteMana = v; }
        public static void SetMinionsEnabled(bool v) { _minionsEnabled = v; }
        public static void SetMinionCount(int v) { _minionCount = v; }
        public static void SetInfiniteFlight(bool v) { _infiniteFlight = v; }
        public static void SetInfiniteAmmo(bool v) { _infiniteAmmo = v; }
        public static void SetInfiniteBreath(bool v) { _infiniteBreath = v; }
        public static void SetNoKnockback(bool v) { _noKnockback = v; }
        public static void SetDamageEnabled(bool v) { _damageEnabled = v; }
        public static void SetDamageMult(int v) { _damageMult = v; }
        public static void SetNoFallDamage(bool v) { _noFallDamage = v; }
        public static void SetNoTreeBombs(bool v) { _noTreeBombs = v; }

        public static void SetSpawnRateMult(int v)
        {
            _spawnRateMult = v;
            // Immediately restore defaults if set to 1
            if (v == 1) RestoreSpawnRate();
        }

        public static void SetRunSpeedMult(int v) { _runSpeedMult = v; }
        public static void SetToolRangeEnabled(bool v) { _toolRangeEnabled = v; }
        public static void SetToolRangeMult(int v) { _toolRangeMult = Math.Max(1, v); }

        // ============================================================
        //  REFLECTION CACHE
        // ============================================================

        private static Type _mainType;
        private static Type _playerType;
        private static Type _npcType;
        private static Type _worldGenType;
        private static FieldInfo _myPlayerField;
        private static FieldInfo _gameMenuField;
        private static FieldInfo _playerArrayField;

        // Player fields
        private static FieldInfo _statLifeField;
        private static FieldInfo _statLifeMax2Field;
        private static FieldInfo _statManaField;
        private static FieldInfo _statManaMax2Field;
        private static FieldInfo _maxMinionsField;
        private static FieldInfo _wingTimeField;
        private static FieldInfo _wingTimeMaxField;
        private static FieldInfo _rocketTimeField;
        private static FieldInfo _breathField;
        private static FieldInfo _breathMaxField;
        private static FieldInfo _breathCDField;
        private static FieldInfo _noKnockbackField;
        private static FieldInfo _noFallDmgField;
        private static FieldInfo _maxRunSpeedField;
        private static FieldInfo _runAccelerationField;
        private static FieldInfo _immuneField;
        private static FieldInfo _immuneTimeField;
        private static FieldInfo _immuneNoBlink;
        private static FieldInfo _tileRangeXField;
        private static FieldInfo _tileRangeYField;
        private static FieldInfo _blockRangeField;

        // NPC fields
        private static FieldInfo _npcLifeField;
        private static FieldInfo _defaultSpawnRateField;
        private static FieldInfo _defaultMaxSpawnsField;

        // Main world flags
        private static FieldInfo _getGoodWorldField;

        private static bool _reflectionReady;

        // Thread-static for NPC damage mult calculation
        [ThreadStatic] private static int _preStrikeLife;

        // Thread-static for ShakeTree getGoodWorld save/restore
        [ThreadStatic] private static bool _savedGetGoodWorld;

        // ============================================================
        //  LIFECYCLE
        // ============================================================

        public static void Initialize(ILogger log)
        {
            _log = log;
            _harmony = new Harmony("com.plunder.opcheats");
            _patchTimer = new Timer(ApplyPatches, null, 5000, Timeout.Infinite);
            _log.Info("OpCheats initialized");
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

            // Restore spawn rate before unpatching
            RestoreSpawnRate();

            _harmony?.UnpatchAll("com.plunder.opcheats");
            _patchesApplied = false;

            _godMode = false;
            _infiniteMana = false;
            _minionsEnabled = false;
            _minionCount = 0;
            _infiniteFlight = false;
            _infiniteAmmo = false;
            _infiniteBreath = false;
            _noKnockback = false;
            _damageEnabled = false;
            _damageMult = 0;
            _noFallDamage = false;
            _noTreeBombs = false;
            _spawnRateMult = 1;
            _runSpeedMult = 1;
            _toolRangeEnabled = false;
            _toolRangeMult = 1;

            _log?.Info("OpCheats unloaded");
        }

        private static void RestoreSpawnRate()
        {
            try
            {
                if (_defaultSpawnRateField != null)
                    _defaultSpawnRateField.SetValue(null, 600);
                if (_defaultMaxSpawnsField != null)
                    _defaultMaxSpawnsField.SetValue(null, 5);
            }
            catch { }
        }

        // ============================================================
        //  REFLECTION INIT
        // ============================================================

        private static void InitReflection()
        {
            if (_reflectionReady) return;

            try
            {
                var asm = Assembly.Load("Terraria");
                _mainType = asm.GetType("Terraria.Main");
                _playerType = asm.GetType("Terraria.Player");
                _npcType = asm.GetType("Terraria.NPC");
                _worldGenType = asm.GetType("Terraria.WorldGen");

                var pubStatic = BindingFlags.Public | BindingFlags.Static;
                var pubInst = BindingFlags.Public | BindingFlags.Instance;

                _myPlayerField = _mainType.GetField("myPlayer", pubStatic);
                _gameMenuField = _mainType.GetField("gameMenu", pubStatic);
                _playerArrayField = _mainType.GetField("player", pubStatic);

                // Player fields
                _statLifeField = _playerType.GetField("statLife", pubInst);
                _statLifeMax2Field = _playerType.GetField("statLifeMax2", pubInst);
                _statManaField = _playerType.GetField("statMana", pubInst);
                _statManaMax2Field = _playerType.GetField("statManaMax2", pubInst);
                _maxMinionsField = _playerType.GetField("maxMinions", pubInst);
                _wingTimeField = _playerType.GetField("wingTime", pubInst);
                _wingTimeMaxField = _playerType.GetField("wingTimeMax", pubInst);
                _rocketTimeField = _playerType.GetField("rocketTime", pubInst);
                _breathField = _playerType.GetField("breath", pubInst);
                _breathMaxField = _playerType.GetField("breathMax", pubInst);
                _breathCDField = _playerType.GetField("breathCD", pubInst);
                _noKnockbackField = _playerType.GetField("noKnockback", pubInst);
                _noFallDmgField = _playerType.GetField("noFallDmg", pubInst);
                _maxRunSpeedField = _playerType.GetField("maxRunSpeed", pubInst);
                _runAccelerationField = _playerType.GetField("runAcceleration", pubInst);
                _immuneField = _playerType.GetField("immune", pubInst);
                _immuneTimeField = _playerType.GetField("immuneTime", pubInst);
                _immuneNoBlink = _playerType.GetField("immuneNoBlink", pubInst);
                _tileRangeXField = _playerType.GetField("tileRangeX", pubInst);
                _tileRangeYField = _playerType.GetField("tileRangeY", pubInst);
                _blockRangeField = _playerType.GetField("blockRange", pubInst);

                // NPC fields
                _npcLifeField = _npcType.GetField("life", pubInst);
                // defaultSpawnRate/defaultMaxSpawns are non-public statics (confirmed by HEROsMod ModUtils.cs)
                var allStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                _defaultSpawnRateField = _npcType.GetField("defaultSpawnRate", allStatic);
                _defaultMaxSpawnsField = _npcType.GetField("defaultMaxSpawns", allStatic);

                // Main world flags
                _getGoodWorldField = _mainType.GetField("getGoodWorld", pubStatic);

                _reflectionReady = true;
                _log.Info("OpCheats: Reflection initialized");
            }
            catch (Exception ex)
            {
                _log.Error($"OpCheats: Reflection error - {ex.Message}");
            }
        }

        // ============================================================
        //  APPLY PATCHES
        // ============================================================

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
                    _log.Error("OpCheats: Reflection failed, cannot patch");
                    return;
                }

                // 1a) Player.ResetEffects postfix — god mode immunity, no knockback, no fall dmg
                //     Runs at frame start BEFORE damage checks (matching AdminPanel pattern)
                var resetEffects = _playerType.GetMethod("ResetEffects",
                    BindingFlags.Public | BindingFlags.Instance);
                if (resetEffects != null)
                {
                    var postfix = typeof(OpCheats).GetMethod(nameof(ResetEffects_Postfix),
                        BindingFlags.NonPublic | BindingFlags.Static);
                    _harmony.Patch(resetEffects, postfix: new HarmonyMethod(postfix));
                    _log.Info("OpCheats: Patched Player.ResetEffects");
                }

                // 2) Player.Hurt prefix — god mode
                PatchPlayerHurt();

                // 3) Player.KillMe prefix — god mode
                PatchPlayerKillMe();

                // 4) NPC.StrikeNPC — one hit kill / damage mult
                PatchNpcStrike();

                // 5) Player.HorizontalMovement prefix — run speed multiplier
                var horizMethod = _playerType.GetMethod("HorizontalMovement",
                    BindingFlags.Public | BindingFlags.Instance);
                if (horizMethod != null)
                {
                    var prefix = typeof(OpCheats).GetMethod(nameof(HorizontalMovement_Prefix),
                        BindingFlags.NonPublic | BindingFlags.Static);
                    _harmony.Patch(horizMethod, prefix: new HarmonyMethod(prefix));
                    _log.Info("OpCheats: Patched Player.HorizontalMovement");
                }

                // 6) Player.PickAmmo — infinite ammo
                PatchPickAmmo();

                // 7) WorldGen.ShakeTree — no tree bombs (FTW/Zenith worlds)
                PatchShakeTree();

                _log.Info("OpCheats: All patches applied");
            }
            catch (Exception ex)
            {
                _log.Error($"OpCheats: Patch error - {ex.Message}");
            }
        }

        private static void PatchPlayerHurt()
        {
            try
            {
                MethodInfo hurtMethod = null;
                foreach (var m in _playerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (m.Name == "Hurt")
                    {
                        if (m.ReturnType == typeof(double))
                        {
                            hurtMethod = m;
                            break;
                        }
                        if (hurtMethod == null) hurtMethod = m;
                    }
                }

                if (hurtMethod != null)
                {
                    var prefix = typeof(OpCheats).GetMethod(nameof(PlayerHurt_Prefix),
                        BindingFlags.NonPublic | BindingFlags.Static);
                    _harmony.Patch(hurtMethod, prefix: new HarmonyMethod(prefix));
                    _log.Info("OpCheats: Patched Player.Hurt");
                }
                else
                {
                    _log.Warn("OpCheats: Player.Hurt not found");
                }
            }
            catch (Exception ex) { _log.Error($"OpCheats: Hurt patch error - {ex.Message}"); }
        }

        private static void PatchPlayerKillMe()
        {
            try
            {
                MethodInfo killMethod = null;
                foreach (var m in _playerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (m.Name == "KillMe") { killMethod = m; break; }
                }

                if (killMethod != null)
                {
                    var prefix = typeof(OpCheats).GetMethod(nameof(PlayerKillMe_Prefix),
                        BindingFlags.NonPublic | BindingFlags.Static);
                    _harmony.Patch(killMethod, prefix: new HarmonyMethod(prefix));
                    _log.Info("OpCheats: Patched Player.KillMe");
                }
                else
                {
                    _log.Warn("OpCheats: Player.KillMe not found");
                }
            }
            catch (Exception ex) { _log.Error($"OpCheats: KillMe patch error - {ex.Message}"); }
        }

        private static void PatchNpcStrike()
        {
            try
            {
                MethodInfo strikeMethod = null;
                foreach (var m in _npcType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (m.Name == "StrikeNPC") { strikeMethod = m; break; }
                }

                if (strikeMethod != null)
                {
                    var prefix = typeof(OpCheats).GetMethod(nameof(NpcStrike_Prefix),
                        BindingFlags.NonPublic | BindingFlags.Static);
                    var postfix = typeof(OpCheats).GetMethod(nameof(NpcStrike_Postfix),
                        BindingFlags.NonPublic | BindingFlags.Static);
                    _harmony.Patch(strikeMethod,
                        prefix: new HarmonyMethod(prefix),
                        postfix: new HarmonyMethod(postfix));
                    _log.Info("OpCheats: Patched NPC.StrikeNPC");
                }
                else
                {
                    _log.Warn("OpCheats: NPC.StrikeNPC not found");
                }
            }
            catch (Exception ex) { _log.Error($"OpCheats: StrikeNPC patch error - {ex.Message}"); }
        }

        private static void PatchPickAmmo()
        {
            try
            {
                MethodInfo pickAmmo = null;
                foreach (var m in _playerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (m.Name == "PickAmmo") { pickAmmo = m; break; }
                }

                if (pickAmmo != null)
                {
                    bool hasDontConsume = false;
                    foreach (var p in pickAmmo.GetParameters())
                    {
                        if (p.Name == "dontConsume") { hasDontConsume = true; break; }
                    }

                    if (hasDontConsume)
                    {
                        var prefix = typeof(OpCheats).GetMethod(nameof(PickAmmo_Prefix),
                            BindingFlags.NonPublic | BindingFlags.Static);
                        _harmony.Patch(pickAmmo, prefix: new HarmonyMethod(prefix));
                        _log.Info("OpCheats: Patched Player.PickAmmo");
                    }
                    else
                    {
                        _log.Warn("OpCheats: PickAmmo has no dontConsume param");
                    }
                }
                else
                {
                    _log.Warn("OpCheats: Player.PickAmmo not found");
                }
            }
            catch (Exception ex) { _log.Error($"OpCheats: PickAmmo patch error - {ex.Message}"); }
        }

        private static void PatchShakeTree()
        {
            try
            {
                var shakeTree = _worldGenType?.GetMethod("ShakeTree",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (shakeTree != null)
                {
                    var prefix = typeof(OpCheats).GetMethod(nameof(ShakeTree_Prefix),
                        BindingFlags.NonPublic | BindingFlags.Static);
                    var postfix = typeof(OpCheats).GetMethod(nameof(ShakeTree_Postfix),
                        BindingFlags.NonPublic | BindingFlags.Static);
                    _harmony.Patch(shakeTree,
                        prefix: new HarmonyMethod(prefix),
                        postfix: new HarmonyMethod(postfix));
                    _log.Info("OpCheats: Patched WorldGen.ShakeTree");
                }
                else
                {
                    // Log available tree-related methods to help find the correct name
                    _log.Warn("OpCheats: WorldGen.ShakeTree not found — searching for tree methods...");
                    if (_worldGenType != null)
                    {
                        foreach (var m in _worldGenType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                        {
                            if (m.Name.IndexOf("tree", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                m.Name.IndexOf("shake", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                _log.Info($"  WorldGen candidate: {m.Name}({string.Join(", ", Array.ConvertAll(m.GetParameters(), p => p.ParameterType.Name + " " + p.Name))})");
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { _log.Error($"OpCheats: ShakeTree patch error - {ex.Message}"); }
        }

        // ============================================================
        //  HARMONY CALLBACKS
        // ============================================================

        /// <summary>
        /// Player.ResetEffects postfix — ALL per-frame cheat overrides.
        /// Runs right after ResetEffects clears per-frame fields, BEFORE UpdateBuffs,
        /// UpdateEquips, ItemCheck, HorizontalMovement, VerticalMovement, damage checks, etc.
        /// Everything is set at frame start so all game systems see the overridden values.
        /// </summary>
        private static void ResetEffects_Postfix(object __instance)
        {
            if (!_reflectionReady) return;

            try
            {
                int myPlayer = (int)_myPlayerField.GetValue(null);
                var players = _playerArrayField.GetValue(null) as Array;
                if (players == null || !ReferenceEquals(players.GetValue(myPlayer), __instance)) return;

                // God Mode — full HP + immunity
                if (_godMode)
                {
                    int maxLife = (int)_statLifeMax2Field.GetValue(__instance);
                    _statLifeField.SetValue(__instance, maxLife);
                    _immuneField?.SetValue(__instance, true);
                    _immuneTimeField?.SetValue(__instance, 2);
                    _immuneNoBlink?.SetValue(__instance, true);
                }

                // Infinite Mana
                if (_infiniteMana && _statManaField != null)
                {
                    int maxMana = (int)_statManaMax2Field.GetValue(__instance);
                    _statManaField.SetValue(__instance, maxMana);
                }

                // Minions
                if (_minionsEnabled && _maxMinionsField != null)
                {
                    if (_minionCount == 0)
                        _maxMinionsField.SetValue(__instance, 999);
                    else
                        _maxMinionsField.SetValue(__instance, _minionCount);
                }

                // Infinite Flight
                if (_infiniteFlight)
                {
                    if (_wingTimeField != null)
                    {
                        object wt = _wingTimeField.GetValue(__instance);
                        if (wt is float) _wingTimeField.SetValue(__instance, 1000f);
                        else if (wt is int) _wingTimeField.SetValue(__instance, 1000);
                    }
                    if (_rocketTimeField != null)
                        _rocketTimeField.SetValue(__instance, 999);
                }

                // Infinite Breath — set breath to max AND breathCD high so the
                // game never ticks breath down (prevents bubble UI flicker).
                // Breath logic: breathCD--; if (<=0) { breath--; breathCD=7; }
                // By keeping breathCD high each frame, breath-- never triggers.
                if (_infiniteBreath && _breathField != null)
                {
                    int maxBreath = _breathMaxField != null
                        ? (int)_breathMaxField.GetValue(__instance) : 200;
                    _breathField.SetValue(__instance, maxBreath);
                    _breathCDField?.SetValue(__instance, 999);
                }

                // No Knockback
                if (_noKnockback && _noKnockbackField != null)
                    _noKnockbackField.SetValue(__instance, true);

                // No Fall Damage
                if (_noFallDamage && _noFallDmgField != null)
                    _noFallDmgField.SetValue(__instance, true);

                // Tool Range — set tileRangeX/Y directly (matches HEROsMod InfiniteReach pattern)
                // HEROsMod only modifies tileRangeX/Y, not blockRange
                // Vanilla defaults after ResetEffects: tileRangeX=5, tileRangeY=4
                if (_toolRangeEnabled && _toolRangeMult > 1)
                {
                    if (_tileRangeXField != null)
                        _tileRangeXField.SetValue(__instance, 5 * _toolRangeMult);
                    if (_tileRangeYField != null)
                        _tileRangeYField.SetValue(__instance, 4 * _toolRangeMult);
                }

                // Spawn Rate
                if (_spawnRateMult == 0)
                {
                    if (_defaultSpawnRateField != null)
                        _defaultSpawnRateField.SetValue(null, int.MaxValue);
                    if (_defaultMaxSpawnsField != null)
                        _defaultMaxSpawnsField.SetValue(null, 0);
                }
                else if (_spawnRateMult > 1)
                {
                    if (_defaultSpawnRateField != null)
                        _defaultSpawnRateField.SetValue(null, Math.Max(1, 600 / _spawnRateMult));
                    if (_defaultMaxSpawnsField != null)
                        _defaultMaxSpawnsField.SetValue(null, 5 * _spawnRateMult);
                }
                else
                {
                    if (_defaultSpawnRateField != null)
                        _defaultSpawnRateField.SetValue(null, 600);
                    if (_defaultMaxSpawnsField != null)
                        _defaultMaxSpawnsField.SetValue(null, 5);
                }
            }
            catch { }
        }

        /// <summary>Player.Hurt prefix — skip damage for local player when god mode on.</summary>
        private static bool PlayerHurt_Prefix(object __instance)
        {
            if (!_godMode) return true;

            try
            {
                int myPlayer = (int)_myPlayerField.GetValue(null);
                var players = _playerArrayField.GetValue(null) as Array;
                if (players != null && ReferenceEquals(players.GetValue(myPlayer), __instance))
                    return false;
            }
            catch { }

            return true;
        }

        /// <summary>Player.KillMe prefix — prevent death for local player when god mode on.</summary>
        private static bool PlayerKillMe_Prefix(object __instance)
        {
            if (!_godMode) return true;

            try
            {
                int myPlayer = (int)_myPlayerField.GetValue(null);
                var players = _playerArrayField.GetValue(null) as Array;
                if (players != null && ReferenceEquals(players.GetValue(myPlayer), __instance))
                    return false;
            }
            catch { }

            return true;
        }

        /// <summary>NPC.StrikeNPC prefix — one hit kill sets life=1, damage mult stores life.</summary>
        private static void NpcStrike_Prefix(object __instance)
        {
            bool isOhk = _damageEnabled && _damageMult == 0;
            bool isMult = _damageEnabled && _damageMult > 1;
            if (!isOhk && !isMult) return;

            try
            {
                if (isOhk)
                {
                    _npcLifeField.SetValue(__instance, 1);
                }
                else
                {
                    _preStrikeLife = (int)_npcLifeField.GetValue(__instance);
                }
            }
            catch { }
        }

        /// <summary>NPC.StrikeNPC postfix — damage mult subtracts extra damage.</summary>
        private static void NpcStrike_Postfix(object __instance)
        {
            bool isOhk = _damageEnabled && _damageMult == 0;
            if (!_damageEnabled || _damageMult <= 1 || isOhk) return;

            try
            {
                int currentLife = (int)_npcLifeField.GetValue(__instance);
                int damageDealt = _preStrikeLife - currentLife;
                if (damageDealt > 0)
                {
                    int extraDamage = damageDealt * (_damageMult - 1);
                    _npcLifeField.SetValue(__instance, currentLife - extraDamage);
                }
            }
            catch { }
        }

        /// <summary>Player.PickAmmo prefix — prevent ammo consumption for local player.</summary>
        private static void PickAmmo_Prefix(object __instance, ref bool dontConsume)
        {
            if (!_infiniteAmmo) return;

            try
            {
                int myPlayer = (int)_myPlayerField.GetValue(null);
                var players = _playerArrayField.GetValue(null) as Array;
                if (players != null && ReferenceEquals(players.GetValue(myPlayer), __instance))
                    dontConsume = true;
            }
            catch { dontConsume = true; }
        }

        /// <summary>Player.HorizontalMovement prefix — multiply speed fields before movement calc.</summary>
        private static void HorizontalMovement_Prefix(object __instance)
        {
            if (_runSpeedMult <= 1) return;

            try
            {
                int myPlayer = (int)_myPlayerField.GetValue(null);
                var players = _playerArrayField.GetValue(null) as Array;
                if (players == null || !ReferenceEquals(players.GetValue(myPlayer), __instance)) return;

                float maxRun = (float)_maxRunSpeedField.GetValue(__instance);
                float runAccel = (float)_runAccelerationField.GetValue(__instance);
                _maxRunSpeedField.SetValue(__instance, maxRun * _runSpeedMult);
                _runAccelerationField.SetValue(__instance, runAccel * _runSpeedMult);
            }
            catch { }
        }

        /// <summary>
        /// WorldGen.ShakeTree prefix — temporarily suppress Main.getGoodWorld
        /// to prevent FTW/Zenith tree bomb spawning. The getGoodWorld check in
        /// ShakeTree is exclusively used for the bomb spawn; all other tree drops
        /// (fruit, acorns, critters, coins) are unaffected.
        /// </summary>
        private static void ShakeTree_Prefix()
        {
            if (!_noTreeBombs || _getGoodWorldField == null) return;

            try
            {
                _savedGetGoodWorld = (bool)_getGoodWorldField.GetValue(null);
                if (_savedGetGoodWorld)
                    _getGoodWorldField.SetValue(null, false);
            }
            catch { }
        }

        /// <summary>WorldGen.ShakeTree postfix — restore Main.getGoodWorld.</summary>
        private static void ShakeTree_Postfix()
        {
            if (!_noTreeBombs || _getGoodWorldField == null) return;

            try
            {
                if (_savedGetGoodWorld)
                {
                    _getGoodWorldField.SetValue(null, true);
                    _savedGetGoodWorld = false;
                }
            }
            catch { }
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
                    byte r = (byte)(enabled ? 255 : 200);
                    byte g = (byte)(enabled ? 100 : 200);
                    byte b = (byte)(enabled ? 100 : 200);
                    newTextMethod.Invoke(null, new object[] { msg, r, g, b });
                }
            }
            catch { }
        }
    }
}
