# Phase 8 Same-Day Memory Corridor Anniversary

- Date: `2026-05-16`
- Topic slug: `phase-8-memory-corridor-anniversary`
- Status: `Archived`
- Scope: `Feature`
- Tags: `anniversary`, `memory-corridor`, `history`, `desktop`, `data-export`

## Summary

Phase 8 将 Phase 6B 的只读同日年轮升级为记忆回廊：同一 `MM-DD` 的多年记录先以时间线卡片呈现，点击后在中间区域阅读该日期最后正式日记；左侧只做日期与纪念日导航，右侧只做纪念日资料与下一年锚点管理，并新增 `.journal/anniversaries/anniversaries.json` 作为可备份、可导入导出的本地源数据。

## Delivered Scope

- 新增纪念日数据域、store/service/API，支持保存/编辑同日纪念日、按 `MM-DD` 查询、记录下一年一句话，并把数据纳入备份、导出、导入与清空流程。
- 同日年轮卡片从正式 entry section 派生 `cardPreview` 和 `entryUpdatedAt`，不再把搜索 `hits` 当作产品级简介。
- 桌面端完成三栏记忆回廊：左侧日期导航与已保存纪念日，中间时间线/正式日记阅读态，右侧纪念日设置与下一年提醒。
- 时间线卡片保持摘要语义：标题和预览行超过 30 字符显示省略号，每张卡片最多展示 3 条预览，完整内容通过阅读态查看。
- 下一年提醒采用手动采纳模型：到达 `targetDate` 后才能采纳为当日 raw input；前端隐藏未来采纳入口，后端 API 同步拒绝提前采纳。
- 同日多个纪念日按 anniversary id 精确选择，导入/刷新失败时清理 stale 纪念日状态，避免旧数据挂到新日期上。

## Out of Scope

- 不做 AI 自动总结多年变化、AI follow-up chat、embedding/语义搜索或云同步。
- 不在同日年轮内提供正式 entry 编辑、删除、版本 diff、rollback UI 或非今日 restore/confirm。
- 不自动生成常看日期，不自动把下一年提醒写入未来正式日记。
- 不交付纪念日删除/归档流程；本阶段只支持新增、编辑、采纳与忽略提醒。

## Verification Snapshot

- `dotnet test Journal.slnx`：367 passed，0 failed。
- `npm test --prefix apps/desktop`：236 passed，0 failed。
- `npm run build --prefix apps/desktop`：passed。
- `git diff --check`：passed。
- Subagent-driven implementation completed with per-task spec review, code quality review, and final holistic review; final review found and fixed the API-level future next-year-note adoption guard and docs path mismatch.

## Source Documents

- Spec: [2026-05-16-phase-8-memory-corridor-anniversary-design.md](../../specs/2026-05-16-phase-8-memory-corridor-anniversary-design.md)
- Visual: [2026-05-16-phase-6c-memory-corridor-prototype.html](../../specs/2026-05-16-phase-6c-memory-corridor-prototype.html)
- Plan: [2026-05-16-phase-8-memory-corridor-anniversary-implementation-plan.md](../../plans/2026-05-16-phase-8-memory-corridor-anniversary-implementation-plan.md)

## Related Problems

- [Memory Corridor Prototype Drift](../../problems/2026-05/2026-05-17-memory-corridor-prototype-drift-problem.md)

## Notes

- Phase 8 intentionally supersedes Phase 6B 的“只读同日年轮”边界：同日阅读仍不编辑正式日记，但纪念日资料和下一年提醒属于独立本地数据域。
- 纪念日 JSON 是源数据；SQLite history index 仍然是可重建缓存，不承担纪念日事实源职责。
- 2026-05-17 人工验收后回补同日记忆回廊的原型结构合同：左侧年份是中间时间轴节点的导航镜像，中间卡片承载简介和阅读入口，测试必须覆盖这种结构关系。
- 2026-05-17 后续体验修正补充了时间线卡片密度规则：外层只展示有限摘要，避免长日记内容把时间轴重新撑成正文列表。
