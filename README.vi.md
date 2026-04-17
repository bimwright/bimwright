<!-- mcp-name: io.github.bimwright/rvt-mcp -->

<p align="center">
  <img src="docs/images/bimwright-logo.jpg" alt="Bimwright — forging the digital craft of the built environment" width="420" />
</p>

<p align="center">
  <a href="https://github.com/bimwright/rvt-mcp/actions/workflows/build.yml"><img src="https://github.com/bimwright/rvt-mcp/actions/workflows/build.yml/badge.svg" alt="build" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-Apache%202.0-blue.svg" alt="license" /></a>
  <a href="#supported-revit-versions"><img src="https://img.shields.io/badge/.NET-4.8%20%7C%208%20%7C%2010-512BD4" alt=".NET" /></a>
</p>

> 🇻🇳 **Bản mirror tiếng Việt.** File gốc là [`README.md`](README.md) — khi có xung đột, bản tiếng Anh được ưu tiên. Mirror này có thể lệch vài ngày so với upstream.

**Bimwright — Revit MCP server có thể đoán trước được.**

Pure C#. Apache-2.0. **28 tool phủ Revit 2022–2027**, transaction-safe và có thể audit được. Mở rộng qua ToolBaker khi cần thêm.

Dành cho AI agent và workflow BIM muốn **chỉnh sửa reversible, reviewable** — không phải autopilot hộp đen.

Đặc điểm chính:

- **Full span R22–R27.** Một codebase, 6 plugin shell, .NET 4.8 → .NET 10. Đa số đối thủ skip ít nhất 1 năm.
- **Pure C# + Apache-2.0.** Không cần Node.js runtime trên máy chạy Revit. License enterprise-safe + dependency graph audit được.
- **Batching an toàn transaction.** `batch_execute` gói cả list command trong một `TransactionGroup` của Revit — 1 undo, tự rollback khi fail.
- **Progressive disclosure.** `--toolsets` + `--read-only` kiểm soát những gì model thấy. Model yếu không bị loãng.
- **ToolBaker tự mở rộng.** Model viết, compile, và register Revit tool mới tại runtime (Debug).

### How Bimwright compares

| | **Bimwright** | Leader¹ | LuDattilo | Weber | Autodesk R27 |
|---|---|---|---|---|---|
| Tools | **28 + ToolBaker** | 25 | ~80–100 | claim 705+ | 6 |
| Revit span | **R22–R27** | R20–R26 | R23–R27 | chưa verify | chỉ R27 |
| Stack | Pure C# | TS + C# | TS + C# | — | C# |
| License | **Apache-2.0** | MIT | — | — | proprietary |
| Phân phối | NuGet + MCP Registry + ZIPs | npm + ZIPs | npm + ZIPs | chỉ GitHub | Site Autodesk |

_Snapshot 2026-04-18. `—` = chưa verify live. Refresh bằng `gh search repos "revit mcp"`._
_¹ `mcp-servers-for-revit/mcp-servers-for-revit` — leader cộng đồng hiện tại (tính theo stars)._

---

## Architecture

```
MCP client (Claude Code, ...) ⇄ stdio ⇄ Bimwright.Rvt.Server (.NET 8) ⇄ TCP/Pipe ⇄ Bimwright.Rvt.Plugin.R<nn> (trong Revit.exe) ⇄ Revit API
```

Hai process. **Server** là .NET global tool; **plugin** là add-in DLL riêng cho từng năm Revit. Xem [ARCHITECTURE.md](ARCHITECTURE.md) để có full picture.

---

## Install

### 1. Server — .NET tool

```bash
dotnet tool install -g Bimwright.Rvt.Server
bimwright-rvt --help
```

Yêu cầu .NET 8 SDK trên máy chạy MCP client.

### 2. Plugin — Revit add-in

Tải latest release từ [GitHub Releases](https://github.com/bimwright/rvt-mcp/releases/latest). Giải nén và chạy:

```powershell
pwsh install.ps1            # detect tất cả năm Revit đã cài
pwsh install.ps1 -WhatIf    # preview, không thay đổi
pwsh install.ps1 -Uninstall # gỡ sạch
```

Script detect các version Revit đã cài qua `HKLM:\SOFTWARE\Autodesk\Revit\` và copy plugin tương ứng vào `%APPDATA%\Autodesk\Revit\Addins\<year>\Bimwright\`.

### 3. Wire vào MCP client của bạn

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

---

## Supported MCP clients

| Client | Trạng thái | Ghi chú |
|--------|-----------|---------|
| Claude Code CLI | ✅ verified | primary test target |
| Claude Desktop | ✅ verified | entry trong `.mcp.json` |
| Cursor | ⏳ pending verification | stdio; dự kiến hoạt động |
| Cline (VS Code) | ⏳ pending verification | stdio; dự kiến hoạt động |
| MCP client khác | ⏳ pending | mở issue nếu bạn thử |

Compat matrix rộng hơn nằm trong roadmap v0.2.

---

<!-- TODO v0.2: add demo.gif above this section -->
## Quickstart — 5 phút cho tool call đầu tiên

1. `dotnet tool install -g Bimwright.Rvt.Server` + `pwsh install.ps1`.
2. Mở Revit, click nút ribbon **Bimwright → Start MCP**.
3. Trong MCP client, chạy `tools/list` — bạn sẽ thấy các toolset mặc định (`query`, `create`, `view`, `meta`).
4. Call `get_current_view_info` — nhận về một DTO như:
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

**28 tool chia thành 10 nhóm.** 4 nhóm bật mặc định (`query`, `create`, `view`, `meta`); các nhóm còn lại opt-in qua `--toolsets` hoặc config.

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
| `toolbaker` | `bake_tool`, `list_baked_tools`, `run_baked_tool`, `send_code_to_revit` *(chỉ Debug)* | off |

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

Compile gate 6/6; runtime verify 4/4 trên R23–R26 (xem `A1` trong commit history). R22 và R27 ship dựa trên compile-evidence vì stack giống R23 và R26 tương ứng.

---

## Security

- **Mặc định bind loopback.** TCP transport chỉ listen trên `127.0.0.1`. Opt in `0.0.0.0` qua `BIMWRIGHT_ALLOW_LAN_BIND=1`.
- **Handshake có token.** Mỗi connection phải trình token per-session ghi trong `%LOCALAPPDATA%\Bimwright\portR<nn>.txt`.
- **Validate schema nghiêm ngặt.** Tool call malformed bị reject với envelope error-as-teacher (`error`, `suggestion`, `hint`) trước khi handler chạy.
- **Mask rò rỉ path.** Exception của handler được sanitize trước khi vào MCP response hoặc log — không để lộ absolute path, UNC share, user-home dir.

Xem [security appendix](docs/roadmap.md#security) để biết full threat model.

---

## Configuration

Ba lớp, lớp sau thắng: **JSON file → env vars → CLI args**.

| Setting | CLI | Env | JSON key |
|---------|-----|-----|----------|
| Năm Revit target | `--target R23` | `BIMWRIGHT_TARGET` | `target` |
| Toolsets | `--toolsets query,create` | `BIMWRIGHT_TOOLSETS` | `toolsets` |
| Read-only | `--read-only` | `BIMWRIGHT_READ_ONLY=1` | `readOnly` |
| Cho phép LAN bind | — | `BIMWRIGHT_ALLOW_LAN_BIND=1` | `allowLanBind` |
| Bật ToolBaker | `--enable-toolbaker` / `--disable-toolbaker` | `BIMWRIGHT_ENABLE_TOOLBAKER` | `enableToolbaker` |

JSON file: `%LOCALAPPDATA%\Bimwright\bimwright.config.json`.

---

## ToolBaker — viết tool của bạn ngay trong chat

Model có thể tạo Revit tool mới giữa session. Bạn hỏi ("schedule tất cả cửa theo fire rating"), nó viết C# dựa trên Revit API, `bake_tool` compile qua Roslyn vào một `AssemblyLoadContext` cô lập, register vào, và các call sau đó dùng nó như tool built-in.

Gate qua `--enable-toolbaker` (mặc định off). `send_code_to_revit` — bản execute không sandbox — chỉ có trong Debug build.

---

## Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) — process model, transport, chiến lược multi-version, pipeline ToolBaker.
- [CONTRIBUTING.md](CONTRIBUTING.md) — setup dev, build matrix, coding style.
- [docs/roadmap.md](docs/roadmap.md) — v0.2 (MCP Resources, hardening ToolBaker), v0.3 (async job polling, aggregator listings), v1.0 (governance).

---

## License

Apache-2.0. Xem [LICENSE](LICENSE).
