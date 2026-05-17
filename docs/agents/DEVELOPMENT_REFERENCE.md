# Agent Development Reference

本文件承接根目录 `AGENTS.md` 中过长的工程路径、命令和数据位置。改代码前先用这里定位模块，再按任务读取具体源码和测试。

## Tech Stack

- Backend: `.NET 10`, minimal API in `src/Journal.Api`.
- Domain model: `src/Journal.Domain`.
- Infrastructure: storage, clock, AI abstraction, JMF rendering/parsing/validation in `src/Journal.Infrastructure`.
- Desktop app: Electron + React + Vite + TypeScript in `apps/desktop`.
- Backend tests: xUnit in `tests/Journal.Tests`.
- Frontend tests: Vitest + Testing Library in `apps/desktop`.

## Key Code Paths

- API composition and endpoints: `src/Journal.Api/Program.cs`.
- Today's main workflow: `src/Journal.Infrastructure/Today/TodayJournalService.cs`.
- AI boundary: `src/Journal.Infrastructure/Ai/IJournalAiProvider.cs`.
- OpenAI-compatible runtime/settings: `OpenAiCompatibleAgentRuntime.cs`, `JournalAiGenerationService.cs`, `JournalAiSettingsService.cs`, `JournalAiSettingsStore.cs`, `JournalAiSettings.cs`.
- AI JSON validation/rendering: `src/Journal.Infrastructure/Jmf/JournalAiJsonValidator.cs`, `JmfMarkdownRenderer.cs`.
- Harness Core service/planner/audit: `JournalHarnessService.cs`, `JournalHarnessPlanner.cs`, `JournalHarnessToolCollector.cs`, `JournalHarnessOperationExecutor.cs`, `JournalHarnessAuditStore.cs`.
- History storage/indexing: `JournalVersionStore.cs`, `EntryWritePipeline.cs`, `JournalIndexStore.cs`, `JournalIndexingService.cs`.
- History service/API composition: `src/Journal.Infrastructure/Today/JournalHistoryService.cs`, `src/Journal.Api/Program.cs`.
- Anniversary source model/store/service: `src/Journal.Domain/Entries/JournalAnniversaryModels.cs`, `src/Journal.Infrastructure/Storage/JournalAnniversaryStore.cs`, `src/Journal.Infrastructure/Today/JournalAnniversaryService.cs`.
- JMF editor structure: `src/Journal.Domain/Entries/JmfSectionCatalog.cs` plus `JmfSection*`, `JmfDocument`, `JmfValidation*`, editor request/state records.
- JMF parse/validate/compose: `JmfMarkdownParser.cs`, `JmfMarkdownValidator.cs`, `JmfMarkdownComposer.cs`.
- Local file layout: `src/Journal.Infrastructure/Storage/LocalJournalPaths.cs`.
- Data export/import: `JournalDataExportService.cs`, `JournalDataImportService.cs`, `GET /journal/data/summary`, `POST /journal/data/export`, `POST /journal/data/import`.
- Main desktop screen: `apps/desktop/src/App.tsx`.
- JMF editor UI: `JournalEditor.tsx`, `JournalBlockCard.tsx`, `InsertBlockMenu.tsx`, `ValidationPanel.tsx`.
- LLM settings UI: `apps/desktop/src/LlmSettingsPanel.tsx`.
- AI audit workbench UI: `apps/desktop/src/AuditWorkbench.tsx`.
- History workbench UI: `apps/desktop/src/HistoryWorkbench.tsx`.
- Memory corridor UI: `apps/desktop/src/AnniversaryWheelWorkbench.tsx`.
- API client/contracts: `apps/desktop/src/api.ts`.
- Windows release workflow: `.github/workflows/release-windows.yml`.

## Development Commands

Use PowerShell from the repository root.

```powershell
dotnet test Journal.slnx
npm test --prefix apps/desktop
npm run build --prefix apps/desktop
```

Focused backend checks:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter TodayJournalEditorServiceTests
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JmfMarkdownParserTests|JmfMarkdownValidatorTests|JmfMarkdownComposerTests"
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JournalAiSettingsTests|JournalAiGenerationServiceTests|OpenAiCompatibleJournalAiProviderTests|TodayJournalEndpointTests"
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JournalVersionStoreTests|JournalIndexStoreTests|JournalIndexingServiceTests|EntryWritePipelineTests|JournalHistoryServiceTests|TodayJournalEndpointTests"
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JournalDataExportServiceTests|JournalDataImportServiceTests|TodayJournalEndpointTests"
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JournalAnniversary|JournalIndexStoreTests|JournalHistoryServiceTests|JournalDataImportServiceTests|JournalDataExportServiceTests"
```

Focused frontend checks:

```powershell
npm test --prefix apps/desktop -- App.test.tsx
npm test --prefix apps/desktop -- HistoryWorkbench.test.tsx
npm test --prefix apps/desktop -- AnniversaryWheelWorkbench.test.tsx
npm test --prefix apps/desktop -- AnniversaryWheelWorkbench.test.tsx App.test.tsx
```

Development run:

```powershell
dotnet run --project src/Journal.Api
npm install --prefix apps/desktop
npm run desktop --prefix apps/desktop
```

The development flow is two-process: start the `.NET` API first, then start the Electron/Vite desktop app. Vite uses `127.0.0.1:5173` with `strictPort`.

## Release Commands

```powershell
.\scripts\release\build-installer.ps1 -ReleaseVersion 0.1.0 -SkipInno
.\scripts\release\build-installer.ps1 -ReleaseVersion 0.1.0
.\scripts\release\verify-installer.ps1 -ReleaseVersion 0.1.0
```

GitHub Actions release workflow:

```text
.github/workflows/release-windows.yml
workflow_dispatch release_version=0.1.0 -> installer artifact
push tag v0.1.0 -> GitHub Release assets
```

## Data Locations

Development and installed-app data lives under `%LocalAppData%/Journal`:

```text
entries/yyyy/MM/yyyy-MM-dd.md
.journal/raw-inputs/yyyy/MM/yyyy-MM-dd.jsonl
.journal/drafts/yyyy/MM/yyyy-MM-dd.md
.journal/drafts/yyyy/MM/yyyy-MM-dd.meta.json
.journal/audit/yyyy/MM/yyyy-MM-dd/<runId>.json
.journal/versions/yyyy/MM/yyyy-MM-dd/<versionId>.md
.journal/versions/yyyy/MM/yyyy-MM-dd/<versionId>.meta.json
.journal/anniversaries/anniversaries.json
.journal/index/journal.db
.journal/settings/ai-providers.json
.journal/exports/Journal-Export-*.zip
.journal/import-backups/<timestamp>/
```

Be careful with changes that alter these paths or formats; update code, tests, README, release docs, and relevant `docs/superpowers` assets together.
