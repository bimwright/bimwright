namespace Bimwright.Rvt.Plugin.ToolBaker
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public static class BakedToolDispatchAuthorizer
    {
        public static bool TryAuthorize(BakedToolRegistry registry, string name, bool isLoadedBakedCommand, out string error)
        {
            var meta = registry == null || string.IsNullOrWhiteSpace(name) ? null : registry.GetMeta(name);
            if (meta == null)
            {
                error = NotFoundMessage(name);
                return false;
            }

            if (string.Equals(meta.LifecycleState, "archived", System.StringComparison.Ordinal))
            {
                error = $"Baked tool '{name}' is archived. Use list_baked_tools to choose an active replacement or accept a newer suggestion.";
                return false;
            }

            if (!isLoadedBakedCommand)
            {
                error = NotLoadedMessage(name);
                return false;
            }

            error = null;
            return true;
        }

        public static bool TryValidateParameters(BakedToolMeta meta, string paramsJson, out string error)
        {
            if (meta == null)
            {
                error = "Baked tool metadata is missing.";
                return false;
            }

            var validation = SchemaValidator.Validate(meta.ParametersSchema, paramsJson);
            if (validation.IsValid)
            {
                error = null;
                return true;
            }

            error = validation.Error;
            if (!string.IsNullOrWhiteSpace(validation.Suggestion))
                error += " Suggestion: " + validation.Suggestion;
            if (!string.IsNullOrWhiteSpace(validation.Hint))
                error += " Hint: " + validation.Hint;
            return false;
        }

        public static bool TryGetCompatWarning(BakedToolMeta meta, string revitVersion, out string warning)
        {
            warning = null;
            if (meta == null || string.IsNullOrWhiteSpace(revitVersion))
                return false;

            try
            {
                var compat = string.IsNullOrWhiteSpace(meta.CompatMap)
                    ? new JObject()
                    : JObject.Parse(meta.CompatMap);
                var tested = compat["tested"] as JObject;
                var entry = tested?[revitVersion] as JObject;
                if (entry == null)
                {
                    warning = $"Baked tool '{meta.Name}' is untested in Revit {revitVersion}; attempting run and recording compatibility.";
                    return true;
                }

                if (entry.Value<bool?>("ok") == false)
                {
                    var lastError = entry.Value<string>("last_error");
                    warning = $"Baked tool '{meta.Name}' previously failed in Revit {revitVersion}" +
                        (string.IsNullOrWhiteSpace(lastError) ? "." : ": " + lastError);
                    return true;
                }
            }
            catch (JsonException)
            {
                warning = $"Baked tool '{meta.Name}' has unreadable compatibility metadata; attempting run.";
                return true;
            }

            return false;
        }

        public static string NotFoundMessage(string name) =>
            $"Baked tool '{name}' not found. Use list_baked_tools to see available tools.";

        public static string NotLoadedMessage(string name) =>
            $"Baked tool '{name}' is registered but not loaded. Restart Revit to reload baked tools.";
    }
}
