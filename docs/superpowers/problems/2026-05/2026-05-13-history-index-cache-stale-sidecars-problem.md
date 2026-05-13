# History Index Cache Stale Sidecars

- Date: `2026-05-13`
- Topic slug: `history-index-cache-stale-sidecars`
- Status: `Captured`
- Scope: `Feature`
- Tags: `history`, `sqlite`, `fts`, `rebuild`, `jmf`

## Symptom

历史索引功能看起来能写入 entry summary，但在边界场景下会出现“保存/扫描成功，搜索结果却代表旧内容或缺少 sidecar 内容”的问题：invalid JMF 只被标记为 `attention` 时，旧 section FTS 仍可能命中；rebuild 只扫描正式 Markdown 时，raw input hit 和 version count 会丢失；非规范备份路径也可能被误当作正式 entry 索引。

## Trigger / Context

- Phase 4A 把 SQLite 定义为 Markdown、raw-input jsonl 和 version 文件之上的可重建缓存。
- `JournalIndexingService` 初版实现先满足了 valid entry 扫描，但没有完整覆盖 raw inputs、versions、invalid JMF 清理和 canonical path 过滤。
- `JournalIndexStore.SearchAsync` 通过 `entries` 聚合 raw/section hit，因此 raw-only 日期如果没有最小 entry row 会完全搜不出来。

## Root Cause

索引层把“状态更新”和“当前文件事实同步”混在了一起：invalid JMF 只改 status，不 upsert 当前文件 metadata，也不清理旧 section/FTS；rebuild 只从 `entries/` 重新生成主表，没有重新读取 `.journal/raw-inputs/` 和 `.journal/versions/`；扫描时只按文件名解析日期，没有校验路径是否等于 `LocalJournalPaths.EntryPath(date)`。

这些缺口违反了同一个原则：SQLite 既然是缓存，就必须完整反映当前文件系统事实，不能保留旧 FTS、漏掉 sidecar 文件，或从非规范路径推断事实源。

## Fix

- `RebuildAsync` 在 reset 后依次重建正式 Markdown、raw input jsonl 和 version metadata。
- `SyncRawInputsAsync` 支持 raw-only 日期，必要时创建最小 `raw-only` entry row，让搜索聚合能返回 raw input hit。
- invalid JMF 走 `UpsertEntryAsync(minimal attention row, [])`，同步当前坏文件 path/hash/mtime/size，并清理旧 sections 和 section FTS。
- `ScanAsync` 只索引 canonical `_paths.EntryPath(date)`，跳过 `entries/backup/2026-05-13.md` 这类非规范同名文件。
- 增加回归测试覆盖 rebuild raw/version、raw-only 搜索、valid -> invalid 清旧 FTS、raw-only -> invalid -> delete 后 missing、canonical path 过滤。

## Why This Fix

相比在 search 层临时过滤旧 hit，修复 indexing 写入/重建边界更符合“SQLite 是缓存”的架构：每次 scan/rebuild 都重新从文件事实恢复数据库状态，查询层不用猜哪些数据已经过期。

raw-only 最小 row 是对当前 `entries` 聚合模型的最小适配，避免为 raw input hit 另开一套并行结果聚合，同时仍保留 entry 缺失和 attention 状态的可见性。

## Recognition Clues

- rebuild 后 raw input 搜不到，或 version count 变成 0。
- invalid JMF 被标记为 `attention`，但搜索仍命中过去 valid entry 的 section 关键词。
- raw input jsonl 存在，但当天没有正式 entry 时搜索完全没有结果。
- `entries/` 下备份或临时 Markdown 文件名形如 `yyyy-MM-dd.md`，扫描结果被非规范文件覆盖。

## Applicability / Non-Applicability

### Applies When

- SQLite/FTS 被设计为可重建缓存，而不是事实源。
- 索引数据来自主文件和 sidecar 文件的组合。
- invalid/missing 状态仍需要在历史列表中可见，但不能保留旧正文命中。

### Does Not Apply When

- 数据库本身就是唯一事实源，文件只是导出物。
- 查询层有完整版本化快照语义，允许同时展示旧版本和当前版本。
- 非规范路径本身就是产品支持的导入目录；那应先设计导入规则，而不是套用 canonical path 过滤。

## Related Artifacts

- Spec: [2026-05-13-phase-4a-local-history-search-design.md](../../specs/2026-05-13-phase-4a-local-history-search-design.md)
- Plan: [2026-05-13-phase-4a-local-history-search-implementation-plan.md](../../plans/2026-05-13-phase-4a-local-history-search-implementation-plan.md)
- Archive: [2026-05-13-phase-4a-local-history-search-archives.md](../../archives/2026-05/2026-05-13-phase-4a-local-history-search-archives.md)
- Related Problems:
  - None yet.
- Code or Test:
  - [JournalIndexingService.cs](../../../../src/Journal.Infrastructure/Storage/JournalIndexingService.cs)
  - [JournalIndexStore.cs](../../../../src/Journal.Infrastructure/Storage/JournalIndexStore.cs)
  - [JournalIndexingServiceTests.cs](../../../../tests/Journal.Tests/JournalIndexingServiceTests.cs)
  - [JournalIndexStoreTests.cs](../../../../tests/Journal.Tests/JournalIndexStoreTests.cs)
