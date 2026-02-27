using System;
using System.Collections.Generic;
using System.Linq;
using TerrariaModder.Core.Input;
using TerrariaModder.Core.UI;
using TerrariaModder.Core.UI.Widgets;

namespace Plunder
{
    public partial class PlunderPanel
    {
        // ============================================================
        //  PACKS TAB
        // ============================================================

        private void DrawPacksTab(ref StackLayout layout)
        {
            int areaX = layout.X;
            int areaW = layout.Width;
            int areaY = _viewTop;
            int areaH = _viewBottom - _viewTop;

            const int splitW = 6; // draggable splitter width
            const float minRatio = 0.15f;
            const float maxRatio = 0.65f;

            int leftW = (int)(areaW * _packsSplitRatio);
            int splitX = areaX + leftW;
            int rightX = splitX + splitW;
            int rightW = areaW - leftW - splitW;

            // Draw panels
            UIRenderer.DrawRect(areaX, areaY, leftW, areaH, UIColors.InputBg);
            UIRenderer.DrawRectOutline(areaX, areaY, leftW, areaH, UIColors.Divider);
            UIRenderer.DrawRect(rightX, areaY, rightW, areaH, UIColors.InputBg);
            UIRenderer.DrawRectOutline(rightX, areaY, rightW, areaH, UIColors.Divider);

            DrawPacksLeftPanel(areaX, areaY, leftW, areaH);
            DrawPacksRightPanel(rightX, areaY, rightW, areaH);

            // Splitter handle
            bool splitHover = WidgetInput.IsMouseOver(splitX - 2, areaY, splitW + 4, areaH);
            UIRenderer.DrawRect(splitX, areaY, splitW, areaH,
                (_packsSplitDragging || splitHover) ? UIColors.Divider : UIColors.SectionBg);

            // Draw grip dots in the center of the splitter
            int gripCenterY = areaY + areaH / 2;
            Color4 gripColor = (_packsSplitDragging || splitHover) ? UIColors.TextDim : UIColors.Divider;
            for (int d = -2; d <= 2; d++)
            {
                int dotY = gripCenterY + d * 6;
                UIRenderer.DrawRect(splitX + 1, dotY, splitW - 2, 2, gripColor);
            }

            // Splitter drag logic
            if (_packsSplitDragging)
            {
                if (WidgetInput.MouseLeft)
                {
                    float newRatio = (float)(WidgetInput.MouseX - areaX) / areaW;
                    _packsSplitRatio = Math.Max(minRatio, Math.Min(maxRatio, newRatio));
                }
                else
                {
                    _packsSplitDragging = false;
                }
            }
            else if (splitHover && WidgetInput.MouseLeftClick)
            {
                _packsSplitDragging = true;
                WidgetInput.ConsumeClick();
            }

            layout.Advance(areaH);
        }

        // ---- PACKS: Left Panel (Pack Tree View) ----

        private void DrawPacksLeftPanel(int px, int py, int pw, int ph)
        {
            const int pad = 4;
            const int btnH = 26;
            const int scrollbarW = 8;
            int innerX = px + pad;
            int innerW = pw - pad * 2;

            // ── Pinned TOP: "Create Pack" button ──
            int topY = py + pad;
            int createBtnW = innerW;
            bool createHover = WidgetInput.IsMouseOver(innerX, topY, createBtnW, btnH);
            UIRenderer.DrawRect(innerX, topY, createBtnW, btnH,
                createHover ? UIColors.ButtonHover : UIColors.Button);
            string createLabel = "+ Create Pack";
            int createLabelW = createLabel.Length * TabCharWidth;
            UIRenderer.DrawText(createLabel, innerX + (createBtnW - createLabelW) / 2,
                topY + (btnH - 14) / 2, UIColors.Text);
            if (createHover && WidgetInput.MouseLeftClick)
            {
                WidgetInput.ConsumeClick();
                string uniqueName = "New Pack " + DateTime.Now.ToString("HHmmss");
                OnCreatePack?.Invoke(uniqueName, "", "General", new List<PackItem>());
                var packs = GetItemPacks?.Invoke();
                if (packs != null)
                {
                    var newest = packs.LastOrDefault(p => !p.IsBuiltIn);
                    if (newest != null) _packsSelectedId = newest.Id;
                }
            }

            int pinnedTopBottom = topY + btnH + pad;

            // ── Pinned BOTTOM: Import Pack button only ──
            int bottomBtnH = 24;
            int bottomAreaH = bottomBtnH + pad;
            int bottomAreaY = py + ph - bottomAreaH;
            int importBtnY = bottomAreaY;

            UIRenderer.DrawRect(px + 1, bottomAreaY - 2, pw - 2, bottomAreaH + 2, UIColors.InputBg);
            UIRenderer.DrawRect(px + 1, bottomAreaY - 2, pw - 2, 1, UIColors.Divider);

            bool importHover = WidgetInput.IsMouseOver(innerX, importBtnY, innerW, bottomBtnH);
            UIRenderer.DrawRect(innerX, importBtnY, innerW, bottomBtnH,
                importHover ? UIColors.ButtonHover : UIColors.Button);
            string impLabel = "Import Pack";
            UIRenderer.DrawTextSmall(impLabel, innerX + (innerW - (int)(impLabel.Length * TabCharWidth * 0.75f)) / 2,
                importBtnY + (bottomBtnH - 11) / 2, UIColors.Text);
            if (importHover && WidgetInput.MouseLeftClick)
            {
                WidgetInput.ConsumeClick();
                try
                {
                    string clipboard = null;
                    var thread = new System.Threading.Thread(() =>
                        clipboard = System.Windows.Forms.Clipboard.GetText());
                    thread.SetApartmentState(System.Threading.ApartmentState.STA);
                    thread.Start();
                    thread.Join();
                    if (!string.IsNullOrEmpty(clipboard))
                    {
                        var imported = OnImportPack?.Invoke(clipboard);
                        if (imported != null) _packsSelectedId = imported.Id;
                    }
                }
                catch { }
            }

            // ── Scrollable MIDDLE: Category tree ──
            int treeY = pinnedTopBottom;
            int treeH = bottomAreaY - 2 - treeY - 15;
            int treeContentW = innerW - scrollbarW - 2;

            var allPacks = GetItemPacks?.Invoke();
            int contentCursor = 0;
            const int catHeaderH = 22;
            const int packItemH = 22;

            if (allPacks != null)
            {
                var categories = allPacks
                    .GroupBy(p => p.Category ?? "General", StringComparer.OrdinalIgnoreCase)
                    .OrderBy(g => CategoryOrder(g.Key));

                foreach (var group in categories)
                {
                    int catY = treeY + contentCursor - _packsLeftScroll;
                    if (catY + catHeaderH > treeY && catY < treeY + treeH)
                    {
                        string catKey = "packs_cat_" + group.Key.ToLower().Replace(" ", "_");
                        if (!_sectionExpanded.ContainsKey(catKey))
                            _sectionExpanded[catKey] = true;
                        bool expanded = _sectionExpanded[catKey];

                        UIRenderer.DrawRect(innerX, catY, treeContentW, catHeaderH, UIColors.SectionBg);
                        string arrow = expanded ? "\u25BC " : "\u25B6 ";
                        UIRenderer.DrawTextSmall(arrow + group.Key.ToUpper(), innerX + 4,
                            catY + (catHeaderH - 11) / 2, UIColors.Warning);

                        if (WidgetInput.IsMouseOver(innerX, catY, treeContentW, catHeaderH)
                            && WidgetInput.MouseLeftClick)
                        {
                            _sectionExpanded[catKey] = !expanded;
                            WidgetInput.ConsumeClick();
                        }
                    }
                    contentCursor += catHeaderH;

                    string catKey2 = "packs_cat_" + group.Key.ToLower().Replace(" ", "_");
                    bool isExpanded = _sectionExpanded.ContainsKey(catKey2) && _sectionExpanded[catKey2];

                    if (isExpanded)
                    {
                        int packIndex = 0;
                        foreach (var pack in group)
                        {
                            int itemY = treeY + contentCursor - _packsLeftScroll;
                            if (itemY + packItemH > treeY && itemY < treeY + treeH)
                            {
                                bool selected = pack.Id == _packsSelectedId;
                                bool itemHover = WidgetInput.IsMouseOver(innerX, itemY, treeContentW, packItemH);

                                Color4 itemBg = selected ? UIColors.ItemActiveBg
                                    : (itemHover ? UIColors.ItemHoverBg : UIColors.InputBg);
                                UIRenderer.DrawRect(innerX, itemY, treeContentW, packItemH, itemBg);
                                if (!selected && !itemHover && packIndex % 2 == 1)
                                    UIRenderer.DrawRect(innerX, itemY, treeContentW, packItemH, new Color4(42, 42, 58, 255));

                                Color4 nameColor = selected ? UIColors.AccentText : UIColors.Text;
                                string displayName = TextUtil.Truncate(pack.Name, treeContentW - 8);
                                UIRenderer.DrawTextSmall(displayName, innerX + 12,
                                    itemY + (packItemH - 11) / 2, nameColor);

                                if (itemHover && WidgetInput.MouseLeftClick)
                                {
                                    _packsSelectedId = pack.Id;
                                    _packsRightScroll = 0;
                                    _packsAddingNewItem = false;
                                    _packsEditingItemIdx = -1;
                                    _packsEditingName = false;
                                    _packsEditingCategory = false;
                                    _packsLookupResults.Clear();
                
                                    _packsSaveError = "";
                                    WidgetInput.ConsumeClick();
                                }
                            }
                            contentCursor += packItemH;
                            packIndex++;
                        }
                    }
                }
            }

            _packsLeftContentH = contentCursor;

            DrawSubScrollbar(px + pw - scrollbarW - 2, treeY, scrollbarW, treeH,
                _packsLeftContentH, treeH,
                ref _packsLeftScroll, ref _packsLeftDrag, ref _packsLeftDragY, ref _packsLeftDragOff);

            if (WidgetInput.IsMouseOver(px, treeY, pw, treeH))
            {
                int scroll = WidgetInput.ScrollWheel;
                if (scroll != 0)
                {
                    WidgetInput.ConsumeScroll();
                    _packsLeftScroll += scroll > 0 ? -30 : 30;
                    int maxScroll = Math.Max(0, _packsLeftContentH - treeH);
                    _packsLeftScroll = Math.Max(0, Math.Min(_packsLeftScroll, maxScroll));
                }
            }

            // Redraw pinned top over scroll bleed
            UIRenderer.DrawRect(px + 1, py + 1, pw - 2, pinnedTopBottom - py - 1, UIColors.InputBg);
            createHover = WidgetInput.IsMouseOver(innerX, topY, createBtnW, btnH);
            UIRenderer.DrawRect(innerX, topY, createBtnW, btnH,
                createHover ? UIColors.ButtonHover : UIColors.Button);
            createLabelW = createLabel.Length * TabCharWidth;
            UIRenderer.DrawText(createLabel, innerX + (createBtnW - createLabelW) / 2,
                topY + (btnH - 14) / 2, UIColors.Text);
        }

        // ---- PACKS: Right Panel (Pack Editor / Viewer) ----

        private void DrawPacksRightPanel(int px, int py, int pw, int ph)
        {
            const int pad = 4;
            const int btnH = 24;
            const int btnGap = 4;
            const int scrollbarW = 8;
            int innerX = px + pad;
            int innerW = pw - pad * 2;

            // ── Pinned TOP ──
            int topY = py + pad;

            var allPacks = GetItemPacks?.Invoke();
            ItemPack selectedPack = null;
            if (_packsSelectedId != null && allPacks != null)
                selectedPack = allPacks.FirstOrDefault(p => p.Id == _packsSelectedId);

            // Pack title — clickable to edit for user packs
            string title = selectedPack != null ? selectedPack.Name : "No Pack Selected";
            int titleH = 20;
            bool titleEditable = selectedPack != null && !selectedPack.IsBuiltIn;

            if (_packsEditingName && titleEditable)
            {
                // Editable title: text input + save button
                int saveW = 42;
                int inputW = innerW - saveW - btnGap;
                _packsNameInput.Draw(innerX, topY, inputW, titleH);

                int saveBtnX = innerX + inputW + btnGap;
                bool saveHover = WidgetInput.IsMouseOver(saveBtnX, topY, saveW, titleH);
                UIRenderer.DrawRect(saveBtnX, topY, saveW, titleH,
                    saveHover ? new Color4(40, 120, 40, 255) : new Color4(30, 90, 30, 255));
                UIRenderer.DrawTextSmall("Save", saveBtnX + 10, topY + (titleH - 11) / 2, UIColors.Text);
                if (saveHover && WidgetInput.MouseLeftClick)
                {
                    WidgetInput.ConsumeClick();
                    string newName = _packsNameInput.Text?.Trim();
                    if (!string.IsNullOrEmpty(newName))
                    {
                        OnRenamePack?.Invoke(selectedPack.Id, newName);
                        title = newName;
                    }
                    _packsEditingName = false;
                }
            }
            else
            {
                // Static title — click to enter edit mode
                UIRenderer.DrawText(title, innerX + 2, topY + 2, UIColors.AccentText);
                if (titleEditable)
                {
                    bool titleHover = WidgetInput.IsMouseOver(innerX, topY, innerW, titleH);
                    if (titleHover && WidgetInput.MouseLeftClick)
                    {
                        WidgetInput.ConsumeClick();
                        _packsEditingName = true;
                        _packsNameInput.Text = selectedPack.Name;
                    }
                }
            }
            int titleBottom = topY + titleH;

            // Category subtitle — clickable to edit for user packs
            int catH = 18;
            bool catEditable = selectedPack != null && !selectedPack.IsBuiltIn;

            if (_packsEditingCategory && catEditable)
            {
                int catSaveW = 42;
                int catInputW = innerW - catSaveW - btnGap;
                _packsCategoryInput.Draw(innerX, titleBottom, catInputW, catH);

                int catSaveX = innerX + catInputW + btnGap;
                bool catSaveHover = WidgetInput.IsMouseOver(catSaveX, titleBottom, catSaveW, catH);
                UIRenderer.DrawRect(catSaveX, titleBottom, catSaveW, catH,
                    catSaveHover ? new Color4(40, 120, 40, 255) : new Color4(30, 90, 30, 255));
                UIRenderer.DrawTextSmall("Save", catSaveX + 10, titleBottom + (catH - 11) / 2, UIColors.Text);
                if (catSaveHover && WidgetInput.MouseLeftClick)
                {
                    WidgetInput.ConsumeClick();
                    string newCat = _packsCategoryInput.Text?.Trim();
                    if (!string.IsNullOrEmpty(newCat))
                        OnUpdatePackCategory?.Invoke(selectedPack.Id, newCat);
                    _packsEditingCategory = false;
                }
            }
            else if (selectedPack != null)
            {
                UIRenderer.DrawTextSmall(selectedPack.Category, innerX + 2, titleBottom + 2, UIColors.TextDim);
                if (catEditable)
                {
                    bool catHover = WidgetInput.IsMouseOver(innerX, titleBottom, innerW, catH);
                    if (catHover && WidgetInput.MouseLeftClick)
                    {
                        WidgetInput.ConsumeClick();
                        _packsEditingCategory = true;
                        _packsCategoryInput.Text = selectedPack.Category;
                    }
                }
            }
            int subtitleBottom = titleBottom + catH;

            // Action buttons — [Spawn Items][Delete/Reset][Add Item][Export Pack]
            bool isBuiltIn = selectedPack != null && selectedPack.IsBuiltIn;
            int row1Y = subtitleBottom + 4;
            int quarterW = (innerW - btnGap * 3) / 4;

            // 1) Spawn Items (green)
            int btn1X = innerX;
            bool spawnHover = WidgetInput.IsMouseOver(btn1X, row1Y, quarterW, btnH);
            UIRenderer.DrawRect(btn1X, row1Y, quarterW, btnH,
                spawnHover ? new Color4(40, 120, 40, 255) : new Color4(30, 90, 30, 255));
            UIRenderer.DrawTextSmall("Spawn Items", btn1X + 4, row1Y + (btnH - 11) / 2, UIColors.Text);
            if (spawnHover && WidgetInput.MouseLeftClick && selectedPack != null)
            {
                WidgetInput.ConsumeClick();
                OnSpawnPack?.Invoke(selectedPack.Id, _packMultiplier);
            }

            // 2) Delete Pack / Reset Pack (red) — context-aware
            int btn2X = btn1X + quarterW + btnGap;
            bool delHover = WidgetInput.IsMouseOver(btn2X, row1Y, quarterW, btnH);
            string btn2Label = isBuiltIn ? "Reset Pack" : "Delete Pack";
            UIRenderer.DrawRect(btn2X, row1Y, quarterW, btnH,
                delHover ? new Color4(160, 40, 40, 255) : new Color4(120, 30, 30, 255));
            UIRenderer.DrawTextSmall(btn2Label, btn2X + 4, row1Y + (btnH - 11) / 2, UIColors.Text);
            if (delHover && WidgetInput.MouseLeftClick && selectedPack != null)
            {
                WidgetInput.ConsumeClick();
                if (isBuiltIn)
                    OnResetBuiltInPack?.Invoke(selectedPack.Id);
                else if (OnDeletePack?.Invoke(selectedPack.Id) == true)
                    _packsSelectedId = null;
            }

            // 3) Add Item (blue) — works for all packs
            int btn3X = btn2X + quarterW + btnGap;
            bool addHover = WidgetInput.IsMouseOver(btn3X, row1Y, quarterW, btnH);
            UIRenderer.DrawRect(btn3X, row1Y, quarterW, btnH,
                addHover ? new Color4(40, 90, 160, 255) : new Color4(30, 70, 130, 255));
            UIRenderer.DrawTextSmall("Add Item", btn3X + 4, row1Y + (btnH - 11) / 2, UIColors.Text);
            if (addHover && WidgetInput.MouseLeftClick && selectedPack != null)
            {
                WidgetInput.ConsumeClick();
                _packsAddingNewItem = true;
                _packsEditingItemIdx = -1;
                _packsItemId.Text = "";
                _packsItemName.Text = "";
                _packsItemCount.Text = "";
                _packsLookupResults.Clear();
                _packsSaveError = "";
            }

            int btn4X = btn3X + quarterW + btnGap;
            int btn4W = innerW - (btn4X - innerX);
            bool expBtnHover = WidgetInput.IsMouseOver(btn4X, row1Y, btn4W, btnH);
            UIRenderer.DrawRect(btn4X, row1Y, btn4W, btnH,
                expBtnHover ? new Color4(160, 120, 30, 255) : new Color4(130, 100, 20, 255));
            UIRenderer.DrawTextSmall("Export Pack", btn4X + 4, row1Y + (btnH - 11) / 2, UIColors.Text);
            if (expBtnHover && WidgetInput.MouseLeftClick && selectedPack != null)
            {
                WidgetInput.ConsumeClick();
                string json = OnExportPack?.Invoke(selectedPack.Id);
                if (json != null)
                {
                    try
                    {
                        var thread = new System.Threading.Thread(() =>
                            System.Windows.Forms.Clipboard.SetText(json));
                        thread.SetApartmentState(System.Threading.ApartmentState.STA);
                        thread.Start();
                        thread.Join();
                    }
                    catch { }
                }
            }

            int pinnedTopBottom = row1Y + btnH + pad;
            UIRenderer.DrawRect(innerX, pinnedTopBottom - 2, innerW, 1, UIColors.Divider);

            // ── Clear changed flags so they don't leak ──
            if (_packsAddingNewItem || _packsEditingItemIdx >= 0)
            {
                if (_packsItemName.HasChanged || _packsItemId.HasChanged)
                    _packsSaveError = "";
            }

            // ── Scrollable MIDDLE: Pack contents ──
            int listY = pinnedTopBottom;
            int listH = py + ph - pad - listY;
            int listContentW = innerW - scrollbarW - 2;
            int contentCursor = 0;

            if (selectedPack != null)
            {
                // Header
                int hdrY = listY + contentCursor - _packsRightScroll;
                if (hdrY + 18 > listY && hdrY < listY + listH)
                {
                    UIRenderer.DrawTextSmall($"ITEMS ({selectedPack.Items.Count})", innerX + 2,
                        hdrY + 2, UIColors.Warning);
                }
                contentCursor += 20;

                // Item list
                for (int i = 0; i < selectedPack.Items.Count; i++)
                {
                    var item = selectedPack.Items[i];

                    if (i == _packsEditingItemIdx)
                    {
                        // ── Inline edit mode for this item ──
                        contentCursor = DrawItemEditFields(innerX, listY, listH, listContentW,
                            contentCursor, selectedPack, i);
                    }
                    else
                    {
                        // ── Normal display row ──
                        int itemY = listY + contentCursor - _packsRightScroll;
                        const int itemRowH = 22;

                        if (itemY + itemRowH > listY && itemY < listY + listH)
                        {
                            bool rowHover = WidgetInput.IsMouseOver(innerX, itemY, listContentW, itemRowH);

                            if (rowHover)
                                UIRenderer.DrawRect(innerX, itemY, listContentW, itemRowH, UIColors.ItemHoverBg);
                            else if (i % 2 == 1)
                                UIRenderer.DrawRect(innerX, itemY, listContentW, itemRowH, new Color4(42, 42, 58, 255));

                            string itemLabel = $"[{item.ItemId}] {item.Name ?? "Unknown"}  x{item.Stack}";
                            itemLabel = TextUtil.Truncate(itemLabel, listContentW - 28);
                            UIRenderer.DrawTextSmall(itemLabel, innerX + 4, itemY + (itemRowH - 11) / 2, UIColors.Text);

                            // Remove button (X)
                            int xBtnX = innerX + listContentW - 20;
                            bool xHover = WidgetInput.IsMouseOver(xBtnX, itemY, 20, itemRowH);
                            UIRenderer.DrawTextSmall("X", xBtnX + 6, itemY + (itemRowH - 11) / 2,
                                xHover ? UIColors.Error : UIColors.TextDim);
                            if (xHover && WidgetInput.MouseLeftClick)
                            {
                                WidgetInput.ConsumeClick();
                                OnRemoveItemFromPack?.Invoke(selectedPack.Id, i);
                                if (_packsEditingItemIdx == i) _packsEditingItemIdx = -1;
                                else if (_packsEditingItemIdx > i) _packsEditingItemIdx--;
                            }
                            // Click row to edit (only if not clicking X)
                            else if (rowHover && WidgetInput.MouseLeftClick)
                            {
                                WidgetInput.ConsumeClick();
                                _packsEditingItemIdx = i;
                                _packsAddingNewItem = false;
                                _packsItemId.Text = item.ItemId.ToString();
                                _packsItemName.Text = item.Name ?? "";
                                _packsItemCount.Text = item.Stack.ToString();
                                _packsLookupResults.Clear();
            
                                _packsSaveError = "";
                            }
                        }
                        contentCursor += itemRowH;
                    }
                }

                // New item entry row
                if (_packsAddingNewItem)
                {
                    int sepY = listY + contentCursor - _packsRightScroll;
                    if (sepY > listY && sepY < listY + listH)
                        UIRenderer.DrawRect(innerX, sepY, listContentW, 1, UIColors.Divider);
                    contentCursor += 4;

                    contentCursor = DrawItemEditFields(innerX, listY, listH, listContentW,
                        contentCursor, selectedPack, -1);
                }

                // Name lookup results (shown below edit/add fields)
                if ((_packsAddingNewItem || _packsEditingItemIdx >= 0) && _packsLookupResults.Count > 0)
                {
                    int lblY = listY + contentCursor - _packsRightScroll;
                    if (lblY + 16 > listY && lblY < listY + listH)
                        UIRenderer.DrawTextSmall("Matches found — click to select:", innerX + 2, lblY + 2, UIColors.TextHint);
                    contentCursor += 18;

                    foreach (var entry in _packsLookupResults)
                    {
                        int rY = listY + contentCursor - _packsRightScroll;
                        if (rY + 20 > listY && rY < listY + listH)
                        {
                            bool rHover = WidgetInput.IsMouseOver(innerX, rY, listContentW, 20);
                            if (rHover)
                                UIRenderer.DrawRect(innerX, rY, listContentW, 20, UIColors.ItemHoverBg);
                            UIRenderer.DrawTextSmall($"  [{entry.Id}] {entry.Name}", innerX + 4,
                                rY + (20 - 11) / 2, rHover ? UIColors.Success : UIColors.TextDim);
                            if (rHover && WidgetInput.MouseLeftClick)
                            {
                                WidgetInput.ConsumeClick();
                                _packsItemId.Text = entry.Id.ToString();
                                _packsItemName.Text = entry.Name;
                                _packsLookupResults.Clear();
                            }
                        }
                        contentCursor += 20;
                    }
                }

                if (selectedPack.Items.Count == 0 && !_packsAddingNewItem && _packsEditingItemIdx < 0)
                {
                    int emptyY = listY + contentCursor - _packsRightScroll;
                    if (emptyY + 18 > listY && emptyY < listY + listH)
                        UIRenderer.DrawTextSmall("No items. Click 'Add Item' to add.", innerX + 4,
                            emptyY + 2, UIColors.TextHint);
                    contentCursor += 20;
                }
            }
            else
            {
                int msgY = listY + 20;
                UIRenderer.DrawTextSmall("Select a pack from the tree on the left.", innerX + 4,
                    msgY, UIColors.TextHint);
                contentCursor = 40;
            }

            _packsRightContentH = contentCursor;

            DrawSubScrollbar(px + pw - scrollbarW - 2, listY, scrollbarW, listH,
                _packsRightContentH, listH,
                ref _packsRightScroll, ref _packsRightDrag, ref _packsRightDragY, ref _packsRightDragOff);

            if (WidgetInput.IsMouseOver(px, listY, pw, listH))
            {
                int scroll = WidgetInput.ScrollWheel;
                if (scroll != 0)
                {
                    WidgetInput.ConsumeScroll();
                    _packsRightScroll += scroll > 0 ? -30 : 30;
                    int maxScroll = Math.Max(0, _packsRightContentH - listH);
                    _packsRightScroll = Math.Max(0, Math.Min(_packsRightScroll, maxScroll));
                }
            }

            // Redraw pinned top over scroll bleed
            UIRenderer.DrawRect(px + 1, py + 1, pw - 2, pinnedTopBottom - py - 1, UIColors.InputBg);

            // Re-draw title
            if (_packsEditingName && titleEditable)
            {
                int saveW = 42;
                int inputW = innerW - saveW - btnGap;
                _packsNameInput.Draw(innerX, topY, inputW, titleH);
                int saveBtnX = innerX + inputW + btnGap;
                bool saveHover = WidgetInput.IsMouseOver(saveBtnX, topY, saveW, titleH);
                UIRenderer.DrawRect(saveBtnX, topY, saveW, titleH,
                    saveHover ? new Color4(40, 120, 40, 255) : new Color4(30, 90, 30, 255));
                UIRenderer.DrawTextSmall("Save", saveBtnX + 10, topY + (titleH - 11) / 2, UIColors.Text);
            }
            else
            {
                UIRenderer.DrawText(title, innerX + 2, topY + 2, UIColors.AccentText);
            }
            // Re-draw category
            if (_packsEditingCategory && catEditable)
            {
                int catSaveW = 42;
                int catInputW = innerW - catSaveW - btnGap;
                _packsCategoryInput.Draw(innerX, titleBottom, catInputW, catH);
                int catSaveX = innerX + catInputW + btnGap;
                bool catSaveHover = WidgetInput.IsMouseOver(catSaveX, titleBottom, catSaveW, catH);
                UIRenderer.DrawRect(catSaveX, titleBottom, catSaveW, catH,
                    catSaveHover ? new Color4(40, 120, 40, 255) : new Color4(30, 90, 30, 255));
                UIRenderer.DrawTextSmall("Save", catSaveX + 10, titleBottom + (catH - 11) / 2, UIColors.Text);
            }
            else if (selectedPack != null)
            {
                UIRenderer.DrawTextSmall(selectedPack.Category, innerX + 2, titleBottom + 2, UIColors.TextDim);
            }

            // Re-draw buttons
            spawnHover = WidgetInput.IsMouseOver(btn1X, row1Y, quarterW, btnH);
            UIRenderer.DrawRect(btn1X, row1Y, quarterW, btnH,
                spawnHover ? new Color4(40, 120, 40, 255) : new Color4(30, 90, 30, 255));
            UIRenderer.DrawTextSmall("Spawn Items", btn1X + 4, row1Y + (btnH - 11) / 2, UIColors.Text);

            delHover = WidgetInput.IsMouseOver(btn2X, row1Y, quarterW, btnH);
            UIRenderer.DrawRect(btn2X, row1Y, quarterW, btnH,
                delHover ? new Color4(160, 40, 40, 255) : new Color4(120, 30, 30, 255));
            UIRenderer.DrawTextSmall(btn2Label, btn2X + 4, row1Y + (btnH - 11) / 2, UIColors.Text);

            addHover = WidgetInput.IsMouseOver(btn3X, row1Y, quarterW, btnH);
            UIRenderer.DrawRect(btn3X, row1Y, quarterW, btnH,
                addHover ? new Color4(40, 90, 160, 255) : new Color4(30, 70, 130, 255));
            UIRenderer.DrawTextSmall("Add Item", btn3X + 4, row1Y + (btnH - 11) / 2, UIColors.Text);

            expBtnHover = WidgetInput.IsMouseOver(btn4X, row1Y, btn4W, btnH);
            UIRenderer.DrawRect(btn4X, row1Y, btn4W, btnH,
                expBtnHover ? new Color4(160, 120, 30, 255) : new Color4(130, 100, 20, 255));
            UIRenderer.DrawTextSmall("Export Pack", btn4X + 4, row1Y + (btnH - 11) / 2, UIColors.Text);

            UIRenderer.DrawRect(innerX, pinnedTopBottom - 2, innerW, 1, UIColors.Divider);
        }

        /// <summary>
        /// Draws the inline edit fields for an item (used for both editing existing and adding new).
        /// itemIndex = -1 means adding a new item; >= 0 means editing that index.
        /// Returns the updated contentCursor.
        ///
        /// Layout:
        ///   Row 1: "ITEM ID"          "ITEM NAME"                    "ITEM COUNT"
        ///   Row 2: [ID input  x]      [Name input ............. x]   [Count input x]
        ///   Row 3: [LOOKUP] [SAVE] [CANCEL]                          error text
        /// </summary>
        private int DrawItemEditFields(int innerX, int listY, int listH, int listContentW,
            int contentCursor, ItemPack pack, int itemIndex)
        {
            const int fieldH = 24;
            const int labelH = 16;
            const int rowGap = 4;
            const int fieldGap = 4;
            const int btnW = 58;
            const int btnH = 24;
            const int btnGap = 4;
            const int idW = 80;
            const int countW = 80;
            bool isEdit = itemIndex >= 0;

            int nameW = listContentW - idW - countW - fieldGap * 2;
            int nameX = innerX + idW + fieldGap;
            int countX = nameX + nameW + fieldGap;

            // ── Row 1: Labels ──
            int lbl1Y = listY + contentCursor - _packsRightScroll;
            if (lbl1Y + labelH > listY && lbl1Y < listY + listH)
            {
                UIRenderer.DrawTextSmall("ITEM ID", innerX + 2, lbl1Y + 2, UIColors.TextDim);
                UIRenderer.DrawTextSmall("ITEM NAME", nameX + 2, lbl1Y + 2, UIColors.TextDim);
                UIRenderer.DrawTextSmall("COUNT", countX + 2, lbl1Y + 2, UIColors.TextDim);
            }
            contentCursor += labelH;

            // ── Row 2: [ID] [Name] [Count] ──
            int r2Y = listY + contentCursor - _packsRightScroll;
            if (r2Y + fieldH > listY && r2Y < listY + listH)
            {
                _packsItemId.Draw(innerX, r2Y, idW, fieldH);
                _packsItemName.Draw(nameX, r2Y, nameW, fieldH);
                _packsItemCount.Draw(countX, r2Y, countW, fieldH);

                // Enter key in name field triggers lookup
                if (_packsItemName.IsFocused && InputState.IsKeyJustPressed(KeyCode.Enter))
                    DoItemNameLookup(pack, itemIndex);
            }
            contentCursor += fieldH + rowGap;

            // ── Row 3: [Lookup] [Save] [Cancel]  error text ──
            int r3Y = listY + contentCursor - _packsRightScroll;
            if (r3Y + btnH > listY && r3Y < listY + listH)
            {
                // Lookup button (blue)
                bool lookupHover = WidgetInput.IsMouseOver(innerX, r3Y, btnW, btnH);
                UIRenderer.DrawRect(innerX, r3Y, btnW, btnH,
                    lookupHover ? new Color4(40, 90, 160, 255) : new Color4(30, 70, 130, 255));
                UIRenderer.DrawTextSmall("Lookup", innerX + 8, r3Y + (btnH - 11) / 2, UIColors.Text);
                if (lookupHover && WidgetInput.MouseLeftClick)
                {
                    WidgetInput.ConsumeClick();
                    DoItemNameLookup(pack, itemIndex);
                }

                // Save button (green)
                int saveX = innerX + btnW + btnGap;
                bool saveHover = WidgetInput.IsMouseOver(saveX, r3Y, btnW, btnH);
                UIRenderer.DrawRect(saveX, r3Y, btnW, btnH,
                    saveHover ? new Color4(40, 120, 40, 255) : new Color4(30, 90, 30, 255));
                UIRenderer.DrawTextSmall("Save", saveX + 14, r3Y + (btnH - 11) / 2, UIColors.Text);
                if (saveHover && WidgetInput.MouseLeftClick)
                {
                    WidgetInput.ConsumeClick();
                    int itemId;
                    int stack;
                    if (int.TryParse(_packsItemId.Text, out itemId) && itemId > 0)
                    {
                        if (!int.TryParse(_packsItemCount.Text, out stack) || stack < 1)
                            stack = 1;
                        string name = string.IsNullOrWhiteSpace(_packsItemName.Text)
                            ? null : _packsItemName.Text.Trim();

                        // Duplicate check within this pack
                        bool isDuplicate = false;
                        for (int d = 0; d < pack.Items.Count; d++)
                        {
                            if (d == itemIndex) continue; // skip self when editing
                            if (pack.Items[d].ItemId == itemId)
                            {
                                isDuplicate = true;
                                break;
                            }
                        }

                        if (isDuplicate)
                        {
                            _packsSaveError = "Duplicate ID!";
                        }
                        else
                        {
                            _packsSaveError = "";
                            if (isEdit)
                            {
                                OnUpdateItemInPack?.Invoke(pack.Id, itemIndex, itemId, stack, name);
                                _packsEditingItemIdx = -1;
                            }
                            else
                            {
                                OnAddItemToPack?.Invoke(pack.Id, itemId, stack, name);
                                _packsItemId.Text = "";
                                _packsItemName.Text = "";
                                _packsItemCount.Text = "";
                            }
                            _packsLookupResults.Clear();
                        }
                    }
                    else
                    {
                        _packsSaveError = "Invalid ID";
                    }
                }

                // Cancel button (neutral)
                int cancelX = saveX + btnW + btnGap;
                bool cancelHover = WidgetInput.IsMouseOver(cancelX, r3Y, btnW, btnH);
                UIRenderer.DrawRect(cancelX, r3Y, btnW, btnH,
                    cancelHover ? UIColors.CloseBtnHover : UIColors.CloseBtn);
                UIRenderer.DrawTextSmall("Cancel", cancelX + 6, r3Y + (btnH - 11) / 2, UIColors.Text);
                if (cancelHover && WidgetInput.MouseLeftClick)
                {
                    WidgetInput.ConsumeClick();
                    if (isEdit) _packsEditingItemIdx = -1;
                    else _packsAddingNewItem = false;
                    _packsLookupResults.Clear();
                    _packsSaveError = "";
                }

                // Error text after buttons
                if (!string.IsNullOrEmpty(_packsSaveError))
                {
                    int errX = cancelX + btnW + 8;
                    UIRenderer.DrawTextSmall(_packsSaveError, errX, r3Y + (btnH - 11) / 2, UIColors.Error);
                }
            }
            contentCursor += btnH + rowGap;

            return contentCursor;
        }

        /// <summary>
        /// Performs item name lookup — single match auto-populates, multiple shows clickable list.
        /// </summary>
        private void DoItemNameLookup(ItemPack pack, int itemIndex)
        {
            // Ensure item catalog is built on first lookup
            OnBuildCatalog?.Invoke();

            string query = _packsItemName.Text?.Trim();
            if (string.IsNullOrEmpty(query) || query.Length < 2)
            {
                _packsSaveError = "Type 2+ chars";
                return;
            }

            _packsLookupResults = OnSearchItems?.Invoke(query, 10) ?? new List<ItemEntry>();
            _packsSaveError = "";

            if (_packsLookupResults.Count == 0)
            {
                _packsSaveError = "No matches";
            }
            else if (_packsLookupResults.Count == 1)
            {
                // Single match — auto-populate
                _packsItemId.Text = _packsLookupResults[0].Id.ToString();
                _packsItemName.Text = _packsLookupResults[0].Name;
                _packsLookupResults.Clear();
            }
            // else: multiple results shown as clickable list below
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
                case "Dyes": return 6;
                default: return 7;
            }
        }

        // ---- PACKS: Scrollbar helper (F6 menu style) ----

        private void DrawSubScrollbar(int x, int y, int w, int h,
            int contentH, int viewH,
            ref int scrollOffset, ref bool dragging, ref int dragStartY, ref int dragStartOffset)
        {
            int maxScroll = Math.Max(0, contentH - viewH);
            if (maxScroll <= 0) return;

            const int minThumb = 20;

            UIRenderer.DrawRect(x, y, w, h, UIColors.ScrollTrack);

            float viewRatio = (float)viewH / contentH;
            int thumbH = Math.Max(minThumb, (int)(h * viewRatio));
            float scrollPct = maxScroll > 0 ? (float)scrollOffset / maxScroll : 0;
            int thumbY = y + (int)((h - thumbH) * scrollPct);

            bool thumbHover = WidgetInput.IsMouseOver(x, thumbY, w, thumbH);
            UIRenderer.DrawRect(x, thumbY, w, thumbH,
                (dragging || thumbHover) ? UIColors.SliderThumbHover : UIColors.ScrollThumb);

            if (dragging)
            {
                if (WidgetInput.MouseLeft)
                {
                    int deltaY = WidgetInput.MouseY - dragStartY;
                    int trackH = h - thumbH;
                    if (trackH > 0)
                    {
                        float pct = (float)deltaY / trackH;
                        scrollOffset = dragStartOffset + (int)(pct * maxScroll);
                    }
                }
                else
                {
                    dragging = false;
                }
            }

            if (thumbHover && WidgetInput.MouseLeftClick && !dragging)
            {
                dragging = true;
                dragStartY = WidgetInput.MouseY;
                dragStartOffset = scrollOffset;
                WidgetInput.ConsumeClick();
            }

            scrollOffset = Math.Max(0, Math.Min(scrollOffset, maxScroll));
        }
    }
}
