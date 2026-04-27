# Changelog

## v0.3.0 - ToolBaker redesign

### Breaking

- Removed `bake_tool`. It is no longer available as an MCP tool. Create new baked tools through the adaptive-bake suggestion flow and `accept_bake_suggestion` instead.

### Added

- Added adaptive-bake suggestion lifecycle tools: `list_bake_suggestions`, `accept_bake_suggestion`, and `dismiss_bake_suggestion`.
- Added accepted-tool indirection through `list_baked_tools` and `run_baked_tool`, including parameter validation and archived-tool guidance.
- Added Revit ribbon/runtime support for accepted baked tools, backed by a plugin runtime cache.
- Added local SQLite-backed bake storage (`bake.db`) plus local audit/usage records under `%LOCALAPPDATA%\Bimwright\`.
- Added compiler policy, privacy, registry, runtime cache, usage clustering, and ToolBaker handler tests.

### Changed

- Adaptive bake is opt-in and default off via `BIMWRIGHT_ENABLE_ADAPTIVE_BAKE=1` or `"enableAdaptiveBake": true`.
- Usage data for adaptive bake is stored locally under `%LOCALAPPDATA%\Bimwright\`.
- Accepted baked tools are discovered with `list_baked_tools` and executed with `run_baked_tool name=<tool_name>`.
- The Revit plugin reads `bake.db` and owns runtime/ribbon loading only; the server is the sole SQLite writer.
- `send_code_to_revit` is gated behind adaptive bake and continues to require per-call Revit confirmation.
- Baked tools no longer appear as promoted native MCP tools in v0.3.x; agents use the stable accepted-tool index instead.
- README and docs now reflect the current 32-tool default surface, 35-tool adaptive surface, supported MCP clients, and R22/R26/R27 accepted-tool smoke evidence.

### Fixed

- Fixed baked-tool dispatch bypasses by preventing `run_baked_tool` execution through `batch_execute` and isolating baked commands from core command lookup.
- Fixed stale cross-version compatibility metadata by refreshing baked-tool registry reads from `bake.db` before list/meta reads.
- Fixed Revit 2026/2027 accepted-tool startup failures by copying native `e_sqlite3.dll` beside `Bimwright.Rvt.Plugin.dll` during deploy and release staging.
- Fixed suggestion refresh hardening: bounded usage replay, capped/deduped suggestions, and guarded adaptive usage capture.

### Security

- Hardened durable logs, journals, prompts, markdown, and live responses so send-code outputs and sensitive literals are redacted or hashed before persistence.
- Added redaction boundary fixes for paths, filenames, escaped outputs, and legacy orphan call archives.
- Added compiler denylist coverage and plugin allow-list narrowing for baked C# execution.
