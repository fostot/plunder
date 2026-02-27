using System;
using System.Collections.Generic;
using System.Linq;
using TerrariaModder.Core.Logging;
using TerrariaModder.Core.UI;
using TerrariaModder.Core.UI.Widgets;

namespace Plunder
{
    public partial class PlunderPanel
    {
        private readonly ILogger _log;
        private readonly PlunderConfig _config;
        private DraggablePanel _panel;

        // Tab system
        private enum Tab { Cheats, Packs, Other, Config }
        private Tab _activeTab = Tab.Cheats;
        private static readonly string[] TabNames = { "CHEATS", "PACKS", "OTHER", "CONFIG" };
        private int _normalPanelWidth;  // stash width when entering wide tabs
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
        private const int ResizeHandleHeight = 18;
        private const int MinPanelHeight = 200;
        private const int MaxPanelHeight = 900;

        // Slider instances (stateful widgets need instances)
        private readonly Slider _fishingPowerSlider = new Slider();
        private readonly Slider _catchRaritySlider = new Slider();
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

        // CHEATS tab — split-panel state
        private float _cheatsSplitRatio = 0.35f;
        private bool _cheatsSplitDragging;
        private string _cheatsSelectedCategory = "survival";
        private readonly TextInput _cheatsSearchInput = new TextInput("Search cheats...", 200);
        private int _cheatsLeftScroll;
        private int _cheatsLeftContentH;
        private bool _cheatsLeftDrag;
        private int _cheatsLeftDragY;
        private int _cheatsLeftDragOff;
        private int _cheatsRightScroll;
        private int _cheatsRightContentH;
        private bool _cheatsRightDrag;
        private int _cheatsRightDragY;
        private int _cheatsRightDragOff;

        // PACKS tab — Pack Manager split-panel state
        private float _packsSplitRatio = 0.35f;
        private bool _packsSplitDragging;
        private string _packsSelectedId;
        private int _packsLeftScroll;
        private int _packsRightScroll;
        private int _packsLeftContentH;
        private int _packsRightContentH;
        private bool _packsLeftDrag;
        private int _packsLeftDragY;
        private int _packsLeftDragOff;
        private bool _packsRightDrag;
        private int _packsRightDragY;
        private int _packsRightDragOff;
        // PACKS tab — Add/Edit Item row state
        private bool _packsAddingNewItem;
        private int _packsEditingItemIdx = -1;
        private readonly TextInput _packsItemId = new TextInput("Item ID...", 10);
        private readonly TextInput _packsItemName = new TextInput("Item name...", 100);
        private readonly TextInput _packsItemCount = new TextInput("Count...", 10);

        // PACKS tab — Pack name/category editing
        private bool _packsEditingName;
        private readonly TextInput _packsNameInput = new TextInput("Pack name...", 100);
        private bool _packsEditingCategory;
        private readonly TextInput _packsCategoryInput = new TextInput("Category...", 50);

        // PACKS tab — Name lookup results
        private List<ItemEntry> _packsLookupResults = new List<ItemEntry>();

        // PACKS tab — Save validation
        private string _packsSaveError = "";

        // ---- Callbacks wired from Mod.cs ----
        // Cheats: Visual
        public Action OnFullBrightToggle;
        public Func<bool> GetFullBrightState;
        public Action OnPlayerGlowToggle;
        public Func<bool> GetPlayerGlowState;
        public Action OnMapRevealToggle;
        public Func<bool> GetMapRevealState;

        // Cheats: Player
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

        // Cheats: World Actions
        public Action OnNoGravestonesToggle;
        public Func<bool> GetNoGravestonesState;
        public Action OnNoDeathDropToggle;
        public Func<bool> GetNoDeathDropState;
        public Action OnKillAllEnemies;
        public Action OnClearItems;
        public Action OnClearProjectiles;

        // Cheats: Environment
        public Action OnTimePauseToggle;
        public Func<bool> GetTimePausedState;
        public Action OnSetDawn;
        public Action OnSetNoon;
        public Action OnSetDusk;
        public Action OnSetMidnight;
        public Action OnFastForwardDawn;
        public Action OnToggleRain;
        public Func<bool> GetRainingState;
        public Action OnToggleBloodMoon;
        public Func<bool> GetBloodMoonState;
        public Action OnToggleEclipse;
        public Func<bool> GetEclipseState;

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

        // Packs: Item Packs
        public Func<IReadOnlyList<ItemPack>> GetItemPacks;
        public Action<string, int> OnSpawnPack;
        public Func<string, string> OnExportPack;
        public Func<string, ItemPack> OnImportPack;
        public Func<string, int, List<ItemEntry>> OnSearchItems;
        public Action<string, string, string, List<PackItem>> OnCreatePack;
        public Action OnBuildCatalog;
        public Func<string, bool> OnDeletePack;
        public Func<string, int, int, string, bool> OnAddItemToPack;
        public Func<string, int, bool> OnRemoveItemFromPack;
        public Func<string, int, int, int, string, bool> OnUpdateItemInPack;
        public Func<string, string, bool> OnRenamePack;
        public Func<string, string, bool> OnUpdatePackCategory;
        public Func<string, bool> OnResetBuiltInPack;

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

            _sectionExpanded["cheats_player"] = true;
            _sectionExpanded["cheats_world"] = true;
            _sectionExpanded["pack_spawn"] = true;
            _sectionExpanded["pack_create"] = false;
            _sectionExpanded["pack_edit"] = false;
            _sectionExpanded["pack_import"] = false;
            _sectionExpanded["pack_export"] = false;
            _sectionExpanded["other_actions"] = true;
            _sectionExpanded["other_debug"] = true;
            _sectionExpanded["other_info"] = true;
            _sectionExpanded["cfg_keybinds"] = true;
            _sectionExpanded["cfg_panel"] = true;

            _normalPanelWidth = config.PanelWidth;
        }

        public void Register() { _panel.RegisterDrawCallback(OnDraw); }
        public void Unregister() { _panel.UnregisterDrawCallback(); _panel.Close(); }

        /// <summary>
        /// Must be called during the Update phase (FrameEvents.OnPreUpdate) so that
        /// focused TextInputs keep EnableTextInput() active across both Update and Draw.
        /// Without this, Terraria's input system eats keystrokes before Draw reads them.
        /// </summary>
        public void Update()
        {
            if (!(_panel?.IsOpen ?? false)) return;

            _cheatsSearchInput.Update();
            _packsItemId.Update();
            _packsItemName.Update();
            _packsItemCount.Update();
            _packsNameInput.Update();
            _packsCategoryInput.Update();
        }

        public void Open()
        {
            int x = _config.PanelX;
            int y = _config.PanelY;
            if (x >= 0 && y >= 0) _panel.Open(x, y);
            else _panel.Open();

            // Wide tabs auto-expand on open
            if (_activeTab == Tab.Cheats || _activeTab == Tab.Packs)
            {
                if (_normalPanelWidth <= 0) _normalPanelWidth = _panel.Width;
                _panel.Width = (int)(_normalPanelWidth * 1.75);
            }
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

        /// <summary>Word-wrapped label — splits text into multiple lines if too wide for layout.</summary>
        private void VLabelWrapped(ref StackLayout layout, string text, Color4 color, int lineHeight = 18)
        {
            int maxWidth = layout.Width;
            var lines = WrapText(text, maxWidth);
            foreach (var line in lines)
            {
                int y = layout.Advance(lineHeight);
                if (InView(y, lineHeight))
                {
                    int textY = y + (lineHeight - 14) / 2;
                    UIRenderer.DrawText(line, layout.X, textY, color);
                }
            }
        }

        private void VLabelWrapped(ref StackLayout layout, string text, int lineHeight = 18)
        {
            VLabelWrapped(ref layout, text, UIColors.Text, lineHeight);
        }

        /// <summary>Word-wrap text to fit within maxWidth pixels, using same algorithm as tooltips.</summary>
        private static List<string> WrapText(string text, int maxWidth)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                lines.Add("");
                return lines;
            }

            foreach (string paragraph in text.Split('\n'))
            {
                if (TextUtil.MeasureWidth(paragraph) <= maxWidth)
                {
                    lines.Add(paragraph);
                    continue;
                }

                string remaining = paragraph;
                while (TextUtil.MeasureWidth(remaining) > maxWidth)
                {
                    // Find last space that fits
                    int breakAt = -1;
                    for (int i = remaining.Length - 1; i > 0; i--)
                    {
                        if (remaining[i] == ' ' && TextUtil.MeasureWidth(remaining.Substring(0, i)) <= maxWidth)
                        {
                            breakAt = i;
                            break;
                        }
                    }
                    // No space found — force break at max fitting chars
                    if (breakAt <= 0)
                    {
                        breakAt = remaining.Length;
                        for (int i = 1; i < remaining.Length; i++)
                        {
                            if (TextUtil.MeasureWidth(remaining.Substring(0, i)) > maxWidth)
                            {
                                breakAt = Math.Max(1, i - 1);
                                break;
                            }
                        }
                    }
                    lines.Add(remaining.Substring(0, breakAt));
                    remaining = remaining.Substring(breakAt).TrimStart();
                }
                if (remaining.Length > 0)
                    lines.Add(remaining);
            }
            return lines;
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
                // Resize bar sits flush inside the 2px panel border (no gap on left/right/bottom)
                int borderThickness = 2;
                int handleTop = _panel.Y + _panel.Height - borderThickness - ResizeHandleHeight;
                int contentH = handleTop - contentY;
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
                    case Tab.Packs: DrawPacksTab(ref layout); break;
                    case Tab.Other: DrawOtherTab(ref layout); break;
                    case Tab.Config: DrawConfigTab(ref layout); break;
                }

                _lastContentHeight = layout.TotalHeight;

                // 2) Draw tab bar ON TOP (opaque bg covers any scroll bleed)
                UIRenderer.DrawRect(cx, cy, cw, TabBarHeight + 4, UIColors.PanelBg);
                DrawCustomTabBar(cx, cy, cw, TabBarHeight);

                // 3) Draw resize handle flush inside panel border (no gap on left/right/bottom)
                int barX = _panel.X + borderThickness;
                int barW = _panel.Width - borderThickness * 2;
                DrawResizeHandle(barX, handleTop, barW);

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

        private const int DefaultPanelHeight = 600;

        private void DrawResizeHandle(int x, int y, int width)
        {
            // Styled background bar (ModMenu section header style, but shorter)
            UIRenderer.DrawRect(x, y, width, ResizeHandleHeight, UIColors.SectionBg);
            UIRenderer.DrawRect(x, y, width, 1, UIColors.Divider);

            bool barHover = WidgetInput.IsMouseOver(x, y, width, ResizeHandleHeight);

            // Drag handle dots in center
            Color4 dotColor = barHover ? UIColors.TextHint : UIColors.Divider;
            int cx = x + width / 2;
            int dotY = y + ResizeHandleHeight / 2 - 1;
            UIRenderer.DrawRect(cx - 12, dotY, 24, 1, dotColor);
            UIRenderer.DrawRect(cx - 8, dotY + 2, 16, 1, dotColor);

            // "Reset" text button on the right (small text, no background, hover color animation)
            string resetText = "Reset";
            int resetW = (int)(TextUtil.MeasureWidth(resetText) * 0.75f);
            int resetX = x + width - resetW - 10;
            int resetTextY = y + (ResizeHandleHeight - 11) / 2;  // 11px = small font height (14 * 0.75)
            bool resetHover = WidgetInput.IsMouseOver(resetX - 4, y, resetW + 8, ResizeHandleHeight);
            Color4 resetColor = resetHover ? UIColors.Warning : UIColors.TextDim;
            UIRenderer.DrawTextSmall(resetText, resetX, resetTextY, resetColor);

            // Handle clicks — reset or resize drag
            if (barHover && WidgetInput.MouseLeftClick)
            {
                if (resetHover)
                {
                    _panel.Height = DefaultPanelHeight;
                    _config.Set("panelHeight", DefaultPanelHeight);
                }
                else if (!_isResizing)
                {
                    _isResizing = true;
                    _resizeStartY = WidgetInput.MouseY;
                    _resizeStartHeight = _panel.Height;
                }
                WidgetInput.ConsumeClick();
            }

            // Block all clicks from passing through the bar
            if (barHover && WidgetInput.MouseRightClick)
                WidgetInput.ConsumeRightClick();
            if (barHover)
                WidgetInput.ConsumeScroll();
        }

        private const int TabCharWidth = 11;

        private void DrawCustomTabBar(int x, int y, int width, int height)
        {
            var visibleTabs = new List<int>();
            for (int i = 0; i < TabNames.Length; i++)
                visibleTabs.Add(i);

            int tabCount = visibleTabs.Count;
            int tabWidth = width / tabCount;

            for (int vi = 0; vi < tabCount; vi++)
            {
                int i = visibleTabs[vi];
                int tabX = x + vi * tabWidth;
                int tabW = (vi == tabCount - 1) ? (width - vi * tabWidth) : tabWidth - 2;
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
                    Tab prevTab = _activeTab;
                    _activeTab = (Tab)i;
                    if (!_scrollOffsets.ContainsKey(_activeTab))
                        _scrollOffsets[_activeTab] = 0;

                    // CHEATS and PACKS tabs expand width to 1.75x; leaving restores it
                    bool wasWide = (prevTab == Tab.Cheats || prevTab == Tab.Packs);
                    bool isWide = (_activeTab == Tab.Cheats || _activeTab == Tab.Packs);
                    if (isWide && !wasWide)
                    {
                        _normalPanelWidth = _panel.Width;
                        _panel.Width = (int)(_normalPanelWidth * 1.75);
                    }
                    else if (!isWide && wasWide && _normalPanelWidth > 0)
                    {
                        _panel.Width = _normalPanelWidth;
                    }
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

        // Tab drawing methods are in partial class files:
        //   PlunderPanel.Cheats.cs  — DrawCheatsTab, DrawPlayerSection, DrawWorldSection
        //   PlunderPanel.Packs.cs   — DrawPacksTab, Pack editor panels, DrawSubScrollbar
        //   PlunderPanel.Other.cs   — DrawOtherTab
        //   PlunderPanel.Config.cs  — DrawConfigTab, DrawKeybindRow

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
