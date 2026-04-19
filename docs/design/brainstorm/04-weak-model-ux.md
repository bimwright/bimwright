# Aspect #4 — Weak-model UX (Description, Errors, Prompts)

> **Note:** entity references like `[[C-XX]]` are opaque tags. Mapping maintained in a
> private research index; not exposed in this repo. Public readers see that
> research was informed by external implementations without specific attribution.

**Status:** ✅ Decided 2026-04-16
**Prerequisites:** Aspect #1, #2, Meta-decision M1
**Evidence:** Haiku benchmark — see [`docs/benchmarks/2026-04-19-l01-haiku-validation.md`](../../benchmarks/2026-04-19-l01-haiku-validation.md)

## Tool Description Template (required for every tool)

Each tool MUST have 4 parts:

```
<tool_name> — <1-line summary: what the tool does>

USE WHEN: <when to call this tool — concrete use cases>
DON'T USE WHEN: <when NOT to → point at the correct tool>
Example: <1 concrete example with real param values, correct format>
```

### Applied examples

```
ai_element_filter — Filter/search elements by category and parameter conditions.

USE WHEN: Find elements matching criteria (all walls on Level 1, doors wider than 900mm).
DON'T USE WHEN: Need currently selected elements → use get_selected_elements.
  Need element count stats → use analyze_model_statistics.
Example: All walls taller than 3000mm →
  ai_element_filter({category: "OST_Walls", parameter_name: "Height",
  operator: "greaterthan", value: "3000"})
```

```
operate_element — Perform visibility/selection operations on elements by ID.

USE WHEN: Select, hide, unhide, isolate, or set ONE SINGLE color for specific elements.
DON'T USE WHEN: Color-code elements BY PARAMETER VALUE → use color_elements instead.
Example: Hide elements →
  operate_element({action: "hide", element_ids: [12345, 67890]})
```

```
color_elements — Auto-assign different colors to elements based on a parameter value.

USE WHEN: Visualize differences — "color walls by material", "tô màu theo vật liệu".
DON'T USE WHEN: Set ONE specific color → use operate_element with action "setcolor".
Example: color_elements({category: "OST_Walls", parameter: "Material"})
```

### Why 4 parts — evidence

Haiku 4.5 benchmark (see benchmark doc):
- Tool selection accuracy: **10/10 both LEAN and RICH** (tool name alone was enough to pick correctly).
- Parameter accuracy: **LEAN 5/10, RICH 10/10** (delta comes entirely from Example).
- **Example is the most important part** — Haiku copies format from the example, doesn't need to "understand" schema.
- USE WHEN / DON'T USE WHEN: useful for edge cases, cheap, keep.

### Rules for writing Example

1. **Required** — no Example = Haiku gets params wrong 50% of the time (measured).
2. **Use real values** — `"OST_Walls"` not `"<category>"`. Haiku copies literally.
3. **Use exact param key names** — `parameter_name` not `parameterName` or `parameter`.
4. **Use exact value format** — `"3000"` (string) if API expects string, `3000` (int) if int.
5. **1 example is enough** — 2 examples only if the tool has genuinely distinct modes (e.g. `operate_element` hide vs setcolor).

## Error Response Format (Error-as-Teacher)

Every error response MUST follow the envelope:

```json
{
  "success": false,
  "error": "<clear description of what went wrong>",
  "suggestion": "<correct value or correct tool>",
  "hint": "<next action the AI should take>"
}
```

### Concrete examples

```json
// Bad category
{
  "error": "Unknown category 'walls'",
  "suggestion": "Did you mean 'OST_Walls'? Common: OST_Walls, OST_Doors, OST_Windows, OST_Floors, OST_Rooms",
  "hint": "Call list_categories to see the full list"
}

// Missing param
{
  "error": "Missing required parameter 'element_ids'",
  "suggestion": "Provide element_ids as int array, e.g. [12345, 67890]",
  "hint": "Use ai_element_filter to find element IDs first"
}

// Wrong tool for context
{
  "error": "operate_element action 'setcolor' sets ONE color. To color by parameter value, use color_elements",
  "suggestion": "color_elements({category: 'OST_Walls', parameter: 'Material'})",
  "hint": "color_elements auto-assigns different colors per unique value"
}
```

### Error-as-Teacher rules

1. **`error`** — clear, says exactly what went wrong.
2. **`suggestion`** — give the correct value OR the correct tool. Don't just say "invalid".
3. **`hint`** — next action. Usually "call tool X first" or "see Example in description".
4. **Inline common values** — when a category/operator is wrong, list 5-10 popular values right in the suggestion.

## ToolBaker — Enforce Template

When ToolBaker (`bake_tool`) creates a new tool, the pipeline MUST:

1. **Ask the caller for a description following the 4-part template** (Summary + USE WHEN + DON'T USE WHEN + Example).
2. **Validate Example exists** — reject tools missing Example with: "Tool description must include at least one Example with real parameter values."
3. **Auto-generate USE WHEN / DON'T USE WHEN** if the caller only provides Summary + Example — derived from param schema + tool name.
4. **Baked tool's error response** must follow the Error-as-Teacher envelope — ToolBaker injects the wrapper if the author's code doesn't return the right shape.

## Prompts for Public Repo

The public repo ships generic prompts only:
- `revit-model-overview` — generic, useful for any user
- `tool-guide` — explains the tool surface + ToolBaker
- `getting-started` — first-run prompt for new users: check connection, list tools, try first query

Internal-tenant-specific prompts (e.g., internal database context, internal tool guides) are NOT shipped in the public repo — they are deployment-private.

## Implications for other aspects

| Aspect | Impact |
|---|---|
| #3 Architecture | Error-as-Teacher needs a middleware layer wrapping the error response for every handler. ToolBaker needs a description validator. |
| #5 Security | ToolBaker enforcing the template = security gate (the caller must declare tool purpose before compile). |
| #7 Testing | Expand Haiku benchmark: 30 queries, add GPT mini + Gemma. Test progressive-disclosure interaction. |

## Pending verification

1. **Expand benchmark** — 30 queries, 3 models (Haiku, GPT mini, Gemma). Owned by aspect #7.
2. **Error-as-Teacher effectiveness** — measure retry success rate for LEAN + error-as-teacher vs RICH alone. Owned by aspect #7.
3. **ToolBaker template enforcement** — implementation detail, post-brainstorm.

---

## Learnings from 2026-04-19 sprint

**Status:** candidate cross-reference, not decided. Source: private sprint roadmap.

### 🔴 Cross-reference from aspect #2

- **L-01** — Tighten tool descriptions to ~40-char imperative verb-lead, params in schema only. Effort S. Source: [[C-05]].
  - Primary home = `#2 tool-surface` (axis-1 tool-config decision). Cross-ref here because of direct weak-model-UX impact.
  - Signal: [[C-05]]'s mean 40-char descriptions were a factor in their one-shot success at GPT-3.5 class. Our current descriptions are more verbose (private dossier §1).
  - Re-run Haiku benchmark (currently 10 queries, planned expand to 30 per Pending verification #1) on a LEAN-schema + 40-char-description variant vs the current baseline. Expected lift: measurable at retry-rate / first-correct-tool-pick metric. Exact magnitude unknown until benchmark runs.

### Next-step

When L-01 ships at #2, re-run Haiku benchmark to measure UX impact. Treat as aspect #7 pending verification #2 (Error-as-Teacher effectiveness) companion experiment.
