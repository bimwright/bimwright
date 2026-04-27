namespace Bimwright.Rvt.Plugin.ToolBaker
{
    public sealed class BakedToolRecord
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Source { get; set; }
        public string ParamsSchema { get; set; }
        public string CompatMap { get; set; }
        public byte[] DllBytes { get; set; }
        public string SourceCode { get; set; }
        public string CreatedFromSuggestionId { get; set; }
        public bool ReviewedByUser { get; set; }
        public string CreatedAt { get; set; }
        public string LastUsedAt { get; set; }
        public int UsageCount { get; set; }
        public double FailureRate { get; set; }
        public string LifecycleState { get; set; }
        public string VersionHistoryBlob { get; set; }
    }
}
