using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using TerrariaModder.Core.Logging;

namespace Plunder
{
    /// <summary>
    /// World Actions module — kill all enemies, clear items, clear projectiles,
    /// no gravestones, no item drop on death.
    /// One-shot actions use reflection on Main.npc[]/item[]/projectile[].
    /// Toggles use Harmony patches (gravestone prevention, death item drop prevention).
    /// </summary>
    public static class WorldActions
    {
        private static ILogger _log;
        private static Harmony _harmony;
        private static Timer _patchTimer;
        private static readonly object _patchLock = new object();
        private static bool _patchesApplied;

        // ---- State ----
        private static bool _noGravestones;
        private static bool _noDeathDrop;

        // ---- Properties ----
        public static bool NoGravestones => _noGravestones;
        public static bool NoDeathDrop => _noDeathDrop;

        // ---- Reflection cache ----
        private static Type _mainType;
        private static Type _npcType;
        private static FieldInfo _npcArrayField;       // Main.npc[] (NPC[])
        private static FieldInfo _itemArrayField;      // Main.item[] (Item[])
        private static FieldInfo _projectileArrayField; // Main.projectile[] (Projectile[])
        private static FieldInfo _maxNPCsField;        // Main.maxNPCs (int)
        private static FieldInfo _maxItemsField;       // Main.maxItems (int)
        private static FieldInfo _maxProjectilesField; // Main.maxProjectiles (int)
        private static FieldInfo _npcLifeField;        // NPC.life (int)
        private static FieldInfo _npcActiveField;      // NPC.active (bool)
        private static FieldInfo _npcTownNPCField;     // NPC.townNPC (bool)
        private static FieldInfo _itemActiveField;     // Item.active (bool)
        private static FieldInfo _projActiveField;     // Projectile.active (bool)
        private static FieldInfo _projTypeField;       // Projectile.type (int)
        private static MethodInfo _projKillMethod;     // Projectile.Kill()
        private static MethodInfo _npcCheckDeadMethod; // NPC.checkDead()
        private static FieldInfo _gameMenuField;       // Main.gameMenu (bool)
        private static bool _reflectionReady;

        // Gravestone projectile type IDs (vanilla values)
        // Tombstone=43, GraveMarker=44, CrossGraveMarker=45, Headstone=46,
        // Gravestone=47, Obelisk=48, RichGravestone1-5=715-719
        private static readonly int[] GravestoneTypes = {
            43, 44, 45, 46, 47, 48, 715, 716, 717, 718, 719
        };

        public static void Initialize(ILogger log)
        {
            _log = log;
            _harmony = new Harmony("com.plunder.worldactions");
            _patchTimer = new Timer(ApplyPatches, null, 5000, Timeout.Infinite);
            _log.Info("WorldActions initialized");
        }

        public static void EnsurePatched()
        {
            if (!_patchesApplied)
                ApplyPatches(null);
        }

        public static void Unload()
        {
            _harmony?.UnpatchAll("com.plunder.worldactions");
            _patchesApplied = false;
            _noGravestones = false;
            _noDeathDrop = false;
            _log?.Info("WorldActions unloaded");
        }

        // ============================================================
        //  TOGGLES
        // ============================================================

        public static void ToggleNoGravestones()
        {
            _noGravestones = !_noGravestones;
            ShowMsg("No Gravestones " + (_noGravestones ? "ON" : "OFF"), _noGravestones);
        }

        public static void SetNoGravestones(bool v) { _noGravestones = v; }

        public static void ToggleNoDeathDrop()
        {
            _noDeathDrop = !_noDeathDrop;
            ShowMsg("No Death Item Drop " + (_noDeathDrop ? "ON" : "OFF"), _noDeathDrop);
        }

        public static void SetNoDeathDrop(bool v) { _noDeathDrop = v; }

        // ============================================================
        //  ONE-SHOT ACTIONS
        // ============================================================

        public static void KillAllEnemies()
        {
            if (!EnsureReflection()) return;

            try
            {
                if (_gameMenuField != null && (bool)_gameMenuField.GetValue(null)) return;

                var npcArray = (Array)_npcArrayField.GetValue(null);
                int maxNPCs = _maxNPCsField != null ? (int)_maxNPCsField.GetValue(null) : npcArray.Length;
                int killed = 0;

                for (int i = 0; i < maxNPCs && i < npcArray.Length; i++)
                {
                    var npc = npcArray.GetValue(i);
                    if (npc == null) continue;

                    bool active = _npcActiveField != null && (bool)_npcActiveField.GetValue(npc);
                    if (!active) continue;

                    bool townNPC = _npcTownNPCField != null && (bool)_npcTownNPCField.GetValue(npc);
                    if (townNPC) continue;

                    // Set life to 0 then call checkDead() for proper death + loot drops
                    _npcLifeField?.SetValue(npc, 0);
                    npcArray.SetValue(npc, i);
                    if (_npcCheckDeadMethod != null)
                        _npcCheckDeadMethod.Invoke(npc, null);
                    killed++;
                }

                ShowMsg($"Killed {killed} enemies", true);
            }
            catch (Exception ex)
            {
                _log.Error($"WorldActions: KillAllEnemies failed - {ex.Message}");
            }
        }

        public static void ClearItems()
        {
            if (!EnsureReflection()) return;

            try
            {
                if (_gameMenuField != null && (bool)_gameMenuField.GetValue(null)) return;

                var itemArray = (Array)_itemArrayField.GetValue(null);
                int maxItems = _maxItemsField != null ? (int)_maxItemsField.GetValue(null) : itemArray.Length;
                int cleared = 0;

                for (int i = 0; i < maxItems && i < itemArray.Length; i++)
                {
                    var item = itemArray.GetValue(i);
                    if (item == null) continue;

                    bool active = _itemActiveField != null && (bool)_itemActiveField.GetValue(item);
                    if (!active) continue;

                    _itemActiveField.SetValue(item, false);
                    itemArray.SetValue(item, i);
                    cleared++;
                }

                ShowMsg($"Cleared {cleared} dropped items", true);
            }
            catch (Exception ex)
            {
                _log.Error($"WorldActions: ClearItems failed - {ex.Message}");
            }
        }

        public static void ClearProjectiles()
        {
            if (!EnsureReflection()) return;

            try
            {
                if (_gameMenuField != null && (bool)_gameMenuField.GetValue(null)) return;

                var projArray = (Array)_projectileArrayField.GetValue(null);
                int maxProj = _maxProjectilesField != null
                    ? (int)_maxProjectilesField.GetValue(null)
                    : projArray.Length;
                int cleared = 0;

                for (int i = 0; i < maxProj && i < projArray.Length; i++)
                {
                    var proj = projArray.GetValue(i);
                    if (proj == null) continue;

                    bool active = _projActiveField != null && (bool)_projActiveField.GetValue(proj);
                    if (!active) continue;

                    // Try Kill() method, fallback to setting active=false
                    if (_projKillMethod != null)
                    {
                        _projKillMethod.Invoke(proj, null);
                    }
                    else
                    {
                        _projActiveField.SetValue(proj, false);
                        projArray.SetValue(proj, i);
                    }
                    cleared++;
                }

                ShowMsg($"Cleared {cleared} projectiles", true);
            }
            catch (Exception ex)
            {
                _log.Error($"WorldActions: ClearProjectiles failed - {ex.Message}");
            }
        }

        // ============================================================
        //  HARMONY PATCHES
        // ============================================================

        private static void ApplyPatches(object _)
        {
            lock (_patchLock)
            {
                if (_patchesApplied) return;
                _patchesApplied = true;
            }

            if (_harmony == null) return;

            try
            {
                var asm = Assembly.Load("Terraria");

                // Patch Projectile.NewProjectile to prevent gravestones
                var projType = asm.GetType("Terraria.Projectile");
                if (projType != null)
                {
                    // Try the static NewProjectile that takes IEntitySource
                    var newProjMethod = projType.GetMethod("NewProjectile",
                        BindingFlags.Public | BindingFlags.Static);

                    if (newProjMethod != null)
                    {
                        var prefix = typeof(WorldActions).GetMethod(nameof(NewProjectile_Prefix),
                            BindingFlags.NonPublic | BindingFlags.Static);
                        _harmony.Patch(newProjMethod, prefix: new HarmonyMethod(prefix));
                        _log.Info("WorldActions: Patched Projectile.NewProjectile (gravestone blocker)");
                    }
                }

                // Patch Player.DropItems to prevent death item drops
                var playerType = asm.GetType("Terraria.Player");
                if (playerType != null)
                {
                    var dropMethod = playerType.GetMethod("DropItems",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (dropMethod != null)
                    {
                        var prefix = typeof(WorldActions).GetMethod(nameof(DropItems_Prefix),
                            BindingFlags.NonPublic | BindingFlags.Static);
                        _harmony.Patch(dropMethod, prefix: new HarmonyMethod(prefix));
                        _log.Info("WorldActions: Patched Player.DropItems (death drop blocker)");
                    }
                    else
                    {
                        _log.Warn("WorldActions: Player.DropItems not found");
                    }
                }

                _log.Info("WorldActions: Patches applied");
            }
            catch (Exception ex)
            {
                _log.Error($"WorldActions: Patch error - {ex.Message}");
            }
        }

        /// <summary>
        /// Harmony prefix for Projectile.NewProjectile — blocks gravestone projectile types.
        /// </summary>
        private static bool NewProjectile_Prefix(int Type)
        {
            if (!_noGravestones) return true;

            for (int i = 0; i < GravestoneTypes.Length; i++)
            {
                if (GravestoneTypes[i] == Type) return false; // Skip gravestone
            }
            return true;
        }

        /// <summary>
        /// Harmony prefix for Player.DropItems — blocks item drop on death for local player.
        /// </summary>
        private static bool DropItems_Prefix(object __instance)
        {
            if (!_noDeathDrop) return true;

            try
            {
                // Only block for local player
                if (_mainType == null) return true;

                var pubStatic = BindingFlags.Public | BindingFlags.Static;
                var myPlayerField = _mainType.GetField("myPlayer", pubStatic);
                var playerArrayField = _mainType.GetField("player", pubStatic);
                if (myPlayerField == null || playerArrayField == null) return true;

                int myPlayer = (int)myPlayerField.GetValue(null);
                var players = (Array)playerArrayField.GetValue(null);
                var localPlayer = players.GetValue(myPlayer);

                // Check if __instance is the local player
                if (object.ReferenceEquals(__instance, localPlayer))
                {
                    return false; // Skip dropping items
                }
            }
            catch { }
            return true;
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
                _npcType = Type.GetType("Terraria.NPC, Terraria") ?? asm.GetType("Terraria.NPC");
                var itemType = Type.GetType("Terraria.Item, Terraria") ?? asm.GetType("Terraria.Item");
                var projType = Type.GetType("Terraria.Projectile, Terraria") ?? asm.GetType("Terraria.Projectile");
                var entityType = Type.GetType("Terraria.Entity, Terraria") ?? asm.GetType("Terraria.Entity");

                if (_mainType == null)
                {
                    _log.Error("WorldActions: Main type not found");
                    return false;
                }

                var pubStatic = BindingFlags.Public | BindingFlags.Static;
                var pubInstance = BindingFlags.Public | BindingFlags.Instance;

                _npcArrayField = _mainType.GetField("npc", pubStatic);
                _itemArrayField = _mainType.GetField("item", pubStatic);
                _projectileArrayField = _mainType.GetField("projectile", pubStatic);
                _maxNPCsField = _mainType.GetField("maxNPCs", pubStatic);
                _maxItemsField = _mainType.GetField("maxItems", pubStatic);
                _maxProjectilesField = _mainType.GetField("maxProjectiles", pubStatic);
                _gameMenuField = _mainType.GetField("gameMenu", pubStatic);

                // NPC fields
                if (_npcType != null)
                {
                    _npcLifeField = _npcType.GetField("life", pubInstance)
                        ?? entityType?.GetField("life", pubInstance);
                    _npcActiveField = _npcType.GetField("active", pubInstance)
                        ?? entityType?.GetField("active", pubInstance);
                    _npcTownNPCField = _npcType.GetField("townNPC", pubInstance);
                    _npcCheckDeadMethod = _npcType.GetMethod("checkDead", pubInstance);
                }

                // Item fields
                if (itemType != null)
                {
                    _itemActiveField = itemType.GetField("active", pubInstance)
                        ?? entityType?.GetField("active", pubInstance);
                }

                // Projectile fields
                if (projType != null)
                {
                    _projActiveField = projType.GetField("active", pubInstance)
                        ?? entityType?.GetField("active", pubInstance);
                    _projTypeField = projType.GetField("type", pubInstance);
                    _projKillMethod = projType.GetMethod("Kill", pubInstance);
                }

                if (_npcArrayField == null || _itemArrayField == null || _projectileArrayField == null)
                {
                    _log.Error("WorldActions: Array fields not found");
                    return false;
                }

                _reflectionReady = true;
                _log.Info("WorldActions: Reflection initialized");
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"WorldActions: Reflection error - {ex.Message}");
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
