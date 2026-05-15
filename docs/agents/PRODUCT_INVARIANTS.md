# Agent Product Invariants

这些是不变量，不是普通建议。改 Journal 行为前先对照本文件，尤其是 draft/formal entry、raw input、LLM safety、history 和 release/data backup 边界。

## Source Material

- Raw user input is source material and must not be overwritten by summaries.
- Formal Markdown is the durable human-readable source of truth.
- Generated caches and indexes must be rebuildable.
- SQLite history index is a cache only. Markdown entries, raw-input jsonl files, and version snapshot files are the source material.

## Draft And Formal Entry

- AI output stays behind JSON validation and preview/confirmation before becoming a formal entry.
- Editor saves are draft writes. `SaveBlockDraftAsync` and `SaveSourceDraftAsync` must not write directly to `entries/`.
- Formal entry overwrites must snapshot the previous entry before writing new Markdown.
- JMF validation protects the formal entry: invalid source or block requests become `attention` drafts with repair information, not partial formal writes.
- Block mode must not edit `raw-inputs`, `keywords`, or `metadata-note`; `raw-inputs` is preserved from the baseline draft/entry.
- Source mode can edit full Markdown, including markers and front matter, but save must pass parser/validator before it can become confirmable.

## LLM Settings And Security

- `GET /settings/ai` must only expose safe API key previews.
- Never add full key values to settings views, Markdown, logs, generated metadata, screenshots, archives, or export packages.
- Environment variables override file settings for active/effective LLM provider.
- On Windows, read environment values from Process first, then User, then Machine.
- File settings are the fallback and the only API-key source revealable through the UI.
- Provider activation remains protected: failed health checks must not switch active provider or persist a broken candidate.

## Harness Core

- Today compose submit and reorganize use `POST /journal/today/harness/runs` plus SSE stream.
- Harness append-input runs persist current user text as raw input for future runs, but planner prompt treats it as current user message, not historical context.
- Harness reorganize-existing runs must not append raw input.
- Reorganize-existing uses a fixed server-side user prompt and provides only existing raw inputs, section catalog, and tool constraints to the LLM.
- Do not provide current draft or confirmed entry to the planner in reorganize-existing mode.
- Harness tools are side-effect-free collection tools. Server-side execution, validation, draft persistence, and audit persistence happen after tool collection.
- Harness tools are limited to append, upsert, revise AI-generated section, and no-op.
- User content must not be deleted, cleared, or replaced.
- Harness execution is draft-only. Executing a run may write a `reviewing` or `attention` draft, never `entries/`.
- Harness provenance is section-level. Do not claim item-level provenance, draft diff, or entry rollback UI unless implemented.

## History And Anniversary

- Restoring a version is draft-only. It creates a `reviewing` draft and must not write `entries/` directly.
- Current version restore is limited to today's date.
- Anniversary mode is read-only. Do not expose restore, delete, diff, or edit actions there unless the product direction changes explicitly.
- The app should support append/update flows, but no user-facing delete model unless product direction changes.

## Data Backup And Release

- Export packages must not contain full API keys.
- Data export/import should treat Markdown entries, raw-input jsonl, drafts, versions, audit records, and safe settings as source material.
- Import must backup current source material before replacing local data.
- Import failure must not destroy the previous local data.
- `GET /journal/data/summary` should report current local data counts without creating export artifacts.
- `rawInputCount` means raw input records, not jsonl file count.

## UI

- Keep the UI quiet, tool-like, fast to scan, and focused on daily writing.
- Keep frontend changes consistent with the current dense desktop tool layout.
- Do not turn the app into a marketing landing page.
