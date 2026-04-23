using System.Collections.Generic;

namespace Bimwright.Rvt.Plugin.Lint
{
    public class FirmProfile
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<FirmMatchHint> MatchHints { get; set; } = new List<FirmMatchHint>();
        public FirmRules Rules { get; set; } = new FirmRules();
    }

    public class FirmMatchHint
    {
        public string Kind { get; set; }    // "sheet_prefix", "view_dominant", "level_pattern"
        public string Regex { get; set; }
        public double Weight { get; set; }
    }

    public class FirmRules
    {
        public string ViewName { get; set; }
        public string SheetNumber { get; set; }
        public string Level { get; set; }
    }
}
