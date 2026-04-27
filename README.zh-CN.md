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
  📖 <a href="README.md">English</a> · <a href="README.vi.md">Tiếng Việt</a> · 简体中文
</p>

> 🇨🇳 简体中文镜像版。原文是 [`README.md`](README.md) — 如有冲突以 EN 为准，镜像版可能会延迟几天同步。

---

我做这个是因为我真的点不动了。

你肯定遇到过这种场景：下午 5 点，BIM 经理发消息过来 *"按新标准把所有东西重命名一下"* — L01 - 地下室、L02 - 商业层、一路下去。模型里几千个 element。手动点不现实。写个 Dynamo 脚本又要花半天。这就是那个痒点。

**rvt-mcp** 是一个和 Revit 2022–2027 一起跑的 add-in。你告诉 Claude（或者 Cursor、Codex、OpenCode — 哪个 agent 都行）你要做什么，它调用 29 个 tool 里的某一个，Revit 在一个 transaction 里把事情做完。不满意？**Ctrl+Z** — 一步，全部回滚。

不上云，什么都不离开你的机器。Apache-2.0，纯 C#。

> 🤖 **用 AI agent？** 让它读 [AGENTS.md](AGENTS.md) — agent 会帮你装 server、plugin、wire MCP client，每一步动手之前都会 preview 给你看。机器上还是要先装好 Revit 和 .NET 8 SDK。

我比较在意的几点：

- **Revit 2022 到 2027 全覆盖。** 一套代码，六个 plugin shell（.NET 4.8 → .NET 10）。R22 和 R27 是 compile-evidence ship 的 — 只在 R23–R26 上 runtime-verified 过 4/4。stack 跟 R23、R26 一样，说实话我挺有把握它能跑，但没跑过的我就不叫 verified。
- **纯 C#，Apache-2.0。** 跑 Revit 的机器上不需要装 Node.js。License 对企业友好，依赖树 audit 干净。
- **原子 batch。** `batch_execute` 把一串命令包在一个 `TransactionGroup` 里。一个 undo step。batch 里任何一个命令挂了，整个 batch 回滚 — 不会出现"改了一半"的状态。
- **弱模型不会被淹。** `--toolsets` + `--read-only` 控制 agent 看得到什么。Haiku 这种小模型不需要知道有 `delete_element` 这个 tool，如果你只是让它拉个 quantity。
- **ToolBaker，opt-in。** 内置 tool 不够用的时候，model 可以自己写一个 C# tool，通过 Roslyn 编译，再由 baked-tool runtime 加载。公开 toolset 不在默认 surface 里；需要时显式 request `toolbaker`。

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

`rvt-mcp` 是一个 **full C# MCP stack**。MCP server、按版本划分的 Revit plugin shell、transport bridge、command handlers、DTO mapping 和 ToolBaker pipeline 全部使用 C# 编写，并基于官方 MCP C# SDK。Revit 机器上没有 Node.js sidecar——只有 .NET + Revit。

这点很重要，因为生态里不少 MCP 示例和 server 都围绕 Node.js/TypeScript runtime。**rvt-mcp** 不是这样。对 Revit/BIM 团队来说，这意味着一种语言、一条 build chain，以及更简单的 debug / audit / deploy 流程。版本差异只放在边缘：每个 Revit 年份一个 thin plugin shell，所有共享逻辑都从 `src/shared/` 编译。更多细节见 [ARCHITECTURE.md](ARCHITECTURE.md)。

---

## 项目结构

```
rvt-mcp/
├── src/
│   ├── Bimwright.Rvt.sln         # 解决方案（server + 6 个 plugin shell）
│   ├── server/                   # Bimwright.Rvt.Server — .NET 8 global tool，stdio MCP
│   ├── shared/                   # 所有 plugin shell 共用的源码 glob
│   │   ├── Handlers/             # 每个 tool 一个文件（create_grid, send_code, …）
│   │   ├── Commands/             # Revit ribbon 命令
│   │   ├── ToolBaker/            # 自演化引擎（baked-tool registry/runtime）
│   │   ├── Transport/            # TCP (R22–R24) + Named Pipe (R25–R27) 抽象
│   │   ├── Infrastructure/       # CommandDispatcher、ExternalEvent marshalling
│   │   └── Security/             # Auth token、密钥掩码
│   ├── plugin-r22/               # Revit 2022 shell — .NET 4.8，TCP
│   ├── plugin-r23/               # Revit 2023 shell — .NET 4.8，TCP
│   ├── plugin-r24/               # Revit 2024 shell — .NET 4.8，TCP
│   ├── plugin-r25/               # Revit 2025 shell — .NET 8，Named Pipe
│   ├── plugin-r26/               # Revit 2026 shell — .NET 8，Named Pipe
│   └── plugin-r27/               # Revit 2027 shell — .NET 10，Named Pipe
├── tests/                        # Golden snapshot + Haiku benchmark + policy 测试
├── benchmarks/                   # 弱模型（Haiku）准确率 harness
├── scripts/                      # stage-plugin-zip.ps1、install.ps1、uninstall-all.ps1
├── docs/                         # Brainstorm、review、ADR
├── server.json                   # MCP registry manifest
├── smithery.yaml                 # Smithery aggregator manifest
├── AGENTS.md                     # Agent-led 安装指南（覆盖 9 个 host client）
└── ARCHITECTURE.md               # threading + transport + DTO 深入说明
```

六个 plugin shell 从同一个 `src/shared/` glob 编译 — 按年份的 `#if` fence 处理 Revit API 变化（R26+ 起 `ElementId.IntegerValue` → `.Value`，R27 加 WPF）。

---

## Install

### 1. Server — .NET tool

```bash
dotnet tool install -g Bimwright.Rvt.Server
bimwright-rvt --help
```

跑 MCP client 的机器上要有 .NET 8 SDK。如果 tool 已经安装过，请运行 `dotnet tool update -g Bimwright.Rvt.Server`。

### 2. Plugin — Revit add-in

从 [GitHub Releases](https://github.com/bimwright/rvt-mcp/releases/latest) 下载 plugin installer bundle。Bundle 名为 `bimwright-rvt-plugin-<tag>.zip`，里面包含 `install.ps1`、`uninstall-all.ps1` 和 6 个按 Revit 年份拆分的 plugin ZIP。

```powershell
$tag = (Invoke-RestMethod https://api.github.com/repos/bimwright/rvt-mcp/releases/latest).tag_name
$zip = "$env:TEMP\bimwright-rvt-plugin-$tag.zip"
$dir = "$env:TEMP\bimwright-rvt-plugin-$tag"
Invoke-WebRequest "https://github.com/bimwright/rvt-mcp/releases/download/$tag/bimwright-rvt-plugin-$tag.zip" -OutFile $zip
Expand-Archive $zip -DestinationPath $dir -Force
Set-Location $dir

pwsh .\install.ps1 -SourceDir . -WhatIf    # 预览，不改任何东西
pwsh .\install.ps1 -SourceDir .            # 自动识别机器上所有 Revit 年份
pwsh .\install.ps1 -Uninstall              # 只卸载 plugin
```

脚本通过 `HKLM:\SOFTWARE\Autodesk\Revit\` 检测已安装的 Revit 版本，把对应的 plugin 复制到 `%APPDATA%\Autodesk\Revit\Addins\<year>\Bimwright\`。

### 3. Wire 到你的 MCP client

每个 Revit 年份往 client 的 MCP 配置（比如 `.mcp.json`）里加一个 entry：

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

不带 `--target` 的话 Bimwright 会通过 `%LOCALAPPDATA%\Bimwright\` 里的 discovery file 自动识别当前在跑的 Revit。

#### OpenCode / Codex Desktop 自动 wire

懒得手动改 `opencode.json` 或 `~/.codex/config.toml`：

```powershell
pwsh .\install.ps1 -SourceDir . -WireClient opencode      # 写入 %USERPROFILE%\.config\opencode\opencode.json
pwsh .\install.ps1 -SourceDir . -WireClient codex         # 写入 %USERPROFILE%\.codex\config.toml
pwsh .\install.ps1 -SourceDir . -WireClient opencode -WhatIf   # 预览
```

脚本会：

- 为机器上检测到的每个 Revit 年份加一个 `bimwright-rvt-r<YY>` entry。
- 保留 config 里所有非 bimwright 的 entry（原地 merge）。
- 改之前先备份原文件成 `<file>.bimwright.bak`。
- 如果 host 的 config 不存在（host 没装）就什么也不做。

Claude Code 用户：把上面那段 JSON 贴到项目的 `.mcp.json` 里 — 脚本不会动项目级别的文件。

### 全部卸载

一次性清掉 plugin、.NET global tool、host 配置里的 entry、discovery file 和 ToolBaker cache：

```powershell
pwsh .\uninstall-all.ps1 -WhatIf    # 预览会删哪些
pwsh .\uninstall-all.ps1            # 交互式确认后执行
pwsh .\uninstall-all.ps1 -Yes       # 跳过确认
pwsh .\uninstall-all.ps1 -KeepLogs  # 保留 *.log 和 *.jsonl
```

注意：

- 会从**整台机器**卸载 `Bimwright.Rvt.Server`（`dotnet tool uninstall -g`），不只是当前目录。
- 项目级别的 `.mcp.json` 不会被扫 — 里面的 `bimwright-rvt-*` entry 请手动删。
- `install.ps1 -Uninstall` 还是只卸 plugin（向后兼容）。
- `-KeepLogs` 保留 `logs\` 目录（如果有）以及 `%LOCALAPPDATA%\Bimwright\` 根下的 `*.log` / `*.jsonl` 文件。

---

## Supported MCP clients

| Client | 状态 | 备注 |
|--------|------|------|
| Claude Code CLI | documented | 项目 `.mcp.json` 或全局 `~/.claude.json` |
| Claude Desktop | documented | `%APPDATA%\Claude\claude_desktop_config.json` |
| OpenCode | scripted | `install.ps1 -WireClient opencode` |
| Codex | scripted | `install.ps1 -WireClient codex` |
| Cursor | documented | 项目或用户级 `mcp.json` |
| Cline (VS Code) | documented | Cline MCP settings JSON |
| VS Code Copilot | documented | 原生 `servers` schema，带 `type: stdio` |
| Gemini CLI | documented | `gemini mcp add ...` 或 settings JSON |
| Antigravity | documented | Gemini/Antigravity MCP config JSON |

见 [AGENTS.md](AGENTS.md)：里面有每个 host 的 config 路径、schema、dry-run 和 rollback 说明。

---

## Quickstart — 5 分钟内跑通第一个 tool call

1. 安装或更新 `Bimwright.Rvt.Server`，下载 plugin bundle，然后在解压目录里运行 `install.ps1`。
2. 打开 Revit，进入 **Add-Ins → BIMwright**，然后点击 MCP toggle 按钮。
3. 在 MCP client 里跑 `tools/list` — 会看到默认的 toolset（`query`、`create`、`view`、`meta`、`lint`）。
4. Call `get_current_view_info` — 会拿到一个 DTO：
   ```json
   { "viewName": "Level 1", "viewType": "FloorPlan", "levelName": "Level 1", "scale": 100 }
   ```
5. 来点实际的：
   ```
   batch_execute({
     "commands": "[
       {\"command\":\"create_grid\",\"params\":{\"name\":\"A\",\"start\":[0,0],\"end\":[20000,0]}},
       {\"command\":\"create_level\",\"params\":{\"name\":\"L2\",\"elevation\":3000}}
     ]"
   })
   ```
   一步 undo，两个 op 原子提交。

---

## Toolsets

**32 个 tool 分成 11 个 toolset。** 5 个 toolset 默认开（`query`、`create`、`view`、`meta`、`lint`），其他通过 `--toolsets` 或 config opt-in。

| Toolset | Tools | 默认 |
|---------|-------|------|
| `query` | get current view, selected elements, available family types, material quantities, model stats, AI element filter | **on** |
| `create` | grid, level, room, line-based, point-based, surface-based element | **on** |
| `view` | create view, get current view info, place view on sheet | **on** |
| `meta` | `show_message`, `batch_execute` | **on** |
| `lint` | 视图命名模式分析、修正建议、firm-profile 检测 | **开启** |
| `modify` | `operate_element`, `color_elements` | off |
| `delete` | `delete_element` | off |
| `annotation` | `tag_all_rooms`, `tag_all_walls` | off |
| `export` | `export_room_data` | off |
| `mep` | `detect_system_elements` | off |
| `toolbaker` | `list_baked_tools`, `run_baked_tool`, `send_code_to_revit` *(需要 plugin env/config opt-in)* | off |

用 `--toolsets query,create,modify,meta` 或 `--toolsets all` 打开。加 `--read-only` 会不管你 request 了什么都把 `create`/`modify`/`delete` 剥掉。

### 完整工具列表

| Toolset | Tool | 说明 |
|---|---|---|
| `query` | `get_current_view_info` | 当前 view 的 metadata（类型、level、scale、detail level）。 |
| `query` | `get_selected_elements` | 当前选中的 element，带 id、name、category、type。 |
| `query` | `get_available_family_types` | project 里的 family type，按 category 过滤。 |
| `query` | `ai_element_filter` | 按 category + parameter + operator 过滤（值用 mm）。 |
| `query` | `analyze_model_statistics` | 按 category 分组的 element 计数。 |
| `query` | `get_material_quantities` | 某个 category 的 area (m²) 和 volume (m³)。 |
| `create` | `create_line_based_element` | Wall 或其他 line-based element。 |
| `create` | `create_point_based_element` | Door、window、furniture 或其他 point element。 |
| `create` | `create_surface_based_element` | 从 polyline 建 floor 或 ceiling。 |
| `create` | `create_level` | 在指定 elevation（mm）建 level。 |
| `create` | `create_grid` | 两点之间建一条 grid line（mm）。 |
| `create` | `create_room` | 在某个点建 room，被 wall 围起来。 |
| `modify` | `operate_element` | 对 ID 列表做 select、hide、unhide、isolate、set-color。 |
| `modify` | `color_elements` | 按 parameter 值给某个 category 上色（auto palette）。 |
| `delete` | `delete_element` | 按 ID 列表删除（destructive；MCP 侧没法 undo）。 |
| `view` | `create_view` | 平面图或 3D view。 |
| `view` | `place_view_on_sheet` | 把 view 放到新的或已有的 sheet 上。 |
| `view` | `analyze_sheet_layout` | 标题栏 + viewport 位置/比例（mm）。 |
| `export` | `export_room_data` | 全部 room：name、number、area、perimeter、level、volume。 |
| `annotation` | `tag_all_walls` | 在 midpoint 给墙打 wall-type tag（跳过已有 tag 的）。 |
| `annotation` | `tag_all_rooms` | 在 location point 打 room tag（跳过已有 tag 的）。 |
| `mep` | `detect_system_elements` | 从一个 seed 沿 connector 遍历，返回 system 成员。 |
| `toolbaker` | `send_code_to_revit` | plugin 可见的 adaptive bake opt-in 打开后，在 Revit 里跑临时 C# 代码。 |
| `toolbaker` | `list_baked_tools` | 列出已注册的 baked tool。 |
| `toolbaker` | `run_baked_tool` | 按名字调用 baked tool。 |
| `meta` | `show_message` | 在 Revit 里弹 TaskDialog — 测连接、通知用户。 |
| `meta` | `batch_execute` | 在一个 TransactionGroup 里原子执行 N 个命令（一次 undo）。 |
| `meta` | `analyze_usage_patterns` | Usage stats：tool 调用次数、session、error（最近 N 天）。 |
| `lint` | `analyze_view_naming_patterns` | 推断视图命名主导模式 + 覆盖率 + 偏离项。 |
| `lint` | `suggest_view_name_corrections` | 为偏离的视图名称建议修正（inferred 或 profile）。 |
| `lint` | `detect_firm_profile` | 指纹项目命名，匹配 firm-profile 库。 |

---

## Supported Revit versions

| Revit | Target Framework | Transport | 备注 |
|-------|------------------|-----------|------|
| 2022  | .NET 4.8 | TCP | |
| 2023  | .NET 4.8 | TCP | |
| 2024  | .NET 4.8 | TCP | |
| 2025  | .NET 8 (`net8.0-windows7.0`) | Named Pipe | 第一个 .NET 8 shell |
| 2026  | .NET 8 (`net8.0-windows7.0`) | Named Pipe | `ElementId.IntegerValue` 被移除 — 用 `RevitCompat.GetId()` |
| 2027  | .NET 10 (`net10.0-windows7.0`) | Named Pipe | Experimental — .NET 10 还在 preview |

Compile gate 6/6；R23–R26 上 runtime verified 4/4（看 commit 历史里的 `A1`）。R22 和 R27 是 compile-evidence ship — stack 和 R23、R26 一样，但我自己没跑过，所以不叫它 verified。

---

## Security

简单说：**你的模型文件不离开你的机器。** MCP server 跑在本地，plugin 跑在 Revit 进程里，两边通过 localhost 说话。就这么回事。

详细说，给帮组织做审查的人：

- **默认 loopback bind。** TCP transport 只监听 `127.0.0.1`。真的需要 LAN 访问的话要设 `BIMWRIGHT_ALLOW_LAN_BIND=1` — 我希望你是明确知道自己在打开它。
- **Per-session token 握手。** 每个连接都要出示一个写在 `%LOCALAPPDATA%\Bimwright\portR<nn>.txt` 里的 token。同用户的攻击者还是赢（他能读到这个文件）。没有你 user profile 读权限的人进不来。
- **Handler 跑之前先 validate schema。** Malformed 的 tool call 会拿到一个 error-as-teacher envelope（`error`、`suggestion`、`hint`），不会直接崩。
- **Exception 里 mask 路径。** Handler 抛异常的话，MCP response 和 log 会被 sanitize — 不会漏绝对路径、UNC share、user home 目录。

完整威胁模型在 [security appendix](docs/roadmap.md#security)。

---

## Configuration

三层，后者赢：**JSON file → env vars → CLI args**。

| Setting | CLI | Env | JSON key |
|---------|-----|-----|----------|
| Target Revit 年份 | `--target R23` | `BIMWRIGHT_TARGET` | `target` |
| Toolsets | `--toolsets query,create` | `BIMWRIGHT_TOOLSETS` | `toolsets` |
| Read-only | `--read-only` | `BIMWRIGHT_READ_ONLY=1` | `readOnly` |
| 允许 LAN bind | — | `BIMWRIGHT_ALLOW_LAN_BIND=1` | `allowLanBind` |
| 启用 ToolBaker | `--enable-toolbaker` / `--disable-toolbaker` | `BIMWRIGHT_ENABLE_TOOLBAKER` | `enableToolbaker` |

JSON 文件路径：`%LOCALAPPDATA%\Bimwright\bimwright.config.json`。

---

## ToolBaker — 内置 tool 不够用的时候，自己烤一个

通用 tool 就是通用。你的真实 BIM 工作不是 — 你有自己的命名规则，自己的 QA 步骤，自己的 export pipeline。每个 session，agent 要 stitch 8–10 个 primitive call 来做同一件事，每次都要烧 token。这事烦得够我做一条出路。

你用一句人话描述 workflow。经过 review 的 C# handler 可以持久化到本地 baked-tool registry，并由 baked-tool runtime 加载。下个 session — 这个 workflow 就是一个 call。

流程：

1. 描述真实的 dataflow，比如 *"按 fire rating schedule 所有门，标出 fail 的，导出 CSV"*。
2. Model 按 `IRevitCommand` contract 写 handler，供你 review。
3. 已接受的 handler source 通过 Roslyn 编译，link 到 live Revit API，然后 load 到 baked-command runtime。
4. 本地 registry 持久化已接受的 handler。之后的 session 会重新加载。
5. 通过 `run_baked_tool` 调用 — 同样的 schema validation，同样的 transaction safety。

用 `--toolsets toolbaker` 或 `--toolsets all` 暴露 `run_baked_tool`。`send_code_to_revit` — 不 sandbox 的 escape hatch — 需要在 plugin 侧 opt in：Revit 进程环境里设置 `BIMWRIGHT_ENABLE_ADAPTIVE_BAKE=1`，或在 `%LOCALAPPDATA%\Bimwright\bimwright.config.json` 里设置 `"enableAdaptiveBake": true`。

---

## Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) — 进程模型、transport、多版本策略、ToolBaker pipeline。
- [CONTRIBUTING.md](CONTRIBUTING.md) — 开发环境、build matrix、代码风格。
- [docs/roadmap.md](docs/roadmap.md) — v0.2（MCP Resources、ToolBaker 加固）、v0.3（async job polling、aggregator listings）、v1.0（治理）。

---

## License

Apache-2.0。看 [LICENSE](LICENSE)。

如果真的用上了，给 repo 点个 star — 让更多人能找到。

---

<p align="center">
  一个 <a href="https://github.com/bimwright">bimwright</a> 项目 —
  <a href="https://github.com/bimwright"><img src="https://raw.githubusercontent.com/bimwright/.github/master/assets/logos/bimwright-logo.png" alt="bimwright" height="24" align="middle" /></a>
</p>
