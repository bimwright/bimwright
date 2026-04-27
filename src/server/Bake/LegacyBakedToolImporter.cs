using System;
using System.Collections.Generic;
using System.IO;
using Bimwright.Rvt.Plugin.ToolBaker;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bimwright.Rvt.Server.Bake
{
    public sealed class LegacyBakedToolImporter
    {
        private readonly BakePaths _paths;
        private readonly BakeDb _db;
        private readonly ToolBakerAuditLog _auditLog;

        public LegacyBakedToolImporter(BakePaths paths, BakeDb db, ToolBakerAuditLog auditLog)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _auditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
        }

        public LegacyBakedToolImportResult ImportIfNeeded()
        {
            var result = new LegacyBakedToolImportResult();
            if (!File.Exists(_paths.LegacyRegistryJson))
                return result;

            foreach (var legacy in ReadLegacyRegistry(_paths.LegacyRegistryJson))
            {
                if (!TryBuildRecord(legacy, out var record, out var invalidReason))
                {
                    result.SkippedInvalid++;
                    _auditLog.Append("skipped-invalid", legacy?.Name, invalidReason);
                    continue;
                }

                if (_db.TryInsertRegistryRecord(record))
                {
                    result.Imported++;
                    _auditLog.Append("imported", record.Name);
                }
                else
                {
                    result.SkippedDuplicate++;
                    _auditLog.Append("skipped-duplicate", record.Name);
                }
            }

            return result;
        }

        private bool TryBuildRecord(LegacyBakedToolMeta legacy, out BakedToolRecord record, out string invalidReason)
        {
            record = null;
            invalidReason = null;

            if (legacy == null || string.IsNullOrWhiteSpace(legacy.Name))
            {
                invalidReason = "missing-name";
                return false;
            }

            var sourcePath = Path.Combine(_paths.LegacyBakedDir, legacy.Name + ".cs");
            if (!File.Exists(sourcePath))
            {
                invalidReason = "missing-source";
                return false;
            }

            var createdAt = string.IsNullOrWhiteSpace(legacy.CreatedUtc)
                ? DateTime.UtcNow.ToString("o")
                : legacy.CreatedUtc;

            record = new BakedToolRecord
            {
                Name = legacy.Name,
                Description = legacy.Description ?? string.Empty,
                Source = "legacy_pre_v0.3",
                ParamsSchema = string.IsNullOrWhiteSpace(legacy.ParametersSchema) ? "{}" : legacy.ParametersSchema,
                CompatMap = "{}",
                DllBytes = null,
                SourceCode = File.ReadAllText(sourcePath),
                CreatedFromSuggestionId = null,
                ReviewedByUser = true,
                CreatedAt = createdAt,
                LastUsedAt = null,
                FailureRate = 0,
                VersionHistoryBlob = JsonConvert.SerializeObject(new
                {
                    source_status = "legacy_imported",
                    legacy_call_count = legacy.CallCount
                }),
            };
            return true;
        }

        private static IReadOnlyList<LegacyBakedToolMeta> ReadLegacyRegistry(string registryPath)
        {
            var json = File.ReadAllText(registryPath);
            var token = JsonConvert.DeserializeObject<JToken>(json, new JsonSerializerSettings
            {
                DateParseHandling = DateParseHandling.None
            });
            if (token is JArray array)
            {
                var serializer = JsonSerializer.Create(new JsonSerializerSettings
                {
                    DateParseHandling = DateParseHandling.None
                });
                return array.ToObject<List<LegacyBakedToolMeta>>(serializer) ?? new List<LegacyBakedToolMeta>();
            }

            return new List<LegacyBakedToolMeta>();
        }

        private sealed class LegacyBakedToolMeta
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string ParametersSchema { get; set; }
            public string CreatedUtc { get; set; }
            public int CallCount { get; set; }
        }
    }

    public sealed class LegacyBakedToolImportResult
    {
        public int Imported { get; set; }
        public int SkippedInvalid { get; set; }
        public int SkippedDuplicate { get; set; }
    }
}
