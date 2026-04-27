using System.Linq;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Rvt.Plugin.Handlers
{
    public class ListBakedToolsHandler : IRevitCommand
    {
        public string Name => "list_baked_tools";
        public string Description => "List all baked (user-compiled) tools with usage stats";
        public string ParametersSchema => "{}";

        public CommandResult Execute(UIApplication app, string paramsJson)
        {
            var registry = App.Instance?.BakedToolRegistry;
            if (registry == null)
                return CommandResult.Ok(new { tools = new object[0] });

            var tools = registry.GetAllSortedForList().Select(m => new
            {
                name = m.Name,
                description = m.Description,
                source = m.Source,
                params_schema = ParseObject(m.ParametersSchema),
                usage_count = m.UsageCount,
                usage_score_30d = m.UsageScore30d,
                last_used = m.LastUsedAt,
                compat_map = ParseObject(m.CompatMap),
                failure_rate = m.FailureRate,
                lifecycle_state = m.LifecycleState,
                created_utc = m.CreatedUtc
            }).ToArray();

            return CommandResult.Ok(new { count = tools.Length, tools });
        }

        private static JObject ParseObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new JObject();
            try
            {
                return JObject.Parse(json);
            }
            catch (JsonException)
            {
                return new JObject();
            }
        }
    }
}
