using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Rvt.Server.Bake
{
    public sealed class ClusterEngine
    {
        private const int SendCodeThreshold = 9;
        private const int SendCodeWindowDays = 10;
        private const int PresetThreshold = 15;
        private const int MacroThreshold = 15;
        private const int PresetMacroWindowDays = 21;
        private const double StabilityThreshold = 0.80;

        public IReadOnlyList<ClusterCandidate> Analyze(IEnumerable<UsageEvent> events, DateTimeOffset? now = null)
        {
            var snapshot = (events ?? Enumerable.Empty<UsageEvent>())
                .Where(e => e != null && !string.IsNullOrWhiteSpace(e.Source) && !string.IsNullOrWhiteSpace(e.NormalizedKey))
                .ToArray();

            var clock = now ?? DateTimeOffset.UtcNow;
            var candidates = new List<ClusterCandidate>();
            candidates.AddRange(AnalyzeSendCode(snapshot, clock));
            candidates.AddRange(AnalyzePreset(snapshot, clock));
            candidates.AddRange(AnalyzeMacro(snapshot, clock));
            return candidates.OrderBy(c => c.Source).ThenBy(c => c.NormalizedKey, StringComparer.Ordinal).ToArray();
        }

        private static IEnumerable<ClusterCandidate> AnalyzeSendCode(IEnumerable<UsageEvent> events, DateTimeOffset now)
        {
            var cutoff = now.AddDays(-SendCodeWindowDays);
            var window = events
                .Where(e => string.Equals(e.Source, "send_code", StringComparison.OrdinalIgnoreCase))
                .Where(e => e.Success && e.TsUtc >= cutoff && e.TsUtc <= now);

            foreach (var group in window.GroupBy(e => e.NormalizedKey))
            {
                var items = group.ToArray();
                if (items.Length < SendCodeThreshold)
                    continue;
                if (items.Any(e => !BodyCacheEnabled(e)))
                    continue;

                var distinctHashes = items
                    .Select(e => e.BodyHash)
                    .Where(h => !string.IsNullOrWhiteSpace(h))
                    .Distinct(StringComparer.Ordinal)
                    .Count();
                if (distinctHashes < 3 && !items.Any(UserAcceptedExactSource))
                    continue;

                yield return Candidate("send_code", group.Key, items);
            }
        }

        private static IEnumerable<ClusterCandidate> AnalyzePreset(IEnumerable<UsageEvent> events, DateTimeOffset now)
        {
            var cutoff = now.AddDays(-PresetMacroWindowDays);
            var window = events
                .Where(e => string.Equals(e.Source, "preset", StringComparison.OrdinalIgnoreCase))
                .Where(e => e.Success && e.TsUtc >= cutoff && e.TsUtc <= now);

            foreach (var group in window.GroupBy(e => e.NormalizedKey))
            {
                var items = group.ToArray();
                if (items.Length < PresetThreshold)
                    continue;
                if (SignatureStability(items) < StabilityThreshold)
                    continue;

                yield return Candidate("preset", group.Key, items);
            }
        }

        private static IEnumerable<ClusterCandidate> AnalyzeMacro(IEnumerable<UsageEvent> events, DateTimeOffset now)
        {
            var cutoff = now.AddDays(-PresetMacroWindowDays);
            var window = events
                .Where(e => string.Equals(e.Source, "macro", StringComparison.OrdinalIgnoreCase))
                .Where(e => e.TsUtc >= cutoff && e.TsUtc <= now);

            foreach (var group in window.GroupBy(e => e.NormalizedKey))
            {
                var items = group.ToArray();
                if (items.Length < MacroThreshold)
                    continue;

                var successRate = items.Count(e => e.Success) / (double)items.Length;
                if (successRate < StabilityThreshold)
                    continue;

                yield return Candidate("macro", group.Key, items);
            }
        }

        private static ClusterCandidate Candidate(string source, string normalizedKey, IReadOnlyList<UsageEvent> items)
        {
            return new ClusterCandidate
            {
                Source = source,
                NormalizedKey = normalizedKey,
                Tool = items[0].Tool,
                Count = items.Count,
                FirstSeenUtc = items.Min(e => e.TsUtc),
                LastSeenUtc = items.Max(e => e.TsUtc),
                PayloadJson = BuildCandidatePayload(items)
            };
        }

        private static string BuildCandidatePayload(IReadOnlyList<UsageEvent> items)
        {
            var payload = new JObject
            {
                ["event_ids"] = new JArray(items.Select(e => e.Id)),
                ["sample"] = SafePayload(items[0])
            };
            return payload.ToString(Formatting.None);
        }

        private static JObject SafePayload(UsageEvent usageEvent)
        {
            if (usageEvent == null || string.IsNullOrWhiteSpace(usageEvent.PayloadJson))
                return new JObject();

            try
            {
                return JObject.Parse(usageEvent.PayloadJson);
            }
            catch (JsonException)
            {
                return new JObject();
            }
        }

        private static bool BodyCacheEnabled(UsageEvent usageEvent)
        {
            return SafePayload(usageEvent).Value<bool?>("body_cache_enabled") == true;
        }

        private static bool UserAcceptedExactSource(UsageEvent usageEvent)
        {
            return SafePayload(usageEvent).Value<bool?>("user_accepted_exact_source") == true;
        }

        private static double SignatureStability(IReadOnlyList<UsageEvent> items)
        {
            if (items.Count == 0)
                return 0;

            var mostCommon = items
                .GroupBy(ParameterKindSignature)
                .Select(g => g.Count())
                .DefaultIfEmpty(0)
                .Max();

            return mostCommon / (double)items.Count;
        }

        private static string ParameterKindSignature(UsageEvent usageEvent)
        {
            var kinds = SafePayload(usageEvent)["parameter_kinds"] as JObject;
            if (kinds == null)
                return string.Empty;

            return string.Join(
                "|",
                kinds.Properties()
                    .OrderBy(p => p.Name, StringComparer.Ordinal)
                    .Select(p => p.Name + ":" + (p.Value.Type == JTokenType.String ? p.Value.Value<string>() : p.Value.ToString(Formatting.None))));
        }
    }

    public sealed class ClusterCandidate
    {
        public string Source { get; set; }
        public string NormalizedKey { get; set; }
        public string Tool { get; set; }
        public int Count { get; set; }
        public DateTimeOffset FirstSeenUtc { get; set; }
        public DateTimeOffset LastSeenUtc { get; set; }
        public string PayloadJson { get; set; }
    }
}
