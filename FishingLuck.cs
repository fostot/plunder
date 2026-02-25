using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using TerrariaModder.Core.Logging;

namespace Plunder
{
    /// <summary>
    /// FishingLuck module - enhances fishing with buff injection, power boost,
    /// legendary crates, and catch reroll via Harmony patches.
    /// All features independently toggleable. Uses pure reflection (no XNA deps).
    /// </summary>
    public static class FishingLuck
    {
        private static ILogger _log;
        private static Harmony _harmony;
        private static Timer _patchTimer;
        private static readonly object _patchLock = new object();
        private static bool _patchesApplied;

        // ---- Feature toggles ----
        private static bool _buffsEnabled;
        private static bool _autoFishingPotion = true;
        private static bool _autoSonarPotion = true;
        private static bool _autoCratePotion = true;
        private static int _fishingPowerMultiplier = 1;
        private static bool _legendaryCratesOnly;
        private static int _catchRerollMinRarity;

        // ---- Buff IDs ----
        private const int BuffFishing = 121;
        private const int BuffSonar = 122;
        private const int BuffCrate = 123;
        private const int BuffDuration = 86400; // ~24 min at 60fps

        // ---- Reflection cache ----
        private static Type _mainType;
        private static Type _playerType;
        private static Type _projectileType;
        private static Type _itemType;
        private static Type _fishingAttemptType;

        private static FieldInfo _myPlayerField;
        private static FieldInfo _playerArrayField;
        private static FieldInfo _gamePausedField;
        private static FieldInfo _gameMenuField;

        private static MethodInfo _addBuffMethod;
        private static MethodInfo _findBuffIndexMethod;
        private static PropertyInfo _heldItemProperty;
        private static FieldInfo _fishingPoleField;
        private static FieldInfo _itemRareField;

        // FishingAttempt fields
        private static FieldInfo _faCrate;
        private static FieldInfo _faLegendary;
        private static FieldInfo _faVeryRare;
        private static FieldInfo _faRare;
        private static FieldInfo _faUncommon;
        private static FieldInfo _faCommon;
        private static FieldInfo _faRolledItemDrop;
        private static FieldInfo _faFishingLevel;

        private static bool _reflectionReady;

        // ---- Public state accessors ----
        public static bool BuffsEnabled => _buffsEnabled;
        public static bool AutoFishingPotion => _autoFishingPotion;
        public static bool AutoSonarPotion => _autoSonarPotion;
        public static bool AutoCratePotion => _autoCratePotion;
        public static int FishingPowerMultiplier => _fishingPowerMultiplier;
        public static bool LegendaryCratesOnly => _legendaryCratesOnly;
        public static int CatchRerollMinRarity => _catchRerollMinRarity;

        public static void Initialize(ILogger log)
        {
            _log = log;
            _harmony = new Harmony("com.plunder.fishingluck");
            _patchTimer = new Timer(ApplyPatches, null, 5000, Timeout.Infinite);
            _log.Info("FishingLuck initialized");
        }

        // ---- Setters (called from Mod.cs / panel) ----
        public static void SetBuffsEnabled(bool v) { _buffsEnabled = v; }
        public static void SetAutoFishingPotion(bool v) { _autoFishingPotion = v; }
        public static void SetAutoSonarPotion(bool v) { _autoSonarPotion = v; }
        public static void SetAutoCratePotion(bool v) { _autoCratePotion = v; }
        public static void SetFishingPowerMultiplier(int v) { _fishingPowerMultiplier = Math.Max(1, Math.Min(10, v)); }
        public static void SetLegendaryCratesOnly(bool v) { _legendaryCratesOnly = v; }
        public static void SetCatchRerollMinRarity(int v) { _catchRerollMinRarity = Math.Max(0, Math.Min(5, v)); }

        public static void ToggleBuffs()
        {
            _buffsEnabled = !_buffsEnabled;
            _log.Info($"FishingLuck Buffs: {(_buffsEnabled ? "ON" : "OFF")}");
            ShowMessage("Fishing Buffs " + (_buffsEnabled ? "Enabled" : "Disabled"),
                _buffsEnabled);
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
            _harmony?.UnpatchAll("com.plunder.fishingluck");
            _patchesApplied = false;
            _buffsEnabled = false;
            _log?.Info("FishingLuck unloaded");
        }

        // ---- Harmony patches ----

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
                    _log.Error("FishingLuck: Reflection init failed, skipping patches");
                    return;
                }

                // 1) Patch Player.Update(int) - postfix for buff injection
                var updateMethod = _playerType.GetMethod("Update",
                    BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(int) }, null);

                if (updateMethod != null)
                {
                    var postfix = typeof(FishingLuck).GetMethod(nameof(PlayerUpdate_Postfix),
                        BindingFlags.NonPublic | BindingFlags.Static);
                    _harmony.Patch(updateMethod, postfix: new HarmonyMethod(postfix));
                    _log.Info("FishingLuck: Patched Player.Update (buff injection)");
                }

                // 2) Patch Projectile.FishingCheck_RollDropLevels for legendary crates + power boost
                var rollDropLevels = _projectileType.GetMethod("FishingCheck_RollDropLevels",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (rollDropLevels != null)
                {
                    var postfix = typeof(FishingLuck).GetMethod(nameof(RollDropLevels_Postfix),
                        BindingFlags.NonPublic | BindingFlags.Static);
                    _harmony.Patch(rollDropLevels, postfix: new HarmonyMethod(postfix));
                    _log.Info("FishingLuck: Patched FishingCheck_RollDropLevels (legendary + power)");
                }
                else
                {
                    _log.Warn("FishingLuck: Could not find FishingCheck_RollDropLevels");
                }

                // 3) Patch Projectile.FishingCheck_RollItemDrop for catch reroll
                var rollItemDrop = _projectileType.GetMethod("FishingCheck_RollItemDrop",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (rollItemDrop != null)
                {
                    var postfix = typeof(FishingLuck).GetMethod(nameof(RollItemDrop_Postfix),
                        BindingFlags.NonPublic | BindingFlags.Static);
                    _harmony.Patch(rollItemDrop, postfix: new HarmonyMethod(postfix));
                    _log.Info("FishingLuck: Patched FishingCheck_RollItemDrop (catch reroll)");
                }
                else
                {
                    _log.Warn("FishingLuck: Could not find FishingCheck_RollItemDrop");
                }

                _log.Info("FishingLuck: All Harmony patches applied");
            }
            catch (Exception ex)
            {
                _log.Error($"FishingLuck: Patch error - {ex.Message}");
            }
        }

        private static void InitReflection()
        {
            if (_reflectionReady) return;

            try
            {
                var asm = Assembly.Load("Terraria");

                _mainType = Type.GetType("Terraria.Main, Terraria") ?? asm.GetType("Terraria.Main");
                _playerType = Type.GetType("Terraria.Player, Terraria") ?? asm.GetType("Terraria.Player");
                _projectileType = Type.GetType("Terraria.Projectile, Terraria") ?? asm.GetType("Terraria.Projectile");
                _itemType = Type.GetType("Terraria.Item, Terraria") ?? asm.GetType("Terraria.Item");
                _fishingAttemptType = Type.GetType("Terraria.DataStructures.FishingAttempt, Terraria")
                    ?? asm.GetType("Terraria.DataStructures.FishingAttempt");

                if (_mainType == null || _playerType == null || _projectileType == null)
                {
                    _log.Error("FishingLuck: Core types not found");
                    return;
                }

                // Main fields
                _myPlayerField = _mainType.GetField("myPlayer", BindingFlags.Public | BindingFlags.Static);
                _playerArrayField = _mainType.GetField("player", BindingFlags.Public | BindingFlags.Static);
                _gameMenuField = _mainType.GetField("gameMenu", BindingFlags.Public | BindingFlags.Static);
                _gamePausedField = _mainType.GetField("gamePaused", BindingFlags.Public | BindingFlags.Static);

                // Player methods
                _addBuffMethod = _playerType.GetMethod("AddBuff",
                    BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(int), typeof(int), typeof(bool) }, null);

                _findBuffIndexMethod = _playerType.GetMethod("FindBuffIndex",
                    BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(int) }, null);

                // Player.HeldItem property
                _heldItemProperty = _playerType.GetProperty("HeldItem",
                    BindingFlags.Public | BindingFlags.Instance);

                // Item fields
                if (_itemType != null)
                {
                    _fishingPoleField = _itemType.GetField("fishingPole",
                        BindingFlags.Public | BindingFlags.Instance);
                    _itemRareField = _itemType.GetField("rare",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // FishingAttempt fields
                if (_fishingAttemptType != null)
                {
                    var flags = BindingFlags.Public | BindingFlags.Instance;
                    _faCrate = _fishingAttemptType.GetField("crate", flags);
                    _faLegendary = _fishingAttemptType.GetField("legendary", flags);
                    _faVeryRare = _fishingAttemptType.GetField("veryrare", flags);
                    _faRare = _fishingAttemptType.GetField("rare", flags);
                    _faUncommon = _fishingAttemptType.GetField("uncommon", flags);
                    _faCommon = _fishingAttemptType.GetField("common", flags);
                    _faRolledItemDrop = _fishingAttemptType.GetField("rolledItemDrop", flags);
                    _faFishingLevel = _fishingAttemptType.GetField("fishingLevel", flags);
                }

                _reflectionReady = true;
                _log.Info("FishingLuck: Reflection initialized");
            }
            catch (Exception ex)
            {
                _log.Error($"FishingLuck: Reflection error - {ex.Message}");
            }
        }

        // Throttle buff injection to every 10 ticks (like AutoBuffs pattern)
        private static int _buffTickCounter;

        /// <summary>
        /// Postfix on Player.Update(int i) - inject permanent fishing buffs.
        /// When enabled, buffs are applied unconditionally (no fishing rod required).
        /// Buffs have ~24 min duration and are reapplied every 10 ticks.
        /// </summary>
        private static void PlayerUpdate_Postfix(object __instance, int i)
        {
            if (!_buffsEnabled || !_reflectionReady) return;

            try
            {
                // Only apply to local player
                if (_gameMenuField != null && (bool)_gameMenuField.GetValue(null)) return;
                int myPlayer = (int)_myPlayerField.GetValue(null);
                if (i != myPlayer) return;

                // Throttle: only check every 10 ticks to reduce overhead
                _buffTickCounter++;
                if (_buffTickCounter % 10 != 0) return;

                // Inject buffs unconditionally — permanent buffs, no rod required
                if (_autoFishingPotion) TryAddBuff(__instance, BuffFishing);
                if (_autoSonarPotion) TryAddBuff(__instance, BuffSonar);
                if (_autoCratePotion) TryAddBuff(__instance, BuffCrate);
            }
            catch { }
        }

        private static void TryAddBuff(object player, int buffId)
        {
            try
            {
                int idx = (int)_findBuffIndexMethod.Invoke(player, new object[] { buffId });
                if (idx < 0)
                {
                    _addBuffMethod.Invoke(player, new object[] { buffId, BuffDuration, true });
                }
            }
            catch { }
        }

        /// <summary>
        /// Postfix on Projectile.FishingCheck_RollDropLevels(ref FishingAttempt).
        /// Applies fishing power boost and legendary crates.
        /// The parameter is passed by ref, so we modify the boxed struct via Harmony's __args.
        /// </summary>
        private static void RollDropLevels_Postfix(object[] __args)
        {
            if (!_reflectionReady) return;
            if (!_legendaryCratesOnly && _fishingPowerMultiplier <= 1) return;

            try
            {
                // __args[0] is the ref FishingAttempt (boxed)
                object attempt = __args[0];
                if (attempt == null) return;

                // Fishing power boost
                if (_fishingPowerMultiplier > 1 && _faFishingLevel != null)
                {
                    int level = (int)_faFishingLevel.GetValue(attempt);
                    _faFishingLevel.SetValue(attempt, level * _fishingPowerMultiplier);
                }

                // Legendary crates - force all quality flags to max
                if (_legendaryCratesOnly)
                {
                    _faCrate?.SetValue(attempt, true);
                    _faLegendary?.SetValue(attempt, true);
                    _faVeryRare?.SetValue(attempt, true);
                    _faRare?.SetValue(attempt, true);
                    _faUncommon?.SetValue(attempt, true);
                    _faCommon?.SetValue(attempt, true);
                }

                // Write back the modified struct
                __args[0] = attempt;
            }
            catch { }
        }

        /// <summary>
        /// Postfix on Projectile.FishingCheck_RollItemDrop(ref FishingAttempt).
        /// If the rolled item's rarity is below the minimum threshold, zero it out
        /// so the catch is skipped (bobber stays, player gets another bite).
        /// </summary>
        private static void RollItemDrop_Postfix(object[] __args)
        {
            if (!_reflectionReady || _catchRerollMinRarity <= 0) return;

            try
            {
                object attempt = __args[0];
                if (attempt == null || _faRolledItemDrop == null) return;

                int itemId = (int)_faRolledItemDrop.GetValue(attempt);
                if (itemId <= 0) return;

                // Look up item rarity
                int rarity = GetItemRarity(itemId);
                if (rarity < _catchRerollMinRarity)
                {
                    _faRolledItemDrop.SetValue(attempt, 0);
                    __args[0] = attempt;
                }
            }
            catch { }
        }

        private static int GetItemRarity(int itemId)
        {
            try
            {
                // Create a temporary Item and SetDefaults to get rarity
                var item = Activator.CreateInstance(_itemType);
                var setDefaults = _itemType.GetMethod("SetDefaults",
                    BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(int) }, null);

                if (setDefaults != null)
                {
                    setDefaults.Invoke(item, new object[] { itemId });
                    return (int)_itemRareField.GetValue(item);
                }
            }
            catch { }
            return 0;
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
                    byte g = (byte)(enabled ? 200 : 200);
                    byte b = (byte)(enabled ? 255 : 200);
                    newTextMethod.Invoke(null, new object[] { msg, r, g, b });
                }
            }
            catch { }
        }
    }
}
