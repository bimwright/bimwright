#nullable enable
using System.Text;

namespace Bimwright.Rvt.Plugin
{
    /// <summary>
    /// Passive observability for S4 pagination — emits a warning string when
    /// a serialized response payload exceeds the threshold. Callers log the
    /// returned string via their own logger. No enforcement.
    /// </summary>
    public static class ResponseSizeGuard
    {
        public const int DefaultThresholdBytes = 100 * 1024; // 100 KB

        public static string? CheckResponse(
            string commandName,
            string serializedPayload,
            int topLevelKeyCount,
            int thresholdBytes = DefaultThresholdBytes)
        {
            var byteCount = Encoding.UTF8.GetByteCount(serializedPayload ?? string.Empty);
            if (byteCount <= thresholdBytes) return null;

            return $"[S4] Oversized response: command={commandName} bytes={byteCount} top_level_keys={topLevelKeyCount} threshold={thresholdBytes}";
        }
    }
}
