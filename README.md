<!-- mcp-name: io.github.bimwright/rvt-mcp -->

<p align="center">
  <img src="https://raw.githubusercontent.com/bimwright/.github/master/assets/logos/rvt-mcp.png" alt="rvt-mcp" width="180" />
</p>

<h1 align="center">rvt-mcp</h1>

<p align="center">
  <a href="https://github.com/bimwright/rvt-mcp/actions/workflows/build.yml"><img src="https://github.com/bimwright/rvt-mcp/actions/workflows/build.yml/badge.svg" alt="build" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-Apache%202.0-blue.svg" alt="license" /></a>
  <a href="#supported-revit-versions"><img src="https://img.shields.io/badge/Revit-2022--2027-186BFF" alt="Revit 2022-2027" /></a>
  <a href="#toolsets"><img src="https://img.shields.io/badge/MCP-32%20tools%20%7C%2035%20adaptive-6C47FF" alt="MCP tools" /></a>
</p>

<p align="center">
  English · <a href="README.vi.md">Tiếng Việt</a> · <a href="README.zh-CN.md">简体中文</a>
</p>

---

## Revit Automation Should Not Stop At "I Don't Code"

Before AI agents, many BIM users already wanted the same thing: make Revit faster, remove repetitive clicks, and shape the software around the way they actually work.

The hard part was never the idea. The hard part was turning the idea into a tool.

To build even a small Revit add-in, a practitioner usually has to:

- Define the input and output clearly enough for software.
- Think through the algorithm, edge cases, parameters, categories, filters, units, and Revit API constraints.
- Prototype in Dynamo, maybe move to Python, then eventually rewrite in C# when the workflow needs to become stable.
- Package the result as an add-in, handle dependencies, install paths, `.addin` manifests, Revit version drift, and ribbon buttons.

That is a lot of work for someone who studied architecture, structure, MEP, quantity takeoff, or BIM coordination - not software engineering.

The usual options are expensive in different ways:

- Spend months learning enough coding to maintain your own tools.
- Pay someone to build custom add-ins.
- Buy fixed add-ins and adapt your workflow to the vendor's assumptions.
- Stay with manual work because the automation path is too much friction.

`rvt-mcp` exists to compress that loop.

It gives AI agents a safe local bridge into Revit, then lets repeated workflows evolve into personal tools through ToolBaker. The goal is not one universal add-in for everyone. Revit serves too many disciplines, offices, standards, and habits for that. The goal is a system where each practitioner can grow a toolkit that matches their own work.

Personal automation should be personal.

---

## What rvt-mcp Is

`rvt-mcp` is a local MCP gateway for Autodesk Revit 2022-2027.

It has two parts:

- `Bimwright.Rvt.Server`: a .NET 8 MCP server launched by Claude, Cursor, Codex, OpenCode, Cline, VS Code Copilot, or another stdio MCP client.
- `Bimwright.Rvt.Plugin`: a Revit add-in shell for each supported Revit year, running inside Revit and executing commands on the Revit UI thread.

The agent talks MCP. The server talks to the plugin over localhost TCP or Named Pipe. The plugin talks to the Revit API.

Your model stays on your machine.

---

## Why It Matters

AI agents make it possible for BIM users to describe intent instead of writing code by hand. But intent alone is not enough. Revit automation still needs a runtime that understands transactions, parameters, units, selection, model state, version drift, safety, and rollback.

`rvt-mcp` is that runtime.

It is designed around four ideas:

- **Local first.** No cloud bridge is required. Revit, the plugin, MCP server, logs, and ToolBaker storage all live on the user's machine.
- **Reversible by default.** Mutating workflows can run through `batch_execute`, wrapping multiple commands in one Revit `TransactionGroup` so one undo step rolls the batch back.
- **Progressively exposed.** Toolsets and `--read-only` mode control what the agent can see and do. Weak or narrow agents do not need destructive tools.
- **Personal over generic.** Adaptive ToolBaker can observe repeated local workflows, propose a personal tool, and make accepted tools available through MCP and the Revit ribbon.

This is not a black-box demo and not courseware. It is public Apache-2.0 code. Claims should be checked by building, testing, running, and reading the source.

---

## The ToolBaker Loop

Most Revit automation dies between "good idea" and "usable add-in".

ToolBaker is the path from agent-assisted workflow to personal tool:

1. Use the existing MCP tools to query, create, lint, inspect, or batch operations in Revit.
2. When advanced automation is needed, opt into the `toolbaker` surface and enable adaptive bake before using `send_code_to_revit` with explicit Revit confirmation.
3. If adaptive bake is enabled, repeated local usage is recorded locally under `%LOCALAPPDATA%\Bimwright\`.
4. Repeated patterns become suggestions, visible through `list_bake_suggestions`.
5. You explicitly accept a suggestion with `accept_bake_suggestion`, including the tool name, schema, and output choice.
6. Accepted tools become callable through `list_baked_tools` / `run_baked_tool` and available from the Revit ribbon runtime cache.

Adaptive bake is off by default. It is for users who want their own local usage data to shape their own tools.

---

## Architecture

```text
+---------------------------+
| AI Client                 |
| Claude / Cursor / Codex   |
+---------------------------+
              |
              | stdio MCP
              v
+---------------------------+
| Bimwright.Rvt.Server      |
| .NET 8 / C#               |
+---------------------------+
              |
              | TCP (R22-R24)
              | Named Pipe (R25-R27)
              v
+---------------------------+
| Plugin Shell              |
| thin add-in per Revit yr  |
+---------------------------+
              |
              | shared command core
              | from `src/shared/`
              v
+---------------------------+
| ExternalEvent Marshal     |
| execution -> Revit UI     |
+---------------------------+
              |
              v
+---------------------------+
| Revit API                 |
+---------------------------+
              |
              v
+---------------------------+
| Model / Transaction /     |
| Undo                      |
+---------------------------+
```

`rvt-mcp` is a full C# MCP stack. The MCP server, per-version Revit plugin shells, transport bridge, command handlers, DTO mapping, and ToolBaker pipeline are all written in C# using the official MCP C# SDK.

There is no Node.js sidecar on the Revit machine.

The version split is explicit at the edge: one thin plugin shell per Revit year, all compiling the same `src/shared/` source glob. See [ARCHITECTURE.md](ARCHITECTURE.md) for the threading, transport, DTO, and ToolBaker details.

---

## Current State

`rvt-mcp` is usable but still young.

- Compile gate covers Revit R22-R27 plugin shells.
- Unit tests cover pure .NET logic, tool-surface snapshots, ToolBaker storage/policy paths, config, logging, privacy, and batching behavior.
- Core runtime coverage exists for R23-R26.
- Accepted ToolBaker list/run/ribbon path has smoke evidence on R22, R26, and R27.
- Fresh-machine install testing is tracked in [docs/testing/fresh-install-checklist.md](docs/testing/fresh-install-checklist.md).

Treat it like serious open-source infrastructure: test it on your own environment before trusting it on production models.

---

## Project Structure

```text
rvt-mcp/
├── src/
│   ├── Bimwright.Rvt.sln         # Solution (server + 6 plugin shells)
│   ├── server/                   # Bimwright.Rvt.Server - .NET 8 global tool, stdio MCP
│   ├── shared/                   # Source glob shared by every plugin shell
│   │   ├── Handlers/             # One file per Revit command handler
│   │   ├── Commands/             # Revit ribbon commands
│   │   ├── ToolBaker/            # Baked-tool registry/runtime/policy
│   │   ├── Transport/            # TCP + Named Pipe abstraction
│   │   ├── Infrastructure/       # Dispatcher, schema validation, ExternalEvent marshal
│   │   └── Security/             # Auth token, redaction, secret masking
│   ├── plugin-r22/               # Revit 2022 shell - .NET 4.8, TCP
│   ├── plugin-r23/               # Revit 2023 shell - .NET 4.8, TCP
│   ├── plugin-r24/               # Revit 2024 shell - .NET 4.8, TCP
│   ├── plugin-r25/               # Revit 2025 shell - .NET 8, Named Pipe
│   ├── plugin-r26/               # Revit 2026 shell - .NET 8, Named Pipe
│   └── plugin-r27/               # Revit 2027 shell - .NET 10, Named Pipe
├── tests/                        # xUnit, tool snapshots, policy/privacy tests
├── benchmarks/                   # Weak-model accuracy harness
├── scripts/                      # install, uninstall, plugin ZIP staging
├── docs/                         # Architecture, roadmap, ToolBaker, testing notes
├── server.json                   # MCP registry manifest
├── smithery.yaml                 # Smithery directory manifest
├── AGENTS.md                     # Agent-led install guide for MCP clients
└── ARCHITECTURE.md               # Deep dive on runtime architecture
```

Six plugin shells compile from the same `src/shared/` glob. Year-specific `#if` fences handle Revit API drift such as `ElementId.IntegerValue` moving to `.Value` in newer versions.

---

## Install

### 1. Server - .NET tool

```bash
dotnet tool install -g Bimwright.Rvt.Server
bimwright-rvt --help
```

Requires .NET 8 SDK on the machine that runs the MCP client. If the tool is already installed, run `dotnet tool update -g Bimwright.Rvt.Server` instead.

### 2. Plugin - Revit add-in

Download the plugin installer bundle from [GitHub Releases](https://github.com/bimwright/rvt-mcp/releases/latest). The bundle is named `bimwright-rvt-plugin-<tag>.zip` and contains `install.ps1`, `uninstall-all.ps1`, and the six per-Revit plugin ZIPs.

```powershell
$tag = (Invoke-RestMethod https://api.github.com/repos/bimwright/rvt-mcp/releases/latest).tag_name
$zip = "$env:TEMP\bimwright-rvt-plugin-$tag.zip"
$dir = "$env:TEMP\bimwright-rvt-plugin-$tag"
Invoke-WebRequest "https://github.com/bimwright/rvt-mcp/releases/download/$tag/bimwright-rvt-plugin-$tag.zip" -OutFile $zip
Expand-Archive $zip -DestinationPath $dir -Force
Set-Location $dir

pwsh .\install.ps1 -SourceDir . -WhatIf    # preview without changes
pwsh .\install.ps1 -SourceDir .            # detects every installed Revit year
pwsh .\install.ps1 -Uninstall              # plugin-only removal
```

The script detects installed Revit versions via `HKLM:\SOFTWARE\Autodesk\Revit\` and copies the matching plugin into `%APPDATA%\Autodesk\Revit\Addins\<year>\Bimwright\`.

### 3. Wire up your MCP client

Add one entry per Revit year to your client's MCP config:

```json
{
  "mcpServers": {
    "bimwright-rvt-r23": {
      "command": "bimwright-rvt",
      "args": ["--target", "R23"]
    }
  }
}
```

Drop the `--target` flag and use one `bimwright-rvt` entry if you want auto-detect through discovery files in `%LOCALAPPDATA%\Bimwright\`.

### Scripted wire for OpenCode / Codex

```powershell
pwsh .\install.ps1 -SourceDir . -WireClient opencode -WhatIf
pwsh .\install.ps1 -SourceDir . -WireClient opencode

pwsh .\install.ps1 -SourceDir . -WireClient codex -WhatIf
pwsh .\install.ps1 -SourceDir . -WireClient codex
```

The script preserves non-bimwright entries and backs up the original config as `<file>.bimwright.bak` before writing.

Claude Code users can also paste the JSON snippet into a project `.mcp.json`. See [AGENTS.md](AGENTS.md) for exact paths and schemas for Claude Code, Claude Desktop, Cursor, Cline, VS Code Copilot, OpenCode, Codex, Gemini CLI, and Antigravity.

### 4. Verify

1. Open Revit 2022-2027 and a model.
2. Use the BIMwright ribbon panel to start/toggle the MCP plugin.
3. In the MCP client, run `tools/list`.
4. Call `get_current_view_info`.

Expected response shape:

```json
{ "viewName": "Level 1", "viewType": "FloorPlan", "levelName": "Level 1", "scale": 100 }
```

Do not claim an install is complete until the MCP client can list tools and call Revit successfully.

### Uninstall everything

To remove plugin, .NET global tool, host-config entries, discovery files, logs, and ToolBaker cache in one pass:

```powershell
pwsh .\uninstall-all.ps1 -WhatIf
pwsh .\uninstall-all.ps1
pwsh .\uninstall-all.ps1 -Yes
pwsh .\uninstall-all.ps1 -KeepLogs
```

`install.ps1 -Uninstall` remains the narrow plugin-only uninstall.

---

## Supported MCP Clients

| Client | Status | Notes |
|--------|--------|-------|
| Claude Code CLI | documented | project `.mcp.json` or global `~/.claude.json` |
| Claude Desktop | documented | `%APPDATA%\Claude\claude_desktop_config.json` |
| OpenCode | scripted | `install.ps1 -WireClient opencode` |
| Codex | scripted | `install.ps1 -WireClient codex` |
| Cursor | documented | project or user `mcp.json` |
| Cline (VS Code) | documented | Cline MCP settings JSON |
| VS Code Copilot | documented | native `servers` schema with `type: stdio` |
| Gemini CLI | documented | `gemini mcp add ...` or settings JSON |
| Antigravity | documented | Gemini/Antigravity MCP config JSON |

---

## Toolsets

The non-adaptive surface contains 32 tools across 11 toolsets. When adaptive bake is enabled, the surface expands to 35 tools.

Default-on toolsets: `query`, `create`, `view`, `meta`, `lint`.

Optional toolsets: `modify`, `delete`, `annotation`, `export`, `mep`, `toolbaker`.

Enable with `--toolsets query,create,modify,meta` or `--toolsets all`. Add `--read-only` to strip `create`, `modify`, and `delete` regardless of what was requested.

| Toolset | Tools | Default |
|---------|-------|---------|
| `query` | get current view, selected elements, available family types, material quantities, model stats, AI element filter | on |
| `create` | grid, level, room, line-based, point-based, surface-based element | on |
| `view` | create view, sheet layout, place view on sheet | on |
| `meta` | `show_message`, `switch_target`, `batch_execute`, usage stats | on |
| `lint` | view-naming pattern analysis, correction suggestions, firm-profile detect | on |
| `modify` | `operate_element`, `color_elements` | off |
| `delete` | `delete_element` | off |
| `annotation` | `tag_all_rooms`, `tag_all_walls` | off |
| `export` | `export_room_data` | off |
| `mep` | `detect_system_elements` | off |
| `toolbaker` | accepted-tool list/run, send-code, adaptive suggestion lifecycle | off |

### All Tools

| Toolset | Tool | Description |
|---|---|---|
| `query` | `get_current_view_info` | Active view metadata: type, level, scale, detail level. |
| `query` | `get_selected_elements` | Currently selected elements with id, name, category, type. |
| `query` | `get_available_family_types` | Family types in the project, filterable by category. |
| `query` | `ai_element_filter` | Filter by category and parameter/operator, values in mm. |
| `query` | `analyze_model_statistics` | Element counts grouped by category. |
| `query` | `get_material_quantities` | Area and volume totals for a category. |
| `create` | `create_line_based_element` | Wall or other line-based element. |
| `create` | `create_point_based_element` | Door, window, furniture or other point element. |
| `create` | `create_surface_based_element` | Floor or ceiling from a polyline. |
| `create` | `create_level` | Level at elevation in mm. |
| `create` | `create_grid` | Grid line between two points in mm. |
| `create` | `create_room` | Room at a point, bound by walls. |
| `modify` | `operate_element` | Select, hide, unhide, isolate, or set-color on IDs. |
| `modify` | `color_elements` | Color-code a category by parameter value. |
| `delete` | `delete_element` | Delete by ID list. Keep off unless explicitly needed. |
| `view` | `create_view` | Floor plan or 3D view. |
| `view` | `place_view_on_sheet` | Drop a view onto a new or existing sheet. |
| `view` | `analyze_sheet_layout` | Title block, viewport positions, and scales in mm. |
| `export` | `export_room_data` | Rooms with name, number, area, perimeter, level, volume. |
| `annotation` | `tag_all_walls` | Wall-type tags at midpoint; skips already tagged. |
| `annotation` | `tag_all_rooms` | Room tags at location point; skips already tagged. |
| `mep` | `detect_system_elements` | Traverse connectors from a seed and return system members. |
| `toolbaker` | `send_code_to_revit` | Run ad-hoc C# inside Revit after explicit opt-in and confirmation. |
| `toolbaker` | `list_baked_tools` | List accepted personal baked tools. |
| `toolbaker` | `run_baked_tool` | Invoke an accepted baked tool by name. |
| `toolbaker` | `list_bake_suggestions` | Adaptive bake only: list local suggestions. |
| `toolbaker` | `accept_bake_suggestion` | Adaptive bake only: accept and apply a local suggestion. |
| `toolbaker` | `dismiss_bake_suggestion` | Adaptive bake only: snooze or dismiss a local suggestion. |
| `meta` | `show_message` | TaskDialog inside Revit for connection tests or notifications. |
| `meta` | `switch_target` | Switch active Revit connection when multiple versions run. |
| `meta` | `batch_execute` | Run commands atomically in one `TransactionGroup`. |
| `meta` | `analyze_usage_patterns` | Local usage stats: tool calls, sessions, errors. |
| `lint` | `analyze_view_naming_patterns` | Infer dominant view-naming pattern and outliers. |
| `lint` | `suggest_view_name_corrections` | Propose corrected names for view outliers. |
| `lint` | `detect_firm_profile` | Fingerprint project naming against firm profiles. |

---

## Supported Revit Versions

| Revit | Target Framework | Transport | Notes |
|-------|------------------|-----------|-------|
| 2022 | .NET 4.8 | TCP | Accepted ToolBaker path smoke-tested |
| 2023 | .NET 4.8 | TCP | Core runtime coverage |
| 2024 | .NET 4.8 | TCP | Core runtime coverage |
| 2025 | .NET 8 (`net8.0-windows7.0`) | Named Pipe | Core runtime coverage |
| 2026 | .NET 8 (`net8.0-windows7.0`) | Named Pipe | Core runtime coverage; accepted ToolBaker path smoke-tested |
| 2027 | .NET 10 (`net10.0-windows7.0`) | Named Pipe | Accepted ToolBaker path smoke-tested |

Runtime behavior can still differ across Revit years because the Revit API changes. Custom baked C# tools should be treated as version-sensitive unless tested across the target years.

---

## Security And Privacy

Short version: your model stays on your machine.

- **Loopback by default.** TCP transport listens on `127.0.0.1`; Named Pipe is local-machine scoped.
- **Per-session token handshake.** Discovery files under `%LOCALAPPDATA%\Bimwright\` carry connection information and auth token.
- **Schema validation.** Malformed tool calls are rejected before command handlers run.
- **Path masking.** Errors returned to the model are sanitized to avoid leaking absolute paths.
- **ToolBaker opt-in.** Adaptive bake and send-code paths require explicit enablement; `send_code_to_revit` still requires Revit-side confirmation.
- **Local storage.** Usage events, bake database, logs, and accepted-tool metadata stay under local Bimwright storage.

See [SECURITY.md](SECURITY.md) for disclosure and threat-model details.

---

## Configuration

Three layers, later wins: JSON file, then env vars, then CLI args.

| Setting | CLI | Env | JSON key |
|---------|-----|-----|----------|
| Target Revit year | `--target R23` | `BIMWRIGHT_TARGET` | `target` |
| Toolsets | `--toolsets query,create` | `BIMWRIGHT_TOOLSETS` | `toolsets` |
| Read-only | `--read-only` | `BIMWRIGHT_READ_ONLY=1` | `readOnly` |
| Allow LAN bind | plugin-side only | `BIMWRIGHT_ALLOW_LAN_BIND=1` | `allowLanBind` |
| Allow ToolBaker when selected | `--enable-toolbaker` / `--disable-toolbaker` | `BIMWRIGHT_ENABLE_TOOLBAKER` | `enableToolbaker` |
| Enable adaptive bake suggestions | `--enable-adaptive-bake` / `--disable-adaptive-bake` | `BIMWRIGHT_ENABLE_ADAPTIVE_BAKE=1` | `enableAdaptiveBake` |
| Cache send-code bodies | `--cache-send-code-bodies` / `--no-cache-send-code-bodies` | `BIMWRIGHT_CACHE_SEND_CODE_BODIES=1` | `cacheSendCodeBodies` |

JSON file path: `%LOCALAPPDATA%\Bimwright\bimwright.config.json`.

---

## Development

```bash
dotnet test tests/Bimwright.Rvt.Tests/Bimwright.Rvt.Tests.csproj
dotnet build src/server/Bimwright.Rvt.Server.csproj -c Release
dotnet build src/plugin-r26/Bimwright.Rvt.Plugin.R26.csproj -c Release
```

Plugin projects auto-deploy after normal `Build`, copying into `%APPDATA%\Autodesk\Revit\Addins\<year>\Bimwright\`. Close Revit before building plugin projects because Revit locks loaded DLLs.

To stage plugin ZIPs for release:

```powershell
pwsh scripts/stage-plugin-zip.ps1 -Config Release
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for test strategy, tool-surface snapshot rules, and contribution notes.

---

## Documentation

- [AGENTS.md](AGENTS.md) - install and wiring guide for AI coding agents.
- [ARCHITECTURE.md](ARCHITECTURE.md) - process model, transport, threading, and DTO strategy.
- [docs/bake.md](docs/bake.md) - adaptive bake, privacy, accepted tools, and compatibility behavior.
- [docs/roadmap.md](docs/roadmap.md) - current hardening plan and deferred work.
- [docs/testing/fresh-install-checklist.md](docs/testing/fresh-install-checklist.md) - public install verification checklist.
- [benchmarks/README.md](benchmarks/README.md) - weak-model benchmark procedure.

---

## License

Apache-2.0. See [LICENSE](LICENSE).

Revit and Autodesk are registered trademarks of Autodesk, Inc. bimwright is an independent open-source project and is not affiliated with, sponsored by, or endorsed by Autodesk, Inc.

---

<p align="center">
  A <a href="https://github.com/bimwright">bimwright</a> project - built for practitioners who would rather automate the work than sell the mystique.
</p>
