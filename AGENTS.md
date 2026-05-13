# AGENTS

## Project Orientation

Journal is a local-first morning journal desktop app. The product idea is: the user writes natural language in the morning, the app preserves the raw expression, a pluggable AI layer turns it into structured JSON, and the backend renders/validates JMF Markdown for long-term local storage.

Current delivered scope is Phase 6 plus Phase 4A local history reliability:

```text
Natural language input -> Mock or real LLM JSON or Harness Core tool plan -> JMF Markdown draft -> block/source edit or harness execution with JMF validation -> user confirmation -> version snapshot when overwriting -> formal Markdown file -> rebuildable SQLite/FTS history index -> history search/version restore workbench
```

Phase 6 includes the Phase 3 generation/confirmation/editor workflow, Phase 5 real OpenAI-compatible LLM integration, Harness Core, and Phase 4A local history/search:

- Backend parses draft/entry Markdown into a JMF document.
- Block mode edits known editable sections while preserving protected/system sections.
- Source mode can edit full Markdown, but saving is guarded by JMF validation.
- Valid editor saves write a `reviewing` draft only.
- Invalid editor saves write an `attention` draft and must not overwrite the formal entry.
- The formal Markdown entry is updated only after the user confirms the current draft.
- Real LLM providers are configured through `GET/PUT /settings/ai`; the safe settings view must not return full API keys.
- File-backed API keys can be revealed on explicit user action through `GET /settings/ai/{providerId}/api-key`; environment-backed keys are never revealable through the API.
- `POST /settings/ai/test` supports candidate settings so the UI can test the current form before saving it.
- `POST /settings/ai/activate` is the protected activation path: test first, then save/enable only on success.
- `POST /journal/today/draft/regenerate` remains a legacy full-draft regeneration compatibility endpoint; the Today workflow should use Harness Run for both normal input and reorganize actions.
- Real LLM output must not overwrite `raw-inputs`; server-side raw input text remains the source of truth in draft and formal JMF.
- Phase 6 adds Harness Core: real LLMs can use side-effect-free Agent Framework tools to plan draft operations.
- Harness operations write draft only; formal entries still require user confirmation.
- Harness tools are limited to append, upsert, revise AI-generated section, and no-op; user content must not be deleted, cleared, or replaced.
- Section-level provenance is stored in JMF markers and hidden from normal preview.
- Audit run records are stored as per-run JSON files under `.journal/audit/yyyy/MM/yyyy-MM-dd/<runId>.json` and exposed through the audit workbench.
- Formal entry overwrites go through `EntryWritePipeline`: snapshot the previous entry first, write Markdown second, then update the rebuildable SQLite index.
- Version snapshots live under `.journal/versions/yyyy/MM/yyyy-MM-dd/` as Markdown plus metadata; first writes do not create snapshots.
- SQLite lives under `.journal/index/journal.db` and is a rebuildable cache over Markdown, raw-input jsonl, and version files. Do not treat it as durable truth.
- History APIs expose search, date detail, version list/detail, scan/rebuild, and restore-version-to-draft.
- The history workbench is a full workspace mode opened from Today Assistant, mirroring audit-style navigation.
- Restoring a version writes a `reviewing` draft only and never writes directly to `entries/`. Current restore is limited to today's date because editor/confirm flows remain today-centered.

Do not assume these are implemented yet unless the code or docs say so: non-today restore/confirm, AI rewrite/follow-up chat, autosave, rich text/WYSIWYG editing, in-app recording, speech-to-text, installers, production Electron hosting of the .NET backend, delete flows, item-level provenance, draft diff, rollback.

## Tech Stack

- Backend: .NET 10, minimal API in `src/Journal.Api`.
- Domain model: `src/Journal.Domain`.
- Infrastructure: storage, clock, AI abstraction, JMF rendering/parsing/validation in `src/Journal.Infrastructure`.
- Desktop app: Electron + React + Vite + TypeScript in `apps/desktop`.
- Backend tests: xUnit in `tests/Journal.Tests`.
- Frontend tests: Vitest + Testing Library in `apps/desktop`.

## Key Code Paths

- API composition and endpoints: `src/Journal.Api/Program.cs`.
- Today's main workflow: `src/Journal.Infrastructure/Today/TodayJournalService.cs`.
- AI boundary: `src/Journal.Infrastructure/Ai/IJournalAiProvider.cs`; current implementations are `MockAiProvider` and `OpenAiCompatibleJournalAiProvider`.
- OpenAI-compatible runtime and settings: `src/Journal.Infrastructure/Ai/OpenAiCompatibleAgentRuntime.cs`, `JournalAiGenerationService.cs`, `JournalAiSettingsService.cs`, `JournalAiSettingsStore.cs`, and `JournalAiSettings.cs`.
- AI JSON validation/rendering: `src/Journal.Infrastructure/Jmf/JournalAiJsonValidator.cs` and `JmfMarkdownRenderer.cs`.
- Harness Core service/planner/audit: `src/Journal.Infrastructure/Harness/JournalHarnessService.cs`, `JournalHarnessPlanner.cs`, `JournalHarnessToolCollector.cs`, `JournalHarnessOperationExecutor.cs`, and `JournalHarnessAuditStore.cs`.
- History storage and indexing: `src/Journal.Infrastructure/Storage/JournalVersionStore.cs`, `EntryWritePipeline.cs`, `JournalIndexStore.cs`, and `JournalIndexingService.cs`.
- History service/API composition: `src/Journal.Infrastructure/Today/JournalHistoryService.cs` and `src/Journal.Api/Program.cs`.
- JMF editor structure: `src/Journal.Domain/Entries/JmfSectionCatalog.cs` plus `JmfSection*`, `JmfDocument`, `JmfValidation*`, and editor request/state records.
- JMF parse/validate/compose layer: `src/Journal.Infrastructure/Jmf/JmfMarkdownParser.cs`, `JmfMarkdownValidator.cs`, and `JmfMarkdownComposer.cs`.
- Local file layout: `src/Journal.Infrastructure/Storage/LocalJournalPaths.cs`.
- Main desktop screen: `apps/desktop/src/App.tsx`.
- JMF editor UI: `apps/desktop/src/JournalEditor.tsx`, `JournalBlockCard.tsx`, `InsertBlockMenu.tsx`, and `ValidationPanel.tsx`.
- LLM settings UI: `apps/desktop/src/LlmSettingsPanel.tsx`.
- AI audit workbench UI: `apps/desktop/src/AuditWorkbench.tsx`.
- History workbench UI: `apps/desktop/src/HistoryWorkbench.tsx`.
- API client and shared frontend contracts: `apps/desktop/src/api.ts`.
- Product direction and phase docs: `PROJECT_VISION.md`, `README.md`, `docs/superpowers/specs/`, `docs/superpowers/plans/`, and `docs/superpowers/archives/`.

## Product Invariants

- Raw user input is the source material and must not be overwritten by summaries.
- Formal Markdown is the durable human-readable source of truth; generated caches or indexes must be rebuildable.
- SQLite history index is a cache only. Markdown entries, raw-input jsonl files, and version snapshot files are the source material.
- Formal entry overwrites must snapshot the previous entry before writing the new Markdown.
- AI output should stay behind the JSON validation and preview/confirmation boundary before it becomes a formal entry.
- Editor saves are draft writes. `SaveBlockDraftAsync` and `SaveSourceDraftAsync` must not write directly to `entries/`.
- JMF validation protects the formal entry: invalid source or block requests should become `attention` drafts with repair information, not partial formal writes.
- Block mode must not edit `raw-inputs`, `keywords`, or `metadata-note`; `raw-inputs` is preserved from the baseline draft/entry.
- Source mode can edit full Markdown, including markers and front matter, but save must pass through parser/validator before it can become confirmable.
- `GET /settings/ai` must only expose safe API key previews. Do not add full key values to settings views, Markdown, logs, generated metadata, screenshots, or archives.
- Environment variables override file settings for the active/effective LLM provider. On Windows, read environment values from Process first, then User, then Machine, so user-level API keys configured outside the current terminal are still picked up. File settings are the fallback and the only source whose API key can be revealed through the UI.
- Provider activation should remain protected: failed health checks should not switch the active provider or persist a broken candidate.
- Reorganizing a draft is still a draft write. It must not write directly to `entries/` and must preserve server-side raw inputs.
- The Today compose submit flow and reorganize action should use `POST /journal/today/harness/runs` plus the run SSE stream, so both paths create audit records.
- Harness append-input runs persist the current user text as raw input for future runs, but the planner prompt must treat it as the current user message, not as historical raw input context.
- Harness reorganize-existing runs must not append raw input; they use a fixed server-side user prompt to reorganize from existing raw inputs, the current draft, the confirmed entry, and the section catalog.
- Harness execution is draft-only. Executing a run may write a `reviewing` or `attention` draft, never `entries/`.
- Harness planner tools are side-effect-free collection tools. Server-side execution, validation, draft persistence, and audit persistence happen after tool collection.
- Harness provenance is section-level. Do not claim item-level provenance, diff, or rollback unless those features are added.
- Restoring a version is draft-only. It must create a `reviewing` draft and must not write `entries/` directly.
- Current version restore is limited to today's date; avoid exposing non-today restore until date-aware editor/confirm behavior exists.
- The app should support append/update flows, but no user-facing delete model unless the product direction changes explicitly.
- Keep the UI quiet, tool-like, fast to scan, and focused on the daily writing workflow.

## Development Commands

Use PowerShell from the repository root.

```powershell
dotnet test Journal.slnx
npm test --prefix apps/desktop
npm run build --prefix apps/desktop
```

Useful focused checks:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter TodayJournalEditorServiceTests
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JmfMarkdownParserTests|JmfMarkdownValidatorTests|JmfMarkdownComposerTests"
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JournalAiSettingsTests|JournalAiGenerationServiceTests|OpenAiCompatibleJournalAiProviderTests|TodayJournalEndpointTests"
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JournalVersionStoreTests|JournalIndexStoreTests|JournalIndexingServiceTests|EntryWritePipelineTests|JournalHistoryServiceTests|TodayJournalEndpointTests"
npm test --prefix apps/desktop -- App.test.tsx
npm test --prefix apps/desktop -- HistoryWorkbench.test.tsx
```

Development run:

```powershell
dotnet run --project src/Journal.Api
npm install --prefix apps/desktop
npm run desktop --prefix apps/desktop
```

The development flow is two-process: start the .NET API first, then start the Electron/Vite desktop app. Vite is configured for `127.0.0.1:5173` with `strictPort`; backend CORS currently allows `http://localhost:5173` and `http://127.0.0.1:5173`.

## Data Locations

Development data is written under `%LocalAppData%/Journal`:

```text
entries/yyyy/MM/yyyy-MM-dd.md
.journal/raw-inputs/yyyy/MM/yyyy-MM-dd.jsonl
.journal/drafts/yyyy/MM/yyyy-MM-dd.md
.journal/drafts/yyyy/MM/yyyy-MM-dd.meta.json
.journal/audit/yyyy/MM/yyyy-MM-dd/<runId>.json
.journal/versions/yyyy/MM/yyyy-MM-dd/<versionId>.md
.journal/versions/yyyy/MM/yyyy-MM-dd/<versionId>.meta.json
.journal/index/journal.db
```

Phase 5 added `%LocalAppData%/Journal/.journal/settings/ai-providers.json` for persisted LLM settings. Phase 6 adds `.journal/audit/yyyy/MM/yyyy-MM-dd/<runId>.json` for harness audit records. Phase 4A adds `.journal/versions/` for overwrite snapshots and `.journal/index/journal.db` for the rebuildable SQLite/FTS cache. Be careful with changes that alter these paths or formats; update docs and tests together.

## Working Rules

- Prefer Chinese for repository docs and user-facing explanation. Keep code comments in English.
- Keep `.NET` changes nullable-clean and aligned with the existing minimal API/service style.
- Keep frontend changes consistent with the current dense desktop tool layout; do not turn the app into a marketing landing page.
- When changing behavior, update or add focused tests in the relevant test project before calling the work complete.
- When changing phase scope or delivered capabilities, update `README.md`, this `AGENTS.md`, and the relevant `docs/superpowers` asset together.
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

Use `none` only when the signal is clearly mechanical, one-off, already covered, or unlikely to help future work. If choosing `none` after meaningful development, state the concrete reason in the final handoff.

### Problem Gate Output

At the end of the gate, report the route decision compactly:

- `none`: no asset, with the concrete reason
- `inbox`: new or updated inbox note, with validation evidence
- `update-existing`: updated archive/problem/inbox asset, with validation evidence
- `new-problem`: formal problem asset, with validation evidence
- `archive` or `both`: only when the route also includes completed requirement history

Before final close-out on meaningful work, confirm whether any new or updated asset is needed.
<!-- asset-compounding-guidance:end -->
