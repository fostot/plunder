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
            _log = context.Logger;
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
            OpCheats.Initialize(_log);

            // Apply fishing config state
            FishingLuck.SetBuffsEnabled(_config.FishingBuffsEnabled);
            FishingLuck.SetAutoFishingPotion(_config.AutoFishingPotion);
            FishingLuck.SetAutoSonarPotion(_config.AutoSonarPotion);
            FishingLuck.SetAutoCratePotion(_config.AutoCratePotion);
            FishingLuck.SetFishingPowerMultiplier(_config.FishingPowerMultiplier);
            FishingLuck.SetLegendaryCratesOnly(_config.LegendaryCratesOnly);
            FishingLuck.SetCatchRerollMinRarity(_config.CatchRerollMinRarity);

            // Apply OpCheats config state
            OpCheats.SetGodMode(_config.OpGodMode);
            OpCheats.SetInfiniteMana(_config.OpInfiniteMana);
            OpCheats.SetMinionsEnabled(_config.OpMinionsEnabled);
            OpCheats.SetMinionCount(_config.OpMinionCount);
            OpCheats.SetInfiniteFlight(_config.OpInfiniteFlight);
            OpCheats.SetInfiniteAmmo(_config.OpInfiniteAmmo);
            OpCheats.SetInfiniteBreath(_config.OpInfiniteBreath);
            OpCheats.SetNoKnockback(_config.OpNoKnockback);
            OpCheats.SetDamageEnabled(_config.OpDamageEnabled);
            OpCheats.SetDamageMult(_config.OpDamageMult);
            OpCheats.SetNoFallDamage(_config.OpNoFallDamage);
            OpCheats.SetNoTreeBombs(_config.OpNoTreeBombs);
            OpCheats.SetSpawnRateMult(_config.OpSpawnRateMult);
            OpCheats.SetRunSpeedMult(_config.OpRunSpeedMult);

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

            // Wire panel callbacks — OP Cheats
            _panel.GetGodModeState = () => OpCheats.GodMode;
            _panel.OnGodModeToggle = OnToggleGodMode;
            _panel.GetInfiniteManaState = () => OpCheats.InfiniteMana;
            _panel.OnInfiniteManaToggle = OnToggleInfiniteMana;
            _panel.GetMinionsEnabledState = () => OpCheats.MinionsEnabled;
            _panel.OnMinionsToggle = OnToggleMinions;
            _panel.GetMinionCount = () => OpCheats.MinionCount;
            _panel.SetMinionCount = (v) =>
            {
                OpCheats.SetMinionCount(v);
                _config.Set("minionCount", v);
            };
            _panel.GetInfiniteFlightState = () => OpCheats.InfiniteFlight;
            _panel.OnInfiniteFlightToggle = OnToggleInfiniteFlight;
            _panel.GetInfiniteAmmoState = () => OpCheats.InfiniteAmmo;
            _panel.OnInfiniteAmmoToggle = OnToggleInfiniteAmmo;
            _panel.GetInfiniteBreathState = () => OpCheats.InfiniteBreath;
            _panel.OnInfiniteBreathToggle = OnToggleInfiniteBreath;
            _panel.GetNoKnockbackState = () => OpCheats.NoKnockback;
            _panel.OnNoKnockbackToggle = OnToggleNoKnockback;
            _panel.GetDamageEnabledState = () => OpCheats.DamageEnabled;
            _panel.OnDamageToggle = OnToggleDamage;
            _panel.GetDamageMult = () => OpCheats.DamageMult;
            _panel.SetDamageMult = (v) =>
            {
                OpCheats.SetDamageMult(v);
                _config.Set("damageMult", v);
            };
            _panel.GetNoFallDamageState = () => OpCheats.NoFallDamage;
            _panel.OnNoFallDamageToggle = OnToggleNoFallDamage;
            _panel.GetNoTreeBombsState = () => OpCheats.NoTreeBombs;
            _panel.OnNoTreeBombsToggle = OnToggleNoTreeBombs;
            _panel.GetSpawnRateMult = () => OpCheats.SpawnRateMult;
            _panel.SetSpawnRateMult = (v) =>
            {
                OpCheats.SetSpawnRateMult(v);
                _config.Set("spawnRateMult", v);
            };
            _panel.GetRunSpeedMult = () => OpCheats.RunSpeedMult;
            _panel.SetRunSpeedMult = (v) =>
            {
                OpCheats.SetRunSpeedMult(v);
                _config.Set("runSpeedMult", v);
            };

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
            OpCheats.EnsurePatched();

            if (_config.ShowPanelOnWorldLoad)
                _panel.Open();
        }

        public void OnWorldUnload()
        {
            _panel?.Close();
        }

        public void Unload()
        {
            _panel?.Unregister();
            FullBright.Unload();
            PlayerGlow.Unload();
            MapReveal.Unload();
            TeleportToCursor.Unload();
            MapTeleport.Unload();
            FishingLuck.Unload();
            OpCheats.Unload();
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

            // Sync OpCheats states
            OpCheats.SetGodMode(_config.OpGodMode);
            OpCheats.SetInfiniteMana(_config.OpInfiniteMana);
            OpCheats.SetMinionsEnabled(_config.OpMinionsEnabled);
            OpCheats.SetMinionCount(_config.OpMinionCount);
            OpCheats.SetInfiniteFlight(_config.OpInfiniteFlight);
            OpCheats.SetInfiniteAmmo(_config.OpInfiniteAmmo);
            OpCheats.SetInfiniteBreath(_config.OpInfiniteBreath);
            OpCheats.SetNoKnockback(_config.OpNoKnockback);
            OpCheats.SetDamageEnabled(_config.OpDamageEnabled);
            OpCheats.SetDamageMult(_config.OpDamageMult);
            OpCheats.SetNoFallDamage(_config.OpNoFallDamage);
            OpCheats.SetNoTreeBombs(_config.OpNoTreeBombs);
            OpCheats.SetSpawnRateMult(_config.OpSpawnRateMult);
            OpCheats.SetRunSpeedMult(_config.OpRunSpeedMult);

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

        // ---- OP Cheats toggle handlers ----

        private void OnToggleGodMode()
        {
            OpCheats.ToggleGodMode();
            _config.Set("godMode", OpCheats.GodMode);
        }

        private void OnToggleInfiniteMana()
        {
            OpCheats.ToggleInfiniteMana();
            _config.Set("infiniteMana", OpCheats.InfiniteMana);
        }

        private void OnToggleMinions()
        {
            OpCheats.ToggleMinions();
            _config.Set("minionsEnabled", OpCheats.MinionsEnabled);
        }

        private void OnToggleInfiniteFlight()
        {
            OpCheats.ToggleInfiniteFlight();
            _config.Set("infiniteFlight", OpCheats.InfiniteFlight);
        }

        private void OnToggleInfiniteAmmo()
        {
            OpCheats.ToggleInfiniteAmmo();
            _config.Set("infiniteAmmo", OpCheats.InfiniteAmmo);
        }

        private void OnToggleInfiniteBreath()
        {
            OpCheats.ToggleInfiniteBreath();
            _config.Set("infiniteBreath", OpCheats.InfiniteBreath);
        }

        private void OnToggleNoKnockback()
        {
            OpCheats.ToggleNoKnockback();
            _config.Set("noKnockback", OpCheats.NoKnockback);
        }

        private void OnToggleDamage()
        {
            OpCheats.ToggleDamage();
            _config.Set("damageEnabled", OpCheats.DamageEnabled);
        }

        private void OnToggleNoFallDamage()
        {
            OpCheats.ToggleNoFallDamage();
            _config.Set("noFallDamage", OpCheats.NoFallDamage);
        }

        private void OnToggleNoTreeBombs()
        {
            OpCheats.ToggleNoTreeBombs();
            _config.Set("noTreeBombs", OpCheats.NoTreeBombs);
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
