# Aspect #5 — Security / Governance

> **Note:** this file documents security decisions for the public product. No
> `[[C-XX]]` tags needed — the decisions here are self-contained.

**Status:** ✅ Decided 2026-04-16 (except S4 deferred — same shape as A1)
**Prerequisites:** Aspects #1, #2, #3, #4, Meta-decision M1
**Related:** a lesson on re-examining trigger before building fence (private dossier `05a-toolbaker-threat-model-lesson.md`)

## Baseline security already in codebase (verified from code)

| Layer | Mechanism | File |
|---|---|---|
| Auth token | 32-byte random, constant-time verify | `src/shared/Security/AuthToken.cs` |
| Transport localhost | TCP bind `127.0.0.1`, Named Pipe PID-based + ACL user-only | `TcpTransportServer.cs`, `PipeTransportServer.cs` |
| Payload size | 1 MiB max per request | Both transports line 217, 245 |
| Rate limit | 20 req / 10 seconds per connection | Both transports line 92, 120 |
| Request timeout | 60 seconds | Both transports line 187, 215 |
| Session logging | JSONL (disk) + ObservableCollection (RAM) + WPF UI | `McpLogger.cs`, `McpSessionLog.cs`, `HistoryWindow.cs` |
| Adaptive bake gate | Runtime opt-in (`BIMWRIGHT_ENABLE_ADAPTIVE_BAKE` or config `enableAdaptiveBake`) + TaskDialog user approval | `SendCodeToRevitHandler.cs` |
| Secret masking in logs | `sk-*`, Bearer tokens, api_key fields auto-masked | `src/shared/Security/SecretMasker.cs` |

## ToolBaker / send_code security — ✅ Decided 2026-04-16

### Context of the decision

Initial proposal was "build sandbox + API whitelist + startup dialog" — defensive mindset. Maintainer push-back: "re-examine the question. Instead of building fences, audit HOW the baker is triggered, WHO creates."

After verifying code, the existing logic is sufficient — no complex sandbox needed. Lesson detail in private dossier.

### Flow verified from code

**Bake time:**
```
AI Agent (Claude/Haiku/GPT) proposes C# code
  ↓ adaptive-bake acceptance flow
  ↓ runtime opt-in is required
  ↓ TaskDialog shows USER (Yes/No, default=No)
User clicks No → fail, no compile
User clicks Yes → accepted code can compile, register, and persist through the baked-tool registry
```

**Startup (auto-load):**
```
App.OnStartup() [plugin-rXX/App.cs:40]
  ↓ _dispatcher.LoadBakedTools(BakedToolRegistry)
[CommandDispatcher.cs:75-94]
  ↓ foreach registry entry: Read .cs → Roslyn recompile → Register
❌ NO DIALOG — silent auto-load
```

**Runtime:**
```
AI calls run_baked_tool(name, params)
  ↓ [RunBakedToolHandler]
  ↓ lookup in dispatcher → execute
❌ NO DIALOG — AI can call any number of times
```

### Real threat model — re-examined

| Threat | Real? | Why |
|---|---|---|
| Malicious AI agent bakes bad code | ❌ No | AI runs on behalf of user via MCP client. Attacker cannot inject. |
| User accidentally approves bad code | ⚠️ Yes | Dialog already shows preview. User clicking Yes on strange code = user fault, not system fault. Default=No mitigates. |
| Attacker writes directly to `baked/` files | ⚠️ Yes | Attacker has file-system access = already owns the machine. ToolBaker isn't the weakest link. |
| Supply-chain (malicious commit in public repo) | ⚠️ Yes | General OSS problem. Not ToolBaker-specific. |

### Decision — ToolBaker security

| Item | Decision |
|---|---|
| **Activation flag** | `--enable-toolbaker` CLI arg, default **ON** (this is the flagship differentiator) |
| **Bake dialog** | Keep TaskDialog (Yes/No default No) |
| **Sandbox API whitelist** | ❌ NOT shipping — user approval is the natural gate |
| **Startup dialog** | ❌ NOT shipping — user already approved at bake time |

### 4 Gap Fixes locked in (implement later, not now)

| # | Gap | Fix | Effort |
|---|---|---|---|
| **G1** | Code preview truncated at 300 chars | Dialog expanded to show full code (scrollable if long) | S |
| **G2** | Startup auto-load is silent | Log "Loaded N baked tools: [list]" to WPF History + stderr console | XS |
| **G3** | No MCP command to remove a baked tool | Add handler `remove_baked_tool` (registry.Remove already exists) | XS |
| **G4** | User pestered when AI bakes multiple tools in a row | Add option 3 in dialog: "Skip for 8 hours" — cache in RAM (reset on Revit restart) | S |

## S3 — Token + transport security ✅ Decided 2026-04-16

Keep the baseline:
- 32-byte random token, constant-time verify
- TCP bind `127.0.0.1` only
- Named Pipe ACL user-only
- Rate limit 20 req/10s per connection

No changes needed — this is good.

## S4 — Response size limit + pagination ⏳ Deferred

Discuss later (deferred 2026-04-16).

## S5 — Error message sanitization (path leak) ✅ Decided 2026-04-16

### Problem

Handler throws exception → `CommandResult.Fail(ex.Message)` → propagates directly to AI + logs to JSONL + displays in WPF UI. Error messages can leak:
- Workspace paths: `D:\...\dev-workspace\...` (leaks internal folder structure)
- Source paths: `D:\...\Handlers\CreateLevelHandler.cs:line 45` (leaks repo layout)
- User paths: `C:\Users\<user>\Documents\Building-X.rvt` (leaks username + project)
- Internal DB paths: `D:\tenant-projects\XYZ\project.db` (leaks deployment details)

### Decision

| Aspect | Choice |
|---|---|
| **Mask level** | **L2 — keep filename, hide path.** E.g., `D:\...\CreateLevelHandler.cs:45` → `CreateLevelHandler.cs:45` |
| **Masking layer** | **B — central at `McpEventHandler.Execute()`.** Single point, no need to touch 35 handlers |
| **Applied to** | **All 3: (1) response to AI, (2) JSONL log on disk, (3) WPF History UI** |

### Implementation spec

Add a `SanitizeError(string)` function in `McpEventHandler.cs`, called before:
1. Setting `result.Error` in the response JSON
2. Passing into `McpLogger.Log(..., errorMsg: sanitized)`
3. Passing into `McpSessionLog.Add(... ErrorMessage = sanitized)`

```csharp
private static string SanitizeError(string error)
{
    if (string.IsNullOrEmpty(error)) return error;

    // 1. Windows absolute paths: D:\..., C:\Users\... → keep last filename only
    error = Regex.Replace(error,
        @"[A-Za-z]:\\(?:[^\\""'\s]+\\)*([^\\""'\s]+)",
        "$1");

    // 2. UNC paths: \\server\share\... → keep last filename only
    error = Regex.Replace(error,
        @"\\\\[^\\""'\s]+\\(?:[^\\""'\s]+\\)*([^\\""'\s]+)",
        "$1");

    // 3. Unix paths (safety): /home/..., /Users/... → keep last filename only
    error = Regex.Replace(error,
        @"/(?:home|Users)/[^/\s""']+/(?:[^/\s""']+/)*([^/\s""']+)",
        "$1");

    return error;
}
```

### Test cases

| Input | Output |
|---|---|
| `Could not find 'D:\...\internal-workspace\config.json'` | `Could not find 'config.json'` |
| `in D:\...\CreateLevelHandler.cs:line 45` | `in CreateLevelHandler.cs:line 45` |
| `Document 'C:\Users\<user>\Building-X.rvt' is null` | `Document 'Building-X.rvt' is null` |
| `Could not open 'D:\tenant-projects\XYZ\project.db'` | `Could not open 'project.db'` |

### Caveats

- Regex must cover paths in quotes, without quotes, with escaped `\\`, with forward slashes.
- Test with real Revit API exception stack traces before launch.
- Do NOT mask error class name, line number, or filename — keep them useful for debugging.

## S6 — Input validation strategy ✅ Decided 2026-04-16

**Decision:** Strict validate middleware at `McpEventHandler.Execute()`.

**Implementation:**
- Every handler already declares `ParametersSchema` (JSON Schema string). Currently exposed to MCP clients to read, not used for validation.
- Middleware: before calling `command.Execute(app, paramsJson)`, validate `paramsJson` against `command.ParametersSchema`.
- Schema mismatch → reject + error-as-teacher response:
  ```json
  {
    "success": false,
    "error": "Parameter validation failed: field 'elevation' must be number, got string",
    "suggestion": "Pass elevation as number: {\"elevation\": 3000}",
    "hint": "See tool description Example section"
  }
  ```

**Cost:** needs a JSON Schema validator. 2 options:
- Newtonsoft.Json.Schema (NuGet)
- Write a mini validator (~50 LOC) — enough for type + required check

**Scope:** all handlers. No per-tool choice — consistency matters more than backward-compat.

**Benefit:** fail fast (AI retries immediately), no need to touch 35 handlers, leverages existing `ParametersSchema`.

## S7 — Fail-closed default ✅ Decided 2026-04-16

**Decision:**
- Keep TCP bind `127.0.0.1`, Named Pipe local only.
- Add CLI flag `--allow-lan-bind` default **OFF** for edge-case remote Revit.
- When on: bind `0.0.0.0` + print warning to stderr:
  ```
  [RevitMCP] ⚠ LAN bind enabled. Token auth active but network exposed.
  ```

**Rationale:** Claude Desktop / Cursor / Cline run on the same machine as Revit — loopback is enough for 99% of use cases. LAN bind is for agentic platforms from remote machines — opt-in explicitly.

## Implementation priorities

| # | Item | Effort | Priority |
|---|---|---|---|
| S5 | Path mask middleware + regex | S | 🔴 Launch |
| S6 | Strict schema validation middleware | M | 🔴 Launch |
| S7 | `--allow-lan-bind` flag | XS | 🔴 Launch |
| ToolBaker G1-G4 | Full code preview + startup log + remove_baked_tool + Skip-8h | M | 🟠 Launch wave 2 |
| S4 | Pagination for large queries | M | ⏳ Deferred |

## Implications for other aspects

| Aspect | Impact from #5 |
|---|---|
| #6 Ecosystem | `--enable-toolbaker` default ON is the flagship differentiator for the product. Docker image must expose the flag. |
| #7 Testing | Test baked tool round-trip: bake → restart Revit → run. Test "Skip for 8h" expiry. |

## Pending verification

1. Test G1–G4 implementation with real Revit.
2. Benchmark performance impact of the compile step at startup (if there are 20+ baked tools).
