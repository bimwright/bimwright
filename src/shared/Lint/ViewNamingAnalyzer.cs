using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Bimwright.Rvt.Plugin.Lint
{
    public static class ViewNamingAnalyzer
    {
        private static readonly HashSet<string> ReservedTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "3D", "Plan", "Section", "Sheet", "Elevation", "Detail", "Area", "RCP"
        };

        /// <summary>
        /// Convert a view name into a pattern template by classifying each whitespace/hyphen/underscore-separated token.
        /// All-digit → {NN}. Pure alpha → {Name} (unless reserved). Mixed → kept literal or tokenized.
        /// </summary>
        public static string Tokenize(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";

            // Split preserving delimiters
            var parts = Regex.Split(name, @"([-_\s]+)");
            var sb = new StringBuilder();

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                // Delimiter pass-through
                if (Regex.IsMatch(part, @"^[-_\s]+$"))
                {
                    sb.Append(part);
                    continue;
                }

                sb.Append(ClassifyToken(part));
            }

            return sb.ToString();
        }

        private static string ClassifyToken(string token)
        {
            // Reserved tokens (3D, Plan, etc) stay literal
            if (ReservedTokens.Contains(token)) return token;
            // Pure digits → {NN}
            if (Regex.IsMatch(token, @"^\d+$")) return "{NN}";
            // Pure alpha → {Name}
            if (Regex.IsMatch(token, @"^[A-Za-z]+$")) return "{Name}";
            // Mixed alpha+digit like "L01": preserve leading letters, digit-substitute trailing
            var m = Regex.Match(token, @"^([A-Za-z]+)(\d+)$");
            if (m.Success) return m.Groups[1].Value + "{NN}";
            // Fallback: unknown shape — keep literal
            return token;
        }
    }
}
