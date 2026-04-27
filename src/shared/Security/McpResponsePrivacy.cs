using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Rvt.Plugin
{
    public static class McpResponsePrivacy
    {
        public static object RedactDataForResponse(string toolName, object data)
        {
            if (!string.Equals(toolName, "send_code_to_revit", System.StringComparison.OrdinalIgnoreCase) || data == null)
                return data;

            try
            {
                var json = JsonConvert.SerializeObject(data, Formatting.None);
                var redacted = BakeRedactor.RedactForBake(json, redactResultFields: true);
                return JToken.Parse(redacted);
            }
            catch
            {
                return new JObject
                {
                    ["result"] = BakeRedactor.RedactForBake(data.ToString(), redactResultFields: true)
                };
            }
        }

        public static string RedactErrorForResponse(string error)
        {
            if (string.IsNullOrEmpty(error))
                return error;

            return BakeRedactor.RedactForBake(ErrorSanitizer.Sanitize(error));
        }
    }
}
