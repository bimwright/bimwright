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
  📖 <a href="README.md">English</a> · Tiếng Việt · <a href="README.zh-CN.md">简体中文</a>
</p>

> 🇻🇳 Bản mirror tiếng Việt. File gốc là [`README.md`](README.md) — khi có xung đột thì bản EN ưu tiên, mirror này có thể lệch vài ngày.

---

Mình làm cái này vì mình chán click rồi.

Chắc anh/chị cũng dính cảnh này: 5 giờ chiều, BIM Manager ping *"đổi tên tất cả theo chuẩn mới"* — L01 - Hầm, L02 - Thương mại, cứ thế đến hết. Model có mấy ngàn element. Click tay thì xác định. Viết Dynamo script thì nửa ngày. Đúng chỗ ngứa.

**rvt-mcp** là một add-in chạy song song với Revit 2022–2027. Anh/chị nói với Claude (hoặc Cursor, Codex, OpenCode — agent nào cũng được) muốn làm gì, nó gọi 1 trong 32 tool local, Revit chạy trong 1 transaction duy nhất. Không ưng? **Ctrl+Z** — 1 bước, rollback sạch.

Không cloud. Không có gì rời khỏi máy của anh/chị. Apache-2.0, pure C#.

> 🤖 **Đang dùng AI agent?** Trỏ nó vào [AGENTS.md](AGENTS.md) — agent tự cài server, plugin, wire host cho anh/chị, mỗi bước đều preview trước khi chạm máy. Vẫn cần Revit đã cài sẵn và .NET 8 SDK trên máy.

Vài thứ mình care:

- **Đủ Revit 2022 đến 2027.** 1 codebase, 6 plugin shell (.NET 4.8 → .NET 10). Compile gate 6/6, core runtime coverage có trên R23–R26, và accepted ToolBaker path đã smoke-test trên R22, R26, R27.
- **Pure C#, Apache-2.0.** Không cần Node.js trên máy chạy Revit. License enterprise-safe, dependency graph audit được.
- **Batch atomic.** `batch_execute` gói cả list command trong 1 `TransactionGroup`. 1 undo step. Nếu 1 command trong batch fail thì cả batch rollback — không có chuyện stuck ở trạng thái nửa chừng.
- **Model yếu không bị loãng.** `--toolsets` + `--read-only` kiểm soát cái model được thấy. Haiku-size không cần biết tới `delete_element` khi anh/chị chỉ hỏi quantity.
- **Self-shaping toolkit, opt-in.** Adaptive bake mặc định tắt. Khi bật, usage local lặp lại có thể thành suggestion để anh/chị accept thành baked tool riêng. Accepted tools chạy được từ ribbon Revit và qua `list_baked_tools` / `run_baked_tool`, kèm compatibility theo từng Revit version.

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

`rvt-mcp` là một **full C# MCP stack**. MCP server, per-version Revit plugin shells, transport bridge, command handlers, DTO mapping, và ToolBaker pipeline đều viết bằng C# dùng official MCP C# SDK. Không có Node.js sidecar trên máy Revit — chỉ .NET + Revit.

Điều đó quan trọng vì nhiều MCP example và server trong ecosystem dựa trên Node.js/TypeScript runtime. Dự án này thì không. Với các team Revit/BIM, điều đó có nghĩa là một ngôn ngữ, một build chain, và quy trình debug / audit / deploy đơn giản hơn. Phần khác biệt theo version nằm ở edge: mỗi năm Revit có một thin plugin shell riêng, và tất cả cùng compile từ `src/shared/`. Xem [ARCHITECTURE.md](ARCHITECTURE.md) để biết thêm chi tiết.

---

## Cấu trúc thư mục

```
rvt-mcp/
├── src/
│   ├── Bimwright.Rvt.sln         # Solution (server + 6 plugin shell)
│   ├── server/                   # Bimwright.Rvt.Server — .NET 8 global tool, stdio MCP
│   ├── shared/                   # Source glob dùng chung cho mọi plugin shell
│   │   ├── Handlers/             # Mỗi tool = 1 file (create_grid, send_code, …)
│   │   ├── Commands/             # Ribbon command trong Revit
│   │   ├── ToolBaker/            # Self-evolution engine (registry/runtime cho baked tool)
│   │   ├── Transport/            # TCP (R22–R24) + Named Pipe (R25–R27)
│   │   ├── Infrastructure/       # CommandDispatcher, ExternalEvent marshalling
│   │   └── Security/             # Auth token, secret masking
│   ├── plugin-r22/               # Revit 2022 shell — .NET 4.8, TCP
│   ├── plugin-r23/               # Revit 2023 shell — .NET 4.8, TCP
│   ├── plugin-r24/               # Revit 2024 shell — .NET 4.8, TCP
│   ├── plugin-r25/               # Revit 2025 shell — .NET 8, Named Pipe
│   ├── plugin-r26/               # Revit 2026 shell — .NET 8, Named Pipe
│   └── plugin-r27/               # Revit 2027 shell — .NET 10, Named Pipe
├── tests/                        # Golden snapshot + Haiku benchmark + policy test
├── benchmarks/                   # Harness đo accuracy của model yếu (Haiku)
├── scripts/                      # stage-plugin-zip.ps1, install.ps1, uninstall-all.ps1
├── docs/                         # Brainstorm, review, ADR
├── server.json                   # Manifest cho MCP registry
├── smithery.yaml                 # Manifest cho Smithery aggregator
├── AGENTS.md                     # Hướng dẫn agent-led install (9 host client)
└── ARCHITECTURE.md               # Deep-dive threading + transport + DTO
```

Sáu plugin shell compile từ chung một `src/shared/` glob — `#if` fence theo năm xử lý Revit API drift (`ElementId.IntegerValue` → `.Value` từ R26+, WPF ở R27).

---

## Install

### 1. Server — .NET tool

```bash
dotnet tool install -g Bimwright.Rvt.Server
bimwright-rvt --help
```

Cần .NET 8 SDK trên máy chạy MCP client. Nếu tool đã cài sẵn, chạy `dotnet tool update -g Bimwright.Rvt.Server` thay vì install.

### 2. Plugin — Revit add-in

Tải plugin installer bundle từ [GitHub Releases](https://github.com/bimwright/rvt-mcp/releases/latest). Bundle có tên `bimwright-rvt-plugin-<tag>.zip`, bên trong có `install.ps1`, `uninstall-all.ps1` và 6 ZIP plugin cho từng năm Revit.

```powershell
$tag = (Invoke-RestMethod https://api.github.com/repos/bimwright/rvt-mcp/releases/latest).tag_name
$zip = "$env:TEMP\bimwright-rvt-plugin-$tag.zip"
$dir = "$env:TEMP\bimwright-rvt-plugin-$tag"
Invoke-WebRequest "https://github.com/bimwright/rvt-mcp/releases/download/$tag/bimwright-rvt-plugin-$tag.zip" -OutFile $zip
Expand-Archive $zip -DestinationPath $dir -Force
Set-Location $dir

pwsh .\install.ps1 -SourceDir . -WhatIf    # preview, không thay đổi
pwsh .\install.ps1 -SourceDir .            # detect tất cả năm Revit đã cài
pwsh .\install.ps1 -Uninstall              # chỉ gỡ plugin
```

Script detect các version Revit đã cài qua `HKLM:\SOFTWARE\Autodesk\Revit\` và copy plugin tương ứng vào `%APPDATA%\Autodesk\Revit\Addins\<year>\Bimwright\`.

### 3. Wire vào MCP client

Thêm 1 entry cho mỗi năm Revit vào config MCP của client (ví dụ `.mcp.json`):

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

Bỏ flag `--target` thì Bimwright auto-detect Revit đang chạy qua discovery file trong `%LOCALAPPDATA%\Bimwright\`.

#### Wire tự động cho OpenCode / Codex Desktop

Thay vì sửa tay `opencode.json` hoặc `~/.codex/config.toml`, chạy:

```powershell
pwsh .\install.ps1 -SourceDir . -WireClient opencode      # ghi entry vào %USERPROFILE%\.config\opencode\opencode.json
pwsh .\install.ps1 -SourceDir . -WireClient codex         # ghi entry vào %USERPROFILE%\.codex\config.toml
pwsh .\install.ps1 -SourceDir . -WireClient opencode -WhatIf   # xem trước
```

Script sẽ:

- Thêm 1 entry `bimwright-rvt-r<YY>` cho mỗi năm Revit detect được trên máy.
- Giữ nguyên mọi entry không thuộc bimwright đã có trong config (merge tại chỗ).
- Backup file gốc thành `<file>.bimwright.bak` trước khi ghi.
- Không làm gì nếu file config của host không tồn tại (chưa cài host đó).

Người dùng Claude Code: dán đoạn JSON ở trên vào `.mcp.json` của project — script không tự sửa file cấp project.

### Gỡ toàn bộ

Xóa plugin, .NET global tool, entry trong config của host, discovery file và ToolBaker cache trong 1 lần:

```powershell
pwsh .\uninstall-all.ps1 -WhatIf    # xem trước những gì sẽ bị xóa
pwsh .\uninstall-all.ps1            # xác nhận tương tác rồi thực thi
pwsh .\uninstall-all.ps1 -Yes       # skip prompt
pwsh .\uninstall-all.ps1 -KeepLogs  # giữ file *.log và *.jsonl
```

Lưu ý:

- Script gỡ `Bimwright.Rvt.Server` khỏi **toàn bộ máy** (`dotnet tool uninstall -g`), không chỉ thư mục hiện tại.
- File `.mcp.json` cấp project không được quét — xóa tay các entry `bimwright-rvt-*` trong đó.
- `install.ps1 -Uninstall` vẫn chỉ gỡ plugin (tương thích ngược).
- `-KeepLogs` giữ thư mục `logs\` (nếu có) và các file `*.log` / `*.jsonl` ở cấp gốc trong `%LOCALAPPDATA%\Bimwright\`.

---

## Supported MCP clients

| Client | Trạng thái | Ghi chú |
|--------|-----------|---------|
| Claude Code CLI | documented | project `.mcp.json` hoặc global `~/.claude.json` |
| Claude Desktop | documented | `%APPDATA%\Claude\claude_desktop_config.json` |
| OpenCode | scripted | `install.ps1 -WireClient opencode` |
| Codex | scripted | `install.ps1 -WireClient codex` |
| Cursor | documented | project hoặc user `mcp.json` |
| Cline (VS Code) | documented | Cline MCP settings JSON |
| VS Code Copilot | documented | native `servers` schema với `type: stdio` |
| Gemini CLI | documented | `gemini mcp add ...` hoặc settings JSON |
| Antigravity | documented | Gemini/Antigravity MCP config JSON |

Xem [AGENTS.md](AGENTS.md) để biết đường dẫn config, schema, dry-run và rollback cho từng host.

---

## Quickstart — 5 phút cho tool call đầu tiên

1. Install hoặc update `Bimwright.Rvt.Server`, tải plugin bundle, rồi chạy `install.ps1` trong thư mục đã giải nén.
2. Mở Revit, vào **Add-Ins → BIMwright**, rồi bấm nút toggle MCP.
3. Trong MCP client, chạy `tools/list` — sẽ thấy các toolset mặc định (`query`, `create`, `view`, `meta`, `lint`).
4. Call `get_current_view_info` — nhận về DTO kiểu:
   ```json
   { "viewName": "Level 1", "viewType": "FloorPlan", "levelName": "Level 1", "scale": 100 }
   ```
5. Thử workflow thật:
   ```
   batch_execute({
     "commands": "[
       {\"command\":\"create_grid\",\"params\":{\"name\":\"A\",\"start\":[0,0],\"end\":[20000,0]}},
       {\"command\":\"create_level\",\"params\":{\"name\":\"L2\",\"elevation\":3000}}
     ]"
   })
   ```
   1 undo step, cả 2 op commit atomic.

---

## Toolsets

**32 tool chia thành 11 toolset.** 5 toolset bật mặc định (`query`, `create`, `view`, `meta`, `lint`); còn lại opt-in qua `--toolsets` hoặc config. Khi adaptive bake bật, 3 tool lifecycle suggestion được thêm vào surface `toolbaker`, tổng surface adaptive là 35 tool.

| Toolset | Tools | Mặc định |
|---------|-------|----------|
| `query` | get current view, selected elements, available family types, material quantities, model stats, AI element filter | **on** |
| `create` | grid, level, room, line-based, point-based, surface-based element | **on** |
| `view` | create view, sheet layout, place view on sheet | **on** |
| `meta` | `show_message`, `switch_target`, `batch_execute`, usage stats | **on** |
| `lint` | phân tích mẫu đặt tên view, gợi ý sửa, phát hiện firm-profile | **bật** |
| `modify` | `operate_element`, `color_elements` | off |
| `delete` | `delete_element` | off |
| `annotation` | `tag_all_rooms`, `tag_all_walls` | off |
| `export` | `export_room_data` | off |
| `mep` | `detect_system_elements` | off |
| `toolbaker` | accepted-tool list/run, send-code, và adaptive suggestion lifecycle tools *(opt-in qua env/config)* | off |

Bật bằng `--toolsets query,create,modify,meta` hoặc `--toolsets all`. Thêm `--read-only` để strip `create`/`modify`/`delete` bất kể request gì.

### Danh sách tool đầy đủ

| Toolset | Tool | Mô tả |
|---|---|---|
| `query` | `get_current_view_info` | Metadata view active (type, level, scale, detail level). |
| `query` | `get_selected_elements` | Element đang chọn với id, tên, category, type. |
| `query` | `get_available_family_types` | Family type trong project, filter được theo category. |
| `query` | `ai_element_filter` | Filter theo category + parameter + operator (giá trị mm). |
| `query` | `analyze_model_statistics` | Đếm element group theo category. |
| `query` | `get_material_quantities` | Area (m²) + volume (m³) cho 1 category. |
| `create` | `create_line_based_element` | Wall hoặc line-based element khác. |
| `create` | `create_point_based_element` | Door, window, furniture hay point element khác. |
| `create` | `create_surface_based_element` | Floor hoặc ceiling từ polyline. |
| `create` | `create_level` | Level tại elevation (mm). |
| `create` | `create_grid` | Grid line giữa 2 điểm (mm). |
| `create` | `create_room` | Room tại 1 điểm, bound bởi wall. |
| `modify` | `operate_element` | Select, hide, unhide, isolate, set-color trên danh sách ID. |
| `modify` | `color_elements` | Color-code 1 category theo parameter value (auto palette). |
| `delete` | `delete_element` | Xóa theo list ID (destructive; không undo qua MCP được). |
| `view` | `create_view` | Floor plan hoặc 3D view. |
| `view` | `place_view_on_sheet` | Đặt view lên sheet (mới hoặc có sẵn). |
| `view` | `analyze_sheet_layout` | Title block + vị trí/scale của viewport (mm). |
| `export` | `export_room_data` | Toàn bộ room: name, number, area, perimeter, level, volume. |
| `annotation` | `tag_all_walls` | Tag wall-type tại midpoint (bỏ qua wall đã tag). |
| `annotation` | `tag_all_rooms` | Room tag tại location point (bỏ qua room đã tag). |
| `mep` | `detect_system_elements` | Lần theo connector từ 1 seed, trả về member của system. |
| `toolbaker` | `send_code_to_revit` | Chạy C# body ad-hoc trong Revit sau khi plugin thấy opt-in adaptive bake. |
| `toolbaker` | `list_baked_tools` | List tool đã bake. |
| `toolbaker` | `run_baked_tool` | Gọi tool đã bake theo tên. |
| `toolbaker` | `list_bake_suggestions` | Chỉ adaptive-bake: list suggestion local. |
| `toolbaker` | `accept_bake_suggestion` | Chỉ adaptive-bake: accept và apply suggestion local. |
| `toolbaker` | `dismiss_bake_suggestion` | Chỉ adaptive-bake: snooze hoặc dismiss suggestion local. |
| `meta` | `show_message` | TaskDialog trong Revit — test connection, notify user. |
| `meta` | `switch_target` | Đổi Revit connection active khi nhiều version đang chạy. |
| `meta` | `batch_execute` | Chạy N command atomic trong 1 TransactionGroup (1 undo). |
| `meta` | `analyze_usage_patterns` | Stats usage: tool calls, session, error (N ngày gần nhất). |
| `lint` | `analyze_view_naming_patterns` | Suy ra mẫu đặt tên view chủ đạo + độ phủ + outliers. |
| `lint` | `suggest_view_name_corrections` | Đề xuất tên view đã sửa cho outliers (inferred hoặc theo profile). |
| `lint` | `detect_firm_profile` | Fingerprint naming của project, khớp với firm-profile library. |

---

## Supported Revit versions

| Revit | Target Framework | Transport | Ghi chú |
|-------|------------------|-----------|---------|
| 2022  | .NET 4.8 | TCP | Accepted ToolBaker path smoke-tested |
| 2023  | .NET 4.8 | TCP | Core runtime coverage |
| 2024  | .NET 4.8 | TCP | Core runtime coverage |
| 2025  | .NET 8 (`net8.0-windows7.0`) | Named Pipe | Shell .NET 8 đầu tiên; core runtime coverage |
| 2026  | .NET 8 (`net8.0-windows7.0`) | Named Pipe | Core runtime coverage; accepted ToolBaker path smoke-tested |
| 2027  | .NET 10 (`net10.0-windows7.0`) | Named Pipe | Accepted ToolBaker path smoke-tested |

Compile gate 6/6. Core runtime coverage đã pass trên R23–R26, và manual smoke test đã cover accepted ToolBaker list/run/ribbon path trên R22, R26, R27. Đây là bằng chứng runtime thực tế, không phải cam kết mọi baked tool portable qua mọi năm Revit; Revit API drift vẫn có thể ảnh hưởng custom C# body.

---

## Security

Nói gọn: **model của anh/chị không rời máy.** MCP server chạy local, plugin chạy trong process của Revit, 2 đứa nói chuyện qua localhost. Hết.

Nói chi tiết, dành cho người duyệt tool này cho tổ chức:

- **Mặc định bind loopback.** TCP transport chỉ listen trên `127.0.0.1`. Nếu thật sự cần LAN thì phải set `BIMWRIGHT_ALLOW_LAN_BIND=1` — mình muốn anh/chị biết chắc mình đang bật nó.
- **Token handshake per-session.** Mỗi connection phải trình token ghi trong `%LOCALAPPDATA%\Bimwright\portR<nn>.txt`. Attacker cùng user vẫn thắng (đọc được file). Người không có quyền đọc user profile thì bị chặn.
- **Validate schema trước khi handler chạy.** Tool call malformed nhận envelope error-as-teacher (`error`, `suggestion`, `hint`) thay vì làm sập cái gì.
- **Mask path trong exception.** Handler throw thì response và log được sanitize — không lộ absolute path, UNC share, user-home.

Full threat model trong [security appendix](docs/roadmap.md#security).

---

## Configuration

3 lớp, lớp sau thắng: **JSON file → env vars → CLI args**.

| Setting | CLI | Env | JSON key |
|---------|-----|-----|----------|
| Năm Revit target | `--target R23` | `BIMWRIGHT_TARGET` | `target` |
| Toolsets | `--toolsets query,create` | `BIMWRIGHT_TOOLSETS` | `toolsets` |
| Read-only | `--read-only` | `BIMWRIGHT_READ_ONLY=1` | `readOnly` |
| Cho phép LAN bind | — | `BIMWRIGHT_ALLOW_LAN_BIND=1` | `allowLanBind` |
| Bật ToolBaker | `--enable-toolbaker` / `--disable-toolbaker` | `BIMWRIGHT_ENABLE_TOOLBAKER` | `enableToolbaker` |
| Bật adaptive bake suggestions | — | `BIMWRIGHT_ENABLE_ADAPTIVE_BAKE=1` | `enableAdaptiveBake` |
| Cache send-code bodies để cluster | — | `BIMWRIGHT_CACHE_SEND_CODE_BODIES=1` | `cacheSendCodeBodies` |

JSON file: `%LOCALAPPDATA%\Bimwright\bimwright.config.json`.

---

## Self-shaping toolkit

Adaptive bake là đường opt-in để biến workflow Revit local lặp lại thành tool cá nhân. Mặc định OFF. Chỉ bật khi anh/chị muốn Bimwright ghi pattern usage local và đề xuất bake suggestion.

Usage data ở lại trên máy trong `%LOCALAPPDATA%\Bimwright\`. Server là SQLite writer duy nhất; plugin Revit chỉ đọc `bake.db` và quản lý runtime command cache/ribbon buttons. Không có usage collection endpoint.

Accepted baked tools chạy được từ ribbon Revit và qua tool indirection của `toolbaker`: gọi `list_baked_tools` để xem accepted tools, rồi `run_baked_tool` với `name=<tool_name>` để chạy. Trong v0.3.x, baked tools không xuất hiện thành native MCP tools riêng. Accepted-tool path đã smoke-test trên R22, R26, R27, gồm cả cross-version compatibility metadata.

`bake_tool` đã bị remove trong v0.3.0. Bake mới đi qua measured suggestions và user acceptance rõ ràng bằng `accept_bake_suggestion`; legacy accepted tools vẫn callable qua `list_baked_tools` / `run_baked_tool`.

Xem [docs/bake.md](docs/bake.md) để biết cách bật, privacy, suggestion handling, archive behavior và cross-Revit compatibility notes.

---

## Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) — process model, transport, chiến lược multi-version, pipeline ToolBaker.
- [CONTRIBUTING.md](CONTRIBUTING.md) — setup dev, build matrix, coding style.
- [docs/bake.md](docs/bake.md) — adaptive bake opt-in, privacy, suggestions, accepted tools, compat behavior.
- [docs/roadmap.md](docs/roadmap.md) — v0.2 (MCP Resources, hardening ToolBaker), v0.3 (async job polling, aggregator listings), v1.0 (governance).

---

## License

Apache-2.0. Xem [LICENSE](LICENSE).

Nếu anh/chị dùng vào việc thật thì star repo giùm — để người khác cũng tìm thấy được.

---

<p align="center">
  Một dự án của <a href="https://github.com/bimwright">bimwright</a> —
  <a href="https://github.com/bimwright"><img src="https://raw.githubusercontent.com/bimwright/.github/master/assets/logos/bimwright-logo.png" alt="bimwright" height="24" align="middle" /></a>
</p>
