using System;

namespace Bimwright.Rvt.Server.Bake
{
    public sealed class UsageEvent
    {
        public string Id { get; set; }
        public DateTimeOffset TsUtc { get; set; }
        public string Source { get; set; }
        public string Tool { get; set; }
        public string NormalizedKey { get; set; }
        public string PayloadJson { get; set; }
        public string BodyHash { get; set; }
        public bool Success { get; set; }
    }
}
