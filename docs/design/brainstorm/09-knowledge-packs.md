# Aspect #9 — Knowledge Packs

> **Note:** entity references like `[[C-XX]]` are opaque tags. Mapping maintained in a
> private research index; not exposed in this repo. Public readers see that
> research was informed by external implementations without specific attribution.

**Status:** ⏳ Pending — opened 2026-04-19 from internal learning sprint follow-up.
**Prerequisites:** Aspect #1 (target persona, private) + Aspect #2 (`02-tool-surface.md`) + private sprint roadmap output.

## Why a new aspect (not #2, #3, or #6)

4 sprint L-IDs (L-12 / L-13 / L-29 / L-30) form a coherent knowledge-asset axis that none of aspects #2–#8 cleanly owns:

- **#2 Tool surface** — schemas, naming, composition. Knowledge packs are input *data* to tools, not tools themselves.
- **#3 Architecture** — transport, dispatch, threading. Knowledge packs are content on top of architecture.
- **#6 Ecosystem** — packaging, registry, platforms. Knowledge packs are internal content, not distribution.
- **#4 Weak-model UX** — error messages, prompts. Knowledge packs back tools, not prompts.

A knowledge pack = **structured data the server loads at startup that teaches tools about domain conventions** — firm prefixes, discipline tags, client-spec patterns, sector templates. Separate lifecycle from tool code.

## The 4 candidate L-IDs

| ID | Marker | Effort | Learning | Source |
|----|--------|--------|----------|--------|
| L-12 | 🟠 | S | Category-taxonomy grouping in README tool-list (17–21 named sections) | [[C-08]] |
| L-13 | 🟠 | S | Firm-profile JSONs as named-standards layer + `detect_firm_profile` tool | [[C-03]] |
| L-29 | 🟠 | M | Template-pack pattern (sector + firm + validation + preflight + resolved cache) | [[C-03]] |
| L-30 | 🟠 | M | Ship first-class `bimwright-rvt-skill/` Claude Code skill folder | [[C-04]] |

All 4 are 🟠 moat-extending (not 🔴 table-stakes). No surveyed external implementation ships a complete version — [[C-03]] comes closest with a `template_packs/` + `validation.json` pattern, but targets sheet-set correctness, not spec compliance.

## Category-defining gap (from sprint §90)

> **Revit compliance-validation knowledge pack.** No surveyed entity ships a schema-backed library of common client-spec violations (firm prefixes, elevation-led level names, discipline tags on panel schedules, naming convention audits). Content-curation + engineering combo that's hard to clone in 1–2 releases.

This is the motivating gap. **L-13 + L-29** are primary vehicles; **L-12 + L-30** are supporting assets.

## Open decisions (next session)

### K1 — Scope boundary: content-only vs tool-backed?

- **Option A:** pure JSON/YAML packs loaded at startup, consumed by existing tools via lookup.
- **Option B:** each pack ships companion tool(s) (e.g., `detect_firm_profile`, `validate_naming`).
- **Tradeoff:** A is cheaper + matches [[C-03]]'s `validation.json` shape. B is more discoverable but inflates tool surface (conflicts with #2 D1 lean-granularity).

### K2 — Lifecycle model

- Ship-with-server (baked) vs. user-loaded folder vs. registry-downloadable packs.
- Ship-with-server = simplest, limits customization. User-loaded folder = needs file-watcher + hot-reload.

### K3 — Schema governance

- Who owns the pack schema? Can users extend or only override?
- JSON Schema validation at load time is non-negotiable — lesson from [[C-03]]: packs without schema gate become drift debt (L-29 private dossier).

### K4 — Distribution

- Same GitHub release ZIP as plugin, or separate `bimwright-packs/` repo?
- If separate, ties to aspect #6 — NuGet data-only assembly or npm-distributable JSON bundle?

### K5 — Localization interaction

- Packs and locale (aspect #8 `category_alias_*.json` pattern from [[C-12]] L-10) share "data loaded at startup" shape. Same subsystem or separate?

## Precedents to lift (private dossiers)

- **[[C-03]] `template_packs/`** — multi-layer pack shape (sector + firm + validation + preflight + resolved cache). Strongest single precedent for K1+K2+K3. See `[[C-03]]` dossier §4 (private).
- **[[C-12]] `glossary_ja.json` + `category_alias_ja.json`** — mid-KB data-only packs, locale not compliance. Sibling to L-29. See private shallow-dives §C.
- **[[C-04]] `UnityMCPSkills/` folder** — first-class skills, framed as "parallel channel to MCP". See private dossier.
- **[[C-08]] README 17-category taxonomy** — even without engineering, grouping alone is a readable knowledge pack. L-12 captures this.

## Implications for other aspects

| Aspect | Impact if #9 lands |
|---|---|
| #2 Tool surface | If K1=B, adds 4–8 new tools; revisit D1 granularity. |
| #3 Architecture | Pack loader = new startup module; hot-reload = file-watcher (new). |
| #5 Security | Pack content can contain executable patterns (regex, scripts); sandbox requirement. |
| #6 Ecosystem | Distribution channel for packs (NuGet, npm, separate repo) = ecosystem decision. |
| #7 Testing | Each pack needs schema validation + load-time tests; L-28 failure taxonomy classifies pack errors. |
| #8 Localization | K5 decides whether VI locale pack (L-10) is a #9 subtype or separate. |

## Pending verification

1. **[[C-03]] pack schema reverse-engineering** — read `template_packs/*.json` structure in depth (dossier captured the multi-layer shape, not the full JSON Schema). Needed before K3.
2. **[[C-04]] skill folder discoverability** — does the folder get auto-read by Claude Code / Cursor when present, or does the user manually invoke? Impact on L-30 shape.
3. **Pack size budget** — [[C-03]]'s largest pack size unknown at shallow-dive depth. Decide pack-size ceiling before K4.

## Decision target

K1–K5 decided before shipping the first pack. First shippable candidate: `firm-profiles/acme-bim-v1.json` (L-13 concrete instance) — low-risk, demonstrates the pattern, no #2 tool-surface conflict if K1=A.
