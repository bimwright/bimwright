using System;
using Bimwright.Rvt.Server.Bake;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Rvt.Server.Handlers
{
    public static class DismissBakeSuggestionHandler
    {
        public static string Handle(
            BakeDb db,
            string id,
            string action,
            DateTimeOffset? now = null,
            string currentRevitVersion = null,
            ToolBakerAuditLog auditLog = null)
        {
            if (db == null)
                throw new ArgumentNullException(nameof(db));

            var suggestion = db.GetSuggestion(id);
            if (suggestion == null)
                return Failure("not_found", "Bake suggestion was not found.");

            var clock = now ?? DateTimeOffset.UtcNow;
            switch (action)
            {
                case "snooze_30d":
                    suggestion.State = BakeSuggestionStates.Snoozed;
                    suggestion.SnoozeUntil = clock.AddDays(30).ToString("o");
                    suggestion.NeverReason = null;
                    break;
                case "never":
                    suggestion.State = BakeSuggestionStates.Never;
                    suggestion.SnoozeUntil = null;
                    suggestion.NeverReason = "user";
                    break;
                case "never_with_gap_signal":
                    if (!string.Equals(suggestion.Source, "send_code", StringComparison.OrdinalIgnoreCase))
                    {
                        return Failure(
                            "gap_signal_requires_send_code",
                            "Gap signal export is only available for send_code suggestions.");
                    }

                    var issueUrl = CreateBakeIssueDraftHandler.Handle(suggestion, currentRevitVersion);
                    suggestion.State = BakeSuggestionStates.Never;
                    suggestion.SnoozeUntil = null;
                    suggestion.NeverReason = "gap_signal";
                    suggestion.UpdatedAt = clock.ToString("o");
                    db.UpsertSuggestion(suggestion);
                    auditLog?.AppendGapIssueDraftCreated(suggestion.Id, clock);
                    return issueUrl;
                default:
                    return Failure("invalid_action", "Dismiss action must be snooze_30d, never, or never_with_gap_signal.");
            }

            suggestion.UpdatedAt = clock.ToString("o");
            db.UpsertSuggestion(suggestion);
            return new JObject
            {
                ["ok"] = true,
                ["id"] = suggestion.Id,
                ["state"] = suggestion.State
            }.ToString(Formatting.None);
        }

        private static string Failure(string code, string message)
        {
            return new JObject
            {
                ["ok"] = false,
                ["error_code"] = code,
                ["message"] = message
            }.ToString(Formatting.None);
        }
    }
}
