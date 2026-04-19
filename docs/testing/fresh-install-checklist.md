# Fresh Install Test — End-to-End Onboarding Verification

Walk the public install flow on a fresh machine as a first-time user. Goal: verify install-from-zero friction before the first non-dev user files an issue.

Target: this repo at latest `master` or latest tag, reached purely via README starting from `https://github.com/bimwright/rvt-mcp`.

## Scope

**In scope:**
- F1: Prep — define fresh-machine boundaries, target Revit version, target MCP client, finding-log skeleton.
- F2: Install-from-zero flow for one primary Revit version (default R24), reading only the public README + linked docs. Record every friction point verbatim.
- F3: Functional smoke — MCP handshake + one trivial handler call (`get_current_view_info`).
- F4: Sample coverage for remaining target-framework families (R22 for .NET 4.8 repeat, R25 for net8.0, R27 for net10.0 + Named Pipe transport) — if primary passes.
- F5: Synthesize — bug list, friction list, concrete README/script/docs improvements.

**Not in scope:**
- Multi-Revit concurrent test.
- Non-Windows install (Revit is Windows-only; not relevant).
- Non-Claude MCP clients (covered separately).
- Automated CI runtime smoke.
- Performance / load testing.
- Security penetration testing.

## Primary-test assumptions (override at F1-001 if wrong)

| Assumption | Default | Override via |
|---|---|---|
| Fresh machine type | Windows 11 VM (Hyper-V or VMware), no dev tools pre-installed | F1-001 |
| Primary Revit version | Revit 2024 (.NET Framework 4.8, most common install in user base) | F1-002 |
| Primary MCP client | Claude Code CLI | F1-003 |
| Starting point for the "user" | `https://github.com/bimwright/rvt-mcp` README only | F1-004 |
| License for Revit on test machine | User has own license / trial on test VM | out-of-scope |

## Phase F1 — Prep (4 tasks)

Goal: lock test parameters, create the finding-log file, confirm the fresh machine is ready.

- [ ] F1-001: Confirm fresh-machine type (VM / spare / coworker). Record OS version, CPU/RAM, Revit installer source. Create a finding-log file with skeleton sections: "Test environment", "F2 install-from-zero log", "F3 smoke-test log", "F4 other-version log", "F5 synthesis".
- [ ] F1-002: Confirm primary Revit version (default R24). Verify the chosen version is installed and licensed on the test machine.
- [ ] F1-003: Confirm primary MCP client (default Claude Code CLI). Note whether a clean install is available on the test machine or needs to be acquired as part of F2.
- [ ] F1-004: Walk the public README once on the test machine at the starting URL. Record: does a first-time reader know what this is, what they need, and what to do next? One-paragraph impression logged.

## Phase F2 — Install-from-zero, primary version (6 tasks)

Goal: follow the public README instructions exactly — no prior knowledge, no shortcuts, no "I know this part" skips. Log every friction point verbatim.

- [ ] F2-001: Install .NET SDK per README (version required for running the MCP server). Log: was the required SDK version clearly specified? Did the link work? Install time. Any PATH/env issues.
- [ ] F2-002: Install the Bimwright plugin for the primary Revit version — following README install method (expected: download plugin ZIP from the Release page, extract to `%APPDATA%\Autodesk\Revit\Addins\<year>\Bimwright\`, addin manifest at parent level). Log: was the path clear? Did the ZIP contain everything needed? Any DLL loading errors on next Revit launch.
- [ ] F2-003: Install the Bimwright MCP server — following README (expected: NuGet global tool install `dotnet tool install -g Bimwright.Rvt.Server`, or direct from Release assets). Log: did the command work first-try? Any NuGet source issues. Install time.
- [ ] F2-004: Install / configure the MCP client (Claude Code CLI) to register the Bimwright server. Log: where does the client config file live? Was the `.mcp.json.example` snippet in the README copy-pasteable? Any token/auth setup needed on first run.
- [ ] F2-005: Open Revit, verify the Bimwright ribbon panel appears without error dialog, verify the "Start MCP" button is clickable and the server starts. Log: any startup error dialogs (screenshots). Discovery file presence at `%LOCALAPPDATA%\RevitMcp\` — name + content.
- [ ] F2-006: Total time from first-click on README to "server running + ribbon visible". Friction-point summary (ranked by severity) appended to the finding-log §F2.

## Phase F3 — Functional smoke, primary version (3 tasks)

Goal: prove the end-to-end path works, not just that the components installed.

- [ ] F3-001: From Claude CLI on the test machine, run `/mcp` — confirm `bimwright` is listed as a connected server. Log the exact output.
- [ ] F3-002: Call `get_current_view_info` with Revit open on a default blank project. Expect DTO `{viewName, viewType, levelName, scale}`. Log exact response. Any error = F-phase bug.
- [ ] F3-003: Call one more non-trivial handler (suggested: `analyze_model_statistics` on a blank project — expected: empty counts, no errors). Log response. This verifies the broader tool surface, not just the simplest path.

## Phase F4 — Sample other target-framework families (3 tasks)

Goal: verify .NET Framework 4.8 / net8.0 / net10.0 all work on the fresh machine. Only run if F2 + F3 pass for the primary version.

Each task repeats F2-002 + F2-005 + F3-001 + F3-002 for the named version. Document only *differences* from the primary-version log to keep the finding file lean.

- [ ] F4-001: R22 (.NET Framework 4.8 — sanity that it's not just R24-specific).
- [ ] F4-002: R25 (net8.0 — first .NET 8 shell; packaging differences: `EnableDynamicLoading=true`, runtime identifiers in ZIP).
- [ ] F4-003: R27 (net10.0 + Named Pipe transport — most divergent shell; biggest risk surface).

## Phase F5 — Synthesize and apply (3 tasks)

Goal: turn raw findings into actionable outputs.

- [ ] F5-001: Read the finding-log end-to-end. Produce three lists in a new section §F5-synthesis:
  1. **Bug list** — anything that failed or produced an error. Each item: severity (P0 blocks install / P1 blocks functional smoke / P2 non-blocking), reproduction steps, suggested fix location.
  2. **Friction list** — anything that worked but was painful (unclear instructions, multi-step copy-paste, PATH surprises, missing prerequisites). Each item: friction type, user-impact estimate, fix suggestion.
  3. **README / script / docs improvement list** — concrete edit proposals derived from items 1 and 2.
- [ ] F5-002: Triage improvement list. Small items (README wording, one-line script fix, new docs paragraph) → apply in-session as separate commits. Larger items (packaging overhaul, new installer, multi-client support) → append to the project's TODO / issues list, tagged for future consideration.
- [ ] F5-003: Write a short closing summary at the top of the finding-log: pass/fail verdict, count of P0/P1/P2 bugs, count of in-session fixes applied, count of deferred items.

## Success criteria

1. Every F2 / F3 / F4 task has a log entry even if "passed with no friction".
2. All P0 bugs found in F2 / F3 either fixed in-session or documented with a clear owner and scope.
3. The §F5-synthesis section has three non-empty lists (even if one says "none found").
4. At least one primary-version path (F2 + F3) passes end-to-end on the fresh machine.
5. If any F4 task fails → documented + escalated, but does not block verification of F2 / F3 (primary path is the launch-hygiene bar; other versions are coverage).

## Ad-hoc (incl. hotfix)

- [ ] *(placeholder — document here any mid-phase discovery that needs handling outside the main task list)*
