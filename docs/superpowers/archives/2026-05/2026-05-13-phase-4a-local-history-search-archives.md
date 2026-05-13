# Phase 4A Local History Search Delivery

- Date: `2026-05-13`
- Topic slug: `phase-4a-local-history-search`
- Status: `Archived`
- Scope: `Feature`
- Tags: `phase-4a`, `history`, `search`, `sqlite`, `versions`, `desktop`

## Summary

Phase 4A 把 Journal 的正式 Markdown 从“只保存当前版本”推进到可追溯的本地历史系统：覆盖正式 entry 前先保存版本快照，再写入正式 Markdown，并把 Markdown、raw inputs 和版本文件同步进可重建 SQLite/FTS5 索引；桌面端新增历史与版本工作台，用当前实际三栏工作区承载搜索、日期详情、版本查看和今日版本恢复为草稿。

## Delivered Scope

- 正式 entry 覆盖前会创建 `.journal/versions/yyyy/MM/yyyy-MM-dd/` 下的 Markdown 与 metadata 快照；首次写入不创建快照，snapshot 失败不会覆盖旧正式 entry。
- 新增 rebuildable SQLite/FTS5 history index，覆盖 entry summary、sections、raw inputs、version metadata、schema recovery、scan/rebuild 和 bounded snippets。
- `EntryWritePipeline` 统一正式写入顺序：snapshot -> write entry -> update index；index 失败不回滚正式 Markdown，但 warning 会进入今日状态。
- 新增 History service/API：搜索、日期详情、版本列表、版本详情、restore-draft、手动 scan/rebuild；恢复版本只写 `reviewing` draft。
- 桌面端新增 History Workbench，从 Today Assistant 进入全工作区模式，支持搜索、状态筛选、日期选择、详情预览、版本列表和恢复今日版本为草稿。

## Out of Scope

- 不包含非今日版本恢复/确认；当前 restore 仅允许 today's date，避免 today-centered editor/confirm 误操作历史日期。
- 不包含多日期编辑、diff/rollback UI、item 级 provenance、删除流程、AI 改写聊天、自动保存或生产安装包。
- SQLite 仍是可重建缓存，不是事实源；不要把数据库当作唯一持久化状态。

## Verification Snapshot

- `dotnet test Journal.slnx`：265/265 backend tests passed。
- `npm test --prefix apps/desktop`：131/131 frontend tests passed。
- `npm run build --prefix apps/desktop`：TypeScript build 和 Vite production build passed。
- Task 4/5/6/7 均经过 subagent-driven implementation、规格审查和代码质量审查；Task 7 额外修复了 history selection stale detail/version race。

## Source Documents

- Spec: [2026-05-13-phase-4a-local-history-search-design.md](../../specs/2026-05-13-phase-4a-local-history-search-design.md)
- Visual: [2026-05-13-phase-4a-history-search-layout-prototype.html](../../specs/2026-05-13-phase-4a-history-search-layout-prototype.html)
- Plan: [2026-05-13-phase-4a-local-history-search-implementation-plan.md](../../plans/2026-05-13-phase-4a-local-history-search-implementation-plan.md)

## Related Problems

- [History Index Cache Stale Sidecars](../../problems/2026-05/2026-05-13-history-index-cache-stale-sidecars-problem.md)
- [History Workbench Stale Selection Restore Mismatch](../../problems/2026-05/2026-05-13-history-workbench-stale-selection-restore-mismatch-problem.md)

## Notes

- 本交付按当前产品边界选择“小恢复”：历史版本可以恢复为今日 `reviewing` draft，但不直接写正式 entry，也不开放非今日恢复。后续若要支持任意日期恢复，需要先做 date-aware editor/confirm。
