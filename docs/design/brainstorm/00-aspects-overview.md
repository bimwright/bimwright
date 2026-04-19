# Strategic Axes Brainstorm — Overview & Progress

> **Note:** entity references like `[[C-XX]]` are opaque tags. Mapping maintained in a
> private research index; not exposed in this repo. Public readers see that
> research was informed by external implementations without specific attribution.

**Started:** 2026-04-15
**Session owner:** Khoa (maintainer) + AI assistant
**Purpose:** Before deciding next-step post-launch-prep, review 8+ strategic axes so positioning + codebase + roadmap are aligned. Walk aspects **one at a time**, no skipping.
**Update 2026-04-19:** Aspect **#9 Knowledge packs** opened from internal learning sprint follow-up. Table now lists 9 aspects.

**Input sources:**
- Codebase baseline: [`docs/architecture/`](../../architecture/)
- External-research baseline: private research index (obfuscated references via `[[C-XX]]`)
- Maintainer direction: in-session, notes recorded into per-aspect files

**Session rules:**
- Each aspect documented in its own file (`01-*.md`, `02-*.md`, …) so future sessions load correctly.
- Decisions include "implications for other aspects" — avoid siloing.
- Data-driven where possible (cross-check counts, enum options). No guessing.
- Decisions are revisable — if new data conflicts, update the file; never hide the contradiction.

## 9 aspects

| # | Aspect | Status | Detail file |
|---|---|---|---|
| 1 | Target model + persona + use case | ✅ Decided 2026-04-15 | `01-target-persona.md` *(private)* |
| 2 | Tool surface design (count, shape, composition) | ✅ Decided 2026-04-16, +14 sprint L-IDs 2026-04-19 | [02-tool-surface.md](02-tool-surface.md) |
| 3 | Architecture & integration depth | ✅ Decided 2026-04-16 (A1 deferred), +9 sprint L-IDs 2026-04-19 | `03-architecture.md` *(private)* |
| 4 | Weak-model UX (description, errors, prompts) | ✅ Decided 2026-04-16, +L-01 cross-ref 2026-04-19 | [04-weak-model-ux.md](04-weak-model-ux.md) |
| 5 | Security / governance | ✅ Decided 2026-04-16 (S4 deferred) | [05-security.md](05-security.md) |
| 6 | Ecosystem fit (packaging, registry, agentic AI platforms) | 🟡 Partial 4/7 — E1/E2/E3/E6 decided, E4/E5/E7 deferred; +4 sprint L-IDs routed (no file yet) | See notes below |
| 7 | Testing & model drift | ✅ Shipped 2026-04-18 (aspect-7 testing drift checklist); +L-28 sprint candidate routed | — (no brainstorm file; design-doc deferred) |
| 8 | Localization | ✅ Closed 2026-04-17 post-launch (passive, VN mirror-only); +L-10 VI-KB sprint candidate routed | — (passive mode; see hybrid positioning approach) |
| 9 | Knowledge packs | ⏳ Pending — opened 2026-04-19 from sprint follow-up; 4 L-IDs (L-12/13/29/30) | [09-knowledge-packs.md](09-knowledge-packs.md) |

Cross-check: 5 decided + 1 partial + 2 shipped-or-closed + 1 pending-new = 9 ✓

## Aspect #6 — Partial decisions (2026-04-16 session)

Brainstorm paused after 4/7 E. Session shifted to execution on already-decided items.

**✅ E1 — Brand + License + Domain:** name `bimwright` (lowercase everywhere, C# namespace `Bimwright.*`), license **Apache-2.0**, domain deferred. Org `bimwright` reserved on GitHub.

**✅ E2 — Packaging day-1:** NuGet dotnet tool (server) + GitHub Release ZIP with `install.ps1` (plugin). Wave 2: MSI installer signed. Wave 3: DXT bundle. Skip: Docker, npm wrapper, PyPI.

**✅ E3 — MCP Registry:** Publish +3 days post-launch, first version **v0.1.1**, namespace **`io.github.bimwright/bimwright`** (post-transfer private → org public).

**✅ E6 — Agentic AI Platform strategy (3-phase):**
- 🔴 Day-1: MCP-native host-ready (no submission needed) + native MCP client connectors
- 🟠 Wave 2: skills-registry platforms (after governance stable, monitor security)
- 🟡 Wave 3: no-code platforms (workflow builders / agent platforms) — pending maintainer selection

**⏳ Deferred — E4 (aggregator strategy), E5 (MCP client compat matrix), E7 (governance + launch channel).** Reason: solo-dev bandwidth constraint. Minimum-viable launch → repo public + README + v0.1.0 release + GitHub topics → auto-scan free reach. Active submissions (Smithery / mcp.so / MCP Market / Cursor Directory / PulseMCP / Cline / MseeP) staged to v0.2 / v0.3 / v1.0 when bandwidth + user pull allow.

**Aspect #6 detail file (`06-ecosystem-fit.md`):** not yet written — will draft when brainstorm resumes or when plans need reference.

## Meta-decisions (pre-aspect)

Beyond the 8 strategic aspects, the session produced meta-level decisions that affect all downstream aspects — **read before walking aspect #2+**.

| # | Decision | Status | Detail file |
|---|---|---|---|
| M1 | Repo split — public (new) vs internal — strategy P2 "fresh repo" | ✅ Decided 2026-04-16 | `00b-repo-split-decision.md` *(private)* |

## Walk order

Proposed: 1 → 2 → 4 → 3 → 5 → 6 → 7 → 8 (strategic → tactical).
Maintainer confirms / overrides per aspect.

## Cross-aspect dependency map (grows)

- **#1 decided → constraints for #2, #4, #6** (weak-model + Agentic Platform target)
- **#1 decided → internal-tenant tools move to opt-in module → impact on #3 (architecture)** *(revised by M1: internal-tenant specifics out of public repo scope)*
- **#2 decided → constraints for #3** (progressive disclosure mechanism, ToolBaker sandbox)
- **#2 decided → constraints for #4** (error-as-teacher = UX focus)
- **#2 decided → constraints for #7** (Haiku benchmark validates granularity + progressive disclosure)

## How to resume a future session

1. Read this file first (current status of 9 aspects).
2. Read the `0N-*.md` files already decided to understand locked-in choices.
3. Continue with the next ⏳ Pending aspect per the walk order.
4. Update the tracker table when an aspect is decided.

## Sprint integration 2026-04-19 — L-ID routing

Source: private learning-sprint roadmap (49 candidates: 11🔴 + 22🟠 + 16⚪). Only 🔴+🟠 = 33 are routed; ⚪ = 16 stay rejected in the private sprint dossier only.

| Aspect | 🔴 count | 🟠 count | Total | Location |
|---|---|---|---|---|
| #2 Tool surface | 6 | 8 | 14 | Appended to `02-tool-surface.md` |
| #3 Architecture | 1 | 8 | 9 | Appended to `03-architecture.md` *(private)* |
| #4 Weak-model UX | 0 | 0 | 1 (L-01 cross-ref) | Cross-ref in `04-weak-model-ux.md` |
| #6 Ecosystem | 3 | 1 | 4 | No brainstorm file yet — routed via this table: L-04 (per-year ZIP), L-07 (Inno installer), L-09 (npx wrapper), L-33 (A2A adapter). Will land when aspect #6 `06-ecosystem-fit.md` drafts. |
| #7 Testing | 0 | 1 | 1 | L-28 (failure-classification + method_gym). Routed via this table; aspect #7 has no brainstorm file (shipped as checklist). Next-session owner adds to v0.2 testing plan. |
| #8 Localization | 1 | 0 | 1 | L-10 (VI locale KB `category_alias_vi.json` + `glossary_vi.json`). Aspect #8 closed passive — L-10 may reopen or fold into #9 K5 decision. |
| #9 Knowledge packs (new) | 0 | 4 | 4 | New file `09-knowledge-packs.md` (L-12 / L-13 / L-29 / L-30) |

Arithmetic: 14 + 9 + 1 + 4 + 1 + 1 + 4 = **34** mentions. Unique L-IDs = **33** (L-01 cross-listed at #2 primary + #4 cross-ref). ✓

Next-step sequencing: ship highest-leverage 🔴 trio (L-01 + L-02 + L-04) first — all effort S, all in this table.
