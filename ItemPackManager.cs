using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TerrariaModder.Core.Logging;

namespace Plunder
{
    /// <summary>
    /// Represents a single item in a pack (ID + quantity).
    /// </summary>
    public class PackItem
    {
        public int ItemId { get; set; }
        public int Stack { get; set; }
        public string Name { get; set; } // Display name (resolved at runtime)

        public PackItem() { }
        public PackItem(int itemId, int stack, string name = null)
        {
            ItemId = itemId;
            Stack = stack;
            Name = name;
        }
    }

    /// <summary>
    /// Represents a collection of items that can be spawned together.
    /// </summary>
    public class ItemPack
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string Category { get; set; } = "General";
        public bool IsBuiltIn { get; set; }
        public List<PackItem> Items { get; set; } = new List<PackItem>();

        public ItemPack() { }
        public ItemPack(string id, string name, string desc, string author, bool builtIn, string category = "General")
        {
            Id = id;
            Name = name;
            Description = desc;
            Author = author;
            IsBuiltIn = builtIn;
            Category = category;
        }
    }

    /// <summary>
    /// Manages item packs - built-in and user-created.
    /// Handles spawning items into player inventory and JSON import/export.
    /// </summary>
    public class ItemPackManager
    {
        private readonly ILogger _log;
        private readonly string _packsDir;
        private readonly List<ItemPack> _packs = new List<ItemPack>();

        // Reflection cache
        private static Type _mainType;
        private static Type _itemType;
        private static Type _playerType;
        private static FieldInfo _myPlayerField;
        private static FieldInfo _playerArrayField;
        private static FieldInfo _playerInventoryField;
        private static FieldInfo _itemTypeField;
        private static FieldInfo _itemStackField;
        private static FieldInfo _itemPrefixField;
        private static FieldInfo _itemMaxStackField;
        private static FieldInfo _itemNameField;
        private static MethodInfo _itemSetDefaultsMethod;
        private static MethodInfo _quickSpawnItemMethod;
        private static bool _reflectionReady;

        public IReadOnlyList<ItemPack> Packs => _packs;

        public ItemPackManager(ILogger log, string modsDir)
        {
            _log = log;
            _packsDir = Path.Combine(modsDir, "packs");
            RegisterBuiltInPacks();
            LoadUserPacks();
        }

        private void RegisterBuiltInPacks()
        {
            // ================================================================
            //  ESSENTIALS
            // ================================================================

            var starter = new ItemPack("starter", "Starter Kit",
                "Essential tools and gear for a fresh start",
                "Plunder", true, "Essentials");
            starter.Items.AddRange(new[]
            {
                new PackItem(3507, 1, "Copper Shortsword"),
                new PackItem(3509, 1, "Copper Pickaxe"),
                new PackItem(3506, 1, "Copper Axe"),
                new PackItem(8, 99, "Torch"),
                new PackItem(28, 10, "Healing Potion"),
                new PackItem(110, 10, "Mana Potion"),
                new PackItem(292, 30, "Rope"),
                new PackItem(2350, 3, "Recall Potion"),
                new PackItem(29, 50, "Shuriken"),
            });
            _packs.Add(starter);

            var explorer = new ItemPack("explorer", "Explorer Pack",
                "Mobility and light sources for cave diving",
                "Plunder", true, "Essentials");
            explorer.Items.AddRange(new[]
            {
                new PackItem(8, 200, "Torch"),
                new PackItem(282, 50, "Glowstick"),
                new PackItem(292, 100, "Rope"),
                new PackItem(2350, 10, "Recall Potion"),
                new PackItem(2351, 5, "Teleportation Potion"),
                new PackItem(296, 5, "Spelunker Potion"),
                new PackItem(298, 5, "Shine Potion"),
                new PackItem(305, 3, "Gravitation Potion"),
                new PackItem(49, 1, "Band of Regeneration"),
                new PackItem(54, 1, "Hermes Boots"),
            });
            _packs.Add(explorer);

            var mining = new ItemPack("mining", "Mining Pack",
                "Picks, bombs, and potions for serious digging",
                "Plunder", true, "Essentials");
            mining.Items.AddRange(new[]
            {
                new PackItem(103, 1, "Nightmare Pickaxe"),
                new PackItem(166, 50, "Bomb"),
                new PackItem(235, 30, "Sticky Bomb"),
                new PackItem(167, 10, "Dynamite"),
                new PackItem(8, 300, "Torch"),
                new PackItem(292, 200, "Rope"),
                new PackItem(2322, 5, "Mining Potion"),
                new PackItem(296, 5, "Spelunker Potion"),
                new PackItem(2329, 5, "Dangersense Potion"),
                new PackItem(298, 5, "Shine Potion"),
            });
            _packs.Add(mining);

            // ================================================================
            //  COMBAT
            // ================================================================

            var bossprep = new ItemPack("bossprep", "Boss Prep",
                "Buff potions and arena supplies for boss fights",
                "Plunder", true, "Combat");
            bossprep.Items.AddRange(new[]
            {
                new PackItem(292, 200, "Rope"),
                new PackItem(8, 200, "Torch"),
                new PackItem(966, 5, "Campfire"),
                new PackItem(1859, 5, "Heart Lantern"),
                new PackItem(188, 30, "Healing Potion"),
                new PackItem(2346, 5, "Endurance Potion"),
                new PackItem(2349, 5, "Wrath Potion"),
                new PackItem(2347, 5, "Rage Potion"),
                new PackItem(2345, 5, "Lifeforce Potion"),
                new PackItem(2348, 5, "Inferno Potion"),
            });
            _packs.Add(bossprep);

            var arena = new ItemPack("arena", "Arena Kit",
                "Furniture and buffs for building a boss arena",
                "Plunder", true, "Combat");
            arena.Items.AddRange(new[]
            {
                new PackItem(966, 10, "Campfire"),
                new PackItem(1859, 10, "Heart Lantern"),
                new PackItem(1431, 5, "Star in a Bottle"),
                new PackItem(2, 999, "Dirt Block"),
                new PackItem(3, 999, "Stone Block"),
                new PackItem(9, 999, "Wood"),
                new PackItem(292, 300, "Rope"),
                new PackItem(8, 300, "Torch"),
            });
            _packs.Add(arena);

            var potions = new ItemPack("potions", "Potion Pack",
                "Full set of combat and utility buff potions",
                "Plunder", true, "Combat");
            potions.Items.AddRange(new[]
            {
                new PackItem(188, 30, "Healing Potion"),
                new PackItem(499, 10, "Greater Healing Potion"),
                new PackItem(500, 10, "Greater Mana Potion"),
                new PackItem(2346, 5, "Endurance Potion"),
                new PackItem(2349, 5, "Wrath Potion"),
                new PackItem(2347, 5, "Rage Potion"),
                new PackItem(2345, 5, "Lifeforce Potion"),
                new PackItem(296, 5, "Spelunker Potion"),
                new PackItem(2348, 5, "Inferno Potion"),
                new PackItem(295, 5, "Featherfall Potion"),
                new PackItem(288, 5, "Obsidian Skin Potion"),
                new PackItem(2328, 5, "Summoning Potion"),
                new PackItem(300, 5, "Battle Potion"),
            });
            _packs.Add(potions);

            // ================================================================
            //  BIOMES
            // ================================================================

            var hell = new ItemPack("hell", "Hell Pack",
                "Gear and supplies for the Underworld",
                "Plunder", true, "Biomes");
            hell.Items.AddRange(new[]
            {
                new PackItem(122, 1, "Molten Pickaxe"),
                new PackItem(120, 1, "Molten Fury"),
                new PackItem(112, 1, "Flower of Fire"),
                new PackItem(218, 1, "Flamelash"),
                new PackItem(220, 1, "Sunfury"),
                new PackItem(274, 1, "Dark Lance"),
                new PackItem(2365, 1, "Imp Staff"),
                new PackItem(193, 1, "Obsidian Skull"),
                new PackItem(288, 10, "Obsidian Skin Potion"),
                new PackItem(265, 100, "Hellfire Arrow"),
            });
            _packs.Add(hell);

            var jungle = new ItemPack("jungle", "Jungle Pack",
                "Weapons and accessories from the Jungle biome",
                "Plunder", true, "Biomes");
            jungle.Items.AddRange(new[]
            {
                new PackItem(190, 1, "Blade of Grass"),
                new PackItem(212, 1, "Anklet of the Wind"),
                new PackItem(887, 1, "Bezoar"),
                new PackItem(209, 15, "Stinger"),
                new PackItem(210, 15, "Vine"),
                new PackItem(8, 200, "Torch"),
                new PackItem(292, 100, "Rope"),
                new PackItem(2329, 5, "Dangersense Potion"),
                new PackItem(296, 5, "Spelunker Potion"),
            });
            _packs.Add(jungle);

            var ocean = new ItemPack("ocean", "Ocean Pack",
                "Gear for ocean and underwater exploration",
                "Plunder", true, "Biomes");
            ocean.Items.AddRange(new[]
            {
                new PackItem(277, 1, "Trident"),
                new PackItem(187, 1, "Flipper"),
                new PackItem(186, 1, "Breathing Reed"),
                new PackItem(275, 20, "Coral"),
                new PackItem(291, 5, "Gills Potion"),
                new PackItem(2350, 5, "Recall Potion"),
                new PackItem(282, 50, "Glowstick"),
            });
            _packs.Add(ocean);

            var ice = new ItemPack("ice", "Ice Pack",
                "Cold-themed weapons and frost protection",
                "Plunder", true, "Biomes");
            ice.Items.AddRange(new[]
            {
                new PackItem(724, 1, "Ice Blade"),
                new PackItem(670, 1, "Ice Boomerang"),
                new PackItem(988, 100, "Frostburn Arrow"),
                new PackItem(2359, 5, "Warmth Potion"),
                new PackItem(8, 200, "Torch"),
                new PackItem(292, 100, "Rope"),
                new PackItem(296, 5, "Spelunker Potion"),
            });
            _packs.Add(ice);

            var sky = new ItemPack("sky", "Sky Pack",
                "Items for sky islands and high-altitude play",
                "Plunder", true, "Biomes");
            sky.Items.AddRange(new[]
            {
                new PackItem(65, 1, "Starfury"),
                new PackItem(158, 1, "Lucky Horseshoe"),
                new PackItem(159, 1, "Shiny Red Balloon"),
                new PackItem(53, 1, "Cloud in a Bottle"),
                new PackItem(305, 5, "Gravitation Potion"),
                new PackItem(295, 5, "Featherfall Potion"),
                new PackItem(292, 200, "Rope"),
            });
            _packs.Add(sky);

            // ================================================================
            //  RESOURCES
            // ================================================================

            var fishing = new ItemPack("fishing", "Fishing Pack",
                "Rods, bait, and potions for fishing",
                "Plunder", true, "Resources");
            fishing.Items.AddRange(new[]
            {
                new PackItem(2291, 1, "Reinforced Fishing Pole"),
                new PackItem(2002, 30, "Master Bait"),
                new PackItem(2354, 5, "Fishing Potion"),
                new PackItem(2355, 5, "Sonar Potion"),
                new PackItem(2356, 5, "Crate Potion"),
                new PackItem(2205, 1, "Angler Earring"),
                new PackItem(2373, 1, "Tackle Box"),
            });
            _packs.Add(fishing);

            var builder = new ItemPack("builder", "Builder Pack",
                "Wiring, paint, and building materials",
                "Plunder", true, "Resources");
            builder.Items.AddRange(new[]
            {
                new PackItem(509, 1, "Wrench"),
                new PackItem(510, 1, "Wire Cutter"),
                new PackItem(530, 100, "Wire"),
                new PackItem(849, 50, "Actuator"),
                new PackItem(1071, 1, "Paintbrush"),
                new PackItem(1072, 1, "Paint Roller"),
                new PackItem(1100, 1, "Paint Scraper"),
                new PackItem(2325, 5, "Builder Potion"),
                new PackItem(3, 999, "Stone Block"),
                new PackItem(9, 999, "Wood"),
            });
            _packs.Add(builder);

            var bars = new ItemPack("bars", "Ore Bars",
                "Pre-smelted bars for crafting (Cobalt through Hallowed)",
                "Plunder", true, "Resources");
            bars.Items.AddRange(new[]
            {
                new PackItem(381, 30, "Cobalt Bar"),
                new PackItem(391, 30, "Adamantite Bar"),
                new PackItem(1006, 20, "Chlorophyte Bar"),
                new PackItem(1225, 20, "Hallowed Bar"),
                new PackItem(175, 30, "Hellstone Bar"),
            });
            _packs.Add(bars);

            // ================================================================
            //  BUILDING
            // ================================================================

            var woodBuild = new ItemPack("build-wood", "Wood & Cabin",
                "Warm wooden building materials for cozy cabins and treehouses",
                "Plunder", true, "Building");
            woodBuild.Items.AddRange(new[]
            {
                new PackItem(9, 999, "Wood"),
                new PackItem(661, 999, "Rich Mahogany"),
                new PackItem(619, 999, "Shadewood"),
                new PackItem(620, 999, "Ebonwood"),
                new PackItem(150, 999, "Wood Wall"),
                new PackItem(93, 99, "Wooden Door"),
                new PackItem(34, 99, "Wooden Table"),
                new PackItem(36, 99, "Wooden Chair"),
                new PackItem(106, 99, "Wood Platform"),
                new PackItem(8, 200, "Torch"),
            });
            _packs.Add(woodBuild);

            var stoneBuild = new ItemPack("build-stone", "Stone Castle",
                "Stone bricks and blocks for castles, towers, and fortresses",
                "Plunder", true, "Building");
            stoneBuild.Items.AddRange(new[]
            {
                new PackItem(3, 999, "Stone Block"),
                new PackItem(38, 999, "Gray Brick"),
                new PackItem(40, 999, "Red Brick"),
                new PackItem(39, 999, "Gray Brick Wall"),
                new PackItem(41, 999, "Red Brick Wall"),
                new PackItem(129, 99, "Stone Slab"),
                new PackItem(106, 99, "Wood Platform"),
                new PackItem(8, 200, "Torch"),
            });
            _packs.Add(stoneBuild);

            var dungeonBuild = new ItemPack("build-dungeon", "Dungeon Gothic",
                "Blue, green, and pink dungeon bricks for gothic and medieval builds",
                "Plunder", true, "Building");
            dungeonBuild.Items.AddRange(new[]
            {
                new PackItem(137, 999, "Blue Brick"),
                new PackItem(138, 999, "Green Brick"),
                new PackItem(139, 999, "Pink Brick"),
                new PackItem(140, 999, "Blue Brick Wall"),
                new PackItem(141, 999, "Green Brick Wall"),
                new PackItem(142, 999, "Pink Brick Wall"),
                new PackItem(106, 99, "Wood Platform"),
                new PackItem(8, 200, "Torch"),
            });
            _packs.Add(dungeonBuild);

            var glassBuild = new ItemPack("build-glass", "Glass & Crystal",
                "Glass blocks and walls for modern, transparent builds and greenhouses",
                "Plunder", true, "Building");
            glassBuild.Items.AddRange(new[]
            {
                new PackItem(170, 999, "Glass"),
                new PackItem(171, 999, "Glass Wall"),
                new PackItem(172, 999, "Stained Glass (Blue)"),
                new PackItem(173, 999, "Stained Glass (Green)"),
                new PackItem(174, 999, "Stained Glass (Red)"),
                new PackItem(106, 99, "Wood Platform"),
                new PackItem(8, 200, "Torch"),
            });
            _packs.Add(glassBuild);

            var iceBuild = new ItemPack("build-ice", "Ice Palace",
                "Ice and snow blocks for frozen castles and winter-themed builds",
                "Plunder", true, "Building");
            iceBuild.Items.AddRange(new[]
            {
                new PackItem(593, 999, "Ice Block"),
                new PackItem(664, 999, "Snow Block"),
                new PackItem(2696, 999, "Frozen Slime Block"),
                new PackItem(1120, 999, "Ice Brick"),
                new PackItem(1121, 999, "Ice Brick Wall"),
                new PackItem(106, 99, "Wood Platform"),
                new PackItem(8, 200, "Torch"),
            });
            _packs.Add(iceBuild);

            var hellBuild = new ItemPack("build-hell", "Hellstone Fortress",
                "Hellstone, obsidian, and lava-themed blocks for infernal builds",
                "Plunder", true, "Building");
            hellBuild.Items.AddRange(new[]
            {
                new PackItem(56, 999, "Obsidian"),
                new PackItem(75, 999, "Obsidian Brick"),
                new PackItem(119, 999, "Hellstone Brick"),
                new PackItem(76, 999, "Obsidian Brick Wall"),
                new PackItem(120, 999, "Hellstone Brick Wall"),
                new PackItem(58, 999, "Hellstone"),
                new PackItem(106, 99, "Wood Platform"),
                new PackItem(8, 200, "Torch"),
            });
            _packs.Add(hellBuild);

            var marble = new ItemPack("build-marble", "Marble Temple",
                "Smooth marble blocks and walls for elegant Greek-style temples",
                "Plunder", true, "Building");
            marble.Items.AddRange(new[]
            {
                new PackItem(3081, 999, "Marble Block"),
                new PackItem(3086, 999, "Smooth Marble Block"),
                new PackItem(3087, 999, "Marble Wall"),
                new PackItem(106, 99, "Wood Platform"),
                new PackItem(8, 200, "Torch"),
            });
            _packs.Add(marble);

            var granite = new ItemPack("build-granite", "Granite Stronghold",
                "Dark granite blocks for imposing, modern-industrial structures",
                "Plunder", true, "Building");
            granite.Items.AddRange(new[]
            {
                new PackItem(3083, 999, "Granite Block"),
                new PackItem(3088, 999, "Smooth Granite Block"),
                new PackItem(3089, 999, "Granite Wall"),
                new PackItem(106, 99, "Wood Platform"),
                new PackItem(8, 200, "Torch"),
            });
            _packs.Add(granite);

            var sandstone = new ItemPack("build-sandstone", "Desert Sandstone",
                "Sandstone and desert blocks for pyramids and arid-themed builds",
                "Plunder", true, "Building");
            sandstone.Items.AddRange(new[]
            {
                new PackItem(3271, 999, "Sandstone Block"),
                new PackItem(3274, 999, "Sandstone Brick"),
                new PackItem(3275, 999, "Sandstone Brick Wall"),
                new PackItem(169, 999, "Sand Block"),
                new PackItem(51, 999, "Sandstone Wall"),
                new PackItem(106, 99, "Wood Platform"),
                new PackItem(8, 200, "Torch"),
            });
            _packs.Add(sandstone);

            var mushroom = new ItemPack("build-mushroom", "Mushroom House",
                "Glowing mushroom blocks for bioluminescent fantasy builds",
                "Plunder", true, "Building");
            mushroom.Items.AddRange(new[]
            {
                new PackItem(190, 999, "Mushroom Grass Seeds"),
                new PackItem(183, 999, "Glowing Mushroom"),
                new PackItem(194, 999, "Mushroom Block"),
                new PackItem(224, 999, "Mushroom Wall"),
                new PackItem(2175, 999, "Mushroom Platform"),
                new PackItem(8, 200, "Torch"),
            });
            _packs.Add(mushroom);

            var livingWood = new ItemPack("build-living", "Living Wood",
                "Living wood and leaf blocks for organic treehouses and fairy builds",
                "Plunder", true, "Building");
            livingWood.Items.AddRange(new[]
            {
                new PackItem(9, 999, "Wood"),
                new PackItem(709, 999, "Living Wood"),
                new PackItem(710, 999, "Leaf Block"),
                new PackItem(711, 999, "Living Wood Wall"),
                new PackItem(712, 999, "Leaf Wall"),
                new PackItem(106, 99, "Wood Platform"),
                new PackItem(8, 200, "Torch"),
            });
            _packs.Add(livingWood);

            // ================================================================
            //  UTILITY
            // ================================================================

            var magic = new ItemPack("magic", "Magic Pack",
                "Magic weapons and mana supplies",
                "Plunder", true, "Utility");
            magic.Items.AddRange(new[]
            {
                new PackItem(165, 1, "Water Bolt"),
                new PackItem(272, 1, "Demon Scythe"),
                new PackItem(112, 1, "Flower of Fire"),
                new PackItem(218, 1, "Flamelash"),
                new PackItem(487, 1, "Crystal Ball"),
                new PackItem(500, 20, "Greater Mana Potion"),
                new PackItem(110, 30, "Mana Potion"),
            });
            _packs.Add(magic);

            var summon = new ItemPack("summon", "Summoner Pack",
                "Summoning weapons and buff stations",
                "Plunder", true, "Utility");
            summon.Items.AddRange(new[]
            {
                new PackItem(2365, 1, "Imp Staff"),
                new PackItem(2999, 1, "Bewitching Table"),
                new PackItem(2328, 10, "Summoning Potion"),
                new PackItem(2346, 5, "Endurance Potion"),
                new PackItem(188, 20, "Healing Potion"),
            });
            _packs.Add(summon);

            var mobility = new ItemPack("mobility", "Mobility Pack",
                "Boots, wings, and movement accessories",
                "Plunder", true, "Utility");
            mobility.Items.AddRange(new[]
            {
                new PackItem(54, 1, "Hermes Boots"),
                new PackItem(128, 1, "Rocket Boots"),
                new PackItem(405, 1, "Spectre Boots"),
                new PackItem(898, 1, "Lightning Boots"),
                new PackItem(53, 1, "Cloud in a Bottle"),
                new PackItem(159, 1, "Shiny Red Balloon"),
                new PackItem(158, 1, "Lucky Horseshoe"),
                new PackItem(295, 5, "Featherfall Potion"),
                new PackItem(305, 5, "Gravitation Potion"),
            });
            _packs.Add(mobility);
        }

        /// <summary>
        /// Spawn all items from a pack into the player's inventory.
        /// Multiplier scales stackable items only (stack > 1). Unique items (stack=1) stay at 1.
        /// </summary>
        public bool SpawnPack(string packId, int multiplier = 1)
        {
            var pack = _packs.FirstOrDefault(p => p.Id == packId);
            if (pack == null)
            {
                _log.Warn($"ItemPacks: Pack '{packId}' not found");
                return false;
            }

            if (!EnsureReflection()) return false;
            multiplier = Math.Max(1, Math.Min(20, multiplier));

            int spawned = 0;
            foreach (var item in pack.Items)
            {
                // Only multiply stackable items (potions, materials, ammo, etc.)
                // Unique items like weapons, tools, accessories stay at stack=1
                int stack = item.Stack > 1 ? item.Stack * multiplier : item.Stack;
                if (SpawnItem(item.ItemId, stack))
                    spawned++;
            }

            string mult = multiplier > 1 ? $" ({multiplier}x)" : "";
            string msg = $"Spawned {spawned}/{pack.Items.Count} items from {pack.Name}{mult}";
            _log.Info(msg);
            ShowMessage(msg, true);
            return spawned > 0;
        }

        /// <summary>
        /// Spawn a single item into the player's inventory using QuickSpawnItem.
        /// Falls back to direct inventory placement.
        /// </summary>
        private bool SpawnItem(int itemId, int stack)
        {
            try
            {
                int myPlayer = (int)_myPlayerField.GetValue(null);
                var players = (Array)_playerArrayField.GetValue(null);
                var player = players.GetValue(myPlayer);

                // Try QuickSpawnItem first
                if (_quickSpawnItemMethod != null)
                {
                    var source = CreateEntitySource(player);
                    if (source != null)
                    {
                        var parms = _quickSpawnItemMethod.GetParameters();
                        if (parms.Length == 3 && parms[1].ParameterType == typeof(int))
                        {
                            _quickSpawnItemMethod.Invoke(player,
                                new object[] { source, itemId, stack });
                            return true;
                        }
                        else if (parms.Length == 2 && parms[1].ParameterType == _itemType)
                        {
                            var item = Activator.CreateInstance(_itemType);
                            InvokeSetDefaults(item, itemId);
                            _itemStackField?.SetValue(item, stack);
                            _quickSpawnItemMethod.Invoke(player,
                                new object[] { source, item });
                            return true;
                        }
                    }
                }

                // Fallback: direct inventory placement
                return PlaceInInventory(player, itemId, stack);
            }
            catch (Exception ex)
            {
                _log.Error($"ItemPacks: Failed to spawn item {itemId}: {ex.Message}");
                return false;
            }
        }

        private bool PlaceInInventory(object player, int itemId, int stack)
        {
            try
            {
                var inventory = _playerInventoryField?.GetValue(player) as Array;
                if (inventory == null) return false;

                int remaining = stack;

                // Slots 0-49 are main inventory
                for (int i = 0; i < Math.Min(inventory.Length, 50) && remaining > 0; i++)
                {
                    var slot = inventory.GetValue(i);
                    if (slot == null) continue;

                    int slotType = (int)_itemTypeField.GetValue(slot);
                    if (slotType != 0) continue; // Not empty

                    InvokeSetDefaults(slot, itemId);
                    int maxStack = (int)_itemMaxStackField.GetValue(slot);
                    if (maxStack <= 0) maxStack = 1;

                    int toPlace = Math.Min(remaining, maxStack);
                    _itemStackField.SetValue(slot, toPlace);
                    remaining -= toPlace;
                }

                return remaining == 0;
            }
            catch { return false; }
        }

        private object CreateEntitySource(object player)
        {
            try
            {
                var asm = Assembly.Load("Terraria");
                var srcType = asm.GetType("Terraria.DataStructures.EntitySource_Parent")
                    ?? asm.GetType("Terraria.DataStructures.EntitySource_Gift");

                if (srcType != null)
                {
                    var ctor = srcType.GetConstructors().FirstOrDefault();
                    if (ctor != null)
                    {
                        var cp = ctor.GetParameters();
                        if (cp.Length == 1) return ctor.Invoke(new[] { player });
                        if (cp.Length == 0) return ctor.Invoke(null);
                    }
                }
            }
            catch { }
            return null;
        }

        private void InvokeSetDefaults(object item, int type)
        {
            if (_itemSetDefaultsMethod == null) return;
            int paramCount = _itemSetDefaultsMethod.GetParameters().Length;
            if (paramCount == 1)
                _itemSetDefaultsMethod.Invoke(item, new object[] { type });
            else
                _itemSetDefaultsMethod.Invoke(item, new object[] { type, null });
        }

        // ---- User Pack Management ----

        /// <summary>
        /// Create a new user pack from a list of item IDs and stacks.
        /// </summary>
        public ItemPack CreatePack(string name, string description,
            List<PackItem> items)
        {
            string id = "user_" + name.ToLower().Replace(" ", "_")
                + "_" + DateTime.Now.Ticks.ToString("x");

            var pack = new ItemPack(id, name, description, "User", false);
            pack.Items.AddRange(items);
            _packs.Add(pack);
            SavePack(pack);
            return pack;
        }

        /// <summary>
        /// Delete a user pack (built-in packs cannot be deleted).
        /// </summary>
        public bool DeletePack(string packId)
        {
            var pack = _packs.FirstOrDefault(p => p.Id == packId);
            if (pack == null || pack.IsBuiltIn) return false;

            _packs.Remove(pack);

            string path = Path.Combine(_packsDir, packId + ".json");
            if (File.Exists(path))
                File.Delete(path);

            return true;
        }

        /// <summary>
        /// Export a pack to a JSON string for sharing.
        /// </summary>
        public string ExportPack(string packId)
        {
            var pack = _packs.FirstOrDefault(p => p.Id == packId);
            if (pack == null) return null;
            return SerializePack(pack);
        }

        /// <summary>
        /// Import a pack from a JSON string.
        /// </summary>
        public ItemPack ImportPack(string json)
        {
            try
            {
                var pack = DeserializePack(json);
                if (pack == null) return null;

                // Ensure unique ID
                pack.Id = "imported_" + DateTime.Now.Ticks.ToString("x");
                pack.IsBuiltIn = false;

                _packs.Add(pack);
                SavePack(pack);
                return pack;
            }
            catch (Exception ex)
            {
                _log.Error($"ItemPacks: Import failed - {ex.Message}");
                return null;
            }
        }

        // ---- Persistence (simple JSON without dependencies) ----

        private void SavePack(ItemPack pack)
        {
            try
            {
                if (!Directory.Exists(_packsDir))
                    Directory.CreateDirectory(_packsDir);

                string path = Path.Combine(_packsDir, pack.Id + ".json");
                File.WriteAllText(path, SerializePack(pack));
            }
            catch (Exception ex)
            {
                _log.Error($"ItemPacks: Save failed - {ex.Message}");
            }
        }

        private void LoadUserPacks()
        {
            try
            {
                if (!Directory.Exists(_packsDir)) return;

                foreach (var file in Directory.GetFiles(_packsDir, "*.json"))
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        var pack = DeserializePack(json);
                        if (pack != null)
                        {
                            pack.IsBuiltIn = false;
                            _packs.Add(pack);
                        }
                    }
                    catch { }
                }

                _log.Info($"ItemPacks: Loaded {_packs.Count(p => !p.IsBuiltIn)} user packs");
            }
            catch { }
        }

        /// <summary>
        /// Manual JSON serialization (no external deps needed on net48).
        /// </summary>
        private string SerializePack(ItemPack pack)
        {
            var lines = new List<string>();
            lines.Add("{");
            lines.Add($"  \"id\": \"{Escape(pack.Id)}\",");
            lines.Add($"  \"name\": \"{Escape(pack.Name)}\",");
            lines.Add($"  \"description\": \"{Escape(pack.Description)}\",");
            lines.Add($"  \"author\": \"{Escape(pack.Author)}\",");
            lines.Add($"  \"category\": \"{Escape(pack.Category ?? "General")}\",");
            lines.Add("  \"items\": [");

            for (int i = 0; i < pack.Items.Count; i++)
            {
                var item = pack.Items[i];
                string comma = i < pack.Items.Count - 1 ? "," : "";
                string name = item.Name != null ? $", \"name\": \"{Escape(item.Name)}\"" : "";
                lines.Add($"    {{ \"itemId\": {item.ItemId}, \"stack\": {item.Stack}{name} }}{comma}");
            }

            lines.Add("  ]");
            lines.Add("}");
            return string.Join("\n", lines);
        }

        private ItemPack DeserializePack(string json)
        {
            // Minimal JSON parsing for our known format
            var pack = new ItemPack();
            pack.Id = ExtractString(json, "id");
            pack.Name = ExtractString(json, "name");
            pack.Description = ExtractString(json, "description");
            pack.Author = ExtractString(json, "author");
            pack.Category = ExtractString(json, "category") ?? "General";

            // Parse items array
            int itemsStart = json.IndexOf("\"items\"");
            if (itemsStart < 0) return pack;

            int arrayStart = json.IndexOf('[', itemsStart);
            int arrayEnd = json.IndexOf(']', arrayStart);
            if (arrayStart < 0 || arrayEnd < 0) return pack;

            string itemsBlock = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);

            // Split on }, { to get individual items
            int braceStart = -1;
            for (int i = 0; i < itemsBlock.Length; i++)
            {
                if (itemsBlock[i] == '{') braceStart = i;
                else if (itemsBlock[i] == '}' && braceStart >= 0)
                {
                    string itemJson = itemsBlock.Substring(braceStart, i - braceStart + 1);
                    int itemId = ExtractInt(itemJson, "itemId");
                    int stack = ExtractInt(itemJson, "stack");
                    string name = ExtractString(itemJson, "name");
                    if (itemId > 0)
                        pack.Items.Add(new PackItem(itemId, Math.Max(1, stack), name));
                    braceStart = -1;
                }
            }

            return pack;
        }

        private static string ExtractString(string json, string key)
        {
            string search = $"\"{key}\"";
            int idx = json.IndexOf(search);
            if (idx < 0) return null;

            int colon = json.IndexOf(':', idx + search.Length);
            if (colon < 0) return null;

            int quote1 = json.IndexOf('"', colon + 1);
            if (quote1 < 0) return null;

            int quote2 = json.IndexOf('"', quote1 + 1);
            if (quote2 < 0) return null;

            return Unescape(json.Substring(quote1 + 1, quote2 - quote1 - 1));
        }

        private static int ExtractInt(string json, string key)
        {
            string search = $"\"{key}\"";
            int idx = json.IndexOf(search);
            if (idx < 0) return 0;

            int colon = json.IndexOf(':', idx + search.Length);
            if (colon < 0) return 0;

            int start = colon + 1;
            while (start < json.Length && (json[start] == ' ' || json[start] == '\t'))
                start++;

            int end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-'))
                end++;

            if (end > start && int.TryParse(json.Substring(start, end - start), out int val))
                return val;

            return 0;
        }

        private static string Escape(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "\\r");
        }

        private static string Unescape(string s)
        {
            if (s == null) return "";
            return s.Replace("\\\"", "\"").Replace("\\\\", "\\")
                    .Replace("\\n", "\n").Replace("\\r", "\r");
        }

        // ---- Reflection ----

        private bool EnsureReflection()
        {
            if (_reflectionReady) return true;

            try
            {
                var asm = Assembly.Load("Terraria");

                _mainType = Type.GetType("Terraria.Main, Terraria")
                    ?? asm.GetType("Terraria.Main");
                _playerType = Type.GetType("Terraria.Player, Terraria")
                    ?? asm.GetType("Terraria.Player");
                _itemType = Type.GetType("Terraria.Item, Terraria")
                    ?? asm.GetType("Terraria.Item");

                if (_mainType == null || _playerType == null || _itemType == null)
                {
                    _log.Error("ItemPacks: Core types not found");
                    return false;
                }

                _myPlayerField = _mainType.GetField("myPlayer",
                    BindingFlags.Public | BindingFlags.Static);
                _playerArrayField = _mainType.GetField("player",
                    BindingFlags.Public | BindingFlags.Static);
                _playerInventoryField = _playerType.GetField("inventory",
                    BindingFlags.Public | BindingFlags.Instance);

                _itemTypeField = _itemType.GetField("type",
                    BindingFlags.Public | BindingFlags.Instance);
                _itemStackField = _itemType.GetField("stack",
                    BindingFlags.Public | BindingFlags.Instance);
                _itemPrefixField = _itemType.GetField("prefix",
                    BindingFlags.Public | BindingFlags.Instance);
                _itemMaxStackField = _itemType.GetField("maxStack",
                    BindingFlags.Public | BindingFlags.Instance);
                _itemNameField = _itemType.GetField("_nameOverride",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? _itemType.GetField("Name", BindingFlags.Public | BindingFlags.Instance);

                // Find SetDefaults(int, ...)
                foreach (var m in _itemType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (m.Name == "SetDefaults")
                    {
                        var p = m.GetParameters();
                        if (p.Length >= 1 && p[0].ParameterType == typeof(int))
                        {
                            _itemSetDefaultsMethod = m;
                            break;
                        }
                    }
                }

                // Find QuickSpawnItem - prefer (IEntitySource, int, int)
                foreach (var m in _playerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (m.Name != "QuickSpawnItem") continue;
                    var p = m.GetParameters();
                    if (p.Length == 3 && p[1].ParameterType == typeof(int)
                        && p[2].ParameterType == typeof(int))
                    {
                        _quickSpawnItemMethod = m;
                        break;
                    }
                }

                // Fallback: (IEntitySource, Item)
                if (_quickSpawnItemMethod == null)
                {
                    foreach (var m in _playerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (m.Name != "QuickSpawnItem") continue;
                        var p = m.GetParameters();
                        if (p.Length == 2 && p[1].ParameterType == _itemType)
                        {
                            _quickSpawnItemMethod = m;
                            break;
                        }
                    }
                }

                _reflectionReady = true;
                _log.Info("ItemPacks: Reflection initialized");
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"ItemPacks: Reflection error - {ex.Message}");
                return false;
            }
        }

        private void ShowMessage(string msg, bool success)
        {
            try
            {
                var newText = _mainType?.GetMethod("NewText",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(byte), typeof(byte), typeof(byte) },
                    null);

                if (newText != null)
                {
                    byte r = (byte)(success ? 100 : 255);
                    byte g = (byte)(success ? 255 : 100);
                    byte b = (byte)(success ? 100 : 100);
                    newText.Invoke(null, new object[] { msg, r, g, b });
                }
            }
            catch { }
        }
    }
}
