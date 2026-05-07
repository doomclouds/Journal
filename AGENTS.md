# AGENTS

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

Scripts provide evidence, not final authority. Use the output to reduce misses and duplicates, then make the final routing decision with project context.

### Routing Boundaries

- Use `inbox` for uncertain but potentially reusable signals.
- Update an existing problem/archive when the new learning belongs to the same feature or failure class.
- Create a new problem only for a stable, distinct failure mode with root-cause evidence and recognition clues.
- Create or update an archive only for completed or accepted requirement threads.
- Use `both` only when a completed requirement also produced stable reusable debugging knowledge.

Before final close-out on meaningful work, confirm whether any new or updated asset is needed.
<!-- asset-compounding-guidance:end -->
