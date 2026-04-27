using System;
using System.Collections.Generic;
using System.Linq;
using Bimwright.Rvt.Server.Bake;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Rvt.Server.Handlers
{
    public static class ListBakeSuggestionsHandler
    {
        public static string Handle(
            BakeDb db,
            IEnumerable<ClusterCandidate> candidates = null,
            SuggestionProposer proposer = null,
            DateTimeOffset? now = null)
        {
            if (db == null)
                throw new ArgumentNullException(nameof(db));

            if (candidates != null)
            {
                proposer ??= new SuggestionProposer();
                foreach (var suggestion in proposer.Propose(candidates, db.ListSuggestions(), now))
                    db.UpsertSuggestion(suggestion);
            }

            var suggestions = db.ListSuggestions()
                .Where(s => !string.Equals(s.State, BakeSuggestionStates.Archived, StringComparison.Ordinal))
                .Select(ToResponse)
                .ToArray();

            return new JObject { ["suggestions"] = new JArray(suggestions) }.ToString(Formatting.None);
        }

        public static string Handle(
            BakeDb db,
            UsageEventLogger usageLogger,
            DateTimeOffset? now = null,
            SuggestionProposer proposer = null)
        {
            var candidates = usageLogger?.RefreshCandidates(now);
            return Handle(db, candidates, proposer, now);
        }

        private static JObject ToResponse(BakeSuggestionRecord suggestion)
        {
            var payload = ParsePayload(suggestion.PayloadJson);
            return new JObject
            {
                ["id"] = suggestion.Id,
                ["title"] = suggestion.Title,
                ["source"] = suggestion.Source,
                ["score"] = suggestion.Score,
                ["state"] = suggestion.State,
                ["output_choices"] = payload["output_choices"] ?? new JArray("mcp_only", "ribbon_plus_mcp"),
                ["created_at"] = suggestion.CreatedAt
            };
        }

        private static JObject ParsePayload(string json)
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
