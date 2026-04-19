---
title: "Teach Claude to use Revit in 10 minutes"
date: 2026-04-20
author: bimwright
tags: [revit, bim, mcp, claude, ai, automation, dotnet]
canonical_url: https://github.com/bimwright/rvt-mcp/blob/master/docs/blog/2026-04-20-teach-claude-revit-in-10-minutes.md
summary: A no-fluff walkthrough of rvt-mcp — the open-source bridge that lets AI agents actually edit your Revit model, safely, without uploading anything to the cloud.
---

# Teach Claude to use Revit in 10 minutes

It's 11 PM. The deadline is 9 AM. The PM just walked over with a "minor" update to the project naming convention: every level needs to be renamed from *Level 1, Level 2, Level 3...* to *L01 - Parking, L02 - Retail, L03 - Office, L04 - Office L2...*

Your model has 47 levels.

You know exactly how this is going to go. Click *Level 1* → Properties panel → Name field → type → Apply. Click *Level 2* → Properties → Name → type → Apply. Forty-five more times. You could write a Dynamo graph or a pyRevit script. You also know that by the time you've set up the node wiring or looked up the API call, you could've just clicked through it.

So you start clicking.

This is the itch `rvt-mcp` was built to scratch.

---

## What rvt-mcp actually is

Let me skip the marketing.

`rvt-mcp` is a small bridge process that runs alongside Revit. You talk to an AI agent — Claude, for example — and say something like *"rename all levels following this pattern."* The agent picks the right tool from the 28 that `rvt-mcp` exposes, calls it, and the tool runs inside Revit inside a single transaction. You see the result in the model. If you don't like it, one Ctrl+Z rolls back all 47 renames at once.

No cloud. Your model never leaves your machine. The bridge speaks to Revit over a named pipe (Revit 2025-2027) or a localhost TCP port (Revit 2022-2024). If you're using a cloud-hosted model like Claude, only the text of your prompts and the AI's replies travel — never your geometry, never your families.

Pure C#. Apache-2.0 license. Revit 2022 through 2027, one codebase, six plugin shells. You don't have to care about that, but it's there if you want to read the source.

---

## The ten-minute install

You need three things on the box:

- Revit 2022-2027 installed.
- .NET 8 SDK.
- An AI host — Claude Code, Claude Desktop, Cursor, Cline, Codex, OpenCode, VS Code Copilot, or Gemini CLI all work.

The easiest path is to let the agent install itself:

1. Clone the repo: `git clone https://github.com/bimwright/rvt-mcp`
2. Open `AGENTS.md` in your AI host of choice.
3. Say: *"install this for me."*

The agent reads `AGENTS.md`, proposes each step (build the server, deploy the plugin DLLs, wire up your host's config file), and waits for your approval at every gate. You can always say no. You can always undo. Nothing happens silently.

If you'd rather install manually, `README.md` has the step-by-step. It's about ten minutes either way. You do not need to know C# or MSBuild to use the tool — just to modify it.

---

## First real task

Open Revit. Open your AI host. Type:

> *List all levels in the current project, sorted by elevation.*

Under the hood the agent calls `get_levels`. Revit returns the list. You see 47 rows — name, elevation in millimetres, element ID.

Now:

> *Rename them so "Level 1" becomes "L01 - Parking", "Level 2" becomes "L02 - Retail", "Level 3" becomes "L03 - Office". For levels 4 and up, use "L04 - Office L2", "L05 - Office L3" and so on.*

The agent proposes a mapping — 47 old names, 47 new names, side by side. You read the first three, scan the rest, approve. One transaction commits. All 47 renamed. You open the Project Browser. Everything reads the new way.

Don't like it? Ctrl+Z. All 47 snap back. Ctrl+Y. Back to the new names. It's just a Revit transaction — the undo stack treats it like any other edit you made by hand.

The same pattern works for creating sheets, filtering elements, batch-setting parameters, generating schedules, creating levels and grids, placing views on sheets, pulling material quantities, and about twenty other things. Same shape every time: *list, propose, approve, commit, undo if needed.*

---

## What changes for you

Three things.

**1. The boring clicks go away.** The hour you would have spent renaming levels becomes two minutes of dialog.

**2. You stay in control.** Every change is a Revit transaction — one undo, full rollback. Nothing happens without a visible proposal you've said yes to. Nothing writes to your model silently.

**3. Your expertise scales.** The judgment about *what* should be renamed to *what* — that's yours, and it always will be. The mechanical typing isn't.

The tool was designed around three words: *predictable, auditable, reversible*. If a change wouldn't survive a code review, it doesn't ship. If an action can't be undone, it isn't a tool — it's a risk.

---

## What it's not

Honesty is part of the brand.

`rvt-mcp` is not:

- **A design agent.** It doesn't decide your project needs 47 levels. That's your call, same as it was yesterday.
- **A cloud platform.** Your `.rvt` stays on your disk. There is no bimwright server in the middle holding your data.
- **A black box.** 28 tools, each named, documented, and open-source. You can read every line before you run it.
- **Magic.** If you ask for something ambiguous, the AI will make assumptions. Read the proposal before you approve it. The agent is smart; you're the reviewer.

It's a tool — the same way Dynamo is a tool, the same way the Revit API itself is a tool. It just happens to understand natural language instead of Python or node wiring.

---

## Try it

`rvt-mcp` is open-source, Apache-2.0, Revit 2022-2027:

**https://github.com/bimwright/rvt-mcp**

Star it if you find it useful. Try it on a real task. Break it, and file an issue — we read every one.

If you've been wanting your AI assistant to actually *do* BIM work with you instead of just talking about it — this is that thing.

---

*`rvt-mcp` is part of [bimwright](https://github.com/bimwright) — open MCP servers for the AEC discipline. Our BIM knowledge base [`bim-wiki`](https://github.com/bimwright/bim-wiki) (ISO 19650 + Vietnamese regulatory landscape, CC-BY-SA 4.0) launched this week too.*

*bimwright. Built right.*
