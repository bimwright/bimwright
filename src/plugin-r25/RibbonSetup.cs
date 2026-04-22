using System.Reflection;
using Autodesk.Revit.UI;

namespace Bimwright.Rvt.Plugin
{
    public class RibbonResult
    {
        public PushButton ToggleButton { get; set; }
        public PushButton HistoryButton { get; set; }
        public PushButton StatusButton { get; set; }
    }

    public static class RibbonSetup
    {
        private const string PanelName = "BIMwright";

        public static RibbonResult Create(UIControlledApplication application)
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

            return new RibbonResult
            {
                ToggleButton = stack[0] as PushButton,
                HistoryButton = stack[1] as PushButton,
                StatusButton = stack[2] as PushButton
            };
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

