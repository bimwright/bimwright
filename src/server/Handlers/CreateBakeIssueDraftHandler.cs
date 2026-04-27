using System;
using Bimwright.Rvt.Server.Bake;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Rvt.Server.Handlers
{
    public static class CreateBakeIssueDraftHandler
    {
        private const string IssueBaseUrl = "https://github.com/bimwright/rvt-mcp/issues/new";

        public static string Handle(BakeSuggestionRecord suggestion, string currentRevitVersion = null)
        {
            if (suggestion == null)
                throw new ArgumentNullException(nameof(suggestion));
            if (!string.Equals(suggestion.Source, "send_code", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Gap issue drafts are only supported for send_code suggestions.", nameof(suggestion));

            var payload = ParsePayload(suggestion.PayloadJson);
            var title = "MCP gap signal: recurring send_code_to_revit pattern";
            var body = BuildBody(payload, currentRevitVersion);
            return IssueBaseUrl +
                "?title=" + Uri.EscapeDataString(title) +
                "&body=" + Uri.EscapeDataString(body) +
                "&labels=mcp-gap";
        }

        private static string BuildBody(JObject payload, string currentRevitVersion)
        {
            var frequency = FormatFrequency(payload);
            var revitVersion = SanitizeRevitVersion(currentRevitVersion);
            var summary =
                "A recurring send_code_to_revit workflow was dismissed as Never. " +
                "This draft intentionally excludes raw code bodies, file paths, URLs, project names, and tokens.";

            return string.Join("\n", new[]
            {
                "**MCP gap signal - auto-generated, please review before submitting**",
                "",
                "- Source: `send_code_to_revit` cluster, blacklisted by user",
                "- Frequency: " + frequency,
                "- Current Revit version: " + revitVersion,
                "",
                "**Redacted summary:**",
                summary,
                "",
                "**Why this is a gap:**",
                "User blacklisted this recurring pattern, meaning the agent kept falling back",
                "to `send_code_to_revit` instead of a native tool. A native handler",
                "may be worth shipping.",
                "",
                "---",
                "Submitted via rvt-mcp Suggestion Inbox v0.3.x"
            });
        }

        private static string FormatFrequency(JObject payload)
        {
            var count = payload.Value<int?>("count");
            var first = ParseTimestamp(payload.Value<string>("first_seen_utc"));
            var last = ParseTimestamp(payload.Value<string>("last_seen_utc"));
            if (count.HasValue && first.HasValue && last.HasValue)
            {
                var days = Math.Max(1, (last.Value.UtcDateTime.Date - first.Value.UtcDateTime.Date).Days + 1);
                return count.Value + " over " + days + " days";
            }

            if (count.HasValue)
                return count.Value + " occurrences";

            return "unknown local frequency";
        }

        private static DateTimeOffset? ParseTimestamp(string value)
        {
            return DateTimeOffset.TryParse(value, out var timestamp) ? timestamp : (DateTimeOffset?)null;
        }

        private static string SanitizeRevitVersion(string currentRevitVersion)
        {
            if (string.IsNullOrWhiteSpace(currentRevitVersion))
                return "unknown";

            var value = currentRevitVersion.Trim().ToUpperInvariant();
            switch (value)
            {
                case "R22":
                case "R23":
                case "R24":
                case "R25":
                case "R26":
                case "R27":
                    return value;
                default:
                    return "unknown";
            }
        }

        private static JObject ParsePayload(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new JObject();

            try
            {
                using var reader = new JsonTextReader(new System.IO.StringReader(json))
                {
                    DateParseHandling = DateParseHandling.None
                };
                return JObject.Load(reader);
            }
            catch (JsonException)
            {
                return new JObject();
            }
        }
    }
}
