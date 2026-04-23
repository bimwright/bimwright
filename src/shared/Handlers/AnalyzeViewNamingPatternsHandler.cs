using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Bimwright.Rvt.Plugin.Lint;

namespace Bimwright.Rvt.Plugin.Handlers
{
    public class AnalyzeViewNamingPatternsHandler : IRevitCommand
    {
        public string Name => "analyze_view_naming_patterns";
        public string Description => "Infer dominant view-naming pattern from project. Returns patterns with coverage + outliers.";
        public string ParametersSchema => @"{""type"":""object"",""properties"":{}}";

        public CommandResult Execute(UIApplication app, string paramsJson)
        {
            var doc = app.ActiveUIDocument?.Document;
            if (doc == null) return CommandResult.Fail("No document is open.");

            var views = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && v.CanBePrinted)
                .ToArray();

            // GroupBy to safely handle duplicate view names across different view types
            var nameToView = views
                .GroupBy(v => v.Name)
                .ToDictionary(g => g.Key, g => g.First(), System.StringComparer.Ordinal);

            var analysis = ViewNamingAnalyzer.Analyze(nameToView.Keys);

            // Fill outlier IDs from the Revit view lookup
            var enrichedOutliers = analysis.Outliers.Select(o => new
            {
                id = nameToView.TryGetValue(o.Name, out var v) ? RevitCompat.GetId(v.Id) : 0L,
                name = o.Name,
                closest_pattern = o.ClosestPattern
            }).ToArray();

            return CommandResult.Ok(new
            {
                total_views = analysis.TotalViews,
                patterns = analysis.Patterns.Select(p => new
                {
                    pattern = p.Pattern,
                    examples = p.Examples,
                    count = p.Count,
                    coverage = p.Coverage
                }).ToArray(),
                dominant = analysis.Dominant,
                outliers = enrichedOutliers
            });
        }
    }
}
