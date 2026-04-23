using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>Coverage threshold for a pattern to qualify as `dominant`.</summary>
        public const double DominantCoverageThreshold = 0.50;

        public static NamingAnalysis Analyze(IEnumerable<string> viewNames)
        {
            var names = viewNames?.Where(n => !string.IsNullOrWhiteSpace(n)).ToArray() ?? new string[0];
            if (names.Length == 0)
            {
                return new NamingAnalysis
                {
                    TotalViews = 0,
                    Patterns = new List<PatternSummary>(),
                    Dominant = null,
                    Outliers = new List<Outlier>()
                };
            }

            // Tokenize + group
            var grouped = names
                .Select(n => new { Name = n, Pattern = Tokenize(n) })
                .GroupBy(x => x.Pattern)
                .OrderByDescending(g => g.Count())
                .ToArray();

            var total = names.Length;
            var patterns = grouped.Select(g => new PatternSummary
            {
                Pattern = g.Key,
                Examples = g.Take(3).Select(x => x.Name).ToArray(),
                Count = g.Count(),
                Coverage = Math.Round((double)g.Count() / total, 4)
            }).ToList();

            var dominant = patterns.Count > 0 && patterns[0].Coverage >= DominantCoverageThreshold
                ? patterns[0].Pattern
                : null;

            // Outliers (Task 3 fills; empty for now)
            return new NamingAnalysis
            {
                TotalViews = total,
                Patterns = patterns,
                Dominant = dominant,
                Outliers = new List<Outlier>()
            };
        }
    }

    public class NamingAnalysis
    {
        public int TotalViews { get; set; }
        public List<PatternSummary> Patterns { get; set; }
        public string Dominant { get; set; }
        public List<Outlier> Outliers { get; set; }
    }

    public class PatternSummary
    {
        public string Pattern { get; set; }
        public string[] Examples { get; set; }
        public int Count { get; set; }
        public double Coverage { get; set; }
    }

    public class Outlier
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string ClosestPattern { get; set; }
        public int EditDistance { get; set; }
    }
}
