using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Rvt.Plugin.ToolBaker
{
    public static class BakedToolParameterDefaults
    {
        public static string BuildDummyParamsJson(string paramsSchema)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(paramsSchema))
                    return "{}";

                var schema = JObject.Parse(paramsSchema);
                var properties = schema["properties"] as JObject;
                if (properties == null)
                    return "{}";

                var result = new JObject();
                foreach (var property in properties.Properties())
                {
                    result[property.Name] = BuildDefaultValue(property.Value as JObject);
                }

                return result.ToString(Formatting.None);
            }
            catch (JsonException)
            {
                return "{}";
            }
            catch (InvalidOperationException)
            {
                return "{}";
            }
        }

        private static JToken BuildDefaultValue(JObject schema)
        {
            if (schema == null)
                return string.Empty;

            if (schema.TryGetValue("default", out var defaultValue))
                return defaultValue.DeepClone();

            if (schema["enum"] is JArray enumValues && enumValues.Count > 0)
                return enumValues[0].DeepClone();

            var type = schema.Value<string>("type");
            if (schema["type"] is JArray typeArray)
                type = typeArray.Count == 0 ? null : typeArray[0].Value<string>();

            switch (type)
            {
                case "integer":
                case "number":
                    return 0;
                case "boolean":
                    return false;
                case "array":
                    return new JArray();
                case "object":
                    return new JObject();
                case "string":
                default:
                    return string.Empty;
            }
        }
    }
}
