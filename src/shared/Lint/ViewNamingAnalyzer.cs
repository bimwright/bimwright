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

        private static int CountTokens(string pattern)
        {
            return Regex.Split(pattern, @"[-_\s]+").Count(p => !string.IsNullOrEmpty(p));
        }

        private static int Levenshtein(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
            if (string.IsNullOrEmpty(b)) return a.Length;
            var d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
                for (int j = 1; j <= b.Length; j++)
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            return d[a.Length, b.Length];
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

            // Outliers: names whose pattern != dominant AND token count is within ±1 of dominant's
            // (close-but-wrong > completely-different). Sorted by edit distance to dominant exemplar ascending.
            var outliers = new List<Outlier>();
            if (dominant != null && patterns.Count > 0)
            {
                var dominantTokenCount = CountTokens(dominant);
                var dominantExample = patterns[0].Examples.FirstOrDefault() ?? "";
                var candidates = names
                    .Select(n => new { Name = n, Pattern = Tokenize(n) })
                    .Where(x => x.Pattern != dominant)
                    .Where(x => Math.Abs(CountTokens(x.Pattern) - dominantTokenCount) <= 1)
                    .Select(x => new Outlier
                    {
                        Id = 0,  // filled by handler with Revit ElementId
                        Name = x.Name,
                        ClosestPattern = dominant,
                        EditDistance = Levenshtein(x.Name, dominantExample)
                    })
                    .OrderBy(o => o.EditDistance)
                    .Take(20)
                    .ToList();
                outliers = candidates;
            }

            return new NamingAnalysis
            {
                TotalViews = total,
                Patterns = patterns,
                Dominant = dominant,
                Outliers = outliers
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
