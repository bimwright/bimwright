using Bimwright.Rvt.Plugin.Lint;
using Xunit;

namespace Bimwright.Rvt.Tests.Lint
{
    public class ViewNamingAnalyzerTests
    {
        [Theory]
        [InlineData("L01-Lobby", "L{NN}-Lobby")]
        [InlineData("L02-Office", "L{NN}-Office")]
        [InlineData("Level 1", "Level {NN}")]
        [InlineData("Site", "Site")]
        [InlineData("3D View 1", "3D View {NN}")]
        [InlineData("Plan-Level-01", "Plan-Level-{NN}")]
        public void Tokenize_produces_expected_pattern(string input, string expected)
        {
            var pattern = ViewNamingAnalyzer.Tokenize(input);
            Assert.Equal(expected, pattern);
        }
    }
}
