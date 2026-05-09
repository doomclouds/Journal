# AGENTS

## Project Orientation

Journal is a local-first morning journal desktop app. The product idea is: the user writes natural language in the morning, the app preserves the raw expression, a pluggable AI layer turns it into structured JSON, and the backend renders/validates JMF Markdown for long-term local storage.

Current delivered scope is Phase 2:

```text
Natural language input -> Mock AI JSON -> JMF Markdown draft -> user confirmation -> formal Markdown file
```

Do not assume these are implemented yet unless the code or docs say so: real AI providers, SQLite indexing, version snapshots, block/source editing, in-app recording, speech-to-text, installers, delete flows.

## Tech Stack

- Backend: .NET 10, minimal API in `src/Journal.Api`.
- Domain model: `src/Journal.Domain`.
- Infrastructure: storage, clock, AI abstraction, JMF validation/rendering in `src/Journal.Infrastructure`.
- Desktop app: Electron + React + Vite + TypeScript in `apps/desktop`.
- Backend tests: xUnit in `tests/Journal.Tests`.
- Frontend tests: Vitest + Testing Library in `apps/desktop`.

## Key Code Paths

- API composition and endpoints: `src/Journal.Api/Program.cs`.
- Today's main workflow: `src/Journal.Infrastructure/Today/TodayJournalService.cs`.
- AI boundary: `src/Journal.Infrastructure/Ai/IJournalAiProvider.cs`; current implementation is `MockAiProvider`.
- JMF validation/rendering: `src/Journal.Infrastructure/Jmf/JournalAiJsonValidator.cs` and `JmfMarkdownRenderer.cs`.
- Local file layout: `src/Journal.Infrastructure/Storage/LocalJournalPaths.cs`.
- Main desktop screen: `apps/desktop/src/App.tsx`.
- API client: `apps/desktop/src/api.ts`.

## Product Invariants

- Raw user input is the source material and must not be overwritten by summaries.
- Formal Markdown is the durable human-readable source of truth; generated caches or indexes must be rebuildable.
- AI output should stay behind the JSON validation and preview/confirmation boundary before it becomes a formal entry.
- The app should support append/update flows, but no user-facing delete model unless the product direction changes explicitly.
- Keep the UI quiet, tool-like, fast to scan, and focused on the daily writing workflow.

## Development Commands

Use PowerShell from the repository root.

```powershell
dotnet test Journal.slnx
npm test --prefix apps/desktop
npm run build --prefix apps/desktop
```

Development run:

```powershell
dotnet run --project src/Journal.Api
npm install --prefix apps/desktop
npm run desktop --prefix apps/desktop
```

The development flow is two-process: start the .NET API first, then start the Electron/Vite desktop app. Vite is configured for `127.0.0.1:5173` with `strictPort`; backend CORS currently allows `http://localhost:5173` and `http://127.0.0.1:5173`.

## Data Locations

Phase 2 development data is written under `%LocalAppData%/Journal`:

```text
entries/yyyy/MM/yyyy-MM-dd.md
.journal/raw-inputs/yyyy/MM/yyyy-MM-dd.jsonl
.journal/drafts/yyyy/MM/yyyy-MM-dd.md
.journal/drafts/yyyy/MM/yyyy-MM-dd.meta.json
```

Be careful with changes that alter these paths or formats; update docs and tests together.

## Working Rules

- Prefer Chinese for repository docs and user-facing explanation. Keep code comments in English.
- Keep `.NET` changes nullable-clean and aligned with the existing minimal API/service style.
- Keep frontend changes consistent with the current dense desktop tool layout; do not turn the app into a marketing landing page.
- When changing behavior, update or add focused tests in the relevant test project before calling the work complete.
- Before continuing feature work, explaining prior decisions, or checking delivery status, search the Superpowers assets below before guessing from memory.

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
