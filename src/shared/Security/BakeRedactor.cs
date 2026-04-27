using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Rvt.Plugin
{
    public static class BakeRedactor
    {
        private const int MaxStandaloneFileNameWhitespaceRuns = 16;

        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

        private static readonly Regex JsonNameField = new Regex(
            @"(?i)(?<prefix>""(?<field>viewName|levelName|sheetName|familyName|projectName|name|level|number|sheetNumber|department|typeName|roomName|roomNumber|family|type|title|documentTitle|systemName|project)""\s*:\s*"")(?<value>(?:\\.|[^""\\])*)(?<suffix>"")",
            RegexOptions.Compiled,
            RegexTimeout);

        private static readonly Regex JsonResultField = new Regex(
            @"(?i)(?<prefix>""(?<field>result)""\s*:\s*"")(?<value>(?:\\.|[^""\\])*)(?<suffix>"")",
            RegexOptions.Compiled,
            RegexTimeout);

        private static readonly Regex MarkdownProjectNameLine = new Regex(
            @"(?im)^(?<prefix>(?:Project\s+)?Name\s*:\s*)(?<value>[^\r\n]+)",
            RegexOptions.Compiled,
            RegexTimeout);

        private static readonly Regex MarkdownActiveViewLine = new Regex(
            @"(?im)^(?<prefix>Active\s+View\s*:\s*)(?<value>[^\r\n]+)",
            RegexOptions.Compiled,
            RegexTimeout);

        private static readonly HashSet<string> StructuredNameFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "viewName",
            "levelName",
            "sheetName",
            "familyName",
            "projectName",
            "name",
            "level",
            "number",
            "sheetNumber",
            "department",
            "typeName",
            "roomName",
            "roomNumber",
            "family",
            "type",
            "title",
            "documentTitle",
            "systemName",
            "project"
        };

        private static readonly Regex Url = new Regex(
            @"https?://[^\s""'<>]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase,
            RegexTimeout);

        private static readonly Regex WindowsPath = new Regex(
            @"(?i)\b[A-Z]:\\(?:[^\\/:*?""<>|\r\n]+\\)*[^\\/:*?""<>|\r\n]+",
            RegexOptions.Compiled,
            RegexTimeout);

        private static readonly Regex JsonEscapedWindowsPath = new Regex(
            @"(?i)\b[A-Z]:(?:\\\\[^\\/:*?""<>|\r\n]+)+",
            RegexOptions.Compiled,
            RegexTimeout);

        private static readonly Regex UncPath = new Regex(
            @"(?i)\\\\[^\\/:*?""<>|\r\n]+(?:\\[^\\/:*?""<>|\r\n]+)+",
            RegexOptions.Compiled,
            RegexTimeout);

        private static readonly Regex JsonEscapedUncPath = new Regex(
            @"(?i)\\\\\\\\[^\\/:*?""<>|\r\n]+(?:\\\\[^\\/:*?""<>|\r\n]+)+",
            RegexOptions.Compiled,
            RegexTimeout);

        private static readonly Regex QuotedProjectFile = new Regex(
            @"(?i)(?<quote>[""'])(?<name>[^""'\\/:*?<>|\r\n]+\.rvt)(\k<quote>)",
            RegexOptions.Compiled,
            RegexTimeout);

        private static readonly Regex QuotedFamilyFile = new Regex(
            @"(?i)(?<quote>[""'])(?<name>[^""'\\/:*?<>|\r\n]+\.rfa)(\k<quote>)",
            RegexOptions.Compiled,
            RegexTimeout);

        private static readonly HashSet<string> LeadingFileStopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "and",
            "command",
            "file",
            "from",
            "in",
            "is",
            "load",
            "loaded",
            "model",
            "open",
            "opened",
            "opens",
            "opening",
            "please",
            "the",
            "to",
            "use",
            "using",
            "was",
            "with"
        };

        private static readonly HashSet<string> LeadingCommandContextWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "can",
            "command",
            "could",
            "please",
            "the",
            "would",
            "you"
        };

        private static readonly HashSet<string> LeadingCommandVerbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "load",
            "loaded",
            "open",
            "opened",
            "opens",
            "opening",
            "use",
            "using"
        };

        public static string RedactForBake(string input, bool redactResultFields = false)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var result = RedactStructuredJson(input, redactResultFields);
            result = SecretMasker.Mask(result);
            result = RedactJsonNameFields(result, redactResultFields);
            result = Url.Replace(result, "<url>");
            result = JsonEscapedUncPath.Replace(result, RedactWindowsPath);
            result = JsonEscapedWindowsPath.Replace(result, RedactWindowsPath);
            result = UncPath.Replace(result, RedactWindowsPath);
            result = WindowsPath.Replace(result, RedactWindowsPath);
            result = QuotedProjectFile.Replace(result, m => m.Groups["quote"].Value + "<project_file>" + m.Groups["quote"].Value);
            result = QuotedFamilyFile.Replace(result, m => m.Groups["quote"].Value + "<family_file>" + m.Groups["quote"].Value);
            result = RedactStandaloneRevitFiles(result, ".rvt", "<project_file>");
            result = RedactStandaloneRevitFiles(result, ".rfa", "<family_file>");
            return result;
        }

        public static string HashBody(string body)
        {
            body = body ?? string.Empty;
            using (var sha1 = SHA1.Create())
            {
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(body));
                return ToLowerHex(hash);
            }
        }

        private static string RedactJsonNameFields(string input, bool redactResultFields)
        {
            var replacements = CreateReplacementMap();

            var result = JsonNameField.Replace(input, match =>
            {
                var field = match.Groups["field"].Value;
                var value = match.Groups["value"].Value;
                var stem = GetTokenStem(field);

                return match.Groups["prefix"].Value + RedactSensitiveValue(stem, value, replacements) + match.Groups["suffix"].Value;
            });

            if (!redactResultFields)
                return result;

            return JsonResultField.Replace(result, match =>
            {
                var field = match.Groups["field"].Value;
                var value = match.Groups["value"].Value;
                var stem = GetTokenStem(field);

                return match.Groups["prefix"].Value + RedactSensitiveValue(stem, value, replacements) + match.Groups["suffix"].Value;
            });
        }

        private static string RedactStructuredJson(string input, bool redactResultFields)
        {
            var first = 0;
            while (first < input.Length && char.IsWhiteSpace(input[first]))
                first++;
            if (first >= input.Length || (input[first] != '{' && input[first] != '['))
                return input;

            try
            {
                var token = JToken.Parse(input);
                RedactStructuredToken(token, CreateReplacementMap(), redactResultFields);
                return token.ToString(Formatting.None);
            }
            catch (JsonException)
            {
                return input;
            }
        }

        private static void RedactStructuredToken(JToken token, Dictionary<string, Dictionary<string, string>> replacements, bool redactResultFields)
        {
            if (token == null)
                return;

            if (token.Type == JTokenType.Object)
            {
                foreach (var property in ((JObject)token).Properties())
                {
                    if (property.Value.Type == JTokenType.String)
                    {
                        var value = property.Value.Value<string>();
                        if (string.Equals(property.Name, "markdown", StringComparison.OrdinalIgnoreCase))
                            property.Value = RedactMarkdownValue(value, replacements);
                        else if (StructuredNameFields.Contains(property.Name) ||
                                 (redactResultFields && string.Equals(property.Name, "result", StringComparison.OrdinalIgnoreCase)))
                            property.Value = RedactSensitiveValue(GetTokenStem(property.Name), value, replacements);
                    }
                    else
                    {
                        RedactStructuredToken(property.Value, replacements, redactResultFields);
                    }
                }
                return;
            }

            if (token.Type == JTokenType.Array)
            {
                foreach (var child in token.Children())
                    RedactStructuredToken(child, replacements, redactResultFields);
            }
        }

        private static string RedactMarkdownValue(string value, Dictionary<string, Dictionary<string, string>> replacements)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            var result = MarkdownProjectNameLine.Replace(value, match =>
                match.Groups["prefix"].Value + RedactSensitiveValue("project_name", match.Groups["value"].Value.Trim(), replacements));

            result = MarkdownActiveViewLine.Replace(result, match =>
                match.Groups["prefix"].Value + RedactSensitiveValue("view_name", match.Groups["value"].Value.Trim(), replacements));

            return RedactMarkdownMepSystemBullets(result, replacements);
        }

        private static string RedactMarkdownMepSystemBullets(string value, Dictionary<string, Dictionary<string, string>> replacements)
        {
            var lines = value.Replace("\r\n", "\n").Split('\n');
            var inMepSystems = false;

            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();
                if (trimmed.StartsWith("## ", StringComparison.Ordinal))
                {
                    inMepSystems = string.Equals(trimmed, "## MEP Systems", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (!inMepSystems || !trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("- ...", StringComparison.Ordinal))
                    continue;

                var bulletIndex = lines[i].IndexOf("- ", StringComparison.Ordinal);
                if (bulletIndex < 0)
                    continue;

                var prefix = lines[i].Substring(0, bulletIndex + 2);
                var systemName = lines[i].Substring(bulletIndex + 2).Trim();
                lines[i] = prefix + RedactSensitiveValue("system_name", systemName, replacements);
            }

            return string.Join("\n", lines);
        }

        private static Dictionary<string, Dictionary<string, string>> CreateReplacementMap()
        {
            return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        }

        private static string RedactSensitiveValue(string stem, string value, Dictionary<string, Dictionary<string, string>> replacements)
        {
            if (string.IsNullOrEmpty(value) || IsPlaceholder(value))
                return value;
            if (value.IndexOf(".rvt", StringComparison.OrdinalIgnoreCase) >= 0)
                return "<project_file>";
            if (value.IndexOf(".rfa", StringComparison.OrdinalIgnoreCase) >= 0)
                return "<family_file>";

            if (!replacements.TryGetValue(stem, out var byValue))
            {
                byValue = new Dictionary<string, string>(StringComparer.Ordinal);
                replacements[stem] = byValue;
            }

            if (!byValue.TryGetValue(value, out var replacement))
            {
                replacement = "<" + stem + "_" + (byValue.Count + 1) + ">";
                byValue[value] = replacement;
            }

            return replacement;
        }

        private static bool IsPlaceholder(string value)
        {
            return value.Length >= 3 && value[0] == '<' && value[value.Length - 1] == '>';
        }

        private static string GetTokenStem(string field)
        {
            switch (field.ToLowerInvariant())
            {
                case "viewname":
                    return "view_name";
                case "levelname":
                    return "level_name";
                case "sheetname":
                    return "sheet_name";
                case "familyname":
                    return "family_name";
                case "projectname":
                    return "project_name";
                case "result":
                    return "result";
                case "name":
                    return "name";
                case "level":
                    return "level_name";
                case "number":
                    return "number";
                case "sheetnumber":
                    return "sheet_number";
                case "department":
                    return "department";
                case "typename":
                    return "type_name";
                case "roomname":
                    return "room_name";
                case "roomnumber":
                    return "room_number";
                case "family":
                    return "family_name";
                case "systemname":
                    return "system_name";
                case "type":
                    return "type_name";
                case "title":
                case "documenttitle":
                    return "document_title";
                case "project":
                    return "project_name";
                default:
                    return "name";
            }
        }

        private static string RedactWindowsPath(Match match)
        {
            var value = match.Value;
            if (value.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase))
                return "<project_file>";
            if (value.EndsWith(".rfa", StringComparison.OrdinalIgnoreCase))
                return "<family_file>";
            return "<local_path>";
        }

        private static string ToLowerHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
                builder.Append(b.ToString("x2"));
            return builder.ToString();
        }

        private static string RedactStandaloneRevitFiles(string input, string extension, string replacement)
        {
            var builder = new StringBuilder(input.Length);
            var appendFrom = 0;
            var searchFrom = 0;
            var scanFloor = 0;

            while (searchFrom < input.Length)
            {
                var dot = input.IndexOf(extension, searchFrom, StringComparison.OrdinalIgnoreCase);
                if (dot < 0)
                    break;

                var end = dot + extension.Length;
                if (!IsExtensionBoundary(input, end))
                {
                    scanFloor = Math.Max(scanFloor, FindContinuationEnd(input, end));
                    searchFrom = scanFloor;
                    continue;
                }

                var start = FindStandaloneFileStart(input, dot);
                if (start < appendFrom)
                    start = appendFrom;
                if (start < scanFloor)
                    start = scanFloor;
                if (start >= end)
                {
                    searchFrom = end;
                    continue;
                }

                builder.Append(input.Substring(appendFrom, start - appendFrom));
                builder.Append(RedactStandaloneFileName(input.Substring(start, end - start), replacement));
                appendFrom = end;
                searchFrom = end;
            }

            builder.Append(input.Substring(appendFrom, input.Length - appendFrom));
            return builder.ToString();
        }

        private static int FindStandaloneFileStart(string input, int dotIndex)
        {
            var whitespaceRuns = 0;
            var inWhitespaceRun = false;

            for (var i = dotIndex - 1; i >= 0; i--)
            {
                var current = input[i];
                if (IsStandaloneFileBoundary(input, i))
                    return i + 1;

                if (char.IsWhiteSpace(current))
                {
                    if (!inWhitespaceRun)
                    {
                        whitespaceRuns++;
                        if (whitespaceRuns > MaxStandaloneFileNameWhitespaceRuns)
                            return i + 1;
                    }

                    inWhitespaceRun = true;
                    continue;
                }

                inWhitespaceRun = false;
            }

            return 0;
        }

        private static bool IsStandaloneFileBoundary(string input, int index)
        {
            var c = input[index];
            switch (c)
            {
                case '"':
                case '\'':
                case ',':
                case ';':
                    return true;
                case '.':
                    return index + 1 >= input.Length || char.IsWhiteSpace(input[index + 1]);
                case '!':
                case '<':
                case '>':
                case '|':
                case '\\':
                case '/':
                case ':':
                case '*':
                case '?':
                case '\r':
                case '\n':
                    return true;
                case ')':
                    return index + 1 >= input.Length || char.IsWhiteSpace(input[index + 1]);
                default:
                    return false;
            }
        }

        private static bool IsExtensionBoundary(string input, int end)
        {
            if (end >= input.Length)
                return true;

            var next = input[end];
            if (next == '.')
            {
                if (end + 1 >= input.Length)
                    return true;

                var afterPeriod = input[end + 1];
                return !IsExtensionContinuationAfterPeriod(afterPeriod);
            }

            return !IsExtensionContinuation(next);
        }

        private static int FindContinuationEnd(string input, int start)
        {
            var i = start;

            if (i < input.Length && input[i] == '.')
            {
                i++;
                while (i < input.Length && IsExtensionContinuation(input[i]))
                    i++;
                return i;
            }

            while (i < input.Length && IsExtensionContinuation(input[i]))
                i++;
            return i;
        }

        private static bool IsExtensionContinuationAfterPeriod(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '-' || c == '+';
        }

        private static bool IsExtensionContinuation(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '+';
        }

        private static string RedactStandaloneFileName(string value, string replacement)
        {
            var prefix = new StringBuilder();

            while (value.Length > 0 && char.IsWhiteSpace(value[0]))
            {
                prefix.Append(value[0]);
                value = value.Substring(1);
            }

            var commandPrefixLength = FindLeadingCommandPrefixLength(value);
            if (commandPrefixLength > 0)
            {
                prefix.Append(value.Substring(0, commandPrefixLength));
                value = value.Substring(commandPrefixLength);
            }

            while (true)
            {
                var firstSpace = value.IndexOf(' ');
                if (firstSpace <= 0)
                    break;

                var firstToken = value.Substring(0, firstSpace);
                if (!LeadingFileStopWords.Contains(firstToken))
                    break;

                prefix.Append(value.Substring(0, firstSpace + 1));
                value = value.Substring(firstSpace + 1);
            }

            return prefix + replacement;
        }

        private static int FindLeadingCommandPrefixLength(string value)
        {
            var position = 0;

            while (position < value.Length)
            {
                while (position < value.Length && char.IsWhiteSpace(value[position]))
                    position++;
                if (position >= value.Length)
                    return 0;

                var tokenStart = position;
                while (position < value.Length && !char.IsWhiteSpace(value[position]))
                    position++;

                var token = value.Substring(tokenStart, position - tokenStart)
                    .Trim(',', ';', ':', '.', '!', '?', '"', '\'');
                if (LeadingCommandVerbs.Contains(token))
                {
                    while (position < value.Length && char.IsWhiteSpace(value[position]))
                        position++;
                    return position;
                }

                if (!LeadingCommandContextWords.Contains(token))
                    return 0;
            }

            return 0;
        }
    }
}
