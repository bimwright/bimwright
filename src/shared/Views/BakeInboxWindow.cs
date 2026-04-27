using System.Linq;
using System.Text;
using Autodesk.Revit.UI;
using Bimwright.Rvt.Plugin.ToolBaker;

namespace Bimwright.Rvt.Plugin.Views
{
    public sealed class BakeInboxWindow
    {
        private readonly BakedToolRegistry _registry;
        private readonly BakedToolRuntimeCache _runtimeCache;

        public BakeInboxWindow(BakedToolRegistry registry, BakedToolRuntimeCache runtimeCache)
        {
            _registry = registry;
            _runtimeCache = runtimeCache;
        }

        public bool IsLoaded { get; private set; }

        public void ShowOrFocus()
        {
            IsLoaded = true;
            var dialog = new TaskDialog("Bake Inbox")
            {
                MainInstruction = "Accepted baked tools",
                MainContent = BuildContent(),
                CommonButtons = TaskDialogCommonButtons.Ok
            };
            dialog.Show();
            IsLoaded = false;
        }

        public void Close()
        {
            IsLoaded = false;
        }

        private string BuildContent()
        {
            var tools = _registry?.GetAll()?.OrderBy(t => t.Name).ToArray() ?? new BakedToolMeta[0];
            if (tools.Length == 0)
                return "No accepted baked tools are available.";

            var sb = new StringBuilder();
            foreach (var tool in tools.Take(24))
            {
                var runtime = _runtimeCache?.GetByName(tool.Name);
                var slot = runtime?.RibbonSlot > 0 ? " [Ribbon slot " + runtime.RibbonSlot + "]" : string.Empty;
                sb.AppendLine((tool.DisplayName ?? tool.Name) + slot);
                if (!string.IsNullOrWhiteSpace(tool.Description))
                    sb.AppendLine("  " + tool.Description);
            }

            if (tools.Length > 24)
                sb.AppendLine("...");

            return sb.ToString();
        }
    }
}
