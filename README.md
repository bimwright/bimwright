<!-- mcp-name: io.github.bimwright/rvt-mcp -->

<p align="center">
  <img src="https://raw.githubusercontent.com/bimwright/.github/master/assets/logos/rvt-mcp.png" alt="rvt-mcp" width="180" />
</p>

<h1 align="center">rvt-mcp</h1>

<p align="center">
  <a href="https://github.com/bimwright/rvt-mcp/actions/workflows/build.yml"><img src="https://github.com/bimwright/rvt-mcp/actions/workflows/build.yml/badge.svg" alt="build" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-Apache%202.0-blue.svg" alt="license" /></a>
  <a href="#supported-revit-versions"><img src="https://img.shields.io/badge/.NET-4.8%20%7C%208%20%7C%2010-512BD4" alt=".NET" /></a>
</p>

<p align="center">
  📖 English · <a href="README.vi.md">Tiếng Việt</a> · <a href="README.zh-CN.md">简体中文</a>
</p>

---

I built this because I got tired of clicking.

You know the scene: 5 PM, your BIM Manager messages *"rename everything to the new standard"* — L01 - Basement, L02 - Commercial, on and on. The model has a few thousand elements. Doing it by hand is out. Writing a Dynamo script takes half a day. That's the itch.

**rvt-mcp** is an add-in that sits next to Revit 2022–2027. You tell Claude (or Cursor, Codex, OpenCode — whatever agent you use) what you want done, it calls one of 32 local tools, and Revit runs the thing inside a single transaction. Not happy? **Ctrl+Z** — one step, everything rolls back.

No cloud. Nothing leaves your machine. Apache-2.0, pure C#.

> 🤖 **Using an AI agent?** Point it at [AGENTS.md](AGENTS.md) — it'll install the server, the plugin, and wire your MCP client, previewing each step before it touches anything. You still need Revit and .NET 8 SDK on the machine.

A few things I care about:

- **Every Revit year from 2022 to 2027.** One codebase, six plugin shells (.NET 4.8 → .NET 10). Compile gate is 6/6, core runtime coverage exists for R23–R26, and the accepted ToolBaker path has now been smoke-tested on R22, R26, and R27.
- **Pure C#, Apache-2.0.** No Node.js on the Revit machine. License is enterprise-safe, dependency graph audits cleanly.
- **Atomic batches.** `batch_execute` wraps a whole command list in one `TransactionGroup`. One undo step. If any command in the batch fails, the whole group rolls back — you never end up with a half-applied edit.
- **Weak models don't drown.** `--toolsets` + `--read-only` gate what the agent can see. A Haiku-sized model doesn't need to know about `delete_element` when you asked it to pull quantities.
- **Self-shaping toolkit, opt-in.** Adaptive bake is off by default. When enabled, repeated local usage can become a suggestion you accept into your own baked-tool registry. Accepted tools are available from the Revit ribbon and through `list_baked_tools` / `run_baked_tool`, with compatibility recorded per Revit version.

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

`rvt-mcp` is a **full C# MCP stack**. The MCP server, per-version Revit plugin shells, transport bridge, command handlers, DTO mapping, and ToolBaker pipeline are all written in C# using the official MCP C# SDK. There is no Node.js sidecar on the Revit machine — just .NET + Revit.

That matters because many MCP examples and servers in the ecosystem are built around a Node.js/TypeScript runtime. This project is not. For Revit shops, that means one language, one build chain, and a simpler story for debugging, auditing, and deployment. The version split is explicit at the edge: one thin plugin shell per Revit year, all compiling the same `src/shared/` source glob. See [ARCHITECTURE.md](ARCHITECTURE.md) for the full deep-dive.

---

## Project Structure

```
rvt-mcp/
├── src/
│   ├── Bimwright.Rvt.sln         # Solution (server + 6 plugin shells)
│   ├── server/                   # Bimwright.Rvt.Server — .NET 8 global tool, stdio MCP
│   ├── shared/                   # Source glob shared by every plugin shell
│   │   ├── Handlers/             # One file per tool (create_grid, send_code, …)
│   │   ├── Commands/             # Revit ribbon commands
│   │   ├── ToolBaker/            # Self-evolution engine (baked-tool registry/runtime)
│   │   ├── Transport/            # TCP (R22–R24) + Named Pipe (R25–R27) abstraction
│   │   ├── Infrastructure/       # CommandDispatcher, ExternalEvent marshalling
│   │   └── Security/             # Auth token, secret masking
│   ├── plugin-r22/               # Revit 2022 shell — .NET 4.8, TCP
│   ├── plugin-r23/               # Revit 2023 shell — .NET 4.8, TCP
│   ├── plugin-r24/               # Revit 2024 shell — .NET 4.8, TCP
│   ├── plugin-r25/               # Revit 2025 shell — .NET 8, Named Pipe
│   ├── plugin-r26/               # Revit 2026 shell — .NET 8, Named Pipe
│   └── plugin-r27/               # Revit 2027 shell — .NET 10, Named Pipe
├── tests/                        # Golden snapshot + Haiku benchmark + policy tests
├── benchmarks/                   # Weak-model (Haiku) accuracy harness
├── scripts/                      # stage-plugin-zip.ps1, install.ps1, uninstall-all.ps1
├── docs/                         # Brainstorms, reviews, ADRs
├── server.json                   # MCP registry manifest
├── smithery.yaml                 # Smithery aggregator manifest
├── AGENTS.md                     # Agent-led install guide (9 host clients)
└── ARCHITECTURE.md               # Deep-dive on threading + transport + DTOs
```

Six plugin shells compile from the same `src/shared/` glob — year-specific `#if` fences handle Revit API drift (`ElementId.IntegerValue` → `.Value` in R26+, WPF in R27).

---

## Install

### 1. Server — .NET tool

```bash
dotnet tool install -g Bimwright.Rvt.Server
bimwright-rvt --help
```

Requires .NET 8 SDK on the machine that runs the MCP client.

### 2. Plugin — Revit add-in

Download the latest release from [GitHub Releases](https://github.com/bimwright/rvt-mcp/releases/latest). Extract it and run:

```powershell
pwsh install.ps1            # detects every installed Revit year
pwsh install.ps1 -WhatIf    # preview without changes
pwsh install.ps1 -Uninstall # clean removal
```

The script detects installed Revit versions via `HKLM:\SOFTWARE\Autodesk\Revit\` and copies the matching plugin into `%APPDATA%\Autodesk\Revit\Addins\<year>\Bimwright\`.

### 3. Wire up your MCP client

Add one entry per Revit year to your client's MCP config (e.g. `.mcp.json`):

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

Drop the `--target` flag and Bimwright auto-detects the running Revit instance via discovery files in `%LOCALAPPDATA%\Bimwright\`.

#### Scripted wire for OpenCode / Codex

Instead of hand-editing `opencode.json` or `~/.codex/config.toml`, run:

```powershell
pwsh install.ps1 -WireClient opencode      # writes entries into %USERPROFILE%\.config\opencode\opencode.json
pwsh install.ps1 -WireClient codex         # writes entries into %USERPROFILE%\.codex\config.toml
pwsh install.ps1 -WireClient opencode -WhatIf   # preview
```

The script:

- Adds one `bimwright-rvt-r<YY>` entry per Revit year detected on this machine.
- Preserves every non-bimwright entry already in the config (merge-in-place).
- Backs up the original as `<file>.bimwright.bak` before writing.
- Does nothing if the host's config file is not present (host not installed).

Claude Code users: paste the JSON snippet above into your project's `.mcp.json` — the scripted path does not auto-edit project-level files.

### Uninstall everything

To remove plugin, .NET global tool, host-config entries, discovery files, and ToolBaker cache in one pass:

```powershell
pwsh uninstall-all.ps1 -WhatIf    # preview what will be removed
pwsh uninstall-all.ps1            # interactive confirm, then execute
pwsh uninstall-all.ps1 -Yes       # skip prompt
pwsh uninstall-all.ps1 -KeepLogs  # preserve *.log and *.jsonl files
```

Notes:

- This removes `Bimwright.Rvt.Server` from **every** project on the machine (`dotnet tool uninstall -g`), not just the current directory.
- Project-level `.mcp.json` files are not scanned — remove any `bimwright-rvt-*` entries in those manually.
- `install.ps1 -Uninstall` remains the narrow plugin-only uninstall (backward compatible).
- `-KeepLogs` preserves the `logs\` subdirectory if present, plus any root-level `*.log` / `*.jsonl` files inside `%LOCALAPPDATA%\Bimwright\`.

---

## Supported MCP clients

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

See [AGENTS.md](AGENTS.md) for exact config paths, schemas, dry-run expectations, and rollback notes for all supported hosts.

---

<!-- TODO v0.2: add demo.gif above this section -->
## Quickstart — 5 minutes to first tool call

1. `dotnet tool install -g Bimwright.Rvt.Server` + `pwsh install.ps1`.
2. Open Revit, go to **Add-Ins → BIMwright**, then click the MCP toggle button.
3. In your MCP client, run `tools/list` — you should see the default toolsets (`query`, `create`, `view`, `meta`, `lint`).
4. Call `get_current_view_info` — you'll get back a DTO like:
   ```json
   { "viewName": "Level 1", "viewType": "FloorPlan", "levelName": "Level 1", "scale": 100 }
   ```
5. Try something real:
   ```
   batch_execute({
     "commands": "[
       {\"command\":\"create_grid\",\"params\":{\"name\":\"A\",\"start\":[0,0],\"end\":[20000,0]}},
       {\"command\":\"create_level\",\"params\":{\"name\":\"L2\",\"elevation\":3000}}
     ]"
   })
   ```
   One undo step, both ops committed atomically.

---

## Toolsets

**32 tools across 11 toolsets.** Five toolsets are on by default (`query`, `create`, `view`, `meta`, `lint`); the rest opt in via `--toolsets` or config. When adaptive bake is enabled, three suggestion lifecycle tools are added to the `toolbaker` surface, bringing the full adaptive surface to 35 tools.

| Toolset | Tools | Default |
|---------|-------|---------|
| `query` | get current view, selected elements, available family types, material quantities, model stats, AI element filter | **on** |
| `create` | grid, level, room, line-based, point-based, surface-based element | **on** |
| `view` | create view, sheet layout, place view on sheet | **on** |
| `meta` | `show_message`, `switch_target`, `batch_execute`, usage stats | **on** |
| `lint` | view-naming pattern analysis, correction suggestions, firm-profile detect | **on** |
| `modify` | `operate_element`, `color_elements` | off |
| `delete` | `delete_element` | off |
| `annotation` | `tag_all_rooms`, `tag_all_walls` | off |
| `export` | `export_room_data` | off |
| `mep` | `detect_system_elements` | off |
| `toolbaker` | accepted-tool list/run, send-code, and adaptive suggestion lifecycle tools *(env/config opt-in)* | off |

Enable with `--toolsets query,create,modify,meta` or `--toolsets all`. Add `--read-only` to strip `create`/`modify`/`delete` regardless of what you requested.

### All tools

| Toolset | Tool | Description |
|---|---|---|
| `query` | `get_current_view_info` | Active view metadata (type, level, scale, detail level). |
| `query` | `get_selected_elements` | Currently selected elements with id, name, category, type. |
| `query` | `get_available_family_types` | Family types in the project, filterable by category. |
| `query` | `ai_element_filter` | Filter by category + parameter + operator (values in mm). |
| `query` | `analyze_model_statistics` | Element counts grouped by category. |
| `query` | `get_material_quantities` | Area (m²) and volume (m³) for a category. |
| `create` | `create_line_based_element` | Wall or other line-based element. |
| `create` | `create_point_based_element` | Door, window, furniture or other point element. |
| `create` | `create_surface_based_element` | Floor or ceiling from a polyline. |
| `create` | `create_level` | Level at elevation (mm). |
| `create` | `create_grid` | Grid line between two points (mm). |
| `create` | `create_room` | Room at a point, bound by walls. |
| `modify` | `operate_element` | Select, hide, unhide, isolate, or set-color on IDs. |
| `modify` | `color_elements` | Color-code a category by parameter value (auto palette). |
| `delete` | `delete_element` | Delete by ID list (destructive; not MCP-undoable). |
| `view` | `create_view` | Floor plan or 3D view. |
| `view` | `place_view_on_sheet` | Drop a view onto a new or existing sheet. |
| `view` | `analyze_sheet_layout` | Title block + viewport positions and scales (mm). |
| `export` | `export_room_data` | All rooms: name, number, area, perimeter, level, volume. |
| `annotation` | `tag_all_walls` | Wall-type tags at midpoint (skips already-tagged). |
| `annotation` | `tag_all_rooms` | Room tags at location point (skips already-tagged). |
| `mep` | `detect_system_elements` | Traverse connectors from a seed; return system members. |
| `toolbaker` | `send_code_to_revit` | Run ad-hoc C# body inside Revit after plugin-visible adaptive-bake opt-in. |
| `toolbaker` | `list_baked_tools` | List registered baked tools. |
| `toolbaker` | `run_baked_tool` | Invoke a baked tool by name. |
| `toolbaker` | `list_bake_suggestions` | Adaptive-bake only: list local suggestions. |
| `toolbaker` | `accept_bake_suggestion` | Adaptive-bake only: accept and apply a local suggestion. |
| `toolbaker` | `dismiss_bake_suggestion` | Adaptive-bake only: snooze or dismiss a local suggestion. |
| `meta` | `show_message` | TaskDialog inside Revit — connection test or user notification. |
| `meta` | `switch_target` | Switch the active Revit connection when multiple versions are running. |
| `meta` | `batch_execute` | Run N commands atomically in one TransactionGroup (single undo). |
| `meta` | `analyze_usage_patterns` | Usage stats: tool call counts, sessions, errors (last N days). |
| `lint` | `analyze_view_naming_patterns` | Infer dominant view-naming pattern + coverage + outliers. |
| `lint` | `suggest_view_name_corrections` | Propose corrected names for view outliers (inferred or profile-based). |
| `lint` | `detect_firm_profile` | Fingerprint project naming, match against firm-profile library. |

---

## Supported Revit versions

| Revit | Target Framework | Transport | Notes |
|-------|------------------|-----------|-------|
| 2022  | .NET 4.8 | TCP | Accepted ToolBaker path smoke-tested |
| 2023  | .NET 4.8 | TCP | Core runtime coverage |
| 2024  | .NET 4.8 | TCP | Core runtime coverage |
| 2025  | .NET 8 (`net8.0-windows7.0`) | Named Pipe | First .NET 8 shell; core runtime coverage |
| 2026  | .NET 8 (`net8.0-windows7.0`) | Named Pipe | Core runtime coverage; accepted ToolBaker path smoke-tested |
| 2027  | .NET 10 (`net10.0-windows7.0`) | Named Pipe | Accepted ToolBaker path smoke-tested |

Compile gate is 6/6. Core runtime coverage has passed on R23–R26, and manual smoke testing has now covered the accepted ToolBaker list/run/ribbon path on R22, R26, and R27. Treat that as practical runtime evidence, not a promise that every baked tool is portable across every Revit year; Revit API drift can still affect custom C# bodies.

---

## Security

Short version: **your model stays on your machine.** The MCP server runs locally, the plugin runs inside your Revit process, they talk over localhost. That's the whole picture.

Longer version, for the people reviewing this for their org:

- **Loopback bind by default.** TCP transport listens on `127.0.0.1` only. If you actually need LAN access, you have to set `BIMWRIGHT_ALLOW_LAN_BIND=1` — I'd rather you knew you were turning it on.
- **Per-session token handshake.** Every connection has to present a token written only to `%LOCALAPPDATA%\Bimwright\portR<nn>.txt`. Same-user attacker still wins (they can read the file). Anyone without read access to your user profile is out.
- **Schema validation before handlers run.** Malformed tool calls get an error-as-teacher envelope (`error`, `suggestion`, `hint`) instead of crashing something.
- **Path masking on exceptions.** If a handler throws, the MCP response and logs get sanitized — no absolute paths, no UNC shares, no user-home dirs.

Full threat model in [the security appendix](docs/roadmap.md#security).

---

## Configuration

Three layers, later wins: **JSON file → env vars → CLI args**.

| Setting | CLI | Env | JSON key |
|---------|-----|-----|----------|
| Target Revit year | `--target R23` | `BIMWRIGHT_TARGET` | `target` |
| Toolsets | `--toolsets query,create` | `BIMWRIGHT_TOOLSETS` | `toolsets` |
| Read-only | `--read-only` | `BIMWRIGHT_READ_ONLY=1` | `readOnly` |
| Allow LAN bind | — | `BIMWRIGHT_ALLOW_LAN_BIND=1` | `allowLanBind` |
| Allow ToolBaker when selected | `--enable-toolbaker` / `--disable-toolbaker` | `BIMWRIGHT_ENABLE_TOOLBAKER` | `enableToolbaker` |
| Enable adaptive bake suggestions | — | `BIMWRIGHT_ENABLE_ADAPTIVE_BAKE=1` | `enableAdaptiveBake` |
| Cache send-code bodies for clustering | — | `BIMWRIGHT_CACHE_SEND_CODE_BODIES=1` | `cacheSendCodeBodies` |

JSON file path: `%LOCALAPPDATA%\Bimwright\bimwright.config.json`.

---

## Self-shaping toolkit

Adaptive bake is the opt-in path for turning repeated local Revit workflows into personal tools. It is default OFF. Enable it only when you want Bimwright to record local usage patterns and propose bake suggestions.

Usage data stays on the machine under `%LOCALAPPDATA%\Bimwright\`. The server is the sole SQLite writer; the Revit plugin reads `bake.db` and owns only the runtime command cache and ribbon buttons. No usage collection endpoint is involved.

Accepted baked tools are available through the Revit ribbon and the `toolbaker` indirection tools: call `list_baked_tools` to inspect your accepted tools, then `run_baked_tool` with `name=<tool_name>` to execute one. In v0.3.x, baked tools do not appear as separate native MCP tools. The accepted-tool path has been smoke-tested across R22, R26, and R27, including cross-version compatibility metadata updates.

`bake_tool` was removed in v0.3.0. New bakes come from measured suggestions and explicit user acceptance through `accept_bake_suggestion`; legacy accepted tools remain callable through `list_baked_tools` / `run_baked_tool`.

See [docs/bake.md](docs/bake.md) for enabling, privacy, suggestion handling, archive behavior, and cross-Revit compatibility notes.

---

## Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) — process model, transport, multi-version strategy, ToolBaker pipeline.
- [CONTRIBUTING.md](CONTRIBUTING.md) — dev setup, build matrix, coding style.
- [docs/bake.md](docs/bake.md) — adaptive bake opt-in, privacy, suggestions, accepted tools, and compat behavior.
- [docs/roadmap.md](docs/roadmap.md) — v0.2 (MCP Resources, ToolBaker hardening), v0.3 (ToolBaker redesign, async job polling, aggregator listings), v1.0 (governance).

---

## License

Apache-2.0. See [LICENSE](LICENSE).

If you use it for something real, give the repo a star — helps others find it.

---

<p align="center">
  A <a href="https://github.com/bimwright">bimwright</a> project —
  <a href="https://github.com/bimwright"><img src="https://raw.githubusercontent.com/bimwright/.github/master/assets/logos/bimwright-logo.png" alt="bimwright" height="24" align="middle" /></a>
</p>
