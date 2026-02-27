using TerrariaModder.Core;

namespace Plunder
{
    public class PlunderConfig
    {
        private readonly ModContext _context;

        // ---- General ----
        public bool Enabled { get; private set; }
        public bool ShowPanelOnWorldLoad { get; private set; }

        // ---- Panel ----
        public int PanelWidth { get; private set; }
        public int PanelHeight { get; private set; }
        public int PanelX { get; private set; }
        public int PanelY { get; private set; }

        // ---- Cheats: Visual ----
        public bool FullBrightEnabled { get; private set; }
        public bool PlayerGlowEnabled { get; private set; }
        public bool MapRevealEnabled { get; private set; }

        // ---- Cheats: Movement ----
        public bool TeleportToCursorEnabled { get; private set; }
        public bool MapTeleportEnabled { get; private set; }

        // ---- Cheats: OP Cheats ----
        public bool OpGodMode { get; private set; }
        public bool OpInfiniteMana { get; private set; }
        public bool OpMinionsEnabled { get; private set; }
        public int OpMinionCount { get; private set; }
        public bool OpInfiniteFlight { get; private set; }
        public bool OpInfiniteAmmo { get; private set; }
        public bool OpInfiniteBreath { get; private set; }
        public bool OpNoKnockback { get; private set; }
        public bool OpDamageEnabled { get; private set; }
        public int OpDamageMult { get; private set; }
        public bool OpNoFallDamage { get; private set; }
        public bool OpNoTreeBombs { get; private set; }
        public int OpSpawnRateMult { get; private set; }
        public int OpRunSpeedMult { get; private set; }
        public bool OpToolRangeEnabled { get; private set; }
        public int OpToolRangeMult { get; private set; }

        // ---- Cheats: World Actions ----
        public bool NoGravestones { get; private set; }
        public bool NoDeathDrop { get; private set; }

        // ---- Developer ----
        public bool DevMode { get; private set; }

        // ---- Cheats: Fishing ----
        public bool FishingBuffsEnabled { get; private set; }
        public bool AutoFishingPotion { get; private set; }
        public bool AutoSonarPotion { get; private set; }
        public bool AutoCratePotion { get; private set; }
        public int FishingPowerMultiplier { get; private set; }
        public bool LegendaryCratesOnly { get; private set; }
        public int CatchRerollMinRarity { get; private set; }

        // Static metadata
        public string ModVersion => BuildVersion.Version;

        // Expose context for CONFIG tab (keybinds/config panel access)
        public ModContext Context => _context;

        public PlunderConfig(ModContext context)
        {
            _context = context;
            Reload();
        }

        public void Reload()
        {
            var cfg = _context.Config;

            // General
            Enabled = cfg.Get<bool>("enabled", true);
            ShowPanelOnWorldLoad = cfg.Get<bool>("showPanelOnWorldLoad", false);

            // Panel
            PanelWidth = cfg.Get<int>("panelWidth", 420);
            PanelHeight = cfg.Get<int>("panelHeight", 600);
            PanelX = cfg.Get<int>("panelX", -1);
            PanelY = cfg.Get<int>("panelY", -1);

            // Developer
            DevMode = cfg.Get<bool>("devMode", false);

            // Cheats: Visual
            FullBrightEnabled = cfg.Get<bool>("fullBrightEnabled", false);
            PlayerGlowEnabled = cfg.Get<bool>("playerGlowEnabled", false);
            MapRevealEnabled = cfg.Get<bool>("mapRevealEnabled", false);

            // Cheats: Movement
            TeleportToCursorEnabled = cfg.Get<bool>("teleportToCursorEnabled", false);
            MapTeleportEnabled = cfg.Get<bool>("mapTeleportEnabled", false);

            // Cheats: OP Cheats
            OpGodMode = cfg.Get<bool>("godMode", false);
            OpInfiniteMana = cfg.Get<bool>("infiniteMana", false);
            OpMinionsEnabled = cfg.Get<bool>("minionsEnabled", false);
            OpMinionCount = cfg.Get<int>("minionCount", 0);
            OpInfiniteFlight = cfg.Get<bool>("infiniteFlight", false);
            OpInfiniteAmmo = cfg.Get<bool>("infiniteAmmo", false);
            OpInfiniteBreath = cfg.Get<bool>("infiniteBreath", false);
            OpNoKnockback = cfg.Get<bool>("noKnockback", false);
            OpDamageEnabled = cfg.Get<bool>("damageEnabled", false);
            OpDamageMult = cfg.Get<int>("damageMult", 0);
            OpNoFallDamage = cfg.Get<bool>("noFallDamage", false);
            OpNoTreeBombs = cfg.Get<bool>("noTreeBombs", false);
            OpSpawnRateMult = cfg.Get<int>("spawnRateMult", 1);
            OpRunSpeedMult = cfg.Get<int>("runSpeedMult", 1);
            OpToolRangeEnabled = cfg.Get<bool>("toolRangeEnabled", false);
            OpToolRangeMult = cfg.Get<int>("toolRangeMult", 1);

            // Cheats: World Actions
            NoGravestones = cfg.Get<bool>("noGravestones", false);
            NoDeathDrop = cfg.Get<bool>("noDeathDrop", false);

            // Cheats: Fishing
            FishingBuffsEnabled = cfg.Get<bool>("fishingBuffsEnabled", false);
            AutoFishingPotion = cfg.Get<bool>("autoFishingPotion", true);
            AutoSonarPotion = cfg.Get<bool>("autoSonarPotion", true);
            AutoCratePotion = cfg.Get<bool>("autoCratePotion", true);
            FishingPowerMultiplier = cfg.Get<int>("fishingPowerMultiplier", 1);
            LegendaryCratesOnly = cfg.Get<bool>("legendaryCratesOnly", false);
            CatchRerollMinRarity = cfg.Get<int>("catchRerollMinRarity", 0);
        }

        public void Set<T>(string key, T value)
        {
            _context.Config.Set<T>(key, value);
            _context.Config.Save();
            Reload();
        }
    }
}
