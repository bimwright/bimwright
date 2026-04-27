using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Rvt.Server.Bake
{
    public sealed class SuggestionProposer
    {
        private const int DailyLlmNamingCap = 5;
        private readonly Func<string, string> _envLookup;
        private readonly ISuggestionNameProvider _nameProvider;

        public SuggestionProposer(Func<string, string> envLookup = null, ISuggestionNameProvider nameProvider = null)
        {
            _envLookup = envLookup ?? Environment.GetEnvironmentVariable;
            _nameProvider = nameProvider;
        }

        public IReadOnlyList<BakeSuggestionRecord> Propose(
            IEnumerable<ClusterCandidate> candidates,
            IEnumerable<BakeSuggestionRecord> existingSuggestions,
            DateTimeOffset? now = null)
        {
            var clock = now ?? DateTimeOffset.UtcNow;
            var existing = (existingSuggestions ?? Enumerable.Empty<BakeSuggestionRecord>())
                .Where(s => s != null)
                .ToArray();
            var existingKeys = new HashSet<string>(
                existing.Select(s => s.ClusterKey),
                StringComparer.Ordinal);
            var namingAttemptsToday = CountNamingAttemptsToday(existing, clock);

            var proposed = new List<BakeSuggestionRecord>();
            foreach (var candidate in candidates ?? Enumerable.Empty<ClusterCandidate>())
            {
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.NormalizedKey) || string.IsNullOrWhiteSpace(candidate.Source))
                    continue;
                if (existingKeys.Contains(candidate.NormalizedKey))
                    continue;

                var payload = BuildPayload(candidate);
                var title = SuggestTitle(candidate, payload, clock, ref namingAttemptsToday);
                var record = new BakeSuggestionRecord
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ClusterKey = candidate.NormalizedKey,
                    Source = NormalizeSource(candidate.Source),
                    Title = title,
                    Description = "Suggested repeated Revit workflow.",
                    State = BakeSuggestionStates.Open,
                    Score = Score(candidate),
                    CreatedAt = clock.ToString("o"),
                    UpdatedAt = clock.ToString("o"),
                    PayloadJson = payload.ToString(Formatting.None),
                    VersionHistoryBlob = "[]"
                };

                var accepted = FindSimilarAccepted(record, existing);
                if (accepted != null)
                {
                    record.State = BakeSuggestionStates.Superseded;
                    payload["superseded_by_suggestion_id"] = accepted.Id;
                    record.PayloadJson = payload.ToString(Formatting.None);
                    record.VersionHistoryBlob = new JArray(new JObject
                    {
                        ["event"] = "superseded_by_existing_accepted",
                        ["accepted_suggestion_id"] = accepted.Id,
                        ["at"] = clock.ToString("o")
                    }).ToString(Formatting.None);
                }

                proposed.Add(record);
                existingKeys.Add(record.ClusterKey);
            }

            return proposed;
        }

        private string SuggestTitle(ClusterCandidate candidate, JObject payload, DateTimeOffset now, ref int namingAttemptsToday)
        {
            if (string.Equals(candidate.Source, "send_code", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(_envLookup("ANTHROPIC_API_KEY")) &&
                namingAttemptsToday < DailyLlmNamingCap &&
                _nameProvider != null)
            {
                namingAttemptsToday++;
                AppendArray(payload, "naming_attempts", new JObject
                {
                    ["attempted_at"] = now.ToString("o"),
                    ["provider"] = "injected"
                });
                return _nameProvider.SuggestName(candidate) ?? DeterministicTitle(candidate);
            }

            return DeterministicTitle(candidate);
        }

        private static int CountNamingAttemptsToday(IEnumerable<BakeSuggestionRecord> existing, DateTimeOffset now)
        {
            var today = now.UtcDateTime.Date;
            var count = 0;
            foreach (var suggestion in existing)
            {
                if (!(ParseObject(suggestion.PayloadJson)?["naming_attempts"] is JArray attempts))
                    continue;

                foreach (var attempt in attempts.OfType<JObject>())
                {
                    if (DateTimeOffset.TryParse(attempt.Value<string>("attempted_at"), out var ts) &&
                        ts.UtcDateTime.Date == today)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static JObject BuildPayload(ClusterCandidate candidate)
        {
            var original = ParseObject(candidate.PayloadJson) ?? new JObject();
            return new JObject
            {
                ["tool"] = candidate.Tool ?? string.Empty,
                ["normalized_key"] = candidate.NormalizedKey,
                ["count"] = candidate.Count,
                ["first_seen_utc"] = candidate.FirstSeenUtc.ToString("o"),
                ["last_seen_utc"] = candidate.LastSeenUtc.ToString("o"),
                ["output_choices"] = new JArray("mcp_only", "ribbon_plus_mcp"),
                ["sample"] = original["sample"] ?? new JObject(),
                ["event_ids"] = original["event_ids"] ?? new JArray()
            };
        }

        private static void AppendArray(JObject payload, string name, JObject item)
        {
            if (!(payload[name] is JArray array))
            {
                array = new JArray();
                payload[name] = array;
            }
            array.Add(item);
        }

        private static BakeSuggestionRecord FindSimilarAccepted(BakeSuggestionRecord candidate, IReadOnlyList<BakeSuggestionRecord> existing)
        {
            var payload = ParseObject(candidate.PayloadJson);
            var tool = (string)payload?["tool"];

            return existing.FirstOrDefault(s =>
                string.Equals(s.State, BakeSuggestionStates.Accepted, StringComparison.Ordinal) &&
                string.Equals(s.Source, candidate.Source, StringComparison.OrdinalIgnoreCase) &&
                (SamePayloadTool(s, tool) || TokenSimilarity(s.ClusterKey, candidate.ClusterKey) >= 0.80));
        }

        private static bool SamePayloadTool(BakeSuggestionRecord suggestion, string tool)
        {
            if (string.IsNullOrWhiteSpace(tool))
                return false;
            return string.Equals((string)ParseObject(suggestion.PayloadJson)?["tool"], tool, StringComparison.Ordinal);
        }

        private static double TokenSimilarity(string left, string right)
        {
            var a = Tokens(left);
            var b = Tokens(right);
            if (a.Count == 0 || b.Count == 0)
                return 0;
            return a.Intersect(b, StringComparer.Ordinal).Count() / (double)a.Union(b, StringComparer.Ordinal).Count();
        }

        private static HashSet<string> Tokens(string value)
        {
            return new HashSet<string>(
                (value ?? string.Empty).Split(new[] { ':', ',', '>', '_', '-' }, StringSplitOptions.RemoveEmptyEntries),
                StringComparer.Ordinal);
        }

        private static double Score(ClusterCandidate candidate)
        {
            var threshold = string.Equals(candidate.Source, "send_code", StringComparison.OrdinalIgnoreCase) ? 9.0 : 15.0;
            return Math.Min(1.0, Math.Max(0.01, candidate.Count / threshold));
        }

        private static string DeterministicTitle(ClusterCandidate candidate)
        {
            if (string.Equals(candidate.Source, "send_code", StringComparison.OrdinalIgnoreCase))
                return "Condense Revit Code";
            if (string.Equals(candidate.Source, "macro", StringComparison.OrdinalIgnoreCase))
                return "Run " + Humanize(candidate.Tool ?? candidate.NormalizedKey);
            return Humanize(candidate.Tool ?? candidate.NormalizedKey);
        }

        private static string Humanize(string value)
        {
            var raw = (value ?? string.Empty).Split(':').Last().Split('>').Last();
            var words = raw.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
                return "Bake Suggested Tool";
            return string.Join(" ", words.Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant()));
        }

        private static string NormalizeSource(string source)
        {
            return string.Equals(source, "send_code", StringComparison.OrdinalIgnoreCase) ? "send_code" :
                string.Equals(source, "macro", StringComparison.OrdinalIgnoreCase) ? "macro" : "preset";
        }

        private static JObject ParseObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;
            try
            {
                using var text = new StringReader(json);
                using var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None };
                return JObject.Load(reader);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    public interface ISuggestionNameProvider
    {
        string SuggestName(ClusterCandidate candidate);
    }

    public static class BakeSuggestionStates
    {
        public const string Open = "open";
        public const string Snoozed = "snoozed";
        public const string Never = "never";
        public const string Accepted = "accepted";
        public const string Superseded = "superseded";
        public const string Archived = "archived";

        public static bool IsValid(string state)
        {
            return state == Open ||
                   state == Snoozed ||
                   state == Never ||
                   state == Accepted ||
                   state == Superseded ||
                   state == Archived;
        }
    }

    public sealed class BakeSuggestionRecord
    {
        public string Id { get; set; }
        public string ClusterKey { get; set; }
        public string Source { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string State { get; set; }
        public double Score { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public string SnoozeUntil { get; set; }
        public string NeverReason { get; set; }
        public string PayloadJson { get; set; }
        public string VersionHistoryBlob { get; set; }
    }
}
