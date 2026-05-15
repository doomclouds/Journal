# AGENTS

## Read First

Journal 已进入 V1 / `0.1.0` Windows 本地发布阶段。根 `AGENTS.md` 只保留 agent 启动入口、硬规则和资产复利导航；长项目上下文已拆到引用文档里。

开始涉及代码、文档、发布、需求状态判断前，按任务读取：

- Project context and delivered scope: [docs/agents/PROJECT_CONTEXT.md](docs/agents/PROJECT_CONTEXT.md)
- Development paths, commands, and data layout: [docs/agents/DEVELOPMENT_REFERENCE.md](docs/agents/DEVELOPMENT_REFERENCE.md)
- Product invariants and safety boundaries: [docs/agents/PRODUCT_INVARIANTS.md](docs/agents/PRODUCT_INVARIANTS.md)
- UI/UX design navigation: [docs/agents/UI_UX_DESIGN_NAVIGATION.md](docs/agents/UI_UX_DESIGN_NAVIGATION.md)
- Public project overview: [README.md](README.md)
- Product vision: [PROJECT_VISION.md](PROJECT_VISION.md)
- Delivery history: [docs/superpowers/archives/INDEX.md](docs/superpowers/archives/INDEX.md)
- Release notes: [docs/release/RELEASE_NOTES.md](docs/release/RELEASE_NOTES.md)

## Current Snapshot

V1 includes local-first daily writing, raw input preservation, JMF draft/edit/validation/confirmation, OpenAI-compatible LLM settings, Harness Core audit runs, rebuildable local history index, same-day anniversary wheel, data backup/import, Windows installer scripts, and GitHub Actions release workflow.

Do not assume these are implemented unless code or docs prove it: non-today restore/confirm, AI follow-up chat, autosave, rich text editing, in-app recording, speech-to-text, delete flows, item-level provenance, draft diff, entry rollback UI, cloud sync, auto update/signing, or full API Key export/import.

## Quick Commands

Use PowerShell from the repository root.

```powershell
dotnet test Journal.slnx
npm test --prefix apps/desktop
npm run build --prefix apps/desktop
```

Release checks:

```powershell
.\scripts\release\build-installer.ps1 -ReleaseVersion 0.1.0 -SkipInno
.\scripts\release\build-installer.ps1 -ReleaseVersion 0.1.0
.\scripts\release\verify-installer.ps1 -ReleaseVersion 0.1.0
```

For focused commands and code paths, read [docs/agents/DEVELOPMENT_REFERENCE.md](docs/agents/DEVELOPMENT_REFERENCE.md).

## Working Rules

- Prefer Chinese for repository docs and user-facing explanation. Keep code comments in English.
- Keep `.NET` changes nullable-clean and aligned with the existing minimal API/service style.
- Keep frontend changes consistent with the current dense desktop tool layout; do not turn the app into a marketing landing page.
- When changing behavior, update or add focused tests in the relevant test project before calling the work complete.
- When changing phase scope or delivered capabilities, update `README.md`, this `AGENTS.md`, `docs/agents/*`, and the relevant `docs/superpowers` asset together.
- Before continuing feature work, explaining prior decisions, or checking delivery status, search the Superpowers assets below before guessing from memory.
- The asset-compounding guidance block below is a required plugin navigation anchor. Do not move, delete, or split the content between its marker comments.

<!-- asset-compounding-guidance:start -->
## Asset Compounding Retrieval Guide

This section is managed by `compound-development-asset`. Keep generic asset-compounding workflow rules in the skill system; keep repository-specific retrieval anchors here.

### Asset Directories

- Specs: `docs/superpowers/specs/`
- Plans: `docs/superpowers/plans/`
- Archives: `docs/superpowers/archives/`
- Problems: `docs/superpowers/problems/`
- Inbox: `docs/superpowers/inbox/`

If one of these directories does not exist, do not assume there is no asset. Search the existing directories first, then decide whether the missing area should be created.

### Retrieval Order

When continuing feature work, explaining prior decisions, or checking whether a requirement is already delivered:

1. Search `docs/superpowers/specs/` and `docs/superpowers/plans/` for the intended behavior and implementation plan.
2. Search `docs/superpowers/archives/` for completed delivery history.
3. Search `docs/superpowers/problems/` for reusable failure modes, root causes, and recovery rules.
4. Search `docs/superpowers/inbox/` for uncertain but possibly reusable signals that have not been promoted yet.
5. If no asset answers the question, inspect current code and tests before guessing.

Preferred keyword search:

```powershell
rg -n "<topic-keyword>" docs/superpowers/specs docs/superpowers/plans docs/superpowers/archives docs/superpowers/problems docs/superpowers/inbox
```

### Script-Assisted Checks

When `compound-development-asset` and `write-superpowers-problem` are available, prefer bundled scripts for deterministic checks:

- `find_related_assets.py`: find matching specs, plans, archives, problems, and inbox notes before creating a new asset.
- `suggest_asset_route.py`: get a first-pass route suggestion: `none`, `inbox`, `update-existing`, `new-problem`, `archive`, or `both`.
- `check_indexes.py`: validate archive/problem/inbox index order, dead links, duplicate entries, and orphan files.
- `archive-superpowers-feature/scripts/validate_archive_asset.py`: validate formal archive assets.
- `write-superpowers-problem/scripts/validate_problem_asset.py`: validate formal problem assets and inbox notes.
- `write-superpowers-problem/scripts/inspect_inbox_lifecycle.py`: inspect related inbox lifecycle status and revisit candidates.

Scripts provide evidence, not final authority. Use the output to reduce misses and duplicates, then make the final routing decision with project context.

### Routing Boundaries

- Use `inbox` for uncertain but potentially reusable signals.
- Update an existing problem/archive when the new learning belongs to the same feature or failure class.
- Treat user validation feedback, CI/release warnings, installer/artifact warnings, and hosted automation deprecations as asset signals; update a related asset if one exists, otherwise park the signal in inbox.
- Create a new problem only for a stable, distinct failure mode with root-cause evidence and recognition clues.
- Create or update an archive only for completed or accepted requirement threads.
- Use `both` only when a completed requirement also produced stable reusable debugging knowledge.

### Completion Gates

Requirement archives and problem archives are separate gates:

- Requirement archiving records what was delivered. Run it when a coherent requirement, phase, feature, or accepted design-to-implementation thread is complete and verified.
- Problem archiving records reusable failure knowledge. Run it after the current task has been implemented, spec-reviewed, code-quality-reviewed, and verified, before starting the next task or when the overall task is ending.

For meaningful development work, the main agent must run a problem-archiving gate after:

- implementation is complete enough to review as a unit
- spec alignment has been checked against `docs/superpowers/specs/` and `docs/superpowers/plans/`
- code quality review has checked correctness, maintainability, test coverage, and integration risks
- verification commands or targeted manual checks have produced concrete evidence

This gate belongs at task boundaries, not inside every small edit. Use it before moving from one planned task to the next, before merge/PR/cleanup when applicable, or before the final response when no next task remains.

### Problem Archiving Ownership

Only the main agent should execute the problem-archiving gate. Subagents may report candidate lessons, suspicious behavior, failed approaches, review findings, or tool quirks, but they should not write or promote problem/inbox/archive assets unless the main agent explicitly delegates that asset-writing task.

During the gate, the main agent should collect candidates from:

- implementation issues and debugging paths
- failed or flaky tests
- spec review mismatches
- code quality review findings
- provider, tool, MCP, SSE, SQLite, filesystem, encoding, or Windows-specific runtime quirks
- subagent reports and unresolved observations

### Inbox-First Problem Routing

When a signal is potentially reusable but not mature enough for a formal problem asset, prefer `inbox` over dropping it.

Use `inbox` for:

- a fix that worked but whose root cause is not yet stable
- a suspicious behavior that may recur but has limited evidence
- a review finding that indicates a possible class of mistakes
- an environment/tool/provider quirk that affected the work but was not fully diagnosed
- a requirement or workflow ambiguity that may need later promotion
- a "could archive or could skip" lesson that future agents might realistically search for
- a release/CI warning that did not fail the run but may affect future builds

Use `none` only when the signal is clearly mechanical, one-off, already covered, or unlikely to help future work. If choosing `none` after meaningful development, state the concrete reason in the final handoff.

Inbox notes should track lifecycle: `Open`, `Partially promoted`, `Promoted`, or `Closed`. When a related problem/archive later covers the signal, update the inbox lifecycle instead of leaving it stale.

### Problem Gate Output

At the end of the gate, report the route decision compactly:

- `none`: no asset, with the concrete reason
- `inbox`: new or updated inbox note, with validation evidence
- `update-existing`: updated archive/problem/inbox asset, with validation evidence
- `new-problem`: formal problem asset, with validation evidence
- `archive` or `both`: only when the route also includes completed requirement history

Before final close-out on meaningful work, confirm whether any new or updated asset is needed.
<!-- asset-compounding-guidance:end -->
