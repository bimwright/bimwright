using System.Collections.Generic;
using Autodesk.Revit.UI;

namespace Bimwright.Rvt.Plugin.Handlers
{
    public class GetSelectedElementsHandler : IRevitCommand
    {
        public string Name => "get_selected_elements";
        public string Description => "Get currently selected elements. Elements deleted between selection and retrieval are reported in staleIds.";
        public string ParametersSchema => "{}";

        public CommandResult Execute(UIApplication app, string paramsJson)
        {
            var uidoc = app.ActiveUIDocument;
            if (uidoc == null)
                return CommandResult.Fail("No document is open.");

            var selectedIds = uidoc.Selection.GetElementIds();
            if (selectedIds.Count == 0)
                return CommandResult.Ok(new { count = 0, elements = new object[0], staleIds = new long[0] });

            var doc = uidoc.Document;
            var elements = new List<object>();
            var staleIds = new List<long>();

            foreach (var id in selectedIds)
            {
                var el = doc.GetElement(id);
                if (el == null)
                {
                    // Element was deleted between GetElementIds() and GetElement() — race with Ctrl+Z or another bot.
                    staleIds.Add(RevitCompat.GetId(id));
                    continue;
                }
                elements.Add(new
                {
                    elementId = RevitCompat.GetId(id),
                    name = el.Name,
                    category = el.Category?.Name,
                    typeName = doc.GetElement(el.GetTypeId())?.Name
                });
            }

            return CommandResult.Ok(new { count = elements.Count, elements, staleIds });
        }
    }
}
