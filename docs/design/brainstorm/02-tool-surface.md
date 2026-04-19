# Aspect #2 — Tool Surface Design (Count, Shape, Composition)

> **Note:** entity references like `[[C-XX]]` are opaque tags. Mapping maintained in a
> private research index; not exposed in this repo. Public readers see that
> research was informed by external implementations without specific attribution.

**Status:** ✅ Decided 2026-04-16
**Prerequisites:** Aspect #1 (target persona, private) + Meta-decision M1 (repo-split decision, private)

## Baseline (post-repo-split, post-aspect-#1)

| Metric | Value |
|---|---|
| Current tools (generic, excluding internal-tenant-specific) | 28 |
| + Must-fill (Group A) | +8 |
| + [[C-07]] compat (Group C) | +6 |
| **Post-launch core target** | **42** |
| Public moat | 6 (ToolBaker × 3, `detect_system_elements`, `analyze_sheet_layout`, `analyze_usage_patterns`) |

Cross-check: 28 + 8 + 6 = 42 ✓

## D1 — Granularity

**Decision:** A as default (one verb per tool) + B (action-param gathered) only when the verbs are variants of the same root action in the same domain.

**Data from 2 precedents:**
- **[[C-07]] (first-party):** 4/6 tools pure A (`open_view`, `select_elements`, `zoom_to_elements`, `query_model`). 2/6 B-lite (output-mode variants: `export_views`, `get_element_data`).
- **[[C-01]] (niche fork):** majority-granular surface (~60 of ~70). B appears only in `modify_element` (move/rotate/mirror/copy = transformation variants) + `operate_element` (visibility variants).
- **Implicit rule in both:** A by default, B only for semantic variants.

**Examples applied here:**
- `select`, `hide`, `isolate`, `color` → grouped B (`operate_element`) ✓ — same visibility domain
- `move`, `rotate`, `mirror`, `copy` → grouped B (`modify_element`) ✓ — same transformation domain
- `create_wall` vs `create_floor` vs `create_room` → split A ✓ — param schemas differ too much
- `select_elements` vs `zoom_to_elements` → split A ✓ — genuinely different verbs

**Validation:** Defer Haiku benchmark to aspect #7. If data skews, revise.

## D2 — Naming Convention

**Decision:** Hybrid — keep heritage names for the 21 tools overlapping with [[C-01]] + use clean (`verb_noun`) names for new tools.

- 21 heritage names preserved → users switching from [[C-01]] hit zero friction.
- 14 new tools (8 must-fill + 6 [[C-07]] compat) named by the [[C-07]] `verb_noun` pattern.
- Awkward heritage names (`ai_element_filter`, `create_line_based_element`) accepted as-is. No rename.
- 6 [[C-07]] compat tools use the exact [[C-07]] tool names (`open_view`, `query_model`, `export_views`, `get_element_data`, `select_elements`, `zoom_to_elements`). `select_elements` overlaps with heritage — OK.

## D3 — Grouping / Namespacing

**Decision:** Flat names (snake_case, no dotted namespace).

- Both precedents are flat.
- Weak models are already trained on flat patterns.
- Progressive disclosure (innovation #1) already provides a grouping mechanism → no extra namespace needed.

## D4 — Schema Weight

**Decision:** Lean schema — server-side validation. NO embedded enums in tool schema.

- [[C-07]] embeds `BuiltInCategory` (~4000 values, ~21k tokens) = anti-pattern.
- Lean schema → lighter prompt (~3k tokens for 40 tools vs 21k+ for 1 [[C-07]] tool).
- Combined with Error-as-Teacher (innovation #3): error response returns `suggestion` + `hint` with common values → AI self-corrects.

Example:
```json
// Schema declaration (lean)
{ "category": { "type": "string", "description": "Revit category, e.g. 'OST_Walls', 'OST_Doors'" } }

// Error response on bad call
{
  "error": "Unknown category 'walls'",
  "suggestion": "Did you mean 'OST_Walls'?",
  "hint": "Call list_categories to see the full list"
}
```

## D5 — Parameter & Response Standards

### D5.1 Units
- **Default:** mm (metric) for every tool we author.
- **Exception:** 6 [[C-07]] compat tools use feet (exact schema match).
- **Server:** converts mm ↔ Revit internal feet.

### D5.2 Element Identification
- **Element (wall, door, pipe):** ID int. Unambiguous.
- **View / Level / Category:** accept name (string). Lookup via `get_*` tools.
- Rationale: elements have many same-named instances → ID is safer. View/level/category are usually unique → name is AI-friendly.

### D5.3 Response Envelope
```json
// Success
{ "success": true, "data": { ... } }

// Error (enhanced for error-as-teacher)
{
  "success": false,
  "error": "...",
  "suggestion": "...",
  "hint": "..."
}
```

## 3 Innovations (all 3, not pick-2)

### Innovation #1 — Progressive Disclosure via Toolset Gating
- ~~Meta-tool `run_specialized`~~ → **REVISED 2026-04-16** after ecosystem benchmark (private dossier).
- Use `--toolsets query,create,view` CLI arg to filter tools at server start (config-time, not runtime).
- Pattern is industry-standard: [[C-06]] (`--toolsets`), [[C-05]] (`--caps`).
- Default ~25 tools (query + create + view + meta), expand via config.
- **Value:** weak-model context budget saved 40-60%, works on EVERY MCP client (no dependency on spec feature).
- **Unique in Revit niche:** no surveyed external implementation has toolset gating.

### Innovation #2 — ToolBaker Flagship
- Harden the existing pipeline: sandbox + confirmation UX + version.
- Promote to "first-class feature" in README / launch copy.
- **Value:** user / platform can cook tools for their specific use case.
- **Unique:** neither [[C-07]] nor [[C-01]] has this.

### Innovation #3 — Error-as-Teacher (self-correcting loop)
- Every error response includes `suggestion` + `hint`.
- Weak models use this as a retry signal → tool-pick accuracy goes up.
- **Unique:** both precedents return generic errors.

## Implications for other aspects

| Aspect | Impact from #2 decisions |
|---|---|
| #3 Architecture | Progressive disclosure needs a refresh mechanism for the tool list. ToolBaker needs sandbox design. |
| #4 Weak-model UX | Error-as-teacher is the UX spine. Tool descriptions must be rich (common values inline as examples). |
| #5 Security | ToolBaker sandbox + confirmation is mandatory. Lean schema doesn't embed sensitive enums. |
| #6 Ecosystem | Progressive disclosure needs per-client testing (Claude Desktop, Cursor, Cline). |
| #7 Testing | Haiku benchmark on the actual tool surface: validates D1 granularity + progressive disclosure effectiveness. |
| #8 Localization | Tool names stay EN. Tool descriptions EN default, VN optional post-launch. |

## Pending verification

1. **Haiku benchmark** — validate A-default granularity vs B on the actual tool list. Owned by aspect #7.
2. **MCP client compat** — test progressive disclosure refresh on Claude Desktop, Cursor, Cline. Owned by aspect #6.
3. **ToolBaker sandbox feasibility** — AppDomain sandbox on .NET 4.8 vs .NET 8 process isolation. Owned by aspect #5.
4. **Heritage name semantic parity** — 21 tools overlapping with [[C-01]] may differ in behavior. Integration test in aspect #7.

---

## Learnings from 2026-04-19 sprint

**Status:** candidate additions, not decided. Source: private sprint roadmap. Does NOT revise D1–D5 or Innovations #1–#3.

### 🔴 Table-stakes gaps

| ID | Learning | Effort | Source |
|----|----------|--------|--------|
| L-01 | Tighten tool descriptions to ~40-char imperative verb-lead, params in schema only | S | [[C-05]] |
| L-02 | Add per-tool MCP `ToolAnnotation` type field (readOnly/action/input/assertion) | S | [[C-05]] |
| L-03 | Structured `list_tools_grouped` with per-category counts + paramDocs dictionary | S | [[C-03]] |
| L-05 | Ship view-naming linting tools (suggest corrections + analyze patterns) | S | [[C-09]] |
| L-06 | Built-in pagination (page_size/cursor/next_cursor) on list-returning tools | M | [[C-04]] |
| L-11 | Reflection triad (`invoke_method` + `reflect_get` + `reflect_set`) behind gate | L | [[C-08]] |

### 🟠 Moat-extending

| ID | Learning | Effort | Source |
|----|----------|--------|--------|
| L-14 | Audit-metadata `confidence: measured/estimated/fallback` on quantity tools | S | [[C-10]] |
| L-16 | `ToolAnnotations(destructiveHint, readOnlyHint)` on every handler | M | [[C-04]] |
| L-17 | Consolidate fine-grained tools into `manage_<noun>` with action discriminator | M | [[C-04]] |
| L-18 | Multi-host tool registry (MCP + OpenAI + Anthropic + Gemini serializers) | M | [[C-09]] |
| L-19 | `plan_and_execute_workflow` — batch_execute plan-review + deferred-execute | M | [[C-09]] |
| L-20 | Discipline-run pre-baked tools (`run_structural_boq`, `run_mep_boq`) | M | [[C-10]] |
| L-31 | In-Revit WPF settings page for per-command runtime toggle (ribbon button) | L | [[C-11]] |
| L-32 | Accessibility-tree-style `get_view_snapshot` with stable `[ref=eN]` IDs | L | [[C-05]] |

### Cross-aspect notes

- **L-01** primary target was `#4 weak-model UX` (description quality → Haiku retry rate). Routed here because it's an axis-1 tool-config decision; cross-ref added to `04-weak-model-ux.md`.
- **L-32** `get_view_snapshot` also touches `#3 architecture` (state-snapshot mechanism). Decide ownership at next planning session.
- **L-17 manage_noun pattern** conflicts with D1 "A default (one verb per tool)". Needs explicit reconsideration, not silent merge.

### Next-step

Pick 1–2 🔴 from L-01 / L-02 / L-03 / L-05 (all effort S) for v0.2 tool-surface revision. L-01 is the strongest — immediate Haiku-benchmark impact (aspect #4 + #7 cross-link).
