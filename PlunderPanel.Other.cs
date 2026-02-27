using TerrariaModder.Core.UI;
using TerrariaModder.Core.UI.Widgets;

namespace Plunder
{
    public partial class PlunderPanel
    {
        // ============================================================
        //  OTHER TAB
        // ============================================================

        private void DrawOtherTab(ref StackLayout layout)
        {
            if (DrawCollapsibleHeader(ref layout, "QUICK ACTIONS", "other_actions"))
            {
                if (VButton(ref layout, "Open Mod Menu (F6)", 28))
                    OnOpenModMenu?.Invoke();
            }

            layout.Space(4);

            if (DrawCollapsibleHeader(ref layout, "DEBUG", "other_debug"))
            {
                bool mapTpDebug = MapTeleport.DebugMode;
                if (VCheckboxTip(ref layout, "Map Teleport Debug", mapTpDebug,
                    "Map Teleport Debug", "Shows coordinate conversion values in chat\nwhen you right-click the map to teleport.\nUse to diagnose teleport accuracy issues."))
                    MapTeleport.ToggleDebug();
            }

            layout.Space(4);

            if (DrawCollapsibleHeader(ref layout, "INFO", "other_info"))
            {
                VLabel(ref layout, $"Plunder v{_config.ModVersion}");
                VLabel(ref layout, "Author: Fostot", UIColors.TextHint);
            }
        }
    }
}
