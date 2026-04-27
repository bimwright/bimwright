using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Rvt.Plugin.ToolBaker
{
    public sealed class BakedToolRuntimeSpec
    {
        public string Kind { get; set; }
        public string Tool { get; set; }
        public string FixedArgsJson { get; set; } = "{}";
        public string[] Sequence { get; set; } = Array.Empty<string>();
    }

    public static class BakedToolRuntimeSource
    {
        private const string MarkerPrefix = "// BIMWRIGHT_BAKED_RUNTIME_SPEC ";

        public static string BuildPreset(string tool, JObject fixedArgs)
        {
            var payload = new JObject
            {
                ["kind"] = "preset",
                ["tool"] = tool ?? string.Empty,
                ["fixed_args"] = fixedArgs == null ? new JObject() : fixedArgs.DeepClone()
            };
            return MarkerPrefix + payload.ToString(Formatting.None);
        }

        public static string BuildMacro(string[] sequence)
        {
            var payload = new JObject
            {
                ["kind"] = "macro",
                ["sequence"] = new JArray((sequence ?? Array.Empty<string>())
                    .Where(s => !string.IsNullOrWhiteSpace(s)))
            };
            return MarkerPrefix + payload.ToString(Formatting.None);
        }

        public static bool HasMarker(string sourceCode)
        {
            return (sourceCode ?? string.Empty)
                .TrimStart()
                .StartsWith(MarkerPrefix, StringComparison.Ordinal);
        }

        public static bool IsAllowedForSuggestionSource(string source)
        {
            return string.Equals(source, "preset", StringComparison.Ordinal) ||
                   string.Equals(source, "macro", StringComparison.Ordinal);
        }

        public static bool TryParse(string sourceCode, out BakedToolRuntimeSpec spec)
        {
            spec = null;
            var text = (sourceCode ?? string.Empty).Trim();
            if (!text.StartsWith(MarkerPrefix, StringComparison.Ordinal))
                return false;

            try
            {
                var payload = JObject.Parse(text.Substring(MarkerPrefix.Length).Trim());
                var kind = payload.Value<string>("kind") ?? string.Empty;
                var fixedArgs = payload["fixed_args"] as JObject ?? new JObject();
                var sequence = (payload["sequence"] as JArray ?? new JArray())
                    .Values<string>()
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();

                if (kind != "preset" && kind != "macro")
                    return false;

                spec = new BakedToolRuntimeSpec
                {
                    Kind = kind,
                    Tool = payload.Value<string>("tool") ?? string.Empty,
                    FixedArgsJson = fixedArgs.ToString(Formatting.None),
                    Sequence = sequence
                };
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
