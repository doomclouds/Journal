# JMF Soft Section Consolidation

- Date: `2026-05-15`
- Topic slug: `jmf-soft-section-consolidation`
- Status: `Archived`
- Scope: `Feature`
- Tags: `jmf`, `sections`, `harness`, `editor`, `ai-prompt`

## Summary

本次交付把 JMF v1 的新内容分类从过细的旧结构收敛为更少的 active sections，降低 AI 和 Harness 把同一事实重复写入相近分类的概率，同时保留旧日记中 `learning`、`future-notes` 和 `gratitude` 的解析、校验、组合与显示兼容。

## Delivered Scope

- 定义 active/legacy section catalog：新内容只面向 `mood`、`work`、`relationship`、`health`、`money` 和 `inspiration`，旧 `learning`、`future-notes`、`gratitude` 保留兼容但不再作为新写入目标。
- Today editor 和前端新增块入口只暴露 active optional sections，`today-focus` 用户可见标题固定为“今日重点”。
- Harness prompt catalog、operation executor 和普通 AI JSON prompt 都对齐 active 分类；legacy Harness target 会以 `harness-target-inactive` 拒绝。
- 文档同步了 active/legacy 边界，并将 `reorganize-existing` 明确为用户选择式单篇转换路径。

## Out of Scope

- 不做全量历史日记自动迁移。
- 不删除旧 Markdown 中的 legacy section，也不让旧日记因为 legacy section 变成 invalid。
- 不引入用户自定义分类系统、Future Notes 完整模型、跨日期编辑或自动迁移提示 UI。

## Verification Snapshot

- `dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JmfSectionCatalogTests|JmfMarkdownValidatorTests|JmfMarkdownComposerTests|JournalHarnessPromptTests|JournalHarnessOperationExecutorTests|TodayJournalEditorServiceTests"`：73 passed。
- `npm test --prefix apps/desktop -- App.test.tsx todayWorkbenchView.test.ts`：139 passed。
- `dotnet test Journal.slnx`：330 passed。
- `npm test --prefix apps/desktop`：210 passed。
- `npm run build --prefix apps/desktop`：build passed。
- `git diff --check`：exit 0。
- 最终整体 review 复查通过，普通 AI JSON 生成链和 Mock 去重逻辑已补齐 active 分类合同。

## Source Documents

- Spec: [2026-05-15-jmf-soft-section-consolidation-design.md](../../specs/2026-05-15-jmf-soft-section-consolidation-design.md)
- Visual: None found for this topic.
- Plan: [2026-05-15-jmf-soft-section-consolidation-implementation-plan.md](../../plans/2026-05-15-jmf-soft-section-consolidation-implementation-plan.md)

## Related Problems

- [2026-05-14-harness-section-boundary-duplication-problem.md](../../problems/2026-05/2026-05-14-harness-section-boundary-duplication-problem.md)

## Notes

- 这次不做后台迁移；用户若要整理某一天旧结构，可以通过“重新整理”基于 raw inputs 生成 active 分类草稿，再自行确认覆盖。
