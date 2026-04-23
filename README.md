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

**rvt-mcp** is an add-in that sits next to Revit 2022–2027. You tell Claude (or Cursor, Codex, OpenCode — whatever agent you use) what you want done, it calls one of 29 tools, and Revit runs the thing inside a single transaction. Not happy? **Ctrl+Z** — one step, everything rolls back.

No cloud. Nothing leaves your machine. Apache-2.0, pure C#.

> 🤖 **Using an AI agent?** Point it at [AGENTS.md](AGENTS.md) — it'll install the server, the plugin, and wire your MCP client, previewing each step before it touches anything. You still need Revit and .NET 8 SDK on the machine.

A few things I care about:

- **Every Revit year from 2022 to 2027.** One codebase, six plugin shells (.NET 4.8 → .NET 10). R22 and R27 ship on compile-evidence — runtime-verified on R23–R26 only. The stack is identical, so honestly I'd be surprised if they broke, but I'm not going to claim what I haven't tested.
- **Pure C#, Apache-2.0.** No Node.js on the Revit machine. License is enterprise-safe, dependency graph audits cleanly.
- **Atomic batches.** `batch_execute` wraps a whole command list in one `TransactionGroup`. One undo step. If any command in the batch fails, the whole group rolls back — you never end up with a half-applied edit.
- **Weak models don't drown.** `--toolsets` + `--read-only` gate what the agent can see. A Haiku-sized model doesn't need to know about `delete_element` when you asked it to pull quantities.
- **ToolBaker, opt-in.** When the built-ins aren't enough, the model can write a new tool in C#, compile it through Roslyn, and register it live. Off by default — turn it on with `--enable-toolbaker` if you want it.

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
│   │   ├── ToolBaker/            # Self-evolution engine (bake_tool, run_baked_tool)
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

#### Scripted wire for OpenCode / Codex Desktop

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
| Claude Code CLI | ✅ verified | primary test target |
| Claude Desktop | ✅ verified | `.mcp.json` entry |
| Cursor | ⏳ pending verification | stdio; expected to work |
| Cline (VS Code) | ⏳ pending verification | stdio; expected to work |
| Other MCP clients | ⏳ pending | open an issue if you try one |

Broader client-compat matrix is on the v0.2 roadmap.

---

<!-- TODO v0.2: add demo.gif above this section -->
## Quickstart — 5 minutes to first tool call

1. `dotnet tool install -g Bimwright.Rvt.Server` + `pwsh install.ps1`.
2. Open Revit, go to **Add-Ins → BIMwright**, then click the MCP toggle button.
3. In your MCP client, run `tools/list` — you should see the default toolsets (`query`, `create`, `view`, `meta`).
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

**32 tools across 11 toolsets.** Five toolsets are on by default (`query`, `create`, `view`, `meta`, `lint`); the rest opt in via `--toolsets` or config.

| Toolset | Tools | Default |
|---------|-------|---------|
| `query` | get current view, selected elements, available family types, material quantities, model stats, AI element filter | **on** |
| `create` | grid, level, room, line-based, point-based, surface-based element | **on** |
| `view` | create view, get current view info, place view on sheet | **on** |
| `meta` | `show_message`, `batch_execute` | **on** |
| `lint` | view-naming pattern analysis, correction suggestions, firm-profile detect | **on** |
| `modify` | `operate_element`, `color_elements` | off |
| `delete` | `delete_element` | off |
| `annotation` | `tag_all_rooms`, `tag_all_walls` | off |
| `export` | `export_room_data` | off |
| `mep` | `detect_system_elements` | off |
| `toolbaker` | `bake_tool`, `list_baked_tools`, `run_baked_tool`, `send_code_to_revit` *(Debug only)* | off |

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
| `toolbaker` | `send_code_to_revit` | Run ad-hoc C# body inside Revit (last resort; Debug builds only). |
| `toolbaker` | `bake_tool` | Register a persistent tool from C# source. |
| `toolbaker` | `list_baked_tools` | List registered baked tools. |
| `toolbaker` | `run_baked_tool` | Invoke a baked tool by name. |
| `meta` | `show_message` | TaskDialog inside Revit — connection test or user notification. |
| `meta` | `batch_execute` | Run N commands atomically in one TransactionGroup (single undo). |
| `meta` | `analyze_usage_patterns` | SQLite stats: tool call counts, sessions, errors (last N days). |
| `lint` | `analyze_view_naming_patterns` | Infer dominant view-naming pattern + coverage + outliers. |
| `lint` | `suggest_view_name_corrections` | Propose corrected names for view outliers (inferred or profile-based). |
| `lint` | `detect_firm_profile` | Fingerprint project naming, match against firm-profile library. |

---

## Supported Revit versions

| Revit | Target Framework | Transport | Notes |
|-------|------------------|-----------|-------|
| 2022  | .NET 4.8 | TCP | |
| 2023  | .NET 4.8 | TCP | |
| 2024  | .NET 4.8 | TCP | |
| 2025  | .NET 8 (`net8.0-windows7.0`) | Named Pipe | First .NET 8 shell |
| 2026  | .NET 8 (`net8.0-windows7.0`) | Named Pipe | `ElementId.IntegerValue` removed — uses `RevitCompat.GetId()` |
| 2027  | .NET 10 (`net10.0-windows7.0`) | Named Pipe | Experimental — .NET 10 still preview |

Compile gate is 6/6; runtime verified 4/4 on R23–R26 (see `A1` in the commit history). R22 and R27 ship on compile-evidence — the stack is identical to R23 and R26 respectively, but I haven't run them myself so I'm not calling them verified.

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
| Enable ToolBaker | `--enable-toolbaker` / `--disable-toolbaker` | `BIMWRIGHT_ENABLE_TOOLBAKER` | `enableToolbaker` |

JSON file path: `%LOCALAPPDATA%\Bimwright\bimwright.config.json`.

---

## ToolBaker — cook your own tool when the built-ins aren't enough

Generic tools are generic. Your actual BIM work isn't — you've got your own naming conventions, your own QA pass, your own export pipeline. Every session, the agent stitches 8–10 primitive calls to do the same thing, and you pay tokens every time. That annoyed me enough to build a way out.

You describe the workflow once in plain language. The model writes a C# handler, `bake_tool` compiles it through Roslyn into an isolated `AssemblyLoadContext`, SQLite persists the bake. Next session — your workflow is one call.

Walkthrough:

1. Describe your real dataflow, e.g. *"schedule every door by fire rating, tag the failures, export to CSV"*.
2. Model writes a handler matching the `IRevitCommand` contract.
3. `bake_tool` compiles via Roslyn, links against the live Revit API, loads into a sandboxed assembly context.
4. SQLite persists. Auto-registers on every future session.
5. Call it like any built-in — same schema validation, same transaction safety.

Gated behind `--enable-toolbaker` (off by default). `send_code_to_revit` — the unsandboxed escape hatch — is Debug-build only, so a release binary physically can't execute arbitrary C#.

---

## Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) — process model, transport, multi-version strategy, ToolBaker pipeline.
- [CONTRIBUTING.md](CONTRIBUTING.md) — dev setup, build matrix, coding style.
- [docs/roadmap.md](docs/roadmap.md) — v0.2 (MCP Resources, ToolBaker hardening), v0.3 (async job polling, aggregator listings), v1.0 (governance).

---

## License

Apache-2.0. See [LICENSE](LICENSE).

If you use it for something real, give the repo a star — helps others find it.

---

<p align="center">
  A <a href="https://github.com/bimwright">bimwright</a> project —
  <a href="https://github.com/bimwright"><img src="https://raw.githubusercontent.com/bimwright/.github/master/assets/logos/bimwright-logo.png" alt="bimwright" height="24" align="middle" /></a>
</p>
