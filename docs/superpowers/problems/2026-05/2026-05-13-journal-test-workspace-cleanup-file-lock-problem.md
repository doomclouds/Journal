# Journal Test Workspace Cleanup File Lock

- Date: `2026-05-13`
- Topic slug: `journal-test-workspace-cleanup-file-lock`
- Status: `Captured`
- Scope: `Test`
- Tags: `tests`, `windows`, `sqlite`, `sse`, `cleanup`

## Symptom

`dotnet test Journal.slnx` 在全量并行运行时偶发失败，但失败点不是业务断言，而是测试结束清理临时目录时报 `UnauthorizedAccessException` / `IOException`。常见表现是 `journal.db`、SQLite WAL/SHM sidecar，或后台 harness/SSE 相关写入仍短暂占用文件，导致 `TempWorkspace.Dispose()` 里的 `Directory.Delete(Root, recursive: true)` 失败。

## Trigger / Context

- Windows 文件删除对仍被进程或后台任务持有的句柄更敏感。
- Phase 4A 增加 SQLite index tests，Phase 6 已有 harness SSE/background run tests。
- 多个测试文件都有本地 `TempWorkspace`，旧实现直接递归删除临时目录，没有重试。
- 聚焦单测常常通过，全量测试更容易暴露句柄释放时序。

## Root Cause

测试清理代码把“测试逻辑完成”和“所有 OS 文件句柄都已释放”当成同一时刻。SQLite connection/WAL/SHM、WebApplicationFactory、SSE/background run 或 finalizer 释放可能比测试方法返回稍晚，直接 `Directory.Delete` 在 Windows 上会把这种短暂滞后放大成测试失败。

这不是产品功能失败，而是测试夹具的清理策略不适合有 SQLite 和后台执行的 Windows 环境。

## Fix

- 新增 `TestWorkspaceCleanup.DeleteDirectory(root)`，在删除临时目录时对 `IOException` / `UnauthorizedAccessException` 做有限重试。
- 每次重试前执行 `GC.Collect()` / `GC.WaitForPendingFinalizers()`，再短暂等待后重试。
- 最终仍失败时重抛第一次异常，不吞掉真实清理问题。
- 将高风险测试文件的 `TempWorkspace.Dispose()` 接到该 helper：`TodayJournalEndpointTests`、`JournalIndexStoreTests`、`JournalHarnessServiceTests`、`JournalIndexingServiceTests`、`JournalHistoryServiceTests`。
- 同时收口 harness SSE 取消测试和 WAL/SHM 诊断 sidecar 写入的测试侧时序，避免测试本身读写 audit/index 时撞后台写入。

## Why This Fix

相比禁用整套测试并行或在产品代码里加入无意义延时，测试清理重试只处理 OS 句柄释放的边界，范围小、可解释，而且保留最终失败信号。它不掩盖断言失败，也不把业务异常吞掉；只有临时目录删除这种测试夹具职责被延迟重试。

## Recognition Clues

- 失败堆栈落在 `TempWorkspace.Dispose()` / `Directory.Delete(Root, recursive: true)`。
- 错误信息提示 `journal.db`、`.db-wal`、`.db-shm` 或测试临时目录内文件正在被使用。
- 单独运行失败测试通过，全量运行或连续运行更容易复现。
- 同一次提交在业务断言层没有稳定失败，复跑可能通过。

## Applicability / Non-Applicability

### Applies When

- Windows 上的测试使用临时目录承载 SQLite、文件型数据库、WebApplicationFactory、SSE 或后台任务输出。
- 测试失败点是清理临时目录，而不是业务断言。
- 需要保留并行测试速度，但降低 OS 文件句柄释放时序导致的噪音。

### Does Not Apply When

- 产品代码在运行时长期泄漏文件句柄；那应该修产品生命周期，而不是只重试测试清理。
- 测试断言失败、HTTP API 行为错误或数据库内容错误；这些不能用 cleanup helper 掩盖。
- 需要验证文件确实被释放的专门测试；那应直接断言句柄生命周期。

## Related Artifacts

- Spec: [2026-05-13-phase-4a-local-history-search-design.md](../../specs/2026-05-13-phase-4a-local-history-search-design.md)
- Plan: [2026-05-13-phase-4a-local-history-search-implementation-plan.md](../../plans/2026-05-13-phase-4a-local-history-search-implementation-plan.md)
- Archive: [2026-05-13-phase-4a-local-history-search-archives.md](../../archives/2026-05/2026-05-13-phase-4a-local-history-search-archives.md)
- Related Problems:
  - [Harness SSE Run Lifecycle Re-Execution](./2026-05-12-harness-sse-run-lifecycle-problem.md)
  - [History Index Cache Stale Sidecars](./2026-05-13-history-index-cache-stale-sidecars-problem.md)
- Code or Test:
  - [TestWorkspaceCleanup.cs](../../../../tests/Journal.Tests/TestWorkspaceCleanup.cs)
  - [TodayJournalEndpointTests.cs](../../../../tests/Journal.Tests/TodayJournalEndpointTests.cs)
  - [JournalIndexStoreTests.cs](../../../../tests/Journal.Tests/JournalIndexStoreTests.cs)
  - [JournalIndexingServiceTests.cs](../../../../tests/Journal.Tests/JournalIndexingServiceTests.cs)
  - [JournalHistoryServiceTests.cs](../../../../tests/Journal.Tests/JournalHistoryServiceTests.cs)
  - [JournalHarnessServiceTests.cs](../../../../tests/Journal.Tests/JournalHarnessServiceTests.cs)
