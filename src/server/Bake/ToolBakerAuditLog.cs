using System;
using System.IO;
using Newtonsoft.Json;

namespace Bimwright.Rvt.Server.Bake
{
    public sealed class ToolBakerAuditLog
    {
        private readonly string _auditPath;
        private readonly object _lock = new object();

        public ToolBakerAuditLog(string auditPath)
        {
            if (string.IsNullOrWhiteSpace(auditPath))
                throw new ArgumentException("Audit log path is required.", nameof(auditPath));

            _auditPath = auditPath;
        }

        public void Append(string status, string name, string detail = null)
        {
            var entry = new
            {
                ts_utc = DateTime.UtcNow.ToString("o"),
                area = "toolbaker_legacy_import",
                status,
                name,
                detail
            };

            AppendEntry(entry);
        }

        public void AppendGapIssueDraftCreated(string suggestionId, DateTimeOffset? now = null)
        {
            var entry = new
            {
                @event = "gap_issue_draft_created",
                suggestion_id = suggestionId ?? string.Empty,
                ts_utc = (now ?? DateTimeOffset.UtcNow).ToString("o")
            };

            AppendEntry(entry);
        }

        private void AppendEntry(object entry)
        {
            var line = JsonConvert.SerializeObject(entry);
            lock (_lock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_auditPath) ?? ".");
                File.AppendAllText(_auditPath, line + Environment.NewLine);
            }
        }
    }
}
