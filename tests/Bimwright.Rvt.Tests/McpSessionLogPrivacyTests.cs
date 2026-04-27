using Bimwright.Rvt.Plugin;
using Xunit;

namespace Bimwright.Rvt.Tests
{
    public class McpSessionLogPrivacyTests
    {
        [Fact]
        public void Add_RedactsSendCodeBodyWhenBodyCacheDisabled()
        {
            var log = new McpSessionLog();
            McpSessionLog.ConfigLoader = () => new BimwrightConfig { CacheSendCodeBodies = false };
            try
            {
                log.Add(new McpCallEntry
                {
                    ToolName = "send_code_to_revit",
                    ParamsJson = @"{""code"":""return \""SensitiveWallType42\"";"",""transactionMode"":""none""}",
                    CodeSnippet = "return \"SensitiveWallType42\";",
                    Success = true,
                    DurationMs = 10,
                    Summary = "Executed SensitiveWallType42"
                });

                var entry = log.Entries[0];

                Assert.DoesNotContain("SensitiveWallType42", entry.ParamsJson);
                Assert.DoesNotContain("transactionMode", entry.ParamsJson);
                Assert.Null(entry.CodeSnippet);
                Assert.DoesNotContain("SensitiveWallType42", entry.Summary);
                Assert.Contains("code_hash", entry.ParamsJson);
                Assert.Contains("code_length", entry.ParamsJson);
            }
            finally
            {
                McpSessionLog.ConfigLoader = () => BimwrightConfig.Load();
            }
        }

        [Fact]
        public void Add_KeepsSendCodeBodyWhenBodyCacheEnabled()
        {
            var log = new McpSessionLog();
            McpSessionLog.ConfigLoader = () => new BimwrightConfig { CacheSendCodeBodies = true };
            try
            {
                log.Add(new McpCallEntry
                {
                    ToolName = "send_code_to_revit",
                    ParamsJson = @"{""code"":""return \""SensitiveWallType42\"";""}",
                    CodeSnippet = "return \"SensitiveWallType42\";",
                    Success = true,
                    DurationMs = 10
                });

                var entry = log.Entries[0];

                Assert.Contains("SensitiveWallType42", entry.ParamsJson);
                Assert.Contains("SensitiveWallType42", entry.CodeSnippet);
            }
            finally
            {
                McpSessionLog.ConfigLoader = () => BimwrightConfig.Load();
            }
        }
    }
}
