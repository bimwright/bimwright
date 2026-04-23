using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Bimwright.Rvt.Plugin.Lint
{
    public class FirmProfileLibrary
    {
        public List<FirmProfile> Profiles { get; set; } = new List<FirmProfile>();

        /// <summary>
        /// Scan the given folders for *.json profile files and load them.
        /// Later folders override earlier folders on id collision (user dir wins over shipped).
        /// </summary>
        public static FirmProfileLibrary LoadFrom(IEnumerable<string> folders)
        {
            var lib = new FirmProfileLibrary();
            var byId = new Dictionary<string, FirmProfile>(StringComparer.OrdinalIgnoreCase);

            foreach (var folder in folders ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(folder)) continue;
                if (!Directory.Exists(folder)) continue;

                foreach (var file in Directory.GetFiles(folder, "*.json"))
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var profile = JsonConvert.DeserializeObject<FirmProfile>(json);
                        if (profile == null || string.IsNullOrWhiteSpace(profile.Id)) continue;
                        byId[profile.Id] = profile;  // later folder wins
                    }
                    catch (Exception)
                    {
                        // Malformed profile — skip silently (strictness would break users with one bad file)
                    }
                }
            }

            lib.Profiles = byId.Values.OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
            return lib;
        }

        /// <summary>
        /// Score each profile against the given evidence and return the best match if
        /// total weight ≥ 0.50, else null. Ties broken by most matching hints, then id.
        /// </summary>
        public FirmProfileMatch Match(FirmMatchEvidence evidence)
        {
            if (Profiles.Count == 0 || evidence == null) return null;

            FirmProfileMatch best = null;
            foreach (var profile in Profiles)
            {
                double score = 0;
                var matched = new List<string>();
                foreach (var hint in profile.MatchHints ?? new List<FirmMatchHint>())
                {
                    string target = null;
                    switch (hint.Kind)
                    {
                        case "sheet_prefix": target = evidence.SheetPrefix; break;
                        case "view_dominant": target = evidence.ViewDominant; break;
                        case "level_pattern": target = evidence.LevelPattern; break;
                    }
                    if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(hint.Regex)) continue;
                    try
                    {
                        if (Regex.IsMatch(target, hint.Regex))
                        {
                            score += hint.Weight;
                            matched.Add(hint.Kind);
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Malformed regex in profile — skip this hint, don't fail entire match
                    }
                }

                if (score < 0.50) continue;

                if (best == null ||
                    score > best.Confidence ||
                    (Math.Abs(score - best.Confidence) < 0.001 && matched.Count > best.MatchedHints.Count))
                {
                    best = new FirmProfileMatch
                    {
                        ProfileId = profile.Id,
                        Confidence = score,
                        MatchedHints = matched
                    };
                }
            }
            return best;
        }
    }

    public class FirmMatchEvidence
    {
        public string SheetPrefix { get; set; }
        public string ViewDominant { get; set; }
        public string LevelPattern { get; set; }
    }

    public class FirmProfileMatch
    {
        public string ProfileId { get; set; }
        public double Confidence { get; set; }
        public List<string> MatchedHints { get; set; } = new List<string>();
    }
}
