using TerrariaModder.Core.UI;
using TerrariaModder.Core.UI.Widgets;

namespace Plunder
{
    public partial class PlunderPanel
    {
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

                layout.Space(4);
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
    }
}
