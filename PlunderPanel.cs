using System;
using System.Collections.Generic;
using System.Linq;
using TerrariaModder.Core.Logging;
using TerrariaModder.Core.UI;
using TerrariaModder.Core.UI.Widgets;

namespace Plunder
{
    public class PlunderPanel
    {
        private readonly ILogger _log;
        private readonly PlunderConfig _config;
        private DraggablePanel _panel;

        // Tab system
        private enum Tab { Cheats, Feats, Other, Config }
        private Tab _activeTab = Tab.Cheats;
        private static readonly string[] TabNames = { "CHEATS", "FEATS", "OTHER", "CONFIG" };
        private const int TabBarHeight = 28;

        // Scroll state (per-tab so switching tabs preserves scroll position)
        private readonly Dictionary<Tab, int> _scrollOffsets = new Dictionary<Tab, int>();
        private int _lastContentHeight;
        private const int ScrollSpeed = 30;

        // Virtual scroll view bounds — items outside are simply not drawn.
        private int _viewTop;
        private int _viewBottom;

        // Resize handle — drag bottom edge to change panel height
        private bool _isResizing;
        private int _resizeStartY;
        private int _resizeStartHeight;
        private const int ResizeHandleHeight = 8;
        private const int MinPanelHeight = 200;
        private const int MaxPanelHeight = 900;

        // Slider instances (stateful widgets need instances)
        private readonly Slider _fishingPowerSlider = new Slider();
        private readonly Slider _catchRaritySlider = new Slider();
        private readonly Slider _packMultiplierSlider = new Slider();
        private readonly Slider _panelWidthSlider = new Slider();
        private readonly Slider _panelHeightSlider = new Slider();
        private readonly Slider _minionCountSlider = new Slider();
        private readonly Slider _damageMultSlider = new Slider();
        private readonly Slider _spawnRateSlider = new Slider();
        private readonly Slider _runSpeedSlider = new Slider();
        private readonly Slider _toolRangeSlider = new Slider();

        // Collapsible section state
        private readonly Dictionary<string, bool> _sectionExpanded = new Dictionary<string, bool>();

        // Dirty-tracking for slider saves
        private int _prevFishingPower = -1;
        private int _prevCatchRarity = -1;

        // Pack multiplier (1x-20x)
        private int _packMultiplier = 1;

        // ---- Callbacks wired from Mod.cs ----
        // Cheats: Visual
        public Action OnFullBrightToggle;
        public Func<bool> GetFullBrightState;
        public Action OnPlayerGlowToggle;
        public Func<bool> GetPlayerGlowState;
        public Action OnMapRevealToggle;
        public Func<bool> GetMapRevealState;

        // Cheats: OP Cheats
        public Action OnGodModeToggle;
        public Func<bool> GetGodModeState;
        public Action OnInfiniteManaToggle;
        public Func<bool> GetInfiniteManaState;
        public Action OnMinionsToggle;
        public Func<bool> GetMinionsEnabledState;
        public Func<int> GetMinionCount;
        public Action<int> SetMinionCount;
        public Action OnInfiniteFlightToggle;
        public Func<bool> GetInfiniteFlightState;
        public Action OnInfiniteAmmoToggle;
        public Func<bool> GetInfiniteAmmoState;
        public Action OnInfiniteBreathToggle;
        public Func<bool> GetInfiniteBreathState;
        public Action OnNoKnockbackToggle;
        public Func<bool> GetNoKnockbackState;
        public Action OnDamageToggle;
        public Func<bool> GetDamageEnabledState;
        public Func<int> GetDamageMult;
        public Action<int> SetDamageMult;
        public Action OnNoFallDamageToggle;
        public Func<bool> GetNoFallDamageState;
        public Action OnNoTreeBombsToggle;
        public Func<bool> GetNoTreeBombsState;
        public Func<int> GetSpawnRateMult;
        public Action<int> SetSpawnRateMult;
        public Func<int> GetRunSpeedMult;
        public Action<int> SetRunSpeedMult;
        public Action OnToolRangeToggle;
        public Func<bool> GetToolRangeEnabledState;
        public Func<int> GetToolRangeMult;
        public Action<int> SetToolRangeMult;

        // Cheats: Movement
        public Action OnTeleportToggle;
        public Func<bool> GetTeleportState;
        public Action OnMapTeleportToggle;
        public Func<bool> GetMapTeleportState;

        // Cheats: Fishing
        public Action OnFishingBuffsToggle;
        public Func<bool> GetFishingBuffsState;
        public Func<bool> GetAutoFishingPotion;
        public Action<bool> SetAutoFishingPotion;
        public Func<bool> GetAutoSonarPotion;
        public Action<bool> SetAutoSonarPotion;
        public Func<bool> GetAutoCratePotion;
        public Action<bool> SetAutoCratePotion;
        public Func<int> GetFishingPowerMultiplier;
        public Action<int> SetFishingPowerMultiplier;
        public Action OnLegendaryCratesToggle;
        public Func<bool> GetLegendaryCratesState;
        public Func<int> GetCatchRerollMinRarity;
        public Action<int> SetCatchRerollMinRarity;

        // Feats: Item Packs
        public Func<IReadOnlyList<ItemPack>> GetItemPacks;
        public Action<string, int> OnSpawnPack;

        // Config: Mod Menu jump
        public Action OnOpenModMenu;

        public bool IsOpen => _panel?.IsOpen ?? false;

        public PlunderPanel(ILogger log, PlunderConfig config)
        {
            _log = log;
            _config = config;
            _panel = new DraggablePanel("plunder", "Plunder v" + config.ModVersion,
                config.PanelWidth, config.PanelHeight);

            _panel.ClipContent = true;

            foreach (Tab t in Enum.GetValues(typeof(Tab)))
                _scrollOffsets[t] = 0;

            _sectionExpanded["opcheats"] = true;
            _sectionExpanded["visual"] = true;
            _sectionExpanded["movement"] = true;
            _sectionExpanded["fishing"] = true;
            _sectionExpanded["feat_essentials"] = true;
            _sectionExpanded["feat_combat"] = true;
            _sectionExpanded["feat_biomes"] = true;
            _sectionExpanded["feat_resources"] = true;
            _sectionExpanded["feat_utility"] = true;
            _sectionExpanded["cfg_keybinds"] = true;
            _sectionExpanded["cfg_panel"] = true;
        }

        public void Register() { _panel.RegisterDrawCallback(OnDraw); }
        public void Unregister() { _panel.UnregisterDrawCallback(); _panel.Close(); }

        public void Open()
        {
            int x = _config.PanelX;
            int y = _config.PanelY;
            if (x >= 0 && y >= 0) _panel.Open(x, y);
            else _panel.Open();
        }

        public void Close() { _panel.Close(); }

        public void Toggle()
        {
            if (_panel.IsOpen) Close();
            else Open();
        }

        public void ApplyConfig() { }

        // ============================================================
        //  VIRTUAL SCROLL HELPERS
        // ============================================================

        private bool InView(int y, int height)
        {
            return (y + height > _viewTop) && (y < _viewBottom);
        }

        /// <summary>Checkbox with label — green when checked, red when unchecked.</summary>
        private bool VCheckbox(ref StackLayout layout, string label, bool isChecked, int height = 24)
        {
            int y = layout.Advance(height);
            if (!InView(y, height)) return false;

            bool hover = WidgetInput.IsMouseOver(layout.X, y, layout.Width, height);
            if (hover)
                UIRenderer.DrawRect(layout.X, y, layout.Width, height, new Color4(60, 60, 90, 60));

            int boxSize = Math.Min(height - 4, 14);
            int boxY = y + (height - boxSize) / 2;
            int boxX = layout.X + 4;

            if (isChecked)
            {
                // Green filled box
                UIRenderer.DrawRect(boxX, boxY, boxSize, boxSize, UIColors.Success);
                UIRenderer.DrawRect(boxX + 3, boxY + 3, boxSize - 6, boxSize - 6, new Color4(30, 80, 30, 255));
            }
            else
            {
                // Red outlined box
                UIRenderer.DrawRect(boxX, boxY, boxSize, boxSize, new Color4(255, 100, 100, 80));
                UIRenderer.DrawRectOutline(boxX, boxY, boxSize, boxSize, UIColors.Error);
            }

            int textX = boxX + boxSize + 6;
            int textY = y + (height - 14) / 2;
            Color4 textColor = isChecked ? UIColors.Text : UIColors.TextDim;
            string display = TextUtil.Truncate(label, layout.Width - (textX - layout.X));
            UIRenderer.DrawText(display, textX, textY, textColor);

            if (hover && WidgetInput.MouseLeftClick)
            {
                WidgetInput.ConsumeClick();
                return true;
            }
            return false;
        }

            /// <summary>Checkbox at specific X position (no auto-advance), with optional tooltip.</summary>
        private bool VCheckboxAt(ref StackLayout layout, int x, int width, int y, string label, bool isChecked, string tipTitle = null, string tipText = null, int height = 24)
        {
            if (!InView(y, height)) return false;

            bool hover = WidgetInput.IsMouseOver(x, y, width, height);
            if (hover && tipTitle != null)
                RichTooltip.Set(tipTitle, tipText);
            if (hover)
                UIRenderer.DrawRect(x, y, width, height, new Color4(60, 60, 90, 60));

            int boxSize = Math.Min(height - 4, 14);
            int boxY = y + (height - boxSize) / 2;
            int boxX = x + 4;

            if (isChecked)
            {
                UIRenderer.DrawRect(boxX, boxY, boxSize, boxSize, UIColors.Success);
                UIRenderer.DrawRect(boxX + 3, boxY + 3, boxSize - 6, boxSize - 6, new Color4(30, 80, 30, 255));
            }
            else
            {
                UIRenderer.DrawRect(boxX, boxY, boxSize, boxSize, new Color4(255, 100, 100, 80));
                UIRenderer.DrawRectOutline(boxX, boxY, boxSize, boxSize, UIColors.Error);
            }

            int textX = boxX + boxSize + 6;
            int textY = y + (height - 14) / 2;
            Color4 textColor = isChecked ? UIColors.Text : UIColors.TextDim;
            string display = TextUtil.Truncate(label, width - (textX - x));
            UIRenderer.DrawText(display, textX, textY, textColor);

            if (hover && WidgetInput.MouseLeftClick)
            {
                WidgetInput.ConsumeClick();
                return true;
            }
            return false;
        }

        /// <summary>Checkbox with tooltip on hover.</summary>
        private bool VCheckboxTip(ref StackLayout layout, string label, bool isChecked, string tipTitle, string tipText, int height = 24)
        {
            int y = layout.Advance(height);
            if (!InView(y, height)) return false;

            bool hover = WidgetInput.IsMouseOver(layout.X, y, layout.Width, height);

            // Show tooltip on hover
            if (hover && tipTitle != null)
                RichTooltip.Set(tipTitle, tipText);

            if (hover)
                UIRenderer.DrawRect(layout.X, y, layout.Width, height, new Color4(60, 60, 90, 60));

            int boxSize = Math.Min(height - 4, 14);
            int boxY = y + (height - boxSize) / 2;
            int boxX = layout.X + 4;

            if (isChecked)
            {
                UIRenderer.DrawRect(boxX, boxY, boxSize, boxSize, UIColors.Success);
                UIRenderer.DrawRect(boxX + 3, boxY + 3, boxSize - 6, boxSize - 6, new Color4(30, 80, 30, 255));
            }
            else
            {
                UIRenderer.DrawRect(boxX, boxY, boxSize, boxSize, new Color4(255, 100, 100, 80));
                UIRenderer.DrawRectOutline(boxX, boxY, boxSize, boxSize, UIColors.Error);
            }

            int textX = boxX + boxSize + 6;
            int textY = y + (height - 14) / 2;
            Color4 textColor = isChecked ? UIColors.Text : UIColors.TextDim;
            string display = TextUtil.Truncate(label, layout.Width - (textX - layout.X));
            UIRenderer.DrawText(display, textX, textY, textColor);

            if (hover && WidgetInput.MouseLeftClick)
            {
                WidgetInput.ConsumeClick();
                return true;
            }
            return false;
        }

        private bool VButton(ref StackLayout layout, string text, int height = 26)
        {
            int y = layout.Advance(height);
            if (!InView(y, height)) return false;
            return Button.Draw(layout.X, y, layout.Width, height, text);
        }

        /// <summary>Button at specific X/width (no auto-advance). Shows tooltip on hover.</summary>
        private bool VButtonTip(int x, int y, int width, int height, string text, string tipTitle, string tipText)
        {
            if (!InView(y, height)) return false;

            bool hover = WidgetInput.IsMouseOver(x, y, width, height);
            if (hover && tipTitle != null)
                RichTooltip.Set(tipTitle, tipText);

            return Button.Draw(x, y, width, height, text);
        }

        private void VLabel(ref StackLayout layout, string text, Color4 color, int height = 18)
        {
            int y = layout.Advance(height);
            if (!InView(y, height)) return;
            string display = TextUtil.Truncate(text, layout.Width);
            int textY = y + (height - 14) / 2;
            UIRenderer.DrawText(display, layout.X, textY, color);
        }

        private void VLabel(ref StackLayout layout, string text, int height = 18)
        {
            VLabel(ref layout, text, UIColors.Text, height);
        }

        private void VSectionHeader(ref StackLayout layout, string title, int height = 22)
        {
            int y = layout.Advance(height);
            if (!InView(y, height)) return;
            SectionHeader.Draw(layout.X, y, layout.Width, title, height);
        }

        // ============================================================
        //  DRAWING
        // ============================================================

        private void OnDraw()
        {
            // Handle resize drag BEFORE BeginDraw so height is correct this frame
            if (_isResizing)
            {
                if (WidgetInput.MouseLeft)
                {
                    int deltaY = WidgetInput.MouseY - _resizeStartY;
                    int newH = Math.Max(MinPanelHeight, Math.Min(MaxPanelHeight, _resizeStartHeight + deltaY));
                    _panel.Height = newH;
                }
                else
                {
                    _isResizing = false;
                    _config.Set("panelHeight", _panel.Height);
                }
            }

            if (!_panel.BeginDraw()) return;
            try
            {
                int cx = _panel.ContentX;
                int cy = _panel.ContentY;
                int cw = _panel.ContentWidth;
                int ch = _panel.ContentHeight;

                int contentY = cy + TabBarHeight + 4;
                int contentH = ch - TabBarHeight - 4 - ResizeHandleHeight;
                if (contentH < 10) { _panel.EndDraw(); return; }

                // Set virtual scroll view bounds
                _viewTop = contentY;
                _viewBottom = contentY + contentH;

                // 1) Draw scrollable content (virtual scroll: items outside not drawn)
                int scrollOffset = _scrollOffsets[_activeTab];
                var layout = new StackLayout(cx, contentY - scrollOffset, cw, spacing: 4);

                switch (_activeTab)
                {
                    case Tab.Cheats: DrawCheatsTab(ref layout); break;
                    case Tab.Feats: DrawFeatsTab(ref layout); break;
                    case Tab.Other: DrawOtherTab(ref layout); break;
                    case Tab.Config: DrawConfigTab(ref layout); break;
                }

                _lastContentHeight = layout.TotalHeight;

                // 2) Draw tab bar ON TOP (opaque bg covers any scroll bleed)
                UIRenderer.DrawRect(cx, cy, cw, TabBarHeight + 4, UIColors.PanelBg);
                DrawCustomTabBar(cx, cy, cw, TabBarHeight);

                // 3) Draw resize handle at bottom of panel
                int handleY = cy + ch - ResizeHandleHeight;
                UIRenderer.DrawRect(cx, handleY, cw, ResizeHandleHeight, UIColors.PanelBg);
                DrawResizeHandle(cx, handleY, cw);

                // 4) Handle scroll for content area
                HandleScrollInput(cx, contentY, cw, contentH);
            }
            catch (Exception ex)
            {
                _log.Error($"PlunderPanel draw error: {ex.Message}");
            }
            finally
            {
                _panel.EndDraw();
                RichTooltip.DrawDeferred();
            }
        }

        private void DrawResizeHandle(int x, int y, int width)
        {
            bool hover = WidgetInput.IsMouseOver(x, y, width, ResizeHandleHeight);
            Color4 color = hover ? UIColors.TextHint : UIColors.Divider;

            int cx = x + width / 2;
            UIRenderer.DrawRect(cx - 12, y + 3, 24, 1, color);
            UIRenderer.DrawRect(cx - 8, y + 5, 16, 1, color);

            if (hover && WidgetInput.MouseLeftClick && !_isResizing)
            {
                _isResizing = true;
                _resizeStartY = WidgetInput.MouseY;
                _resizeStartHeight = _panel.Height;
                WidgetInput.ConsumeClick();
            }
        }

        private const int TabCharWidth = 11;

        private void DrawCustomTabBar(int x, int y, int width, int height)
        {
            int tabCount = TabNames.Length;
            int tabWidth = width / tabCount;

            for (int i = 0; i < tabCount; i++)
            {
                int tabX = x + i * tabWidth;
                int tabW = (i == tabCount - 1) ? (width - i * tabWidth) : tabWidth - 2;
                bool isActive = i == (int)_activeTab;
                bool hover = WidgetInput.IsMouseOver(tabX, y, tabW, height);

                // Background
                Color4 bg = isActive ? UIColors.ItemActiveBg : (hover ? UIColors.SectionBg : UIColors.InputBg);
                UIRenderer.DrawRect(tabX, y, tabW, height, bg);

                // Active indicator bar
                if (isActive)
                    UIRenderer.DrawRect(tabX, y + height - 2, tabW, 2, UIColors.Accent);

                // Centered text — use char count estimate since MeasureText is inaccurate
                int textWidth = TabNames[i].Length * TabCharWidth;
                int textX = tabX + (tabW - textWidth) / 2;
                int textY = y + (height - 14) / 2;
                Color4 textColor = isActive ? UIColors.AccentText : UIColors.TextDim;
                UIRenderer.DrawText(TabNames[i], textX, textY, textColor);

                // Click handling
                if (hover && WidgetInput.MouseLeftClick)
                {
                    WidgetInput.ConsumeClick();
                    _activeTab = (Tab)i;
                    if (!_scrollOffsets.ContainsKey(_activeTab))
                        _scrollOffsets[_activeTab] = 0;
                }
            }
        }

        /// <summary>
        /// Collapsible section header — ▼/▶ arrow + accent text + divider line.
        /// </summary>
        private bool DrawCollapsibleHeader(ref StackLayout layout, string title, string key)
        {
            if (!_sectionExpanded.ContainsKey(key))
                _sectionExpanded[key] = true;

            bool expanded = _sectionExpanded[key];
            string prefix = expanded ? "\u25BC " : "\u25B6 ";
            string display = prefix + title;

            int y = layout.Advance(22);

            if (InView(y, 22))
            {
                bool hover = WidgetInput.IsMouseOver(layout.X, y, layout.Width, 22);
                if (hover)
                    UIRenderer.DrawRect(layout.X, y, layout.Width, 22, UIColors.SectionBg);

                int textY = y + (22 - 14) / 2;
                UIRenderer.DrawText(display, layout.X, textY, UIColors.Accent);
                UIRenderer.DrawRect(layout.X, y + 20, layout.Width, 1, UIColors.Divider);

                if (hover && WidgetInput.MouseLeftClick)
                {
                    WidgetInput.ConsumeClick();
                    expanded = !expanded;
                    _sectionExpanded[key] = expanded;
                }
            }

            layout.Space(2);
            return expanded;
        }

        // ============================================================
        //  CHEATS TAB
        // ============================================================

        private void DrawCheatsTab(ref StackLayout layout)
        {
            // ---- OP CHEATS ----
            if (DrawCollapsibleHeader(ref layout, "OP CHEATS", "opcheats"))
            {
                DrawOpCheatsSection(ref layout);
            }

            layout.Space(4);

            // ---- VISUAL ---- compact checkboxes, 2 per row
            if (DrawCollapsibleHeader(ref layout, "VISUAL", "visual"))
            {
                bool fullBright = GetFullBrightState?.Invoke() ?? false;
                bool playerGlow = GetPlayerGlowState?.Invoke() ?? false;
                bool mapReveal = GetMapRevealState?.Invoke() ?? false;

                int hw = (layout.Width - 8) / 2;
                int rowY = layout.Advance(24);

                if (VCheckboxAt(ref layout, layout.X, hw, rowY, "Full Bright", fullBright,
                    "Full Bright", "Removes all darkness and shadow.\nEverything is fully lit at all times."))
                    OnFullBrightToggle?.Invoke();
                if (VCheckboxAt(ref layout, layout.X + hw + 8, hw, rowY, "Player Glow", playerGlow,
                    "Player Glow", "Emits a light aura around your character.\nUseful for exploring dark areas."))
                    OnPlayerGlowToggle?.Invoke();

                if (VCheckboxTip(ref layout, "Map Reveal", mapReveal,
                    "Full Map Reveal", "Reveals the entire world map.\nRe-reveals every 10 seconds to catch changes."))
                    OnMapRevealToggle?.Invoke();
            }

            layout.Space(4);

            // ---- MOVEMENT ---- compact checkboxes with tooltips
            if (DrawCollapsibleHeader(ref layout, "MOVEMENT", "movement"))
            {
                bool teleport = GetTeleportState?.Invoke() ?? false;
                bool mapTp = GetMapTeleportState?.Invoke() ?? false;

                if (VCheckboxTip(ref layout, "Teleport To Cursor", teleport,
                    "Teleport To Cursor", "Press T to teleport to cursor position.\nRebindable in Config tab."))
                    OnTeleportToggle?.Invoke();

                if (VCheckboxTip(ref layout, "Map Click Teleport", mapTp,
                    "Map Click Teleport", "Right-click on the fullscreen map\nto teleport to that location."))
                    OnMapTeleportToggle?.Invoke();
            }

            layout.Space(4);

            // ---- FISHING LUCK ----
            if (DrawCollapsibleHeader(ref layout, "FISHING LUCK", "fishing"))
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

                    // 3 checkboxes across, indented
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

                layout.Space(4);

                // Fishing Power slider
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

                layout.Space(4);

                bool legendary = GetLegendaryCratesState?.Invoke() ?? false;
                if (VCheckboxTip(ref layout, "Legendary Crates Only", legendary,
                    "Legendary Crates Only", "Re-rolls all catches until you get\na Legendary crate."))
                    OnLegendaryCratesToggle?.Invoke();

                layout.Space(4);

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
            }
        }

        // ============================================================
        //  OP CHEATS SECTION
        // ============================================================

        private void DrawOpCheatsSection(ref StackLayout layout)
        {
            bool godMode = GetGodModeState?.Invoke() ?? false;
            bool infMana = GetInfiniteManaState?.Invoke() ?? false;
            bool infFlight = GetInfiniteFlightState?.Invoke() ?? false;
            bool infAmmo = GetInfiniteAmmoState?.Invoke() ?? false;

            // Row 1: God Mode + Inf Mana
            int hw = (layout.Width - 8) / 2;
            int rowY = layout.Advance(24);
            if (VCheckboxAt(ref layout, layout.X, hw, rowY, "God Mode", godMode,
                "God Mode", "Full invincibility. HP stays max,\nall damage is blocked."))
                OnGodModeToggle?.Invoke();
            if (VCheckboxAt(ref layout, layout.X + hw + 8, hw, rowY, "Inf Mana", infMana,
                "Infinite Mana", "Mana stays at maximum at all times."))
                OnInfiniteManaToggle?.Invoke();

            // Row 2: Inf Flight + Inf Ammo
            rowY = layout.Advance(24);
            if (VCheckboxAt(ref layout, layout.X, hw, rowY, "Inf Flight", infFlight,
                "Infinite Flight", "Wing and rocket time never run out.\nFly indefinitely."))
                OnInfiniteFlightToggle?.Invoke();
            if (VCheckboxAt(ref layout, layout.X + hw + 8, hw, rowY, "Inf Ammo", infAmmo,
                "Infinite Ammo", "Ammo is never consumed when shooting."))
                OnInfiniteAmmoToggle?.Invoke();

            // Row 3: Inf Breath + No Knockback
            bool infBreath = GetInfiniteBreathState?.Invoke() ?? false;
            bool noKb = GetNoKnockbackState?.Invoke() ?? false;
            rowY = layout.Advance(24);
            if (VCheckboxAt(ref layout, layout.X, hw, rowY, "Inf Breath", infBreath,
                "Infinite Breath", "Breath meter stays full.\nNever drown underwater."))
                OnInfiniteBreathToggle?.Invoke();
            if (VCheckboxAt(ref layout, layout.X + hw + 8, hw, rowY, "No Knockback", noKb,
                "No Knockback", "Player cannot be knocked back by enemies."))
                OnNoKnockbackToggle?.Invoke();

            // Row 4: No Fall Damage + No Tree Bombs
            bool noFall = GetNoFallDamageState?.Invoke() ?? false;
            bool noTreeBombs = GetNoTreeBombsState?.Invoke() ?? false;
            rowY = layout.Advance(24);
            if (VCheckboxAt(ref layout, layout.X, hw, rowY, "No Fall Dmg", noFall,
                "No Fall Damage", "Prevents all fall damage."))
                OnNoFallDamageToggle?.Invoke();
            if (VCheckboxAt(ref layout, layout.X + hw + 8, hw, rowY, "No Tree Bombs", noTreeBombs,
                "No Tree Bombs", "Prevents trees from spawning lit bombs\nin For The Worthy / Zenith worlds."))
                OnNoTreeBombsToggle?.Invoke();

            layout.Space(4);

            // Minions: checkbox + slider
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

            layout.Space(4);

            // Damage: checkbox + slider
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

            layout.Space(4);

            // Spawn Rate slider
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

            layout.Space(4);

            // Run Speed slider
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

            layout.Space(4);

            // Tool Range — checkbox + slider
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
        }

        // ============================================================
        //  FEATS TAB — organized by category, compact buttons, tooltips
        // ============================================================

        private void DrawFeatsTab(ref StackLayout layout)
        {
            // Multiplier slider at the top
            VLabel(ref layout, $"Spawn Multiplier: {_packMultiplier}x" +
                (_packMultiplier > 1 ? " (stackable items only)" : ""), UIColors.TextHint);
            int multY = layout.Advance(22);
            if (InView(multY, 22))
            {
                _packMultiplier = _packMultiplierSlider.Draw(
                    layout.X, multY, layout.Width, 22, _packMultiplier, 1, 20);
            }

            layout.Space(6);

            var packs = GetItemPacks?.Invoke();
            if (packs == null || packs.Count == 0)
            {
                VLabel(ref layout, "No item packs available", UIColors.TextDim);
                return;
            }

            // Group packs by category
            var categories = packs
                .GroupBy(p => p.Category ?? "General")
                .OrderBy(g => CategoryOrder(g.Key));

            foreach (var group in categories)
            {
                string catKey = "feat_" + group.Key.ToLower().Replace(" ", "_");
                if (DrawCollapsibleHeader(ref layout, group.Key.ToUpper(), catKey))
                {
                    DrawPackButtons(ref layout, group.ToList());
                }
                layout.Space(2);
            }
        }

        private static int CategoryOrder(string cat)
        {
            switch (cat)
            {
                case "Essentials": return 0;
                case "Combat": return 1;
                case "Biomes": return 2;
                case "Resources": return 3;
                case "Building": return 4;
                case "Utility": return 5;
                default: return 6;
            }
        }

        private void DrawPackButtons(ref StackLayout layout, List<ItemPack> packs)
        {
            const int btnHeight = 26;
            const int gap = 6;

            // Lay out buttons in rows, 2 per row
            for (int i = 0; i < packs.Count; i += 2)
            {
                int hw = (layout.Width - gap) / 2;
                int rowY = layout.Advance(btnHeight);

                // First button
                var pack1 = packs[i];
                string tip1 = BuildPackTooltip(pack1, _packMultiplier);
                if (VButtonTip(layout.X, rowY, hw, btnHeight, pack1.Name, pack1.Name, tip1))
                    OnSpawnPack?.Invoke(pack1.Id, _packMultiplier);

                // Second button (if exists)
                if (i + 1 < packs.Count)
                {
                    var pack2 = packs[i + 1];
                    string tip2 = BuildPackTooltip(pack2, _packMultiplier);
                    if (VButtonTip(layout.X + hw + gap, rowY, hw, btnHeight, pack2.Name, pack2.Name, tip2))
                        OnSpawnPack?.Invoke(pack2.Id, _packMultiplier);
                }
            }
        }

        private string BuildPackTooltip(ItemPack pack, int multiplier)
        {
            string tip = pack.Description;
            if (multiplier > 1)
                tip += $"\n({multiplier}x multiplier applied to stackable items)";
            tip += "\n\nItems:";
            foreach (var item in pack.Items)
            {
                string name = item.Name ?? $"Item #{item.ItemId}";
                int displayStack = item.Stack;
                if (multiplier > 1 && item.Stack > 1)
                    displayStack = item.Stack * multiplier;
                if (displayStack > 1)
                    tip += $"\n  {name} x{displayStack}";
                else
                    tip += $"\n  {name}";
            }
            return tip;
        }

        // ============================================================
        //  OTHER TAB
        // ============================================================

        private void DrawOtherTab(ref StackLayout layout)
        {
            VSectionHeader(ref layout, "QUICK ACTIONS");

            if (VButton(ref layout, "Open Mod Menu (F6)", 28))
                OnOpenModMenu?.Invoke();

            layout.Space(8);
            VSectionHeader(ref layout, "DEBUG");

            bool mapTpDebug = MapTeleport.DebugMode;
            if (VCheckboxTip(ref layout, "Map Teleport Debug", mapTpDebug,
                "Map Teleport Debug", "Shows coordinate conversion values in chat\nwhen you right-click the map to teleport.\nUse to diagnose teleport accuracy issues."))
                MapTeleport.ToggleDebug();

            layout.Space(8);
            VSectionHeader(ref layout, "INFO");
            VLabel(ref layout, $"Plunder v{_config.ModVersion}");
            VLabel(ref layout, "Author: Zero", UIColors.TextHint);
        }

        // ============================================================
        //  CONFIG TAB
        // ============================================================

        private void DrawConfigTab(ref StackLayout layout)
        {
            // ---- KEYBINDS ----
            if (DrawCollapsibleHeader(ref layout, "KEYBINDS", "cfg_keybinds"))
            {
                VLabel(ref layout, "Current bindings (edit in Mod Menu)", UIColors.TextHint);
                layout.Space(2);

                DrawKeybindRow(ref layout, "Toggle Panel", "]");
                DrawKeybindRow(ref layout, "Full Bright", "Y");
                DrawKeybindRow(ref layout, "Player Glow", null);
                DrawKeybindRow(ref layout, "Toggle Teleport", "NumPad4");
                DrawKeybindRow(ref layout, "Teleport", "T");
                DrawKeybindRow(ref layout, "Fishing Buffs", null);
                DrawKeybindRow(ref layout, "Map Click Teleport", null);

                layout.Space(4);
                if (VButton(ref layout, "Open Keybind Settings (F6)", 26))
                    OnOpenModMenu?.Invoke();
            }

            layout.Space(4);

            // ---- PANEL SETTINGS ----
            if (DrawCollapsibleHeader(ref layout, "PANEL SETTINGS", "cfg_panel"))
            {
                // Panel Width — read from panel directly for immediate feedback
                int pw = _panel.Width;
                VLabel(ref layout, $"Panel Width: {pw}");
                int pwY = layout.Advance(22);
                if (InView(pwY, 22))
                {
                    int newPw = _panelWidthSlider.Draw(layout.X, pwY, layout.Width, 22, pw, 300, 600);
                    if (newPw != pw)
                    {
                        _panel.Width = newPw;
                        _config.Set("panelWidth", newPw);
                    }
                }

                layout.Space(2);

                // Panel Height — read from panel directly for immediate feedback
                int ph = _panel.Height;
                VLabel(ref layout, $"Panel Height: {ph}");
                int phY = layout.Advance(22);
                if (InView(phY, 22))
                {
                    int newPh = _panelHeightSlider.Draw(layout.X, phY, layout.Width, 22, ph, MinPanelHeight, MaxPanelHeight);
                    if (newPh != ph)
                    {
                        _panel.Height = newPh;
                        _config.Set("panelHeight", newPh);
                    }
                }

                layout.Space(6);

                bool showOnLoad = _config.ShowPanelOnWorldLoad;
                if (VCheckbox(ref layout, "Show Panel On World Load", showOnLoad))
                    _config.Set("showPanelOnWorldLoad", !showOnLoad);

                layout.Space(2);
                VLabel(ref layout, "Changes are saved automatically.", UIColors.TextHint);
            }
        }

        private void DrawKeybindRow(ref StackLayout layout, string label, string key)
        {
            int y = layout.Advance(20);
            if (!InView(y, 20)) return;

            int textY = y + (20 - 14) / 2;

            bool unbound = key == null;
            string display = unbound ? "(unbound)" : key;
            Color4 keyColor = unbound ? UIColors.TextHint : new Color4(255, 255, 180, 255);

            int keyWidth = UIRenderer.MeasureText(display);
            int keyX = layout.X + layout.Width - keyWidth - 4;

            // Truncate label so it doesn't overlap the key binding
            int labelMaxWidth = layout.Width - keyWidth - 16;
            string truncLabel = TextUtil.Truncate(label, labelMaxWidth);
            UIRenderer.DrawText(truncLabel, layout.X + 4, textY, UIColors.TextDim);
            UIRenderer.DrawText(display, keyX, textY, keyColor);
        }

        // ============================================================
        //  SCROLL INPUT
        // ============================================================

        private void HandleScrollInput(int x, int y, int w, int h)
        {
            int maxScroll = Math.Max(0, _lastContentHeight - h);

            if (WidgetInput.IsMouseOver(x, y, w, h))
            {
                int scroll = WidgetInput.ScrollWheel;
                if (scroll != 0)
                {
                    WidgetInput.ConsumeScroll();
                    int direction = scroll > 0 ? -1 : 1;
                    _scrollOffsets[_activeTab] += direction * ScrollSpeed;
                }
            }

            _scrollOffsets[_activeTab] = Math.Max(0,
                Math.Min(_scrollOffsets[_activeTab], maxScroll));
        }
    }
}
