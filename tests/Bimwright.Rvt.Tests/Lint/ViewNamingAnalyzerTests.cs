using Bimwright.Rvt.Plugin.Lint;
using Xunit;

namespace Bimwright.Rvt.Tests.Lint
{
    public class ViewNamingAnalyzerTests
    {
        [Theory]
        [InlineData("L01-Lobby", "L{NN}-{Name}")]
        [InlineData("L02-Office", "L{NN}-{Name}")]
        [InlineData("Level 1", "{Name} {NN}")]
        [InlineData("Site", "{Name}")]
        [InlineData("3D View 1", "3D {Name} {NN}")]
        [InlineData("Plan-Level-01", "Plan-{Name}-{NN}")]
        public void Tokenize_produces_expected_pattern(string input, string expected)
        {
            var pattern = ViewNamingAnalyzer.Tokenize(input);
            Assert.Equal(expected, pattern);
        }
    }
}
