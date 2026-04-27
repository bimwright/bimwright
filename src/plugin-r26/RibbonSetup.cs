using System.Reflection;
using System.Collections.Generic;
using Autodesk.Revit.UI;
using Bimwright.Rvt.Plugin.ToolBaker;

namespace Bimwright.Rvt.Plugin
{
    public class RibbonResult
    {
        public PushButton ToggleButton { get; set; }
        public PushButton HistoryButton { get; set; }
        public PushButton StatusButton { get; set; }
        public PushButton BakeInboxButton { get; set; }
    }

    public static class RibbonSetup
    {
        private const string PanelName = "BIMwright";
        private static readonly HashSet<string> CreatedButtons = new HashSet<string>();

        public static RibbonResult Create(UIControlledApplication application, BimwrightConfig config = null, BakedToolRuntimeCache runtimeCache = null)
        {
            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            var panel = ResolvePanel(application);

            var toggleData = new PushButtonData(
                "ToggleMcp", "MCP: ON",
                assemblyPath,
                "Bimwright.Rvt.Plugin.Commands.ToggleMcpCommand")
            {
                LargeImage = IconGenerator.McpOn32,
                Image = IconGenerator.McpOn16,
                ToolTip = "Start/Stop MCP Server"
            };

            var historyData = new PushButtonData(
                "ShowHistory", "History (0)",
                assemblyPath,
                "Bimwright.Rvt.Plugin.Commands.ShowHistoryCommand")
            {
                LargeImage = IconGenerator.History32,
                Image = IconGenerator.History16,
                ToolTip = "Show MCP command history"
            };

            var statusData = new PushButtonData(
                "ShowStatus", "Status",
                assemblyPath,
                "Bimwright.Rvt.Plugin.Commands.ShowStatusCommand")
            {
                LargeImage = IconGenerator.Info32,
                Image = IconGenerator.Info16,
                ToolTip = "Show MCP status"
            };

            var stack = panel.AddStackedItems(toggleData, historyData, statusData);
            PushButton bakeInboxButton = null;
            if (config?.EnableAdaptiveBakeOrDefault == true)
                bakeInboxButton = AddBakeInboxButton(panel, assemblyPath);

            if (config?.EnableAdaptiveBakeOrDefault == true)
                AddOrUpdateBakedToolButtons(application, runtimeCache);

            return new RibbonResult
            {
                ToggleButton = stack[0] as PushButton,
                HistoryButton = stack[1] as PushButton,
                StatusButton = stack[2] as PushButton,
                BakeInboxButton = bakeInboxButton
            };
        }

        public static void AddOrUpdateBakedToolButtons(UIControlledApplication application, BakedToolRuntimeCache runtimeCache)
        {
            if (application == null || runtimeCache == null)
                return;

            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            var panel = ResolvePanel(application);
            foreach (var entry in runtimeCache.GetRibbonEntries())
            {
                if (entry.RibbonSlot <= 0 || entry.RibbonSlot > BakedToolRuntimeCache.MaxRibbonSlots)
                    continue;

                var buttonName = "BakedToolSlot" + entry.RibbonSlot.ToString("00");
                if (CreatedButtons.Contains(buttonName))
                    continue;

                var data = new PushButtonData(
                    buttonName,
                    ShortLabel(entry.DisplayName),
                    assemblyPath,
                    "Bimwright.Rvt.Plugin.Commands.RunBakedRibbonSlot" + entry.RibbonSlot.ToString("00") + "Command")
                {
                    LargeImage = IconGenerator.Info32,
                    Image = IconGenerator.Info16,
                    ToolTip = entry.Description
                };

                try
                {
                    panel.AddItem(data);
                    CreatedButtons.Add(buttonName);
                }
                catch
                {
                    CreatedButtons.Add(buttonName);
                }
            }
        }

        private static PushButton AddBakeInboxButton(RibbonPanel panel, string assemblyPath)
        {
            const string buttonName = "ShowBakeInbox";
            if (CreatedButtons.Contains(buttonName))
                return null;

            var data = new PushButtonData(
                buttonName,
                "Bake Inbox",
                assemblyPath,
                "Bimwright.Rvt.Plugin.Commands.ShowBakeInboxCommand")
            {
                LargeImage = IconGenerator.Info32,
                Image = IconGenerator.Info16,
                ToolTip = "Show accepted baked tools"
            };

            try
            {
                var button = panel.AddItem(data) as PushButton;
                CreatedButtons.Add(buttonName);
                return button;
            }
            catch
            {
                CreatedButtons.Add(buttonName);
                return null;
            }
        }

        private static string ShortLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return "Baked Tool";
            return label.Length <= 18 ? label : label.Substring(0, 18);
        }

        private static RibbonPanel ResolvePanel(UIControlledApplication application)
        {
            foreach (var panel in application.GetRibbonPanels(Tab.AddIns))
            {
                if (panel.Name == PanelName) return panel;
            }

            return application.CreateRibbonPanel(Tab.AddIns, PanelName);
        }
    }
}
