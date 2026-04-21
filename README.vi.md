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

**rvt-mcp** là một add-in chạy song song với Revit 2022–2027. Anh/chị nói với Claude (hoặc Cursor, Codex, OpenCode — agent nào cũng được) muốn làm gì, nó gọi 1 trong ~28 tool, Revit chạy trong 1 transaction duy nhất. Không ưng? **Ctrl+Z** — 1 bước, rollback sạch.

Không cloud. Không có gì rời khỏi máy của anh/chị. Apache-2.0, pure C#.

> 🤖 **Đang dùng AI agent?** Trỏ nó vào [AGENTS.md](AGENTS.md) — agent tự cài server, plugin, wire host cho anh/chị, mỗi bước đều preview trước khi chạm máy. Vẫn cần Revit đã cài sẵn và .NET 8 SDK trên máy.

Vài thứ mình care:

- **Đủ Revit 2022 đến 2027.** 1 codebase, 6 plugin shell (.NET 4.8 → .NET 10). R22 với R27 ship dựa trên compile-evidence — chỉ runtime-verified 4/4 trên R23–R26 thôi. Stack giống hệt R23 và R26 nên thành thật mà nói mình khá chắc nó chạy, nhưng mình không nói verified cái mình chưa chạy được.
- **Pure C#, Apache-2.0.** Không cần Node.js trên máy chạy Revit. License enterprise-safe, dependency graph audit được.
- **Batch atomic.** `batch_execute` gói cả list command trong 1 `TransactionGroup`. 1 undo step. Nếu 1 command trong batch fail thì cả batch rollback — không có chuyện stuck ở trạng thái nửa chừng.
- **Model yếu không bị loãng.** `--toolsets` + `--read-only` kiểm soát cái model được thấy. Haiku-size không cần biết tới `delete_element` khi anh/chị chỉ hỏi quantity.
- **ToolBaker, opt-in.** Khi built-in không đủ, model tự viết tool mới bằng C#, compile qua Roslyn, register luôn tại runtime. Mặc định tắt — bật bằng `--enable-toolbaker` nếu anh/chị muốn.

---

## Architecture

```
MCP client (Claude Code, ...) ⇄ stdio ⇄ Bimwright.Rvt.Server (.NET 8) ⇄ TCP/Pipe ⇄ Bimwright.Rvt.Plugin.R<nn> (trong Revit.exe) ⇄ Revit API
```

Hai process. **Server** là .NET global tool; **plugin** là add-in DLL riêng cho từng năm Revit. Xem [ARCHITECTURE.md](ARCHITECTURE.md) để có full picture.

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
│   │   ├── ToolBaker/            # Self-evolution engine (bake_tool, run_baked_tool)
│   │   ├── Transport/            # TCP (R22–R26) + Named Pipe (R27)
│   │   ├── Infrastructure/       # CommandDispatcher, ExternalEvent marshalling
│   │   └── Security/             # Auth token, secret masking
│   ├── plugin-r22/               # Revit 2022 shell — .NET 4.8, TCP
│   ├── plugin-r23/               # Revit 2023 shell — .NET 4.8, TCP
│   ├── plugin-r24/               # Revit 2024 shell — .NET 4.8, TCP
│   ├── plugin-r25/               # Revit 2025 shell — .NET 8, TCP
│   ├── plugin-r26/               # Revit 2026 shell — .NET 8, TCP
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

Cần .NET 8 SDK trên máy chạy MCP client.

### 2. Plugin — Revit add-in

Tải latest release từ [GitHub Releases](https://github.com/bimwright/rvt-mcp/releases/latest). Giải nén và chạy:

```powershell
pwsh install.ps1            # detect tất cả năm Revit đã cài
pwsh install.ps1 -WhatIf    # preview, không thay đổi
pwsh install.ps1 -Uninstall # gỡ sạch
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
pwsh install.ps1 -WireClient opencode      # ghi entry vào %USERPROFILE%\.config\opencode\opencode.json
pwsh install.ps1 -WireClient codex         # ghi entry vào %USERPROFILE%\.codex\config.toml
pwsh install.ps1 -WireClient opencode -WhatIf   # xem trước
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
pwsh uninstall-all.ps1 -WhatIf    # xem trước những gì sẽ bị xóa
pwsh uninstall-all.ps1            # xác nhận tương tác rồi thực thi
pwsh uninstall-all.ps1 -Yes       # skip prompt
pwsh uninstall-all.ps1 -KeepLogs  # giữ file *.log và *.jsonl
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
| Claude Code CLI | ✅ verified | target test chính |
| Claude Desktop | ✅ verified | entry trong `.mcp.json` |
| Cursor | ⏳ pending verification | stdio; dự kiến work |
| Cline (VS Code) | ⏳ pending verification | stdio; dự kiến work |
| MCP client khác | ⏳ pending | mở issue nếu anh/chị thử |

Compat matrix rộng hơn nằm trong roadmap v0.2.

---

<!-- TODO v0.2: add demo.gif above this section -->
## Quickstart — 5 phút cho tool call đầu tiên

1. `dotnet tool install -g Bimwright.Rvt.Server` + `pwsh install.ps1`.
2. Mở Revit, click nút ribbon **Bimwright → Start MCP**.
3. Trong MCP client, chạy `tools/list` — sẽ thấy các toolset mặc định (`query`, `create`, `view`, `meta`).
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

**28 tool chia thành 10 nhóm.** 4 nhóm bật mặc định (`query`, `create`, `view`, `meta`); còn lại opt-in qua `--toolsets` hoặc config.

| Toolset | Tools | Mặc định |
|---------|-------|----------|
| `query` | get current view, selected elements, available family types, material quantities, model stats, AI element filter | **on** |
| `create` | grid, level, room, line-based, point-based, surface-based element | **on** |
| `view` | create view, get current view info, place view on sheet | **on** |
| `meta` | `show_message`, `batch_execute` | **on** |
| `modify` | `operate_element`, `color_elements` | off |
| `delete` | `delete_element` | off |
| `annotation` | `tag_all_rooms`, `tag_all_walls` | off |
| `export` | `export_room_data` | off |
| `mep` | `detect_system_elements` | off |
| `toolbaker` | `bake_tool`, `list_baked_tools`, `run_baked_tool`, `send_code_to_revit` *(Debug only)* | off |

Bật bằng `--toolsets query,create,modify,meta` hoặc `--toolsets all`. Thêm `--read-only` để strip `create`/`modify`/`delete` bất kể request gì.

---

## Supported Revit versions

| Revit | Target Framework | Transport | Ghi chú |
|-------|------------------|-----------|---------|
| 2022  | .NET 4.8 | TCP | |
| 2023  | .NET 4.8 | TCP | |
| 2024  | .NET 4.8 | TCP | |
| 2025  | .NET 8 (`net8.0-windows7.0`) | Named Pipe | Shell .NET 8 đầu tiên |
| 2026  | .NET 8 (`net8.0-windows7.0`) | Named Pipe | `ElementId.IntegerValue` bị remove — dùng `RevitCompat.GetId()` |
| 2027  | .NET 10 (`net10.0-windows7.0`) | Named Pipe | Experimental — .NET 10 vẫn preview |

Compile gate 6/6; runtime verified 4/4 trên R23–R26 (xem `A1` trong commit history). R22 và R27 ship trên compile-evidence — stack giống hệt R23 và R26, nhưng mình chưa tự chạy được nên không gọi là verified.

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

JSON file: `%LOCALAPPDATA%\Bimwright\bimwright.config.json`.

---

## ToolBaker — nướng tool riêng khi built-in không đủ

Tool generic thì generic. Task BIM thực của anh/chị không như vậy — có quy ước đặt tên riêng, có bước QA riêng, có pipeline export riêng. Mỗi session, agent phải stitch 8–10 primitive call để làm đúng cái workflow đó, và anh/chị đốt token mỗi lần. Mình bực đủ để xây đường thoát.

Anh/chị mô tả workflow 1 lần bằng tiếng người. Model viết 1 handler C#, `bake_tool` compile qua Roslyn vào `AssemblyLoadContext` cô lập, SQLite persist. Session sau — workflow đó chỉ còn 1 call.

Walkthrough:

1. Mô tả dataflow thực, ví dụ *"schedule tất cả cửa theo fire rating, tag cái fail, export ra CSV"*.
2. Model generate handler theo contract `IRevitCommand`.
3. `bake_tool` compile qua Roslyn, link với Revit API live, load vào sandboxed assembly context.
4. SQLite persist. Auto-register mỗi session sau.
5. Call như tool built-in — cùng schema validation, cùng transaction safety.

Gate qua `--enable-toolbaker` (mặc định off). `send_code_to_revit` — escape hatch không sandbox — chỉ có trong Debug build, nên release binary không thể execute C# bừa được.

---

## Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) — process model, transport, chiến lược multi-version, pipeline ToolBaker.
- [CONTRIBUTING.md](CONTRIBUTING.md) — setup dev, build matrix, coding style.
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
