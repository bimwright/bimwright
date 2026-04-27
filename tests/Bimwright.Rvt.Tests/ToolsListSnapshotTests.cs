using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Bimwright.Rvt.Plugin;
using Bimwright.Rvt.Tests.Helpers;
using ModelContextProtocol.Server;
using Xunit;

namespace Bimwright.Rvt.Tests
{
    public class ToolsListSnapshotTests
    {
        private static readonly string GoldenPath = Path.Combine(
            Path.GetDirectoryName(typeof(ToolsListSnapshotTests).Assembly.Location)!,
            "..", "..", "..", "Golden", "tools-list.json");

        private static readonly string AdaptiveGoldenPath = Path.Combine(
            Path.GetDirectoryName(typeof(ToolsListSnapshotTests).Assembly.Location)!,
            "..", "..", "..", "Golden", "tools-list-adaptive-bake.json");

        [Fact]
        public void Tools_list_matches_golden_snapshot()
        {
            var captured = CaptureToolsList(AllToolsetsConfig(enableAdaptiveBake: false));

            var update = Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1";
            var goldenExists = File.Exists(GoldenPath);

            if (update || !goldenExists)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(GoldenPath)!);
                File.WriteAllText(GoldenPath, captured);
                if (!goldenExists)
                {
                    Console.Error.WriteLine(
                        $"[ToolsListSnapshot] Golden file bootstrapped at {GoldenPath}. " +
                        "Please commit it.");
                }
                return;
            }

            var expected = File.ReadAllText(GoldenPath);
            Assert.Equal(expected.ReplaceLineEndings("\n"), captured.ReplaceLineEndings("\n"));
        }

        [Fact]
        public void Adaptive_bake_tools_list_matches_golden_snapshot()
        {
            var captured = CaptureToolsList(new BimwrightConfig
            {
                EnableAdaptiveBake = true,
                Toolsets = new System.Collections.Generic.List<string> { "all" }
            });

            var update = Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1";
            var goldenExists = File.Exists(AdaptiveGoldenPath);

            if (update || !goldenExists)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AdaptiveGoldenPath)!);
                File.WriteAllText(AdaptiveGoldenPath, captured);
                if (!goldenExists)
                {
                    Console.Error.WriteLine(
                        $"[ToolsListSnapshot] Adaptive golden file bootstrapped at {AdaptiveGoldenPath}. " +
                        "Please commit it.");
                }
                return;
            }

            var expected = File.ReadAllText(AdaptiveGoldenPath);
            Assert.Equal(expected.ReplaceLineEndings("\n"), captured.ReplaceLineEndings("\n"));
        }

        [Fact]
        public void Default_tools_snapshot_does_not_expose_adaptive_bake_suggestions()
        {
            var captured = CaptureToolsList(AllToolsetsConfig(enableAdaptiveBake: false));

            Assert.DoesNotContain("\"name\": \"list_bake_suggestions\"", captured);
            Assert.DoesNotContain("\"name\": \"accept_bake_suggestion\"", captured);
            Assert.DoesNotContain("\"name\": \"dismiss_bake_suggestion\"", captured);
        }

        [Fact]
        public void Adaptive_bake_snapshot_exposes_exactly_three_suggestion_handlers()
        {
            var captured = CaptureToolsList(new BimwrightConfig
            {
                EnableAdaptiveBake = true,
                Toolsets = new System.Collections.Generic.List<string> { "all" }
            });

            Assert.Contains("\"name\": \"list_bake_suggestions\"", captured);
            Assert.Contains("\"name\": \"accept_bake_suggestion\"", captured);
            Assert.Contains("\"name\": \"dismiss_bake_suggestion\"", captured);
            Assert.Equal(3, new[]
            {
                "\"name\": \"list_bake_suggestions\"",
                "\"name\": \"accept_bake_suggestion\"",
                "\"name\": \"dismiss_bake_suggestion\""
            }.Count(captured.Contains));
            Assert.DoesNotContain("\"name\": \"bake_tool\"", captured);
        }

        [Fact]
        public void Generated_tools_snapshot_does_not_include_removed_bake_tool()
        {
            var captured = CaptureToolsList(AllToolsetsConfig(enableAdaptiveBake: false));

            Assert.DoesNotContain("\"name\": \"bake_tool\"", captured);
        }

        private static string CaptureToolsList(BimwrightConfig config)
        {
            // ToolsetFilter is a public type in Server — gives a stable handle to the
            // Server assembly without forcing `Program` to become public.
            var serverAssembly = typeof(Bimwright.Rvt.Server.ToolsetFilter).Assembly;
            var programType = serverAssembly.GetType("Bimwright.Rvt.Server.Program")!;
            var resolveToolTypes = programType.GetMethod("ResolveRegisteredToolTypes", BindingFlags.NonPublic | BindingFlags.Static)!;
            var enabled = Bimwright.Rvt.Server.ToolsetFilter.Resolve(config);

            var toolClasses = ((Type[])resolveToolTypes.Invoke(null, new object[] { enabled, config })!)
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .ToArray();

            var tools = toolClasses
                .SelectMany(cls => cls.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                    .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null))
                .Select(ToToolMetadata)
                .ToArray();

            return SnapshotSerializer.Serialize(tools.Length, tools);
        }

        private static BimwrightConfig AllToolsetsConfig(bool enableAdaptiveBake)
        {
            return new BimwrightConfig
            {
                EnableAdaptiveBake = enableAdaptiveBake,
                Toolsets = new System.Collections.Generic.List<string> { "all" }
            };
        }

        private static object ToToolMetadata(MethodInfo method)
        {
            var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>()!;
            var descAttr = method.GetCustomAttribute<DescriptionAttribute>();
            var description = descAttr?.Description ?? string.Empty;
            var name = toolAttr.Name ?? ToSnakeCase(method.Name);

            var parameters = method.GetParameters()
                .Select(p => new
                {
                    name = p.Name,
                    type = p.ParameterType.Name,
                    required = !p.HasDefaultValue,
                    description = p.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty
                })
                .ToArray();

            return new
            {
                name,
                description_hash = SnapshotSerializer.HashDescription(description),
                inputSchema = new
                {
                    type = "object",
                    properties = parameters.ToDictionary(p => p.name!, p => new { type = p.type, description = p.description }),
                    required = parameters.Where(p => p.required).Select(p => p.name).ToArray()
                }
            };
        }

        private static string ToSnakeCase(string pascal)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < pascal.Length; i++)
            {
                if (i > 0 && char.IsUpper(pascal[i])) sb.Append('_');
                sb.Append(char.ToLowerInvariant(pascal[i]));
            }
            return sb.ToString();
        }
    }
}
