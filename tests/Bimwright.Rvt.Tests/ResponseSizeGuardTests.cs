using Bimwright.Rvt.Plugin;
using Xunit;

namespace Bimwright.Rvt.Tests
{
    public class ResponseSizeGuardTests
    {
        [Fact]
        public void Check_under_threshold_returns_null()
        {
            var payload = "x"; // 1 byte UTF-8
            var warning = ResponseSizeGuard.CheckResponse("some_command", payload, topLevelKeyCount: 3);
            Assert.Null(warning);
        }

        [Fact]
        public void Check_exactly_at_threshold_returns_null()
        {
            var payload = new string('x', 100 * 1024); // 100 KB UTF-8 (ASCII → 1 byte/char)
            var warning = ResponseSizeGuard.CheckResponse("some_command", payload, topLevelKeyCount: 1);
            Assert.Null(warning);
        }

        [Fact]
        public void Check_above_threshold_returns_warning()
        {
            var payload = new string('x', 100 * 1024 + 1); // 100 KB + 1 byte
            var warning = ResponseSizeGuard.CheckResponse("ai_element_filter", payload, topLevelKeyCount: 2);
            Assert.NotNull(warning);
            Assert.Contains("ai_element_filter", warning);
            Assert.Contains("102401", warning); // byte size
            Assert.Contains("top_level_keys=2", warning); // key count
        }

        [Fact]
        public void Check_threshold_is_configurable()
        {
            var payload = new string('x', 50);
            var warning = ResponseSizeGuard.CheckResponse("some_command", payload, topLevelKeyCount: 1, thresholdBytes: 40);
            Assert.NotNull(warning);
        }
    }
}
