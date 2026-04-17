using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
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

        [Fact]
        public void Tools_list_matches_golden_snapshot()
        {
            var captured = CaptureToolsList();

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

        private static string CaptureToolsList()
        {
            // ToolsetFilter is a public type in Server — gives a stable handle to the
            // Server assembly without forcing `Program` to become public.
            var serverAssembly = typeof(Bimwright.Rvt.Server.ToolsetFilter).Assembly;

            var toolClasses = serverAssembly.GetTypes()
                .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .ToArray();

            var tools = toolClasses
                .SelectMany(cls => cls.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                    .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null))
                .Select(ToToolMetadata)
                .ToArray();

            return SnapshotSerializer.Serialize(tools.Length, tools);
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
