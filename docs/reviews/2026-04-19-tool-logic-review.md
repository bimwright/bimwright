# Tool Logic Review — 2026-04-19

**Authored by:** Claude Opus 4.7 (1M ctx) via parallel sub-agent sweep + manual verify.
**Scope:** All 36 MCP tools in `src/server/Program.cs` + their handlers in `src/shared/Handlers/` + ToolBaker infra (`src/shared/ToolBaker/`).
**Intent:** Snapshot of issues to consider during the next-session general review. Nothing fixed yet. Listed in order of ship-blocking severity.
**Do not cherry-pick from this list without re-reading the handler** — some claims trusted the sub-agent and were not all deep-verified.

## Verify-status legend

- `V` — verified myself by reading the exact file/line during this session.
- `S` — from sub-agent review, not independently re-verified.

---

## 🔴 Critical (4) — fix before cutting v0.2

### C1 — `operate_element` schema/switch mismatch `[V]`

**File:** `src/shared/Handlers/OperateElementHandler.cs:14`

Schema advertises `operation` enum as `["move", "rotate", "mirror", "copy", "color"]`. The switch (`:33`) handles `select / hide / unhide / isolate / setcolor`. **Zero overlap.** Any MCP client that validates against the advertised schema sends `operation:"move"` and gets `Unknown operation`.

Fix: rewrite `ParametersSchema` to `enum:["select","hide","unhide","isolate","setcolor"]`. Server-tool description in `Program.cs` already documents the correct set — this is purely the handler-side schema string.

### C2 — `create_room` broken on R23–R27 `[V]`

**File:** `src/shared/Handlers/CreateRoomHandler.cs:58-60`

```csharp
#else // R23+
var phase = doc.Phases.get_Item(doc.Phases.Size - 1);
var room = doc.Create.NewRoom(phase);
room.Location.Move(new XYZ(point.U, point.V, level.Elevation));
#endif
```

`NewRoom(Phase)` creates an **unplaced** room. `room.Location` is null for unplaced rooms → `.Move(...)` throws NRE. The in-code comment claims `NewRoom(Level, UV)` was "removed in R23+" — **this is false**; the overload still exists in R23 through R27.

Fix: delete the `#if/#else` guard. Use `doc.Create.NewRoom(level, point)` on all versions.

### C3 — `get_current_view_info` crashes on 3D / section / drafting views `[V]`

**File:** `src/shared/Handlers/GetCurrentViewHandler.cs:27`

Calls `RevitCompat.GetId(view.GenLevel?.Id)`. For 3D, section, drafting, and sheet views, `view.GenLevel` is null → `?.Id` returns null → `RevitCompat.GetId(null)` crashes (verified `src/shared/Infrastructure/RevitCompat.cs:13` dereferences `id.Value` / `id.IntegerValue` without null-check).

`RevitCompat.GetIdOrNull(...)` already exists at `RevitCompat.cs:23` and handles null safely.

Fix: change `GetId` → `GetIdOrNull`.

### C4 — `create_surface_based_element` NREs on empty project `[S]`

**File:** `src/shared/Handlers/CreateSurfaceBasedElementHandler.cs:55-74`

When caller omits `typeId`, handler falls back to `FirstElement() as FloorType` / `as CeilingType`. Empty project (no family loaded) → `floorTypeEl == null` → `floorTypeEl.Id` NRE on line 63 (or 74 for ceiling). Outer try/catch surfaces the error as `"Failed to create floor: Object reference not set"` — misleading.

Fix: after the fallback assign, null-check and return `CommandResult.Fail("No floor type loaded in the project.")` / `"No ceiling type loaded..."`.

---

## 🟠 Medium (5)

> Note: original internal review listed 6 Medium findings. One (text-prefix SQL
> bypass in an internal-tenant-specific handler) is omitted here because the
> affected handler is not in the public codebase.

### M2 — `BakedToolRegistry` non-atomic `registry.json` writes `[V]`

**File:** `src/shared/ToolBaker/BakedToolRegistry.cs:42, 65, 74`

`File.WriteAllText(_registryPath, json)` three places. Crash / power-loss mid-write → corrupt JSON. `Load()` at `:80-91` wraps in `catch { }` and returns an empty registry. Net effect: **all baked tools silently disappear** after a crash.

Fix: write to `registry.json.tmp` then `File.Replace(src, dst, null)` (same pattern the `install.ps1 -WireClient` already uses via `Write-ConfigAtomic`).

### M3 — `delete_element` swallows failure reasons `[V]`

**File:** `src/shared/Handlers/DeleteElementHandler.cs:47-50`

`catch { failed++; }` drops the exception. Transaction still commits the successful deletes. Caller gets `{deleted, failed, total}` — accurate counts but zero diagnostics for why specific IDs failed. Not data loss, but a partial-failure UX hole.

Fix: `catch (Exception ex) { failedIds.Add(id); errors.Add($"{id}: {ex.Message}"); }` and return both lists in the DTO. Consider whether partial-commit semantics are desired or whether the whole transaction should roll back on first failure.

### M4 — Unbounded `FilteredElementCollector` on UI thread `[S]`

**Files:** `src/shared/Handlers/AnalyzeModelStatisticsHandler.cs:21-22`, `src/shared/Handlers/GetModelOverviewHandler.cs:45-55`

Iterate every element in the document on the Revit UI thread. On 50k–300k-element production models this freezes Revit. Not a crash; it's a stated-use-case perf regression.

Fix options (any of):
- Add a category filter upfront and aggregate per-category.
- Use `ToElementIds()` only and count by `.OfCategoryId()` subqueries.
- Document the perf characteristic in the tool description.

### M5 — `BakedToolRegistry` is not thread-safe `[V]`

**File:** `src/shared/ToolBaker/BakedToolRegistry.cs` (whole class)

`Dictionary<string, BakedToolMeta>` + `File.WriteAllText` without locks. Works today because the MCP plugin serializes every command via `ExternalEvent` onto the Revit UI thread, so concurrent access is currently impossible. But the assumption is fragile — if anyone ever dispatches off-thread, silent dict corruption is a possibility.

Fix: add a private `object _lock` and wrap `Save / IncrementCallCount / Remove / Load` in `lock (_lock) { ... }`. Cheap insurance.

### M6 — Legacy baking preview caps at 300 chars `[V]`

**File:** `src/shared/Handlers/BakeToolHandler.cs:41`

TaskDialog `MainContent` shows `code.Substring(0, 300) + "..."`. Malicious code can be benign for 300 chars then do `File.Delete(@"C:\Windows\*")` at char 301. User clicks Yes without seeing it.

The old compile-time debug gate was the real security boundary — preview was UX, not enforcement. Still worth either (a) raising the cap / showing "preview truncated" or (b) changing the TaskDialog footer text to explicitly say "preview may hide later code — only bake code you trust completely".

---

## 🟡 Low (4)

> Note: original internal review listed 5 Low findings. One (SQL string-interp
> in an internal-tenant-specific handler) is omitted here because the affected
> handler is not in the public codebase.

- **L1** `RunBakedToolHandler.cs:31` — `IncrementCallCount` fires before `Execute`. Failed executions still bump the counter. Move after success.
- **L2** `GetSelectedElementsHandler.cs:27` — `el.Name` NRE possible if an element was deleted externally between selection and this handler running. `doc.GetElement(id)` can return null. Null-guard.
- **L3** `ToolCompiler.cs:70-71` — Assembly dedupe by `.Version` silently picks highest. If baked code targets a specific older API, this upgrades invisibly. Rare; worth a log if conflict detected.
- **L5** `BakedToolRegistry.cs:90` — `catch { }` in `Load()` silently swallows JSON-parse errors. Log at minimum so corrupted-registry cases are diagnosable.

---

## ✅ Clean pass (at least 20 handlers)

Read and found nothing substantive to flag:

`ShowMessageHandler`, `AiElementFilterHandler` (unit conversion via `ConvertToDisplayUnits` is correct, `SpecTypeId` usage proper), `ColorElementsHandler`, `CreateGridHandler`, `CreateLevelHandler`, `CreateLineBasedElementHandler`, `CreatePointBasedElementHandler`, `CreateViewHandler`, `AnalyzeSheetLayoutHandler`, `DetectSystemElementsHandler` (BFS with visited-set, bounding box guarded), `ExportRoomDataHandler`, `TagAllWallsHandler`, `TagAllRoomsHandler`, `PlaceViewOnSheetHandler` (pre-checks `CanAddViewToSheet`).

*(Internal-tenant-specific handlers also reviewed and cleared, but omitted from this list — not present in the public codebase.)*

Bake infrastructure architecture is sound overall — the issues above are implementation polish, not design flaws. Re-compile on plugin load (`CommandDispatcher.LoadBakedTools` at `src/shared/Infrastructure/CommandDispatcher.cs:75`) correctly survives Revit restart.

---

## Suggested fix order when the next session picks this up

1. **C1 + C2 + C3 + C4** together in one commit or four small ones — these are trivial code changes (≤5 lines each) and independently testable. Mechanical. *(Shipped as v0.1.2, 2026-04-19.)*
2. **M2** atomic registry write — reuse the `install.ps1 -Uninstall` atomic-write pattern so there is one consistent approach in the codebase.
3. **M3** `delete_element` error reporting — decide partial-commit vs. atomic-rollback semantics first (maintainer decision, not mechanical).
4. **M4 M5 M6 + all L** — batch into a "polish" commit for v0.3.

## Methodology + limitations

- Bake infra (4 files) read line-by-line by Opus 4.7.
- Other 33 handlers reviewed by a `feature-dev:code-reviewer` sub-agent; 4 of its critical claims were spot-checked by re-reading the file (C2, C3 verified `V`; C4 trusted `S`; the sub-agent's `DeleteElement` classification was reclassified from Critical to Medium after rereading — the partial-commit is a UX hole, not data loss).
- Nothing was run in Revit. All claims are static-analysis only.
- Only the Claude Code + Haiku benchmark scope was exercised end-to-end today; the other tools have not been touched this session.

## Related artifacts

- Benchmark report (public): `docs/benchmarks/2026-04-19-l01-haiku-validation.md`
- L-01 commit: `ad3086e`
- L-02 commit: `47496b7`
- L-04 commit: `d73236e`
- Review commit: this file
