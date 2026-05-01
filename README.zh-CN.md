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
  <a href="README.md">English</a> · <a href="README.vi.md">Tiếng Việt</a> · 简体中文
</p>

---

## Revit 自动化不应该止步于“我不会写代码”

在 AI agent 出现之前，很多 BIM 用户就已经想做同一件事：让 Revit 更快，减少重复点击，并把软件调成真正适合自己工作的样子。

难点从来不是想法。难点是把想法变成工具。

哪怕只是做一个小型 Revit add-in，BIM 从业者通常也要：

- 把 input 和 output 定义到软件能理解的程度。
- 思考算法、边界情况、parameter、category、filter、单位，以及 Revit API 的限制。
- 先用 Dynamo prototype，可能再转 Python，最后如果要稳定使用，又要重写成 C#。
- 把结果打包成 add-in，处理 dependency、安装路径、`.addin` manifest、Revit 版本差异和 ribbon button。

这对学习建筑、结构、机电、算量或 BIM coordination 的人来说太重了。他们不是软件工程师。

过去的选择都很贵，只是贵的方式不同：

- 花几个月甚至几年学到足够维护自己工具的编程能力。
- 付钱请别人写 custom add-in。
- 买现成 add-in，然后让自己的 workflow 去适应 vendor 的假设。
- 因为自动化门槛太高，继续手工做。

`rvt-mcp` 的目标是压缩这个循环。

它给 AI agent 一条安全的本地桥梁进入 Revit，再让重复出现的 workflow 通过 ToolBaker 演化成个人工具。目标不是做一个适合所有人的万能 add-in。Revit 服务的专业、公司、标准和习惯太多，没有一个通用工具能跟上所有人。目标是让每个实践者都能长出适合自己工作方式的工具箱。

个人自动化就应该是个人化的。

---

## rvt-mcp 是什么

`rvt-mcp` 是面向 Autodesk Revit 2022-2027 的本地 MCP gateway。

它由两部分组成：

- `Bimwright.Rvt.Server`：.NET 8 MCP server，由 Claude、Cursor、Codex、OpenCode、Cline、VS Code Copilot 或其他 stdio MCP client 启动。
- `Bimwright.Rvt.Plugin`：每个 Revit 年份一个 add-in shell，运行在 Revit 内部，并在 Revit UI thread 上执行命令。

Agent 说 MCP。Server 通过 localhost TCP 或 Named Pipe 和 plugin 通信。Plugin 和 Revit API 通信。

你的模型留在你的机器上。

---

## 为什么它重要

AI agent 让 BIM 用户可以描述意图，而不是手写代码。但只有意图还不够。Revit 自动化仍然需要一个 runtime，理解 transaction、parameter、单位、selection、model state、version drift、安全和 rollback。

`rvt-mcp` 就是这个 runtime。

它围绕四个原则设计：

- **Local first.** 不需要 cloud bridge。Revit、plugin、MCP server、logs 和 ToolBaker storage 都在用户机器上。
- **Reversible by default.** 会修改模型的 workflow 可以通过 `batch_execute` 执行，把多个 command 包在一个 Revit `TransactionGroup` 里，一次 undo 可以回滚整个 batch。
- **Progressively exposed.** Toolsets 和 `--read-only` 控制 agent 能看到什么、能做什么。弱模型或窄任务不需要看到 destructive tools。
- **Personal over generic.** Adaptive ToolBaker 可以观察重复的本地 workflow，提出个人工具建议，并把 accepted tools 暴露给 MCP 和 Revit ribbon。

这不是 black-box demo，也不是 courseware。它是 Apache-2.0 的公开源码。所有 claim 都应该通过 build、test、运行和读源码来验证。

---

## ToolBaker 循环

大多数 Revit 自动化死在“好想法”和“可用 add-in”之间。

ToolBaker 是从 agent-assisted workflow 走向个人工具的路径：

1. 使用现有 MCP tools 在 Revit 里 query、create、lint、inspect 或 batch operations。
2. 当需要更高级的 automation 时，opt in `toolbaker`，启用 adaptive bake，然后在明确 Revit confirmation 后使用 `send_code_to_revit`。
3. 如果 adaptive bake 已启用，重复的本地 usage 会记录在 `%LOCALAPPDATA%\Bimwright\` 下。
4. 重复 pattern 会变成 suggestion，可通过 `list_bake_suggestions` 查看。
5. 你显式通过 `accept_bake_suggestion` 接受 suggestion，包括 tool name、schema 和 output choice。
6. Accepted tools 可以通过 `list_baked_tools` / `run_baked_tool` 调用，也会进入 Revit ribbon runtime cache。

Adaptive bake 默认关闭。它适合希望用自己的本地使用数据塑造自己工具的人。

---

## 架构

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

`rvt-mcp` 是完整 C# MCP stack。MCP server、按 Revit 年份拆分的 plugin shells、transport bridge、command handlers、DTO mapping 和 ToolBaker pipeline 都用 C# 编写，并使用官方 MCP C# SDK。

Revit 机器上没有 Node.js sidecar。

版本差异明确放在边界：每个 Revit 年份一个薄 plugin shell，共同 compile `src/shared/`。详见 [ARCHITECTURE.md](ARCHITECTURE.md)，包括 threading、transport、DTO 和 ToolBaker 细节。

---

## 当前状态

`rvt-mcp` 可用，但仍然年轻。

- Compile gate 覆盖 Revit R22-R27 plugin shells。
- Unit tests 覆盖 pure .NET logic、tool-surface snapshots、ToolBaker storage/policy paths、config、logging、privacy 和 batching behavior。
- R23-R26 有 core runtime coverage。
- Accepted ToolBaker list/run/ribbon path 在 R22、R26、R27 有 smoke evidence。
- Fresh-machine install testing 在 [docs/testing/fresh-install-checklist.md](docs/testing/fresh-install-checklist.md) 跟踪。

请把它当作严肃的 open-source infrastructure：在自己的环境测试后，再用于 production models。

---

## 项目结构

```text
rvt-mcp/
├── src/
│   ├── Bimwright.Rvt.sln         # Solution (server + 6 plugin shells)
│   ├── server/                   # Bimwright.Rvt.Server - .NET 8 global tool, stdio MCP
│   ├── shared/                   # 所有 plugin shell 共享的 source glob
│   │   ├── Handlers/             # 每个 Revit command handler 一个文件
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
└── ARCHITECTURE.md               # Runtime architecture deep dive
```

六个 plugin shells 都从同一份 `src/shared/` compile。按年份的 `#if` 处理 Revit API drift，例如新版本中 `ElementId.IntegerValue` 迁移到 `.Value`。

---

## 安装

### 1. Server - .NET tool

```bash
dotnet tool install -g Bimwright.Rvt.Server
bimwright-rvt --help
```

运行 MCP client 的机器需要 .NET 8 SDK。如果 tool 已安装，请运行 `dotnet tool update -g Bimwright.Rvt.Server`。

### 2. Plugin - Revit add-in

从 [GitHub Releases](https://github.com/bimwright/rvt-mcp/releases/latest) 下载 plugin installer bundle。Bundle 名为 `bimwright-rvt-plugin-<tag>.zip`，包含 `install.ps1`、`uninstall-all.ps1` 和 6 个按 Revit 年份拆分的 plugin ZIP。

```powershell
$tag = (Invoke-RestMethod https://api.github.com/repos/bimwright/rvt-mcp/releases/latest).tag_name
$zip = "$env:TEMP\bimwright-rvt-plugin-$tag.zip"
$dir = "$env:TEMP\bimwright-rvt-plugin-$tag"
Invoke-WebRequest "https://github.com/bimwright/rvt-mcp/releases/download/$tag/bimwright-rvt-plugin-$tag.zip" -OutFile $zip
Expand-Archive $zip -DestinationPath $dir -Force
Set-Location $dir

pwsh .\install.ps1 -SourceDir . -WhatIf    # preview，不做修改
pwsh .\install.ps1 -SourceDir .            # 检测所有已安装 Revit 年份
pwsh .\install.ps1 -Uninstall              # 仅卸载 plugin
```

脚本通过 `HKLM:\SOFTWARE\Autodesk\Revit\` 检测已安装 Revit 版本，并把对应 plugin 复制到 `%APPDATA%\Autodesk\Revit\Addins\<year>\Bimwright\`。

### 3. Wire MCP client

在 MCP client config 中为每个 Revit 年份添加一个 entry：

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

如果希望通过 `%LOCALAPPDATA%\Bimwright\` 里的 discovery files auto-detect，可以去掉 `--target`，只保留一个 `bimwright-rvt` entry。

### OpenCode / Codex scripted wire

```powershell
pwsh .\install.ps1 -SourceDir . -WireClient opencode -WhatIf
pwsh .\install.ps1 -SourceDir . -WireClient opencode

pwsh .\install.ps1 -SourceDir . -WireClient codex -WhatIf
pwsh .\install.ps1 -SourceDir . -WireClient codex
```

脚本保留非 bimwright entries，并在写入前把原 config 备份为 `<file>.bimwright.bak`。

Claude Code 用户也可以把 JSON snippet 粘贴到项目 `.mcp.json`。详见 [AGENTS.md](AGENTS.md)，里面有 Claude Code、Claude Desktop、Cursor、Cline、VS Code Copilot、OpenCode、Codex、Gemini CLI 和 Antigravity 的精确路径和 schema。

### 4. Verify

1. 打开 Revit 2022-2027 和一个 model。
2. 使用 BIMwright ribbon panel start/toggle MCP plugin。
3. 在 MCP client 中运行 `tools/list`。
4. 调用 `get_current_view_info`。

示例 response：

```json
{ "viewName": "Level 1", "viewType": "FloorPlan", "levelName": "Level 1", "scale": 100 }
```

在 MCP client 能 list tools 并成功调用 Revit 之前，不要声称安装完成。

### 卸载全部

一次性移除 plugin、.NET global tool、host-config entries、discovery files、logs 和 ToolBaker cache：

```powershell
pwsh .\uninstall-all.ps1 -WhatIf
pwsh .\uninstall-all.ps1
pwsh .\uninstall-all.ps1 -Yes
pwsh .\uninstall-all.ps1 -KeepLogs
```

`install.ps1 -Uninstall` 仍然是较窄的 plugin-only uninstall。

---

## 支持的 MCP clients

| Client | 状态 | 说明 |
|--------|------|------|
| Claude Code CLI | documented | project `.mcp.json` 或 global `~/.claude.json` |
| Claude Desktop | documented | `%APPDATA%\Claude\claude_desktop_config.json` |
| OpenCode | scripted | `install.ps1 -WireClient opencode` |
| Codex | scripted | `install.ps1 -WireClient codex` |
| Cursor | documented | project 或 user `mcp.json` |
| Cline (VS Code) | documented | Cline MCP settings JSON |
| VS Code Copilot | documented | native `servers` schema with `type: stdio` |
| Gemini CLI | documented | `gemini mcp add ...` 或 settings JSON |
| Antigravity | documented | Gemini/Antigravity MCP config JSON |

---

## Toolsets

非 adaptive surface 包含 32 个 tools，分布在 11 个 toolsets。启用 adaptive bake 后，surface 扩展到 35 个 tools。

默认启用 toolsets：`query`、`create`、`view`、`meta`、`lint`。

可选 toolsets：`modify`、`delete`、`annotation`、`export`、`mep`、`toolbaker`。

使用 `--toolsets query,create,modify,meta` 或 `--toolsets all` 启用。加上 `--read-only` 会移除 `create`、`modify`、`delete`，无论它们是如何被请求的。

| Toolset | Tools | Default |
|---------|-------|---------|
| `query` | current view, selected elements, family types, material quantities, model stats, AI element filter | on |
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

### 全部 tools

| Toolset | Tool | 描述 |
|---|---|---|
| `query` | `get_current_view_info` | Active view metadata: type, level, scale, detail level. |
| `query` | `get_selected_elements` | 当前选中 elements: id, name, category, type. |
| `query` | `get_available_family_types` | Project 中的 family types，可按 category filter. |
| `query` | `ai_element_filter` | 按 category 和 parameter/operator filter，数值单位为 mm. |
| `query` | `analyze_model_statistics` | 按 category 统计 element 数量. |
| `query` | `get_material_quantities` | 某 category 的 area 和 volume 汇总. |
| `create` | `create_line_based_element` | Wall 或其他 line-based element. |
| `create` | `create_point_based_element` | Door, window, furniture 或其他 point element. |
| `create` | `create_surface_based_element` | 从 polyline 创建 floor 或 ceiling. |
| `create` | `create_level` | 按 mm elevation 创建 level. |
| `create` | `create_grid` | 按两个点创建 grid line，单位 mm. |
| `create` | `create_room` | 在 point 创建 room，由 walls 围合. |
| `modify` | `operate_element` | Select, hide, unhide, isolate 或按 IDs set-color. |
| `modify` | `color_elements` | 按 parameter value 给 category 上色. |
| `delete` | `delete_element` | 按 ID list 删除。除非明确需要，否则保持关闭. |
| `view` | `create_view` | 创建 floor plan 或 3D view. |
| `view` | `place_view_on_sheet` | 把 view 放到新 sheet 或现有 sheet 上. |
| `view` | `analyze_sheet_layout` | Title block、viewport positions 和 scales，单位 mm. |
| `export` | `export_room_data` | Rooms: name, number, area, perimeter, level, volume. |
| `annotation` | `tag_all_walls` | 在 midpoint 打 wall-type tag，跳过已 tag 的 wall. |
| `annotation` | `tag_all_rooms` | 在 location point 打 room tag，跳过已 tag 的 room. |
| `mep` | `detect_system_elements` | 从 seed 沿 connectors traverse，返回 system members. |
| `toolbaker` | `send_code_to_revit` | 明确 opt-in 和 confirmation 后，在 Revit 中运行 ad-hoc C#. |
| `toolbaker` | `list_baked_tools` | 列出已 accept 的 personal baked tools. |
| `toolbaker` | `run_baked_tool` | 按名称调用 accepted baked tool. |
| `toolbaker` | `list_bake_suggestions` | Adaptive bake only: 列出 local suggestions. |
| `toolbaker` | `accept_bake_suggestion` | Adaptive bake only: accept 并 apply local suggestion. |
| `toolbaker` | `dismiss_bake_suggestion` | Adaptive bake only: snooze 或 dismiss local suggestion. |
| `meta` | `show_message` | Revit 内 TaskDialog，用于 connection test 或通知. |
| `meta` | `switch_target` | 多个 Revit version 同时运行时切换 connection. |
| `meta` | `batch_execute` | 在一个 `TransactionGroup` 中 atomically 执行 commands. |
| `meta` | `analyze_usage_patterns` | Local usage stats: tool calls, sessions, errors. |
| `lint` | `analyze_view_naming_patterns` | 推断 dominant view-naming pattern 和 outliers. |
| `lint` | `suggest_view_name_corrections` | 为 view outliers 提出 corrected names. |
| `lint` | `detect_firm_profile` | 根据 firm profiles fingerprint project naming. |

---

## Supported Revit Versions

| Revit | Target Framework | Transport | 备注 |
|-------|------------------|-----------|------|
| 2022 | .NET 4.8 | TCP | Accepted ToolBaker path smoke-tested |
| 2023 | .NET 4.8 | TCP | Core runtime coverage |
| 2024 | .NET 4.8 | TCP | Core runtime coverage |
| 2025 | .NET 8 (`net8.0-windows7.0`) | Named Pipe | Core runtime coverage |
| 2026 | .NET 8 (`net8.0-windows7.0`) | Named Pipe | Core runtime coverage; accepted ToolBaker path smoke-tested |
| 2027 | .NET 10 (`net10.0-windows7.0`) | Named Pipe | Accepted ToolBaker path smoke-tested |

不同 Revit 年份的 runtime behavior 仍可能不同，因为 Revit API 会变化。Custom baked C# tools 在跨版本测试前，应视为 version-sensitive。

---

## Security 和 Privacy

简短版：你的模型留在你的机器上。

- **默认 loopback。** TCP transport listen 在 `127.0.0.1`；Named Pipe scoped local-machine。
- **Per-session token handshake。** `%LOCALAPPDATA%\Bimwright\` 下的 discovery files 包含 connection info 和 auth token。
- **Schema validation。** 错误 shape 的 tool call 会在 command handler 运行前被 reject。
- **Path masking。** 返回给 model 的 error 会 sanitize，避免泄露 absolute path。
- **ToolBaker opt-in。** Adaptive bake 和 send-code paths 需要明确启用；`send_code_to_revit` 仍需要 Revit-side confirmation。
- **Local storage。** Usage events、bake database、logs 和 accepted-tool metadata 都在本地 Bimwright storage。

详见 [SECURITY.md](SECURITY.md) 的 threat model 和 vulnerability disclosure 流程。

---

## Configuration

三层配置，后者覆盖前者：JSON file、env vars、CLI args。

| Setting | CLI | Env | JSON key |
|---------|-----|-----|----------|
| Target Revit year | `--target R23` | `BIMWRIGHT_TARGET` | `target` |
| Toolsets | `--toolsets query,create` | `BIMWRIGHT_TOOLSETS` | `toolsets` |
| Read-only | `--read-only` | `BIMWRIGHT_READ_ONLY=1` | `readOnly` |
| Allow LAN bind | plugin-side only | `BIMWRIGHT_ALLOW_LAN_BIND=1` | `allowLanBind` |
| Allow ToolBaker when selected | `--enable-toolbaker` / `--disable-toolbaker` | `BIMWRIGHT_ENABLE_TOOLBAKER` | `enableToolbaker` |
| Enable adaptive bake suggestions | `--enable-adaptive-bake` / `--disable-adaptive-bake` | `BIMWRIGHT_ENABLE_ADAPTIVE_BAKE=1` | `enableAdaptiveBake` |
| Cache send-code bodies | `--cache-send-code-bodies` / `--no-cache-send-code-bodies` | `BIMWRIGHT_CACHE_SEND_CODE_BODIES=1` | `cacheSendCodeBodies` |

JSON file path: `%LOCALAPPDATA%\Bimwright\bimwright.config.json`。

---

## Development

```bash
dotnet test tests/Bimwright.Rvt.Tests/Bimwright.Rvt.Tests.csproj
dotnet build src/server/Bimwright.Rvt.Server.csproj -c Release
dotnet build src/plugin-r26/Bimwright.Rvt.Plugin.R26.csproj -c Release
```

Plugin projects 在 normal `Build` 后会 auto-deploy，复制到 `%APPDATA%\Autodesk\Revit\Addins\<year>\Bimwright\`。Build plugin 前请关闭 Revit，因为 Revit 会锁住已加载 DLL。

为 release stage plugin ZIPs：

```powershell
pwsh scripts/stage-plugin-zip.ps1 -Config Release
```

详见 [CONTRIBUTING.md](CONTRIBUTING.md)，包括 test strategy、tool-surface snapshot rules 和 contribution notes。

---

## 文档

- [AGENTS.md](AGENTS.md) - AI coding agents 的 install 和 MCP client wiring guide。
- [ARCHITECTURE.md](ARCHITECTURE.md) - process model、transport、threading 和 DTO strategy。
- [docs/bake.md](docs/bake.md) - adaptive bake、privacy、accepted tools 和 compatibility behavior。
- [docs/roadmap.md](docs/roadmap.md) - 当前 hardening plan 和 deferred work。
- [docs/testing/fresh-install-checklist.md](docs/testing/fresh-install-checklist.md) - public install verification checklist。
- [benchmarks/README.md](benchmarks/README.md) - weak-model benchmark procedure。

---

## License

Apache-2.0。见 [LICENSE](LICENSE)。

Revit 和 Autodesk 是 Autodesk, Inc. 的注册商标。bimwright 是独立 open-source 项目，与 Autodesk, Inc. 无关联、无赞助、无背书。

---

<p align="center">
  一个 <a href="https://github.com/bimwright">bimwright</a> 项目 - 给那些想把工作自动化，而不是贩卖神秘感的人。
</p>
