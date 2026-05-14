# Phase 6B Same-Day Anniversary Wheel

- Date: `2026-05-14`
- Topic slug: `phase-6b-anniversary-wheel`
- Status: `Archived`
- Scope: `Feature`
- Tags: `history`, `anniversary`, `desktop`, `sqlite`, `ui`

## Summary

同日年轮把“今天的月日”变成历史回看的入口：用户可以从 Today Assistant 打开历史工作台的 anniversary mode，按 `MM-DD` 回看历年同一天的正式日记、原始材料摘要和版本快照，而不改变当前今日日记的主视觉与编辑流程。

## Delivered Scope

- 新增 `GET /journal/history/anniversary/{monthDay}`，基于可重建 SQLite history index 的 `entries.month_day` 查询同月日记录。
- 年轮结果覆盖 processed、attention、missing 和 raw-only indexed days，并给每个日期返回 section/raw-input 轻量摘要。
- 桌面端新增独立三栏 `AnniversaryWheelWorkbench`，包含日期选择、年份卡片、Markdown 预览、原始材料摘要、版本查看和既有 draft-only restore 操作。
- Today Assistant 新增 `同日年轮` 入口，默认使用今天的 `monthDay`，并补齐 detail/version 异步请求 stale guard。
- 前端 month-day 输入与后端语义对齐：`02-29` 合法，非法日期不会发起 anniversary 请求，也不会把旧结果挂到新标题下。

## Out of Scope

- 不提供跨日期恢复确认；版本恢复仍受现有“仅今日可恢复为草稿”限制。
- 不新增删除、diff、item-level provenance 或真正的年轮 AI 总结。
- 不改变普通 Today 日记页面的视觉结构和编辑交互。

## Verification Snapshot

- `dotnet test Journal.slnx`：293 passed，0 failed。
- `npm test --prefix apps/desktop`：145 passed，0 failed。
- `npm run build --prefix apps/desktop`：passed。
- `git diff --check`：passed。
- Subagent-driven implementation completed with per-task spec reviews and code quality reviews; final review found and fixed invalid month-day stale-result behavior.

## Source Documents

- Spec: [2026-05-14-phase-6b-anniversary-wheel-design.md](../../specs/2026-05-14-phase-6b-anniversary-wheel-design.md)
- Visual: [2026-05-14-phase-6b-anniversary-wheel-prototype.html](../../specs/2026-05-14-phase-6b-anniversary-wheel-prototype.html)
- Plan: [2026-05-14-phase-6b-anniversary-wheel-implementation-plan.md](../../plans/2026-05-14-phase-6b-anniversary-wheel-implementation-plan.md)

## Related Problems

- [History Workbench Stale Selection Restore Mismatch](../../problems/2026-05/2026-05-13-history-workbench-stale-selection-restore-mismatch-problem.md)

## Notes

- SQLite 仍然只是可重建缓存；Markdown entries、raw-input jsonl 和 version files 仍是可持久追溯的源材料。
- 本次年轮复用了 History Workbench 的版本查看与恢复约束，因此相关竞态修复也同步覆盖普通 history mode。
