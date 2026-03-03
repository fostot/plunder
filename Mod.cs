using System;
using System.IO;
using System.Reflection;
using TerrariaModder.Core;
using TerrariaModder.Core.Events;
using TerrariaModder.Core.Logging;

namespace Plunder
{
    public class Mod : IMod
    {
        public string Id => "plunder";
        public string Name => "Plunder";
        public string Version => BuildVersion.Version;

        private ILogger _log;
        private ModContext _context;
        private PlunderConfig _config;
        private PlunderPanel _panel;
        private ItemPackManager _itemPacks;

        public void Initialize(ModContext context)
        {
            _log = new PlunderLogger(context.Logger);
            _context = context;

            _config = new PlunderConfig(context);

            if (!_config.Enabled)
            {
                _log.Info("Plunder is disabled in config");
                return;
            }

            // Initialize modules
            FullBright.Initialize(_log, _config.FullBrightEnabled);
            PlayerGlow.Initialize(_log, _config.PlayerGlowEnabled);
            MapReveal.Initialize(_log, _config.MapRevealEnabled);
            TeleportToCursor.Initialize(_log, _config.TeleportToCursorEnabled);
            MapTeleport.Initialize(_log, _config.MapTeleportEnabled);
            FishingLuck.Initialize(_log);
            Cheats.Initialize(_log);
            EnvironmentControl.Initialize(_log);
            WorldActions.Initialize(_log);

            // Apply fishing config state
            FishingLuck.SetBuffsEnabled(_config.FishingBuffsEnabled);
            FishingLuck.SetAutoFishingPotion(_config.AutoFishingPotion);
            FishingLuck.SetAutoSonarPotion(_config.AutoSonarPotion);
            FishingLuck.SetAutoCratePotion(_config.AutoCratePotion);
            FishingLuck.SetFishingPowerMultiplier(_config.FishingPowerMultiplier);
            FishingLuck.SetLegendaryCratesOnly(_config.LegendaryCratesOnly);
            FishingLuck.SetCatchRerollMinRarity(_config.CatchRerollMinRarity);

            // Apply Cheats config state
            Cheats.SetGodMode(_config.GodMode);
            Cheats.SetInfiniteMana(_config.InfiniteMana);
            Cheats.SetMinionsEnabled(_config.MinionsEnabled);
            Cheats.SetMinionCount(_config.MinionCount);
            Cheats.SetInfiniteFlight(_config.InfiniteFlight);
            Cheats.SetInfiniteAmmo(_config.InfiniteAmmo);
            Cheats.SetInfiniteBreath(_config.InfiniteBreath);
            Cheats.SetNoKnockback(_config.NoKnockback);
            Cheats.SetDamageEnabled(_config.DamageEnabled);
            Cheats.SetDamageMult(_config.DamageMult);
            Cheats.SetNoFallDamage(_config.NoFallDamage);
            Cheats.SetNoTreeBombs(_config.NoTreeBombs);
            Cheats.SetSpawnRateMult(_config.SpawnRateMult);
            Cheats.SetRunSpeedMult(_config.RunSpeedMult);
            Cheats.SetToolRangeEnabled(_config.ToolRangeEnabled);
            Cheats.SetToolRangeMult(_config.ToolRangeMult);

            // Apply WorldActions config state
            WorldActions.SetNoGravestones(_config.NoGravestones);
            WorldActions.SetNoDeathDrop(_config.NoDeathDrop);

            // Initialize item packs
            string modsDir = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location);
            _itemPacks = new ItemPackManager(_log, modsDir);

            // Create UI panel
            _panel = new PlunderPanel(_log, _config);

            // Wire panel callbacks — Visual
            _panel.GetFullBrightState = () => FullBright.IsActive;
            _panel.OnFullBrightToggle = OnToggleFullBright;
            _panel.GetPlayerGlowState = () => PlayerGlow.IsActive;
            _panel.OnPlayerGlowToggle = OnTogglePlayerGlow;
            _panel.GetMapRevealState = () => MapReveal.IsActive;
            _panel.OnMapRevealToggle = OnToggleMapReveal;

            // Wire panel callbacks — Movement
            _panel.GetTeleportState = () => TeleportToCursor.IsActive;
            _panel.OnTeleportToggle = OnToggleTeleport;
            _panel.GetMapTeleportState = () => MapTeleport.IsActive;
            _panel.OnMapTeleportToggle = OnToggleMapTeleport;

            // Wire panel callbacks — Cheats
            _panel.GetGodModeState = () => Cheats.GodMode;
            _panel.OnGodModeToggle = OnToggleGodMode;
            _panel.GetInfiniteManaState = () => Cheats.InfiniteMana;
            _panel.OnInfiniteManaToggle = OnToggleInfiniteMana;
            _panel.GetMinionsEnabledState = () => Cheats.MinionsEnabled;
            _panel.OnMinionsToggle = OnToggleMinions;
            _panel.GetMinionCount = () => Cheats.MinionCount;
            _panel.SetMinionCount = (v) =>
            {
                Cheats.SetMinionCount(v);
                _config.Set("minionCount", v);
            };
            _panel.GetInfiniteFlightState = () => Cheats.InfiniteFlight;
            _panel.OnInfiniteFlightToggle = OnToggleInfiniteFlight;
            _panel.GetInfiniteAmmoState = () => Cheats.InfiniteAmmo;
            _panel.OnInfiniteAmmoToggle = OnToggleInfiniteAmmo;
            _panel.GetInfiniteBreathState = () => Cheats.InfiniteBreath;
            _panel.OnInfiniteBreathToggle = OnToggleInfiniteBreath;
            _panel.GetNoKnockbackState = () => Cheats.NoKnockback;
            _panel.OnNoKnockbackToggle = OnToggleNoKnockback;
            _panel.GetDamageEnabledState = () => Cheats.DamageEnabled;
            _panel.OnDamageToggle = OnToggleDamage;
            _panel.GetDamageMult = () => Cheats.DamageMult;
            _panel.SetDamageMult = (v) =>
            {
                Cheats.SetDamageMult(v);
                _config.Set("damageMult", v);
            };
            _panel.GetNoFallDamageState = () => Cheats.NoFallDamage;
            _panel.OnNoFallDamageToggle = OnToggleNoFallDamage;
            _panel.GetNoTreeBombsState = () => Cheats.NoTreeBombs;
            _panel.OnNoTreeBombsToggle = OnToggleNoTreeBombs;
            _panel.GetSpawnRateMult = () => Cheats.SpawnRateMult;
            _panel.SetSpawnRateMult = (v) =>
            {
                Cheats.SetSpawnRateMult(v);
                _config.Set("spawnRateMult", v);
            };
            _panel.GetRunSpeedMult = () => Cheats.RunSpeedMult;
            _panel.SetRunSpeedMult = (v) =>
            {
                Cheats.SetRunSpeedMult(v);
                _config.Set("runSpeedMult", v);
            };
            _panel.GetToolRangeEnabledState = () => Cheats.ToolRangeEnabled;
            _panel.OnToolRangeToggle = OnToggleToolRange;
            _panel.GetToolRangeMult = () => Cheats.ToolRangeMult;
            _panel.SetToolRangeMult = (v) =>
            {
                Cheats.SetToolRangeMult(v);
                _config.Set("toolRangeMult", v);
            };

            // Wire panel callbacks — Environment
            _panel.GetTimePausedState = () => EnvironmentControl.TimePaused;
            _panel.OnTimePauseToggle = EnvironmentControl.ToggleTimePause;
            _panel.OnSetDawn = EnvironmentControl.SetDawn;
            _panel.OnSetNoon = EnvironmentControl.SetNoon;
            _panel.OnSetDusk = EnvironmentControl.SetDusk;
            _panel.OnSetMidnight = EnvironmentControl.SetMidnight;
            _panel.OnFastForwardDawn = EnvironmentControl.FastForwardToDawn;
            _panel.GetRainingState = EnvironmentControl.IsRaining;
            _panel.OnToggleRain = EnvironmentControl.ToggleRain;
            _panel.GetBloodMoonState = EnvironmentControl.IsBloodMoon;
            _panel.OnToggleBloodMoon = EnvironmentControl.ToggleBloodMoon;
            _panel.GetEclipseState = EnvironmentControl.IsEclipse;
            _panel.OnToggleEclipse = EnvironmentControl.ToggleEclipse;

            // Wire panel callbacks — World Actions
            _panel.GetNoGravestonesState = () => WorldActions.NoGravestones;
            _panel.OnNoGravestonesToggle = OnToggleNoGravestones;
            _panel.GetNoDeathDropState = () => WorldActions.NoDeathDrop;
            _panel.OnNoDeathDropToggle = OnToggleNoDeathDrop;
            _panel.OnKillAllEnemies = WorldActions.KillAllEnemies;
            _panel.OnClearItems = WorldActions.ClearItems;
            _panel.OnClearProjectiles = WorldActions.ClearProjectiles;

            // Wire panel callbacks — Fishing
            _panel.GetFishingBuffsState = () => FishingLuck.BuffsEnabled;
            _panel.OnFishingBuffsToggle = OnToggleFishingBuffs;
            _panel.GetAutoFishingPotion = () => FishingLuck.AutoFishingPotion;
            _panel.SetAutoFishingPotion = (v) =>
            {
                FishingLuck.SetAutoFishingPotion(v);
                _config.Set("autoFishingPotion", v);
            };
            _panel.GetAutoSonarPotion = () => FishingLuck.AutoSonarPotion;
            _panel.SetAutoSonarPotion = (v) =>
            {
                FishingLuck.SetAutoSonarPotion(v);
                _config.Set("autoSonarPotion", v);
            };
            _panel.GetAutoCratePotion = () => FishingLuck.AutoCratePotion;
            _panel.SetAutoCratePotion = (v) =>
            {
                FishingLuck.SetAutoCratePotion(v);
                _config.Set("autoCratePotion", v);
            };
            _panel.GetFishingPowerMultiplier = () => FishingLuck.FishingPowerMultiplier;
            _panel.SetFishingPowerMultiplier = (v) =>
            {
                FishingLuck.SetFishingPowerMultiplier(v);
                _config.Set("fishingPowerMultiplier", v);
            };
            _panel.GetLegendaryCratesState = () => FishingLuck.LegendaryCratesOnly;
            _panel.OnLegendaryCratesToggle = OnToggleLegendaryCrates;
            _panel.GetCatchRerollMinRarity = () => FishingLuck.CatchRerollMinRarity;
            _panel.SetCatchRerollMinRarity = (v) =>
            {
                FishingLuck.SetCatchRerollMinRarity(v);
                _config.Set("catchRerollMinRarity", v);
            };

            // Wire panel callbacks — Item Packs
            _panel.GetItemPacks = () => _itemPacks.Packs;
            _panel.OnSpawnPack = (id, mult) => _itemPacks.SpawnPack(id, mult);
            _panel.OnExportPack = (id) => _itemPacks.ExportPack(id);
            _panel.OnImportPack = (json) => _itemPacks.ImportPack(json);
            _panel.OnSearchItems = (query, max) => _itemPacks.SearchItems(query, max);
            _panel.OnCreatePack = (name, desc, cat, items) => _itemPacks.CreatePack(name, desc, cat, items);
            _panel.OnBuildCatalog = () => _itemPacks.BuildItemCatalog();
            _panel.OnDeletePack = (id) => _itemPacks.DeletePack(id);
            _panel.OnAddItemToPack = (packId, itemId, stack, name) => _itemPacks.AddItemToPack(packId, itemId, stack, name);
            _panel.OnRemoveItemFromPack = (packId, idx) => _itemPacks.RemoveItemFromPack(packId, idx);
            _panel.OnUpdateItemInPack = (packId, idx, itemId, stack, name) => _itemPacks.UpdateItemInPack(packId, idx, itemId, stack, name);
            _panel.OnRenamePack = (packId, newName) => _itemPacks.RenamePack(packId, newName);
            _panel.OnUpdatePackCategory = (packId, cat) => _itemPacks.UpdatePackCategory(packId, cat);
            _panel.OnResetBuiltInPack = (packId) => _itemPacks.ResetBuiltInPack(packId);

            // Wire panel callbacks — Mod Menu
            _panel.OnOpenModMenu = OpenModMenu;

            // Register keybinds
            context.RegisterKeybind("toggle-panel", "Toggle Plunder Panel",
                "Open or close the main Plunder panel", "OemCloseBrackets",
                OnTogglePanel);

            context.RegisterKeybind("toggle-fullbright", "Toggle Full Bright",
                "Toggle darkness removal on/off", "Y",
                OnToggleFullBright);

            context.RegisterKeybind("toggle-player-glow", "Toggle Player Glow",
                "Toggle player light emission on/off", "None",
                OnTogglePlayerGlow);

            context.RegisterKeybind("toggle-fishing-buffs", "Toggle Fishing Buffs",
                "Toggle auto fishing buff injection on/off", "None",
                OnToggleFishingBuffs);

            context.RegisterKeybind("toggle-teleport", "Toggle Teleport To Cursor",
                "Toggle the teleport to cursor feature on/off", "NumPad4",
                OnToggleTeleport);

            context.RegisterKeybind("do-teleport", "Teleport To Cursor",
                "Teleport to the mouse cursor position (must be enabled first)", "T",
                TeleportToCursor.Teleport);

            context.RegisterKeybind("toggle-map-teleport", "Toggle Map Click Teleport",
                "Toggle right-click map teleport on/off", "None",
                OnToggleMapTeleport);

            context.RegisterKeybind("toggle-godmode", "Toggle God Mode",
                "Toggle invincibility on/off", "None",
                OnToggleGodMode);

            _panel.Register();
            FrameEvents.OnPreUpdate += _panel.Update;

            _log.Info($"Plunder v{BuildVersion.Version} initialized");
            _log.Info("  ] = Panel | Y = FullBright | NumPad4 = Toggle Teleport | T = Teleport");
        }

        public void OnWorldLoad()
        {
            if (!_config.Enabled) return;

            // Ensure all Harmony patches are applied
            FullBright.EnsurePatched();
            PlayerGlow.EnsurePatched();
            MapReveal.EnsurePatched();
            MapTeleport.EnsurePatched();
            FishingLuck.EnsurePatched();
            Cheats.EnsurePatched();
            WorldActions.EnsurePatched();

            if (_config.ShowPanelOnWorldLoad)
                _panel.Open();
        }

        public void OnWorldUnload()
        {
            _panel?.Close();
        }

        public void Unload()
        {
            FrameEvents.OnPreUpdate -= _panel.Update;
            _panel?.Unregister();
            FullBright.Unload();
            PlayerGlow.Unload();
            MapReveal.Unload();
            TeleportToCursor.Unload();
            MapTeleport.Unload();
            FishingLuck.Unload();
            Cheats.Unload();
            EnvironmentControl.Unload();
            WorldActions.Unload();
            _log.Info("Plunder unloaded");
        }

        public void OnConfigChanged()
        {
            _config?.Reload();
            _panel?.ApplyConfig();

            // Sync all module states from config
            FullBright.SetActive(_config.FullBrightEnabled);
            PlayerGlow.SetActive(_config.PlayerGlowEnabled);
            MapReveal.SetActive(_config.MapRevealEnabled);
            TeleportToCursor.SetActive(_config.TeleportToCursorEnabled);
            MapTeleport.SetActive(_config.MapTeleportEnabled);
            FishingLuck.SetBuffsEnabled(_config.FishingBuffsEnabled);
            FishingLuck.SetAutoFishingPotion(_config.AutoFishingPotion);
            FishingLuck.SetAutoSonarPotion(_config.AutoSonarPotion);
            FishingLuck.SetAutoCratePotion(_config.AutoCratePotion);
            FishingLuck.SetFishingPowerMultiplier(_config.FishingPowerMultiplier);
            FishingLuck.SetLegendaryCratesOnly(_config.LegendaryCratesOnly);
            FishingLuck.SetCatchRerollMinRarity(_config.CatchRerollMinRarity);

            // Sync Cheats states
            Cheats.SetGodMode(_config.GodMode);
            Cheats.SetInfiniteMana(_config.InfiniteMana);
            Cheats.SetMinionsEnabled(_config.MinionsEnabled);
            Cheats.SetMinionCount(_config.MinionCount);
            Cheats.SetInfiniteFlight(_config.InfiniteFlight);
            Cheats.SetInfiniteAmmo(_config.InfiniteAmmo);
            Cheats.SetInfiniteBreath(_config.InfiniteBreath);
            Cheats.SetNoKnockback(_config.NoKnockback);
            Cheats.SetDamageEnabled(_config.DamageEnabled);
            Cheats.SetDamageMult(_config.DamageMult);
            Cheats.SetNoFallDamage(_config.NoFallDamage);
            Cheats.SetNoTreeBombs(_config.NoTreeBombs);
            Cheats.SetSpawnRateMult(_config.SpawnRateMult);
            Cheats.SetRunSpeedMult(_config.RunSpeedMult);
            Cheats.SetToolRangeEnabled(_config.ToolRangeEnabled);
            Cheats.SetToolRangeMult(_config.ToolRangeMult);

            // Sync WorldActions states
            WorldActions.SetNoGravestones(_config.NoGravestones);
            WorldActions.SetNoDeathDrop(_config.NoDeathDrop);

            _log.Info("Plunder config reloaded");
        }

        // ---- Toggle handlers ----

        private void OnTogglePanel()
        {
            _panel?.Toggle();
        }

        private void OnToggleFullBright()
        {
            FullBright.Toggle();
            _config.Set("fullBrightEnabled", FullBright.IsActive);
        }

        private void OnTogglePlayerGlow()
        {
            PlayerGlow.Toggle();
            _config.Set("playerGlowEnabled", PlayerGlow.IsActive);
        }

        private void OnToggleMapReveal()
        {
            MapReveal.Toggle();
            _config.Set("mapRevealEnabled", MapReveal.IsActive);
        }

        private void OnToggleTeleport()
        {
            TeleportToCursor.Toggle();
            _config.Set("teleportToCursorEnabled", TeleportToCursor.IsActive);
        }

        private void OnToggleFishingBuffs()
        {
            FishingLuck.ToggleBuffs();
            _config.Set("fishingBuffsEnabled", FishingLuck.BuffsEnabled);
        }

        private void OnToggleMapTeleport()
        {
            MapTeleport.Toggle();
            _config.Set("mapTeleportEnabled", MapTeleport.IsActive);
        }

        // ---- Cheats toggle handlers ----

        private void OnToggleGodMode()
        {
            Cheats.ToggleGodMode();
            _config.Set("godMode", Cheats.GodMode);
        }

        private void OnToggleInfiniteMana()
        {
            Cheats.ToggleInfiniteMana();
            _config.Set("infiniteMana", Cheats.InfiniteMana);
        }

        private void OnToggleMinions()
        {
            Cheats.ToggleMinions();
            _config.Set("minionsEnabled", Cheats.MinionsEnabled);
        }

        private void OnToggleInfiniteFlight()
        {
            Cheats.ToggleInfiniteFlight();
            _config.Set("infiniteFlight", Cheats.InfiniteFlight);
        }

        private void OnToggleInfiniteAmmo()
        {
            Cheats.ToggleInfiniteAmmo();
            _config.Set("infiniteAmmo", Cheats.InfiniteAmmo);
        }

        private void OnToggleInfiniteBreath()
        {
            Cheats.ToggleInfiniteBreath();
            _config.Set("infiniteBreath", Cheats.InfiniteBreath);
        }

        private void OnToggleNoKnockback()
        {
            Cheats.ToggleNoKnockback();
            _config.Set("noKnockback", Cheats.NoKnockback);
        }

        private void OnToggleDamage()
        {
            Cheats.ToggleDamage();
            _config.Set("damageEnabled", Cheats.DamageEnabled);
        }

        private void OnToggleNoFallDamage()
        {
            Cheats.ToggleNoFallDamage();
            _config.Set("noFallDamage", Cheats.NoFallDamage);
        }

        private void OnToggleNoTreeBombs()
        {
            Cheats.ToggleNoTreeBombs();
            _config.Set("noTreeBombs", Cheats.NoTreeBombs);
        }

        private void OnToggleToolRange()
        {
            Cheats.ToggleToolRange();
            _config.Set("toolRangeEnabled", Cheats.ToolRangeEnabled);
        }

        // ---- World Actions toggle handlers ----

        private void OnToggleNoGravestones()
        {
            WorldActions.ToggleNoGravestones();
            _config.Set("noGravestones", WorldActions.NoGravestones);
        }

        private void OnToggleNoDeathDrop()
        {
            WorldActions.ToggleNoDeathDrop();
            _config.Set("noDeathDrop", WorldActions.NoDeathDrop);
        }

        private void OnToggleLegendaryCrates()
        {
            bool newState = !FishingLuck.LegendaryCratesOnly;
            FishingLuck.SetLegendaryCratesOnly(newState);
            _config.Set("legendaryCratesOnly", newState);
        }

        private void OpenModMenu()
        {
            try
            {
                var menuType = Type.GetType(
                    "TerrariaModder.Core.UI.ModMenu, TerrariaModder.Core");

                if (menuType == null)
                {
                    var coreAsm = Assembly.Load("TerrariaModder.Core");
                    menuType = coreAsm?.GetType("TerrariaModder.Core.UI.ModMenu");
                }

                if (menuType != null)
                {
                    var toggleMethod = menuType.GetMethod("ToggleMenu",
                        BindingFlags.Public | BindingFlags.Static);
                    toggleMethod?.Invoke(null, null);
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to open Mod Menu: {ex.Message}");
            }
        }
    }
}
