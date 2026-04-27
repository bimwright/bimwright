<!-- agents-install-guide -->
<!-- mcp-name: io.github.bimwright/rvt-mcp -->

# AGENTS.md — Bimwright install guide for AI agents

This file is machine-readable install instructions for AI coding agents (Claude Code, Cursor, Cline, VS Code Copilot, Gemini CLI, Antigravity, OpenCode, Codex, and any other stdio MCP client). A user pointed you here because they want Bimwright (a Revit MCP server) wired up without hand-editing config files themselves.

**What you can do:**

- Install the server (.NET global tool).
- Deploy the plugin into `%APPDATA%\Autodesk\Revit\Addins\<year>\` for every installed Revit year.
- Wire any of the 9 supported MCP hosts listed below.
- Verify the handshake and roll back on failure.

**What you cannot do:**

- Install Revit, manage Revit licensing, or launch Revit for the first time. If Revit 2022–2027 is not installed, stop and tell the user.
- Install .NET 8 SDK or PowerShell — prompt the user to install these from the official sources.

---

## Rules for agents

**Read these before touching anything. They exist so Bimwright stays predictable, auditable, and reversible.**

1. **Preview every change.** Use `-WhatIf`, `--dry-run`, or a printed diff before any write. Tell the user the exact file path and the exact change.
2. **Three explicit approval gates — do not collapse without the user saying so:**
   - Before `dotnet tool install -g Bimwright.Rvt.Server`.
   - Before running `install.ps1` without `-WhatIf`.
   - Before editing any MCP host config file.
3. **Never bypass the Revit undo stack at runtime.** Bimwright's design guarantee is that every edit is reviewable and reversible. Don't advise users to work around transaction wrapping or disable `batch_execute` safety.
4. **On any failure, offer rollback.** Config edits are auto-backed up to `<file>.bimwright.bak`. The full stack comes off with `pwsh uninstall-all.ps1 -Yes`.
5. **Verify before claiming done.** After wiring, run `tools/list` in the host and confirm at least one `bimwright-rvt-*` entry responds, then call `get_current_view_info` with no args.

If the user explicitly says "skip the prompts, just install" — still do gate 1 (preview) and gate 5 (verify), but collapse gates 2 and 3 into a single upfront approval. **Never silently skip preview or verify.**

### Baked-tool routing

When the user's request may match a personal baked tool, call list_baked_tools first.
In v0.3.x baked tools never appear directly in native tools/list.
Run accepted tools through run_baked_tool name=<tool_name>.

---

## Prerequisites (check first, stop if any are missing)

| Requirement | How to check (PowerShell) | If missing |
|---|---|---|
| Windows | `$IsWindows` (pwsh 7+) or `[System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform('Windows')` | Stop — Revit is Windows-only. |
| Revit 2022–2027 | `Get-ChildItem 'HKLM:\SOFTWARE\Autodesk\Revit\' -ErrorAction SilentlyContinue` | Tell the user to install Revit. You cannot. |
| .NET 8 SDK | `dotnet --list-sdks \| Select-String '^8\.'` | Prompt: <https://dotnet.microsoft.com/download/dotnet/8.0>. |
| PowerShell ≥5.1 | `$PSVersionTable.PSVersion` | Prompt: <https://aka.ms/powershell>. |

If Revit is not running when the user first tries a tool call, that's fine — the server only needs Revit alive at tool-call time, not at install time.

---

## Step 1 — Install the server (.NET global tool)

```powershell
dotnet tool install -g Bimwright.Rvt.Server
bimwright-rvt --help
```

**Gate A.** This writes to `%USERPROFILE%\.dotnet\tools`. Preview the command to the user and get approval before running.

If the tool is already installed, use `dotnet tool update -g Bimwright.Rvt.Server` instead.

---

## Step 2 — Install the plugin (per-Revit-year add-in)

Download and extract the latest release:

```powershell
$tag = (Invoke-RestMethod https://api.github.com/repos/bimwright/rvt-mcp/releases/latest).tag_name
$zip = "$env:TEMP\bimwright-$tag.zip"
Invoke-WebRequest "https://github.com/bimwright/rvt-mcp/releases/download/$tag/bimwright-rvt-plugin-$tag.zip" -OutFile $zip
Expand-Archive $zip -DestinationPath "$env:TEMP\bimwright-$tag" -Force
```

Preview, then install:

```powershell
pwsh "$env:TEMP\bimwright-$tag\install.ps1" -WhatIf   # show diff — required
pwsh "$env:TEMP\bimwright-$tag\install.ps1"           # apply after approval
```

**Gate B.** `install.ps1` detects installed Revit years via `HKLM:\SOFTWARE\Autodesk\Revit\` and copies matching plugin DLLs into `%APPDATA%\Autodesk\Revit\Addins\<year>\Bimwright\`. Show the `-WhatIf` output and get approval before running without the flag.

The release asset file name may change. If the download 404s, fetch the release page and look for the plugin ZIP asset.

---

## Step 3 — Wire the MCP host

Pick the host the user is actually running. Before editing any config, **detect which Revit years are installed** — only emit entries for those years:

```powershell
$years = Get-ChildItem 'HKLM:\SOFTWARE\Autodesk\Revit\' -ErrorAction SilentlyContinue |
         ForEach-Object { if ($_.PSChildName -match '^(\d{4})$' -and [int]$Matches[1] -ge 2022 -and [int]$Matches[1] -le 2027) { $Matches[1] } }
```

### Canonical snippet (7 of 9 hosts)

Most hosts use `{ "mcpServers": { ... } }` with this per-server shape:

```json
{
  "mcpServers": {
    "bimwright-rvt-r24": {
      "command": "bimwright-rvt",
      "args": ["--target", "R24"]
    }
  }
}
```

Emit one entry per installed Revit year (`r22`, `r23`, `r24`, `r25`, `r26`, `r27`). Preserve every non-bimwright entry already in the config — merge, don't replace.

Drop the `--target` flag for a single auto-detecting entry (`bimwright-rvt`) if the user runs only one Revit version.

---

### 3.a — Claude Code CLI

**Config paths (pick one):**

- Project-level: `.mcp.json` in the user's current project root.
- User-level: `%USERPROFILE%\.claude.json` (merges into every project).

**Schema:** canonical `mcpServers`.

**Scripted alternative:**

```powershell
claude mcp add bimwright-rvt-r24 bimwright-rvt -- --target R24
```

Notes:

- Claude Code users historically paste JSON manually — the scripted `claude mcp add` command is the cleaner path when available.
- Don't overwrite `.mcp.json` if the user has other MCP servers in it. Merge in place.

---

### 3.b — Claude Desktop

**Config path:** `%APPDATA%\Claude\claude_desktop_config.json`.

**Schema:** canonical `mcpServers`.

**Preview required.** Read the file, show user the diff, then write. Back up to `claude_desktop_config.json.bimwright.bak` first.

**Restart required.** Claude Desktop reloads MCP config on app restart. Tell the user to quit and relaunch.

---

### 3.c — Cursor

**Config paths (pick one):**

- Project-level: `.cursor/mcp.json` in the user's current project root.
- User-level: `%USERPROFILE%\.cursor\mcp.json`.

**Schema:** canonical `mcpServers`.

**Restart recommended.** Cursor usually picks up changes on chat reload, but a full restart is safest.

---

### 3.d — Cline (VS Code extension)

**Config path:** `%APPDATA%\Code\User\globalStorage\saoudrizwan.claude-dev\cline_mcp_settings.json`.

**Schema:** canonical `mcpServers`.

**Scripted alternative:** Click "Configure MCP Servers" in the Cline pane — it opens this file directly. If the extension is not installed, prompt the user to install it from the VS Code marketplace.

---

### 3.e — VS Code Copilot (native MCP)

**⚠ Different schema.** VS Code Copilot uses `servers` (not `mcpServers`) and requires `"type": "stdio"`:

```json
{
  "servers": {
    "bimwright-rvt-r24": {
      "type": "stdio",
      "command": "bimwright-rvt",
      "args": ["--target", "R24"]
    }
  }
}
```

**Config paths (pick one):**

- Workspace: `.vscode/mcp.json` in the user's current project root.
- User-level: `mcp.json` in the VS Code user profile — open it via the command palette action **MCP: Open User Configuration**.

**Scripted alternative:** Run the command palette action **MCP: Add Server** for a guided flow.

---

### 3.f — OpenCode (scripted)

```powershell
pwsh install.ps1 -WireClient opencode -WhatIf   # preview
pwsh install.ps1 -WireClient opencode           # apply
```

Writes to `%USERPROFILE%\.config\opencode\opencode.json`. Preserves existing entries and backs up to `opencode.json.bimwright.bak`.

Prefer the scripted path over hand-editing. If the config file doesn't exist (host not installed), the script skips gracefully.

---

### 3.g — Codex (scripted)

```powershell
pwsh install.ps1 -WireClient codex -WhatIf
pwsh install.ps1 -WireClient codex
```

Writes to `%USERPROFILE%\.codex\config.toml`. Preserves existing entries and backs up to `config.toml.bimwright.bak`.

Codex uses TOML, not JSON — do not hand-edit unless you know TOML-array-of-tables. The scripted path handles the syntax.

---

### 3.h — Gemini CLI

**Scripted (preferred):**

```powershell
gemini mcp add bimwright-rvt-r24 bimwright-rvt -- --target R24
```

**Hand-edit fallback — config path:** `%USERPROFILE%\.gemini\settings.json`.

**Schema:** canonical `mcpServers`.

---

### 3.i — Antigravity (Google)

**Config path:** `%USERPROFILE%\.gemini\antigravity\mcp_config.json`.

**Schema:** canonical `mcpServers`.

**UI alternative:** Open MCP settings in Antigravity → **View raw config** → edit the JSON directly. Confirm with the user whether they prefer file-edit or UI-edit.

---

## Step 4 — Verify

1. **List tools.** Ask the host to call `tools/list` against the wired server. Expect at minimum: `get_current_view_info`, `analyze_model_statistics`, `batch_execute`, plus whatever toolsets the user enabled.

2. **Handshake call.** With Revit 2022–2027 running and a model open, call `get_current_view_info` with no args. A valid response looks like:

    ```json
    {
      "view_name": "Level 1",
      "view_type": "FloorPlan",
      "project_name": "Untitled"
    }
    ```

3. **Report.** Tell the user: the Revit year(s) wired, the host config file edited, and the `.bimwright.bak` backup location(s).

If any of these fail, **do not claim the install succeeded.** Go to rollback.

---

## Rollback

### Full uninstall (everything Bimwright touched)

```powershell
pwsh uninstall-all.ps1 -WhatIf    # preview what comes off
pwsh uninstall-all.ps1 -Yes       # apply without prompt
pwsh uninstall-all.ps1 -KeepLogs  # preserve logs
```

Removes: .NET global tool, plugin DLLs for every Revit year, discovery files at `%LOCALAPPDATA%\Bimwright\`, ToolBaker cache, and bimwright entries in scanned host configs.

**Scope caveat.** `uninstall-all.ps1` scans known host configs (OpenCode, Codex, Claude Desktop, Claude Code user-level) but does not scan project-level `.mcp.json` files. If you edited a project `.mcp.json`, restore it from `.bimwright.bak` manually or remove the entries by hand.

### Partial rollback

```powershell
pwsh install.ps1 -Uninstall   # plugin only (keeps server + host configs)
```

### Restore a single host config from backup

```powershell
Copy-Item 'path\to\config.ext.bimwright.bak' 'path\to\config.ext' -Force
```

---

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `bimwright-rvt: command not found` | `dotnet tool install` didn't put tools on PATH. | Add `%USERPROFILE%\.dotnet\tools` to PATH, restart shell. |
| `tools/list` returns 0 entries from bimwright | Host not reloaded, or Revit not running. | Restart host. Launch Revit. Retry. |
| `install.ps1` fails with "Revit running" | Revit has plugin DLLs locked. | Close every Revit window, retry. |
| Host config parse error after edit | Agent wrote invalid JSON/TOML. | Restore from `.bimwright.bak`, retry with a diff preview. |
| Server starts but no tools show up | Toolset filter hiding them. | Check `--toolsets` / `--read-only` flags on the host config entry. |

For anything not in this table, open an issue at <https://github.com/bimwright/rvt-mcp/issues> with the host name, Revit year, and the exact error.

---

## Honest scope

Bimwright handles `get_current_view_info`, `analyze_model_statistics`, `batch_execute`, and 25+ other tools across Revit 2022–2027. It does not handle installing Revit, licensing, cloud sync, or any Autodesk account operations. If the user asks for those, point them at <https://www.autodesk.com/support/revit>.

For extending the tool surface at runtime, see ToolBaker in the main [README.md](README.md).
