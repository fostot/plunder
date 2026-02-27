using System;
using System.Collections.Generic;
using TerrariaModder.Core.UI;
using TerrariaModder.Core.UI.Widgets;

namespace Plunder
{
    public partial class PlunderPanel
    {
        // ============================================================
        //  CHEATS TAB — Tag-based split-panel
        // ============================================================

        // ---- Data Structures ----

        private struct CheatOptionDef
        {
            public string Id;
            public string Label;
            public string CategoryId;
            public string[] Tags;
        }

        private struct CheatCategoryDef
        {
            public string Id;
            public string Label;
            public string GroupId;   // null for standalone, "player"/"world" for grouped
            public string FilterTag; // null = filter by CategoryId, non-null = filter by this tag
        }

        // ---- Static Data ----

        private static readonly string[][] _cheatGroups = {
            new[] { "player", "PLAYER" },
            new[] { "world",  "WORLD" },
        };

        private static readonly CheatCategoryDef[] _cheatCategories = {
            new CheatCategoryDef { Id = "infinite",    Label = "Infinite",    GroupId = "player", FilterTag = "infinite" },
            new CheatCategoryDef { Id = "survival",    Label = "Survival",    GroupId = "player", FilterTag = null },
            new CheatCategoryDef { Id = "combat",      Label = "Combat",      GroupId = "player", FilterTag = null },
            new CheatCategoryDef { Id = "movement",    Label = "Movement",    GroupId = "player", FilterTag = null },
            new CheatCategoryDef { Id = "resources",   Label = "Resources",   GroupId = "player", FilterTag = null },
            new CheatCategoryDef { Id = "spawns",      Label = "Spawns",      GroupId = "world",  FilterTag = null },
            new CheatCategoryDef { Id = "actions",     Label = "Actions",     GroupId = "world",  FilterTag = null },
            new CheatCategoryDef { Id = "environment", Label = "Environment", GroupId = null,     FilterTag = null },
            new CheatCategoryDef { Id = "visual",      Label = "Visual",      GroupId = null,     FilterTag = null },
            new CheatCategoryDef { Id = "fishing",     Label = "Fishing",     GroupId = null,     FilterTag = null },
        };

        private static readonly string[] _cheatCategoryOrder = {
            "survival", "combat", "movement", "resources", "spawns", "actions",
            "environment", "visual", "fishing"
        };

        private static readonly CheatOptionDef[] _cheatOptions = {
            // Survival
            new CheatOptionDef { Id = "god_mode",        Label = "God Mode (Infinite HP)",  CategoryId = "survival",    Tags = new[] { "health", "invincible", "immortal", "god", "hp", "infinite" } },
            new CheatOptionDef { Id = "inf_breath",      Label = "Infinite Breath",         CategoryId = "survival",    Tags = new[] { "breath", "drown", "water", "oxygen", "infinite" } },
            new CheatOptionDef { Id = "no_fall",         Label = "No Fall Damage",          CategoryId = "survival",    Tags = new[] { "fall", "damage", "gravity" } },
            new CheatOptionDef { Id = "no_knockback",    Label = "No Knockback",            CategoryId = "survival",    Tags = new[] { "knockback", "push", "hit" } },
            new CheatOptionDef { Id = "no_death_drop",   Label = "No Item Drop on Death",   CategoryId = "survival",    Tags = new[] { "death", "drop", "items", "die", "inventory" } },
            // Combat
            new CheatOptionDef { Id = "damage",          Label = "Damage Override",         CategoryId = "combat",      Tags = new[] { "damage", "attack", "multiplier", "one hit", "ohk" } },
            new CheatOptionDef { Id = "inf_ammo",        Label = "Infinite Ammo",           CategoryId = "combat",      Tags = new[] { "ammo", "bullets", "arrows", "shoot", "gun", "infinite" } },
            new CheatOptionDef { Id = "minions",         Label = "Minions Override",        CategoryId = "combat",      Tags = new[] { "minions", "summon", "slots", "pets" } },
            // Movement
            new CheatOptionDef { Id = "run_speed",       Label = "Run Speed",               CategoryId = "movement",    Tags = new[] { "speed", "run", "walk", "fast", "slow" } },
            new CheatOptionDef { Id = "inf_flight",      Label = "Infinite Flight",         CategoryId = "movement",    Tags = new[] { "flight", "fly", "wings", "rocket", "hover", "infinite" } },
            new CheatOptionDef { Id = "teleport",        Label = "Teleport to Cursor",      CategoryId = "movement",    Tags = new[] { "teleport", "cursor", "warp", "tp" } },
            new CheatOptionDef { Id = "map_teleport",    Label = "Map Click Teleport",      CategoryId = "movement",    Tags = new[] { "teleport", "map", "warp", "click", "tp" } },
            // Resources
            new CheatOptionDef { Id = "inf_mana",        Label = "Infinite Mana",           CategoryId = "resources",   Tags = new[] { "mana", "magic", "spell", "mp", "infinite" } },
            new CheatOptionDef { Id = "tool_range",      Label = "Tool Range",              CategoryId = "resources",   Tags = new[] { "tool", "range", "reach", "mine", "pickaxe", "place", "build" } },
            // Spawns
            new CheatOptionDef { Id = "spawn_rate",      Label = "Spawn Rate",              CategoryId = "spawns",      Tags = new[] { "spawn", "enemy", "rate", "monsters", "mob" } },
            new CheatOptionDef { Id = "no_tree_bombs",   Label = "No Tree Bombs",           CategoryId = "spawns",      Tags = new[] { "tree", "bomb", "worthy", "zenith" } },
            new CheatOptionDef { Id = "no_gravestones",  Label = "No Gravestones",          CategoryId = "spawns",      Tags = new[] { "gravestone", "tombstone", "death", "grave" } },
            // Actions
            new CheatOptionDef { Id = "kill_all",        Label = "Kill All Enemies",        CategoryId = "actions",     Tags = new[] { "kill", "enemy", "npc", "slay", "clear" } },
            new CheatOptionDef { Id = "clear_items",     Label = "Clear Items",             CategoryId = "actions",     Tags = new[] { "clear", "items", "drop", "ground", "loot" } },
            new CheatOptionDef { Id = "clear_projectiles", Label = "Clear Projectiles",     CategoryId = "actions",     Tags = new[] { "clear", "projectile", "bullet", "arrow" } },
            // Environment
            new CheatOptionDef { Id = "pause_time",      Label = "Pause Time",              CategoryId = "environment", Tags = new[] { "time", "pause", "freeze", "stop", "clock" } },
            new CheatOptionDef { Id = "set_time",        Label = "Set Time",                CategoryId = "environment", Tags = new[] { "time", "dawn", "noon", "dusk", "midnight", "morning", "night", "day" } },
            new CheatOptionDef { Id = "fast_forward",    Label = "Fast Forward",            CategoryId = "environment", Tags = new[] { "time", "fast", "forward", "sundial", "skip" } },
            new CheatOptionDef { Id = "rain",            Label = "Rain",                    CategoryId = "environment", Tags = new[] { "rain", "weather", "storm" } },
            new CheatOptionDef { Id = "blood_moon",      Label = "Blood Moon",              CategoryId = "environment", Tags = new[] { "blood", "moon", "event", "night" } },
            new CheatOptionDef { Id = "eclipse",         Label = "Eclipse",                 CategoryId = "environment", Tags = new[] { "eclipse", "solar", "event", "day" } },
            // Visual
            new CheatOptionDef { Id = "full_bright",     Label = "Full Bright",             CategoryId = "visual",      Tags = new[] { "bright", "light", "dark", "see", "vision" } },
            new CheatOptionDef { Id = "player_glow",     Label = "Player Glow",             CategoryId = "visual",      Tags = new[] { "glow", "light", "aura", "shine" } },
            new CheatOptionDef { Id = "map_reveal",      Label = "Map Reveal",              CategoryId = "visual",      Tags = new[] { "map", "reveal", "explore", "fog", "uncover" } },
            // Fishing
            new CheatOptionDef { Id = "auto_fish_buffs", Label = "Auto Fishing Buffs",      CategoryId = "fishing",     Tags = new[] { "fish", "buff", "potion", "auto" } },
            new CheatOptionDef { Id = "fishing_power",   Label = "Fishing Power",           CategoryId = "fishing",     Tags = new[] { "fish", "power", "multiplier", "catch" } },
            new CheatOptionDef { Id = "legendary_crates", Label = "Legendary Crates",       CategoryId = "fishing",     Tags = new[] { "legendary", "crate", "fish", "rare" } },
            new CheatOptionDef { Id = "min_catch_rarity", Label = "Min Catch Rarity",       CategoryId = "fishing",     Tags = new[] { "rarity", "catch", "fish", "reroll", "quality" } },
        };

        // ---- Search / Filter Helpers ----

        private static List<CheatOptionDef> SearchCheatOptions(string query)
        {
            var q = query.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(q)) return null;

            var results = new List<CheatOptionDef>();
            for (int i = 0; i < _cheatOptions.Length; i++)
            {
                var opt = _cheatOptions[i];
                if (opt.Label.ToLowerInvariant().Contains(q))
                {
                    results.Add(opt);
                    continue;
                }
                bool tagMatch = false;
                for (int t = 0; t < opt.Tags.Length; t++)
                {
                    if (opt.Tags[t].Contains(q)) { tagMatch = true; break; }
                }
                if (tagMatch) results.Add(opt);
            }
            return results;
        }

        private static List<CheatOptionDef> GetOptionsForCategory(string categoryId)
        {
            if (categoryId == "all")
                return new List<CheatOptionDef>(_cheatOptions);

            // Check if tag-filter category
            for (int i = 0; i < _cheatCategories.Length; i++)
            {
                if (_cheatCategories[i].Id == categoryId && _cheatCategories[i].FilterTag != null)
                {
                    string tag = _cheatCategories[i].FilterTag;
                    var results = new List<CheatOptionDef>();
                    for (int j = 0; j < _cheatOptions.Length; j++)
                    {
                        for (int t = 0; t < _cheatOptions[j].Tags.Length; t++)
                        {
                            if (_cheatOptions[j].Tags[t] == tag)
                            {
                                results.Add(_cheatOptions[j]);
                                break;
                            }
                        }
                    }
                    return results;
                }
            }

            // Normal category filter
            var list = new List<CheatOptionDef>();
            for (int i = 0; i < _cheatOptions.Length; i++)
            {
                if (_cheatOptions[i].CategoryId == categoryId)
                    list.Add(_cheatOptions[i]);
            }
            return list;
        }

        private static string GetCategoryLabel(string categoryId)
        {
            for (int i = 0; i < _cheatCategories.Length; i++)
            {
                if (_cheatCategories[i].Id == categoryId)
                    return _cheatCategories[i].Label;
            }
            return categoryId;
        }

        // ============================================================
        //  CHEATS TAB ENTRY POINT
        // ============================================================

        private void DrawCheatsTab(ref StackLayout layout)
        {
            int areaX = layout.X;
            int areaW = layout.Width;
            int areaY = _viewTop;
            int areaH = _viewBottom - _viewTop;

            const int splitW = 6;
            const float minRatio = 0.15f;
            const float maxRatio = 0.65f;

            int leftW = (int)(areaW * _cheatsSplitRatio);
            int splitX = areaX + leftW;
            int rightX = splitX + splitW;
            int rightW = areaW - leftW - splitW;

            // Draw sub-panel frames
            UIRenderer.DrawRect(areaX, areaY, leftW, areaH, UIColors.InputBg);
            UIRenderer.DrawRectOutline(areaX, areaY, leftW, areaH, UIColors.Divider);
            UIRenderer.DrawRect(rightX, areaY, rightW, areaH, UIColors.InputBg);
            UIRenderer.DrawRectOutline(rightX, areaY, rightW, areaH, UIColors.Divider);

            DrawCheatsLeftPanel(areaX, areaY, leftW, areaH);
            DrawCheatsRightPanel(rightX, areaY, rightW, areaH);

            // Splitter handle
            bool splitHover = WidgetInput.IsMouseOver(splitX - 2, areaY, splitW + 4, areaH);
            UIRenderer.DrawRect(splitX, areaY, splitW, areaH,
                (_cheatsSplitDragging || splitHover) ? UIColors.Divider : UIColors.SectionBg);

            int gripCenterY = areaY + areaH / 2;
            Color4 gripColor = (_cheatsSplitDragging || splitHover) ? UIColors.TextDim : UIColors.Divider;
            for (int d = -2; d <= 2; d++)
            {
                int dotY = gripCenterY + d * 6;
                UIRenderer.DrawRect(splitX + 1, dotY, splitW - 2, 2, gripColor);
            }

            if (_cheatsSplitDragging)
            {
                if (WidgetInput.MouseLeft)
                {
                    float newRatio = (float)(WidgetInput.MouseX - areaX) / areaW;
                    _cheatsSplitRatio = Math.Max(minRatio, Math.Min(maxRatio, newRatio));
                }
                else
                {
                    _cheatsSplitDragging = false;
                }
            }
            else if (splitHover && WidgetInput.MouseLeftClick)
            {
                _cheatsSplitDragging = true;
                WidgetInput.ConsumeClick();
            }

            layout.Advance(areaH);
        }

        // ============================================================
        //  LEFT PANEL — Search + Category Tree
        // ============================================================

        private void DrawCheatsLeftPanel(int px, int py, int pw, int ph)
        {
            const int pad = 4;
            const int scrollbarW = 8;
            int innerX = px + pad;
            int innerW = pw - pad * 2;

            // ── Pinned TOP: Search bar (space reservation — actual Draw is below after scroll) ──
            int searchH = 28;
            int searchY = py + pad;
            int pinnedTopBottom = searchY + searchH;

            // ── Scrollable MIDDLE: Category tree ──
            int treeY = pinnedTopBottom;
            int treeH = py + ph - pad - treeY;
            int treeContentW = innerW - scrollbarW - 2;
            int contentCursor = 0;
            const int catItemH = 22;
            const int groupHeaderH = 22;

            bool isSearching = !string.IsNullOrEmpty(_cheatsSearchInput.Text);

            // "ALL" at top
            {
                int itemY = treeY + contentCursor - _cheatsLeftScroll;
                if (itemY + catItemH > treeY && itemY < treeY + treeH)
                {
                    bool selected = _cheatsSelectedCategory == "all" && !isSearching;
                    bool hover = WidgetInput.IsMouseOver(innerX, itemY, treeContentW, catItemH);
                    Color4 bg = selected ? UIColors.ItemActiveBg : (hover ? UIColors.ItemHoverBg : UIColors.InputBg);
                    UIRenderer.DrawRect(innerX, itemY, treeContentW, catItemH, bg);
                    if (selected)
                        UIRenderer.DrawRect(innerX, itemY, 2, catItemH, UIColors.Accent);
                    Color4 textColor = selected ? UIColors.AccentText : UIColors.Text;
                    UIRenderer.DrawText("ALL", innerX + 6, itemY + (catItemH - 14) / 2, textColor);

                    if (hover && WidgetInput.MouseLeftClick)
                    {
                        _cheatsSelectedCategory = "all";
                        _cheatsRightScroll = 0;
                        _cheatsSearchInput.Text = "";
                        WidgetInput.ConsumeClick();
                    }
                }
                contentCursor += catItemH;
            }

            // Groups (PLAYER, WORLD) and their sub-categories
            for (int gi = 0; gi < _cheatGroups.Length; gi++)
            {
                string groupId = _cheatGroups[gi][0];
                string groupLabel = _cheatGroups[gi][1];
                string expandKey = "cheats_" + groupId;

                if (!_sectionExpanded.ContainsKey(expandKey))
                    _sectionExpanded[expandKey] = true;
                bool expanded = _sectionExpanded[expandKey];

                // Group header
                int hdrY = treeY + contentCursor - _cheatsLeftScroll;
                if (hdrY + groupHeaderH > treeY && hdrY < treeY + treeH)
                {
                    bool hdrHover = WidgetInput.IsMouseOver(innerX, hdrY, treeContentW, groupHeaderH);
                    if (hdrHover)
                        UIRenderer.DrawRect(innerX, hdrY, treeContentW, groupHeaderH, UIColors.SectionBg);

                    string arrow = expanded ? "\u25BC " : "\u25B6 ";
                    UIRenderer.DrawTextSmall(arrow + groupLabel, innerX + 4,
                        hdrY + (groupHeaderH - 11) / 2, UIColors.Accent);

                    if (hdrHover && WidgetInput.MouseLeftClick)
                    {
                        _sectionExpanded[expandKey] = !expanded;
                        WidgetInput.ConsumeClick();
                    }
                }
                contentCursor += groupHeaderH;

                // Sub-categories (if expanded)
                if (expanded)
                {
                    for (int ci = 0; ci < _cheatCategories.Length; ci++)
                    {
                        if (_cheatCategories[ci].GroupId != groupId) continue;
                        var cat = _cheatCategories[ci];

                        int catY = treeY + contentCursor - _cheatsLeftScroll;
                        if (catY + catItemH > treeY && catY < treeY + treeH)
                        {
                            bool selected = _cheatsSelectedCategory == cat.Id && !isSearching;
                            bool hover = WidgetInput.IsMouseOver(innerX, catY, treeContentW, catItemH);
                            Color4 bg = selected ? UIColors.ItemActiveBg : (hover ? UIColors.ItemHoverBg : UIColors.InputBg);
                            UIRenderer.DrawRect(innerX, catY, treeContentW, catItemH, bg);
                            if (selected)
                                UIRenderer.DrawRect(innerX, catY, 2, catItemH, UIColors.Accent);
                            Color4 textColor = selected ? UIColors.AccentText : UIColors.TextDim;
                            UIRenderer.DrawTextSmall(cat.Label, innerX + 16,
                                catY + (catItemH - 11) / 2, textColor);

                            if (hover && WidgetInput.MouseLeftClick)
                            {
                                _cheatsSelectedCategory = cat.Id;
                                _cheatsRightScroll = 0;
                                _cheatsSearchInput.Text = "";
                                WidgetInput.ConsumeClick();
                            }
                        }
                        contentCursor += catItemH;
                    }
                }
            }

            // Standalone categories (no group)
            for (int ci = 0; ci < _cheatCategories.Length; ci++)
            {
                var cat = _cheatCategories[ci];
                if (cat.GroupId != null) continue;

                int catY = treeY + contentCursor - _cheatsLeftScroll;
                if (catY + catItemH > treeY && catY < treeY + treeH)
                {
                    bool selected = _cheatsSelectedCategory == cat.Id && !isSearching;
                    bool hover = WidgetInput.IsMouseOver(innerX, catY, treeContentW, catItemH);
                    Color4 bg = selected ? UIColors.ItemActiveBg : (hover ? UIColors.ItemHoverBg : UIColors.InputBg);
                    UIRenderer.DrawRect(innerX, catY, treeContentW, catItemH, bg);
                    if (selected)
                        UIRenderer.DrawRect(innerX, catY, 2, catItemH, UIColors.Accent);
                    Color4 textColor = selected ? UIColors.AccentText : UIColors.TextDim;
                    UIRenderer.DrawText(cat.Label, innerX + 6, catY + (catItemH - 14) / 2, textColor);

                    if (hover && WidgetInput.MouseLeftClick)
                    {
                        _cheatsSelectedCategory = cat.Id;
                        _cheatsRightScroll = 0;
                        _cheatsSearchInput.Text = "";
                        WidgetInput.ConsumeClick();
                    }
                }
                contentCursor += catItemH;
            }

            _cheatsLeftContentH = contentCursor;

            // Scrollbar
            DrawSubScrollbar(px + pw - scrollbarW - 2, treeY, scrollbarW, treeH,
                _cheatsLeftContentH, treeH,
                ref _cheatsLeftScroll, ref _cheatsLeftDrag, ref _cheatsLeftDragY, ref _cheatsLeftDragOff);

            // Mouse wheel in tree area
            if (WidgetInput.IsMouseOver(px, treeY, pw, treeH))
            {
                int scroll = WidgetInput.ScrollWheel;
                if (scroll != 0)
                {
                    WidgetInput.ConsumeScroll();
                    _cheatsLeftScroll += scroll > 0 ? -30 : 30;
                    int maxScroll = Math.Max(0, _cheatsLeftContentH - treeH);
                    _cheatsLeftScroll = Math.Max(0, Math.Min(_cheatsLeftScroll, maxScroll));
                }
            }

            // Redraw pinned search bar over any scroll bleed
            UIRenderer.DrawRect(px + 1, py + 1, pw - 2, pinnedTopBottom - py - 1, UIColors.InputBg);
            _cheatsSearchInput.Draw(innerX, searchY, innerW, searchH - 4);
        }

        // ============================================================
        //  RIGHT PANEL — Title + Options
        // ============================================================

        private void DrawCheatsRightPanel(int px, int py, int pw, int ph)
        {
            const int pad = 4;
            const int scrollbarW = 8;
            int innerX = px + pad;
            int innerW = pw - pad * 2 - scrollbarW - 2;

            // ── Pinned TOP: Category title ──
            int titleH = 24;
            int titleY = py + pad;

            bool isSearching = !string.IsNullOrEmpty(_cheatsSearchInput.Text);
            string titleText;
            if (isSearching)
                titleText = "SEARCH RESULTS";
            else if (_cheatsSelectedCategory == "all")
                titleText = "ALL CHEATS";
            else
                titleText = GetCategoryLabel(_cheatsSelectedCategory).ToUpperInvariant();

            UIRenderer.DrawText(titleText, innerX + 2, titleY + (titleH - 14) / 2, UIColors.AccentText);
            int pinnedTopBottom = titleY + titleH;
            UIRenderer.DrawRect(innerX, pinnedTopBottom - 2, innerW + scrollbarW, 1, UIColors.Divider);

            // ── Scrollable content ──
            int listY = pinnedTopBottom + 2;
            int listH = py + ph - pad - listY;

            // Temporarily override view bounds for V* helpers
            int savedViewTop = _viewTop;
            int savedViewBottom = _viewBottom;
            _viewTop = listY;
            _viewBottom = listY + listH;

            var rightLayout = new StackLayout(innerX, listY - _cheatsRightScroll, innerW, spacing: 4);

            if (isSearching)
            {
                DrawCheatsSearchResults(ref rightLayout);
            }
            else if (_cheatsSelectedCategory == "all")
            {
                DrawCheatsAllOptions(ref rightLayout);
            }
            else
            {
                var options = GetOptionsForCategory(_cheatsSelectedCategory);
                DrawCheatsOptionList(ref rightLayout, options);
            }

            _cheatsRightContentH = rightLayout.TotalHeight;

            // Restore view bounds
            _viewTop = savedViewTop;
            _viewBottom = savedViewBottom;

            // Scrollbar
            DrawSubScrollbar(px + pw - scrollbarW - 2, listY, scrollbarW, listH,
                _cheatsRightContentH, listH,
                ref _cheatsRightScroll, ref _cheatsRightDrag, ref _cheatsRightDragY, ref _cheatsRightDragOff);

            // Mouse wheel in right panel
            if (WidgetInput.IsMouseOver(px, listY, pw, listH))
            {
                int scroll = WidgetInput.ScrollWheel;
                if (scroll != 0)
                {
                    WidgetInput.ConsumeScroll();
                    _cheatsRightScroll += scroll > 0 ? -30 : 30;
                    int maxScroll = Math.Max(0, _cheatsRightContentH - listH);
                    _cheatsRightScroll = Math.Max(0, Math.Min(_cheatsRightScroll, maxScroll));
                }
            }

            // Redraw pinned title over scroll bleed
            UIRenderer.DrawRect(px + 1, py + 1, pw - 2, pinnedTopBottom - py, UIColors.InputBg);
            UIRenderer.DrawText(titleText, innerX + 2, titleY + (titleH - 14) / 2, UIColors.AccentText);
            UIRenderer.DrawRect(innerX, pinnedTopBottom - 2, innerW + scrollbarW, 1, UIColors.Divider);
        }

        // ============================================================
        //  CONTENT RENDERERS
        // ============================================================

        /// <summary>
        /// Styled category section header for the right panel — accent bar on the left,
        /// subtle background, accent-colored text. Non-interactive.
        /// </summary>
        private void DrawCheatsSectionHeader(ref StackLayout layout, string title, int height = 22)
        {
            int y = layout.Advance(height);
            if (!InView(y, height)) return;

            // Subtle background
            UIRenderer.DrawRect(layout.X, y, layout.Width, height, UIColors.SectionBg);
            // Accent bar on the left
            UIRenderer.DrawRect(layout.X, y, 3, height, UIColors.Accent);
            // Text
            UIRenderer.DrawTextSmall(title, layout.X + 8, y + (height - 11) / 2, UIColors.Accent);
        }

        private void DrawCheatsAllOptions(ref StackLayout layout)
        {
            for (int ci = 0; ci < _cheatCategoryOrder.Length; ci++)
            {
                string catId = _cheatCategoryOrder[ci];
                var options = new List<CheatOptionDef>();
                for (int i = 0; i < _cheatOptions.Length; i++)
                {
                    if (_cheatOptions[i].CategoryId == catId)
                        options.Add(_cheatOptions[i]);
                }
                if (options.Count == 0) continue;

                DrawCheatsSectionHeader(ref layout, GetCategoryLabel(catId).ToUpperInvariant());
                DrawCheatsOptionList(ref layout, options);
                layout.Space(4);
            }
        }

        private void DrawCheatsSearchResults(ref StackLayout layout)
        {
            var matches = SearchCheatOptions(_cheatsSearchInput.Text);
            if (matches == null || matches.Count == 0)
            {
                VLabel(ref layout, "No matches found.", UIColors.TextHint);
                return;
            }

            // Group by category and show with headers
            for (int ci = 0; ci < _cheatCategoryOrder.Length; ci++)
            {
                string catId = _cheatCategoryOrder[ci];
                var catMatches = new List<CheatOptionDef>();
                for (int i = 0; i < matches.Count; i++)
                {
                    if (matches[i].CategoryId == catId)
                        catMatches.Add(matches[i]);
                }
                if (catMatches.Count == 0) continue;

                DrawCheatsSectionHeader(ref layout, GetCategoryLabel(catId).ToUpperInvariant());
                DrawCheatsOptionList(ref layout, catMatches);
                layout.Space(4);
            }
        }

        private void DrawCheatsOptionList(ref StackLayout layout, List<CheatOptionDef> options)
        {
            for (int i = 0; i < options.Count; i++)
            {
                DrawCheatOption(ref layout, options[i].Id);
            }
        }

        // ============================================================
        //  INDIVIDUAL OPTION DRAW — switch on option ID
        // ============================================================

        private void DrawCheatOption(ref StackLayout layout, string optionId)
        {
            switch (optionId)
            {
                case "god_mode":
                {
                    bool state = GetGodModeState?.Invoke() ?? false;
                    if (VCheckboxTip(ref layout, "God Mode (Infinite HP)", state,
                        "God Mode", "Full invincibility. HP stays max,\nall damage is blocked."))
                        OnGodModeToggle?.Invoke();
                    break;
                }
                case "inf_breath":
                {
                    bool state = GetInfiniteBreathState?.Invoke() ?? false;
                    if (VCheckboxTip(ref layout, "Infinite Breath", state,
                        "Infinite Breath", "Breath meter stays full.\nNever drown underwater."))
                        OnInfiniteBreathToggle?.Invoke();
                    break;
                }
                case "no_fall":
                {
                    bool state = GetNoFallDamageState?.Invoke() ?? false;
                    if (VCheckboxTip(ref layout, "No Fall Damage", state,
                        "No Fall Damage", "Prevents all fall damage."))
                        OnNoFallDamageToggle?.Invoke();
                    break;
                }
                case "no_knockback":
                {
                    bool state = GetNoKnockbackState?.Invoke() ?? false;
                    if (VCheckboxTip(ref layout, "No Knockback", state,
                        "No Knockback", "Player cannot be knocked back by enemies."))
                        OnNoKnockbackToggle?.Invoke();
                    break;
                }
                case "no_death_drop":
                {
                    bool state = GetNoDeathDropState?.Invoke() ?? false;
                    if (VCheckboxTip(ref layout, "No Item Drop on Death", state,
                        "No Item Drop on Death", "Prevents dropping items from your inventory\nwhen you die. Only affects your character."))
                        OnNoDeathDropToggle?.Invoke();
                    break;
                }
                case "damage":
                {
                    bool dmgEnabled = GetDamageEnabledState?.Invoke() ?? false;
                    int dmgMult = GetDamageMult?.Invoke() ?? 0;
                    string dmgLabel = dmgEnabled
                        ? (dmgMult == 0 ? "Damage: One Hit Kill" : $"Damage: {dmgMult}x")
                        : "Damage Override";
                    if (VCheckboxTip(ref layout, dmgLabel, dmgEnabled,
                        "Damage Override", "Override attack damage.\n0 = One Hit Kill, 2-20 = multiplier."))
                        OnDamageToggle?.Invoke();
                    if (dmgEnabled)
                    {
                        int dmgY = layout.Advance(22);
                        if (InView(dmgY, 22))
                        {
                            int newDmg = _damageMultSlider.Draw(layout.X + 20, dmgY, layout.Width - 20, 22, dmgMult, 0, 20);
                            if (newDmg != dmgMult)
                                SetDamageMult?.Invoke(newDmg);
                        }
                    }
                    break;
                }
                case "inf_ammo":
                {
                    bool state = GetInfiniteAmmoState?.Invoke() ?? false;
                    if (VCheckboxTip(ref layout, "Infinite Ammo", state,
                        "Infinite Ammo", "Ammo is never consumed when shooting."))
                        OnInfiniteAmmoToggle?.Invoke();
                    break;
                }
                case "minions":
                {
                    bool minions = GetMinionsEnabledState?.Invoke() ?? false;
                    int minionCount = GetMinionCount?.Invoke() ?? 0;
                    string minionLabel = minions
                        ? (minionCount == 0 ? "Minions: Infinite" : $"Minions: {minionCount} max")
                        : "Minions Override";
                    if (VCheckboxTip(ref layout, minionLabel, minions,
                        "Minions Override", "Override max minion slots.\n0 = Infinite, 1-20 = set cap."))
                        OnMinionsToggle?.Invoke();
                    if (minions)
                    {
                        int mcY = layout.Advance(22);
                        if (InView(mcY, 22))
                        {
                            int newMc = _minionCountSlider.Draw(layout.X + 20, mcY, layout.Width - 20, 22, minionCount, 0, 20);
                            if (newMc != minionCount)
                                SetMinionCount?.Invoke(newMc);
                        }
                    }
                    break;
                }
                case "run_speed":
                {
                    int runSpeed = GetRunSpeedMult?.Invoke() ?? 1;
                    string runLabel = runSpeed == 1 ? "Run Speed: 1x (normal)" : $"Run Speed: {runSpeed}x";
                    {
                        int lblY = layout.Advance(18);
                        if (InView(lblY, 18))
                        {
                            bool lblHover = WidgetInput.IsMouseOver(layout.X, lblY, layout.Width, 18);
                            if (lblHover)
                                RichTooltip.Set("Run Speed Multiplier",
                                    "Multiplies your movement speed.\n" +
                                    "1x = Normal run speed (100%).\n" +
                                    $"{(runSpeed > 1 ? $"{runSpeed}x = {runSpeed * 100}% of normal speed.\n" : "")}" +
                                    "Affects max run speed and acceleration.");
                            string display = TextUtil.Truncate(runLabel, layout.Width);
                            UIRenderer.DrawText(display, layout.X, lblY + (18 - 14) / 2, UIColors.Text);
                        }
                    }
                    int rsY = layout.Advance(22);
                    if (InView(rsY, 22))
                    {
                        int newRs = _runSpeedSlider.Draw(layout.X, rsY, layout.Width, 22, runSpeed, 1, 10);
                        if (newRs != runSpeed)
                            SetRunSpeedMult?.Invoke(newRs);
                    }
                    break;
                }
                case "inf_flight":
                {
                    bool state = GetInfiniteFlightState?.Invoke() ?? false;
                    if (VCheckboxTip(ref layout, "Infinite Flight", state,
                        "Infinite Flight", "Wing and rocket time never run out.\nFly indefinitely."))
                        OnInfiniteFlightToggle?.Invoke();
                    break;
                }
                case "teleport":
                {
                    bool state = GetTeleportState?.Invoke() ?? false;
                    if (VCheckboxTip(ref layout, "Teleport To Cursor", state,
                        "Teleport To Cursor", "Press T to teleport to cursor position.\nRebindable in Config tab."))
                        OnTeleportToggle?.Invoke();
                    break;
                }
                case "map_teleport":
                {
                    bool state = GetMapTeleportState?.Invoke() ?? false;
                    if (VCheckboxTip(ref layout, "Map Click Teleport", state,
                        "Map Click Teleport", "Right-click on the fullscreen map\nto teleport to that location."))
                        OnMapTeleportToggle?.Invoke();
                    break;
                }
                case "inf_mana":
                {
                    bool state = GetInfiniteManaState?.Invoke() ?? false;
                    if (VCheckboxTip(ref layout, "Infinite Mana", state,
                        "Infinite Mana", "Mana stays at maximum at all times."))
                        OnInfiniteManaToggle?.Invoke();
                    break;
                }
                case "tool_range":
                {
                    int toolRange = GetToolRangeMult?.Invoke() ?? 1;
                    bool toolRangeOn = GetToolRangeEnabledState?.Invoke() ?? false;
                    string trLabel = !toolRangeOn ? "Tool Range Override"
                        : toolRange <= 1 ? "Tool Range: 1x (normal)"
                        : $"Tool Range: {toolRange}x";
                    {
                        int lblY = layout.Advance(18);
                        if (InView(lblY, 18))
                        {
                            bool lblHover = WidgetInput.IsMouseOver(layout.X, lblY, layout.Width, 18);
                            if (lblHover)
                                RichTooltip.Set("Tool Range Multiplier",
                                    "Multiplies how far your tools can reach.\n" +
                                    "Affects pickaxes, axes, hammers, and\nblock placement distance.\n" +
                                    "1x = Normal range.\n" +
                                    $"{(toolRange > 1 ? $"{toolRange}x = {toolRange}x further reach.\n" : "")}" +
                                    "Great for mining from a safe distance.");
                            string display = TextUtil.Truncate(trLabel, layout.Width);
                            UIRenderer.DrawText(display, layout.X, lblY + (18 - 14) / 2, UIColors.Text);
                        }
                    }
                    if (VCheckboxTip(ref layout, "Enabled", toolRangeOn,
                        "Enable Tool Range Override", "Toggle extended tool reach on/off."))
                        OnToolRangeToggle?.Invoke();
                    if (toolRangeOn)
                    {
                        int trY = layout.Advance(22);
                        if (InView(trY, 22))
                        {
                            int newTr = _toolRangeSlider.Draw(layout.X, trY, layout.Width, 22, toolRange, 1, 10);
                            if (newTr != toolRange)
                                SetToolRangeMult?.Invoke(newTr);
                        }
                    }
                    break;
                }
                case "spawn_rate":
                {
                    int spawnRate = GetSpawnRateMult?.Invoke() ?? 1;
                    string spawnLabel = spawnRate == 0 ? "Spawn Rate: OFF (no spawns)"
                        : spawnRate == 1 ? "Spawn Rate: 1x (normal)"
                        : $"Spawn Rate: {spawnRate}x";
                    {
                        int lblY = layout.Advance(18);
                        if (InView(lblY, 18))
                        {
                            bool lblHover = WidgetInput.IsMouseOver(layout.X, lblY, layout.Width, 18);
                            if (lblHover)
                                RichTooltip.Set("Spawn Rate Multiplier",
                                    "Controls how fast enemies spawn.\n" +
                                    "0 = No spawns at all.\n" +
                                    "1x = Normal spawn rate.\n" +
                                    $"{(spawnRate > 1 ? $"{spawnRate}x = Enemies spawn {spawnRate}x faster\nwith {spawnRate}x more allowed at once.\n" : "")}" +
                                    "Higher values may be less noticeable\nnear towns due to NPC safety zones.");
                            string display = TextUtil.Truncate(spawnLabel, layout.Width);
                            UIRenderer.DrawText(display, layout.X, lblY + (18 - 14) / 2, UIColors.Text);
                        }
                    }
                    int srY = layout.Advance(22);
                    if (InView(srY, 22))
                    {
                        int newSr = _spawnRateSlider.Draw(layout.X, srY, layout.Width, 22, spawnRate, 0, 20);
                        if (newSr != spawnRate)
                            SetSpawnRateMult?.Invoke(newSr);
                    }
                    break;
                }
                case "no_tree_bombs":
                {
                    bool state = GetNoTreeBombsState?.Invoke() ?? false;
                    if (VCheckboxTip(ref layout, "No Tree Bombs", state,
                        "No Tree Bombs", "Prevents trees from spawning lit bombs\nin For The Worthy / Zenith worlds."))
                        OnNoTreeBombsToggle?.Invoke();
                    break;
                }
                case "no_gravestones":
                {
                    bool state = GetNoGravestonesState?.Invoke() ?? false;
                    if (VCheckboxTip(ref layout, "No Gravestones", state,
                        "No Gravestones", "Prevents gravestone projectiles from spawning\nwhen you die."))
                        OnNoGravestonesToggle?.Invoke();
                    break;
                }
                case "kill_all":
                {
                    if (VButton(ref layout, "Kill All Enemies", 26))
                        OnKillAllEnemies?.Invoke();
                    break;
                }
                case "clear_items":
                {
                    if (VButton(ref layout, "Clear Items", 26))
                        OnClearItems?.Invoke();
                    break;
                }
                case "clear_projectiles":
                {
                    if (VButton(ref layout, "Clear Projectiles", 26))
                        OnClearProjectiles?.Invoke();
                    break;
                }
                case "pause_time":
                {
                    bool state = GetTimePausedState?.Invoke() ?? false;
                    if (VCheckboxTip(ref layout, "Pause Time", state,
                        "Pause Time", "Freezes the day/night cycle at the current time.\nTime stays locked until you toggle this off."))
                        OnTimePauseToggle?.Invoke();
                    break;
                }
                case "set_time":
                {
                    int hw = (layout.Width - 8) / 2;
                    int rowY = layout.Advance(26);
                    if (InView(rowY, 26))
                    {
                        if (VButtonTip(layout.X, rowY, hw, 26, "Dawn",
                            "Set Dawn", "Sets time to dawn (4:30 AM).\nDay begins."))
                            OnSetDawn?.Invoke();
                        if (VButtonTip(layout.X + hw + 8, rowY, hw, 26, "Noon",
                            "Set Noon", "Sets time to noon (12:00 PM).\nMiddle of the day."))
                            OnSetNoon?.Invoke();
                    }
                    rowY = layout.Advance(26);
                    if (InView(rowY, 26))
                    {
                        if (VButtonTip(layout.X, rowY, hw, 26, "Dusk",
                            "Set Dusk", "Sets time to dusk (7:30 PM).\nNight begins."))
                            OnSetDusk?.Invoke();
                        if (VButtonTip(layout.X + hw + 8, rowY, hw, 26, "Midnight",
                            "Set Midnight", "Sets time to midnight (12:00 AM).\nMiddle of the night."))
                            OnSetMidnight?.Invoke();
                    }
                    break;
                }
                case "fast_forward":
                {
                    if (VButton(ref layout, "Fast Forward to Dawn", 26))
                        OnFastForwardDawn?.Invoke();
                    break;
                }
                case "rain":
                {
                    bool state = GetRainingState?.Invoke() ?? false;
                    if (VCheckboxTip(ref layout, "Rain", state,
                        "Toggle Rain", "Starts or stops rain.\nAffects fishing power and spawn rates."))
                        OnToggleRain?.Invoke();
                    break;
                }
                case "blood_moon":
                {
                    bool state = GetBloodMoonState?.Invoke() ?? false;
                    if (VCheckboxTip(ref layout, "Blood Moon", state,
                        "Toggle Blood Moon", "Starts or ends a Blood Moon event.\nAutomatically sets night if needed."))
                        OnToggleBloodMoon?.Invoke();
                    break;
                }
                case "eclipse":
                {
                    bool state = GetEclipseState?.Invoke() ?? false;
                    if (VCheckboxTip(ref layout, "Eclipse", state,
                        "Toggle Solar Eclipse", "Starts or ends a Solar Eclipse.\nAutomatically sets daytime if needed."))
                        OnToggleEclipse?.Invoke();
                    break;
                }
                case "full_bright":
                {
                    bool state = GetFullBrightState?.Invoke() ?? false;
                    if (VCheckboxTip(ref layout, "Full Bright", state,
                        "Full Bright", "Removes all darkness and shadow.\nEverything is fully lit at all times."))
                        OnFullBrightToggle?.Invoke();
                    break;
                }
                case "player_glow":
                {
                    bool state = GetPlayerGlowState?.Invoke() ?? false;
                    if (VCheckboxTip(ref layout, "Player Glow", state,
                        "Player Glow", "Emits a light aura around your character.\nUseful for exploring dark areas."))
                        OnPlayerGlowToggle?.Invoke();
                    break;
                }
                case "map_reveal":
                {
                    bool state = GetMapRevealState?.Invoke() ?? false;
                    if (VCheckboxTip(ref layout, "Map Reveal", state,
                        "Full Map Reveal", "Reveals the entire world map.\nRe-reveals every 10 seconds to catch changes."))
                        OnMapRevealToggle?.Invoke();
                    break;
                }
                case "auto_fish_buffs":
                {
                    bool buffsOn = GetFishingBuffsState?.Invoke() ?? false;
                    if (VCheckboxTip(ref layout, "Auto Fishing Buffs", buffsOn,
                        "Auto Fishing Buffs", "Automatically applies fishing buff potions.\nToggle individual potions below."))
                        OnFishingBuffsToggle?.Invoke();

                    if (buffsOn)
                    {
                        layout.Space(2);
                        bool fp = GetAutoFishingPotion?.Invoke() ?? true;
                        bool sp = GetAutoSonarPotion?.Invoke() ?? true;
                        bool cp = GetAutoCratePotion?.Invoke() ?? true;

                        int indent = 20;
                        int thirdW = (layout.Width - indent - 8) / 3;
                        int subY = layout.Advance(24);

                        if (VCheckboxAt(ref layout, layout.X + indent, thirdW, subY, "Fishing Pot", fp,
                            "Fishing Potion", "Auto-applies the Fishing Potion buff\nfor increased fishing power."))
                            SetAutoFishingPotion?.Invoke(!fp);
                        if (VCheckboxAt(ref layout, layout.X + indent + thirdW + 4, thirdW, subY, "Sonar Pot", sp,
                            "Sonar Potion", "Auto-applies the Sonar Potion buff\nto see what's on the hook before reeling in."))
                            SetAutoSonarPotion?.Invoke(!sp);
                        if (VCheckboxAt(ref layout, layout.X + indent + (thirdW + 4) * 2, thirdW, subY, "Crate Pot", cp,
                            "Crate Potion", "Auto-applies the Crate Potion buff\nfor increased crate catch rate."))
                            SetAutoCratePotion?.Invoke(!cp);
                    }
                    break;
                }
                case "fishing_power":
                {
                    int fpMult = GetFishingPowerMultiplier?.Invoke() ?? 1;
                    VLabel(ref layout, $"Fishing Power: {fpMult}x");
                    int sliderY = layout.Advance(22);
                    if (InView(sliderY, 22))
                    {
                        int newFpMult = _fishingPowerSlider.Draw(
                            layout.X, sliderY, layout.Width, 22, fpMult, 1, 10);
                        if (newFpMult != _prevFishingPower)
                        {
                            _prevFishingPower = newFpMult;
                            SetFishingPowerMultiplier?.Invoke(newFpMult);
                        }
                    }
                    break;
                }
                case "legendary_crates":
                {
                    bool state = GetLegendaryCratesState?.Invoke() ?? false;
                    if (VCheckboxTip(ref layout, "Legendary Crates Only", state,
                        "Legendary Crates Only", "Re-rolls all catches until you get\na Legendary crate."))
                        OnLegendaryCratesToggle?.Invoke();
                    break;
                }
                case "min_catch_rarity":
                {
                    int minRarity = GetCatchRerollMinRarity?.Invoke() ?? 0;
                    string rarityLabel = minRarity == 0 ? "Off" :
                        minRarity == 1 ? "Blue+" :
                        minRarity == 2 ? "Green+" :
                        minRarity == 3 ? "Orange+" :
                        minRarity == 4 ? "LightRed+" : "Pink+";
                    VLabel(ref layout, $"Min Catch Rarity: {rarityLabel}");
                    int raritySliderY = layout.Advance(22);
                    if (InView(raritySliderY, 22))
                    {
                        int newRarity = _catchRaritySlider.Draw(
                            layout.X, raritySliderY, layout.Width, 22, minRarity, 0, 5);
                        if (newRarity != _prevCatchRarity)
                        {
                            _prevCatchRarity = newRarity;
                            SetCatchRerollMinRarity?.Invoke(newRarity);
                        }
                    }
                    break;
                }
            }
        }
    }
}
