using System;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Rvt.Plugin
{
    public class McpCallEntry
    {
        public int Index { get; set; }
        public DateTime Timestamp { get; set; }
        public string ToolName { get; set; }
        public bool Success { get; set; }
        public long DurationMs { get; set; }
        public string ParamsJson { get; set; }
        public string ErrorMessage { get; set; }
        public string CodeSnippet { get; set; }
        // New fields for History redesign
        public string ResultJson { get; set; }
        public string Summary { get; set; }
        public string ToolDescription { get; set; }
        public int? RerunOfIndex { get; set; }
    }

    public class McpSessionLog
    {
        private int _nextIndex = 1;
        internal static Func<BimwrightConfig> ConfigLoader = () => BimwrightConfig.Load();

        public ObservableCollection<McpCallEntry> Entries { get; } = new ObservableCollection<McpCallEntry>();

        public void Add(McpCallEntry entry)
        {
            ApplyPrivacyPolicy(entry);
            entry.Index = _nextIndex++;
            if (entry.Timestamp == default)
                entry.Timestamp = DateTime.Now;
            Entries.Add(entry);
        }

        // Legacy overload — kept for backward compatibility until all callers migrate
        public void Add(string toolName, string paramsJson, bool success,
                        long durationMs, string errorMsg = null, string codeSnippet = null)
        {
            Add(new McpCallEntry
            {
                ToolName = toolName,
                ParamsJson = paramsJson,
                Success = success,
                DurationMs = durationMs,
                ErrorMessage = errorMsg,
                CodeSnippet = codeSnippet
            });
        }

        public void Clear()
        {
            Entries.Clear();
            _nextIndex = 1;
        }

        public int Count => Entries.Count;

        private static void ApplyPrivacyPolicy(McpCallEntry entry)
        {
            if (entry == null)
                return;

            var isSendCode = string.Equals(entry.ToolName, "send_code_to_revit", StringComparison.OrdinalIgnoreCase);
            entry.ErrorMessage = McpResponsePrivacy.RedactErrorForResponse(entry.ErrorMessage);
            entry.ResultJson = BakeRedactor.RedactForBake(entry.ResultJson, redactResultFields: isSendCode);

            if (!isSendCode)
                return;

            var cacheBodies = false;
            try { cacheBodies = ConfigLoader?.Invoke()?.CacheSendCodeBodiesOrDefault ?? false; }
            catch { }

            if (cacheBodies)
                return;

            var code = ExtractCodeBody(entry.ParamsJson, entry.CodeSnippet);
            var codeHash = BakeRedactor.HashBody(code);
            entry.ParamsJson = JsonConvert.SerializeObject(new
            {
                code_hash = codeHash,
                code_length = code.Length
            }, Formatting.None);
            entry.CodeSnippet = null;
            entry.Summary = $"send_code_to_revit body redacted; code_hash={codeHash}; code_length={code.Length}";
        }

        private static string ExtractCodeBody(string paramsJson, string codeSnippet)
        {
            if (!string.IsNullOrEmpty(codeSnippet))
                return codeSnippet;
            if (string.IsNullOrEmpty(paramsJson))
                return string.Empty;

            try
            {
                var obj = JObject.Parse(paramsJson);
                var code = obj["code"];
                if (code == null || code.Type == JTokenType.Null)
                    return string.Empty;
                if (code.Type == JTokenType.String)
                    return code.Value<string>() ?? string.Empty;
                return code.ToString(Formatting.None);
            }
            catch
            {
                return paramsJson;
            }
        }
    }
}
