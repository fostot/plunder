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

        // ---- Cheats: Player/Combat/World ----
        public bool GodMode { get; private set; }
        public bool InfiniteMana { get; private set; }
        public bool MinionsEnabled { get; private set; }
        public int MinionCount { get; private set; }
        public bool InfiniteFlight { get; private set; }
        public bool InfiniteAmmo { get; private set; }
        public bool InfiniteBreath { get; private set; }
        public bool NoKnockback { get; private set; }
        public bool DamageEnabled { get; private set; }
        public int DamageMult { get; private set; }
        public bool NoFallDamage { get; private set; }
        public bool NoTreeBombs { get; private set; }
        public int SpawnRateMult { get; private set; }
        public int RunSpeedMult { get; private set; }
        public bool ToolRangeEnabled { get; private set; }
        public int ToolRangeMult { get; private set; }

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

            // Cheats: Player/Combat/World
            GodMode = cfg.Get<bool>("godMode", false);
            InfiniteMana = cfg.Get<bool>("infiniteMana", false);
            MinionsEnabled = cfg.Get<bool>("minionsEnabled", false);
            MinionCount = cfg.Get<int>("minionCount", 0);
            InfiniteFlight = cfg.Get<bool>("infiniteFlight", false);
            InfiniteAmmo = cfg.Get<bool>("infiniteAmmo", false);
            InfiniteBreath = cfg.Get<bool>("infiniteBreath", false);
            NoKnockback = cfg.Get<bool>("noKnockback", false);
            DamageEnabled = cfg.Get<bool>("damageEnabled", false);
            DamageMult = cfg.Get<int>("damageMult", 0);
            NoFallDamage = cfg.Get<bool>("noFallDamage", false);
            NoTreeBombs = cfg.Get<bool>("noTreeBombs", false);
            SpawnRateMult = cfg.Get<int>("spawnRateMult", 1);
            RunSpeedMult = cfg.Get<int>("runSpeedMult", 1);
            ToolRangeEnabled = cfg.Get<bool>("toolRangeEnabled", false);
            ToolRangeMult = cfg.Get<int>("toolRangeMult", 1);

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
