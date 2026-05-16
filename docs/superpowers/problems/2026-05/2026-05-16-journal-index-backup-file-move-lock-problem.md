# Journal Index Backup File Move Lock

- Date: `2026-05-16`
- Topic slug: `journal-index-backup-file-move-lock`
- Status: `Captured`
- Scope: `Runtime`
- Tags: `sqlite`, `windows`, `release`, `index`, `file-lock`

## Symptom

`v0.1.1` GitHub Actions release run 在 `dotnet test Journal.slnx` 阶段失败，失败用例是 `JournalIndexStoreTests.EnsureReadyAsync_WhenExistingSchemaMissesRequiredColumns_BacksUpAndRebuilds`。错误落在 `JournalIndexStore.MoveIfExists` 的 `File.Move`，Windows 返回 `The process cannot access the file because it is being used by another process`。

## Trigger / Context

- `EnsureReadyCoreAsync` 检测到现有 SQLite index schema 缺少必需列。
- 代码先关闭当前 `SqliteConnection`，然后调用 `BackupAndResetAsync` 把旧 `journal.db` 移入 backup 目录。
- CI 运行在 Windows runner 上，SQLite/provider/OS 可能在 `CloseAsync` 返回后仍短暂持有数据库文件句柄。

## Root Cause

`BackupAndResetAsync` 把“连接关闭完成”和“底层 OS 文件句柄立即可移动”当成同一时刻。Windows 对打开句柄的移动/删除更敏感，SQLite 连接关闭、command/finalizer 释放和文件锁释放之间存在短暂滞后；直接 `File.Move` 会把这种短锁放大成 release 阻断。

## Fix

- `JournalIndexStore.MoveIfExists` 对 `IOException` / `UnauthorizedAccessException` 做有限重试。
- 每次重试前执行 `GC.Collect()` / `GC.WaitForPendingFinalizers()`，再短暂等待。
- 重试耗尽后重抛第一次异常，不吞掉真实的长期锁定、权限或路径问题。

## Why This Fix

相比在测试里延时或跳过 schema recovery 用例，产品代码里的备份移动本身也需要面对 Windows 文件锁短暂滞后。有限重试只覆盖可恢复的 OS 时序边界，不改变 SQLite index 仍是可重建缓存的架构，也不会掩盖持续占用或权限错误。

## Recognition Clues

- 失败堆栈落在 `JournalIndexStore.BackupAndResetAsync` / `MoveIfExists` / `File.Move`。
- 错误信息是 Windows 文件正在被另一个进程使用。
- 触发场景通常是 schema recovery、corrupt recovery、manual rebuild 或 backup/reset 之后立刻移动 `journal.db` / `journal.db-wal` / `journal.db-shm`。
- 本地或 CI 复跑可能通过，因为根因是短暂文件锁时序。

## Applicability / Non-Applicability

### Applies When

- Windows 上移动或删除刚关闭的 SQLite database / WAL / SHM 文件。
- 操作语义允许等待短暂文件锁释放。
- 文件是可重建缓存或 backup/reset 流程的一部分。

### Does Not Apply When

- 文件被长期业务进程持有；应修生命周期或协调关闭，而不是无限重试。
- 目标是保证强原子替换；那应设计 temp file + replace/rename 合同。
- 错误是 schema 内容、数据一致性或查询结果错误；这些不能用文件移动重试解决。

## Related Artifacts

- Spec: [2026-05-13-phase-4a-local-history-search-design.md](../../specs/2026-05-13-phase-4a-local-history-search-design.md)
- Archive: [2026-05-13-phase-4a-local-history-search-archives.md](../../archives/2026-05/2026-05-13-phase-4a-local-history-search-archives.md)
- Related Problems:
  - [Journal Test Workspace Cleanup File Lock](./2026-05-13-journal-test-workspace-cleanup-file-lock-problem.md)
  - [History Index Cache Stale Sidecars](./2026-05-13-history-index-cache-stale-sidecars-problem.md)
- Code or Test:
  - [JournalIndexStore.cs](../../../../src/Journal.Infrastructure/Storage/JournalIndexStore.cs)
  - [JournalIndexStoreTests.cs](../../../../tests/Journal.Tests/JournalIndexStoreTests.cs)
