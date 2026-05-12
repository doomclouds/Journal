# Harness No-Op Draft Overwrite

- Date: `2026-05-12`
- Topic slug: `harness-noop-draft-overwrite`
- Status: `Captured`
- Scope: `Feature`
- Tags: `harness-core`, `no-op`, `draft`, `audit`

## Symptom

模型返回 no-op / no-change 后，系统仍然写入或刷新 `reviewing` draft。无有效改动时，草稿时间戳或内容可能被无意义改写；没有 baseline 时甚至可能制造空 draft。

## Trigger / Context

- Harness planner 允许模型选择 no-op。
- Operation executor 层 no-op 不改变 JMF document。
- Service 层根据 execution result 统一写 draft。
- 最终审查检查 no-op 边界时发现 service 层仍无条件写 `JournalStatus.Reviewing` draft。

## Root Cause

no-op 的“不改变文档”只在 executor 层成立，但 service orchestration 把所有 valid execution 都当成可写草稿处理。状态虽然可计算为 `no-change`，但 draft 写入逻辑没有按 `no-change` 分支跳过。

这是典型的分层合同漂移：低层操作保持无副作用，高层编排又补了一次副作用。

## Fix

- `JournalHarnessService` 只在最终 run status 为 `reviewing` 时写 draft。
- no-op / no-change 只持久化 audit run、保留 raw input、返回当前 today state，不创建或刷新 draft。
- 增加 service-level 回归测试：
  - no-op 且无 baseline draft/entry 时不写 draft，但 audit run 为 `no-change`。
  - no-op 且已有 draft 时不覆盖原 draft 内容、状态和更新时间。

## Why This Fix

no-op 的产品语义是“本次输入已记录，但没有可整理改动”。写空 draft 或刷新现有 draft 都会制造虚假改动，使审计和用户可见状态不可信。把 no-op 限定为 audit-only 能保留事实记录，又不会污染草稿。

## Recognition Clues

- run status 是 `no-change`，但 `.journal/drafts/...md` 或 `.meta.json` 被更新。
- 没有工具调用或全是 no-op，却出现新的 reviewing draft。
- executor 测试显示 document unchanged，但 service 测试缺少 draft write 断言。

## Applicability / Non-Applicability

### Applies When

- 系统允许 planner 返回 no-op。
- service 层统一处理 valid execution 并写 draft。
- no-op 应表示“不改草稿，只记录审计”。

### Does Not Apply When

- 模型执行了 append/upsert/revise 并产生有效 JMF 改动；这应写 reviewing draft。
- validation 失败进入 attention；那是失败路径，不是 no-op。
- 用户手动编辑块保存；那属于 editor draft 写入，不是 harness no-op。

## Related Artifacts

- Spec: [2026-05-12-journal-harness-core-design.md](../../specs/2026-05-12-journal-harness-core-design.md)
- Plan: [2026-05-12-journal-harness-core-implementation-plan.md](../../plans/2026-05-12-journal-harness-core-implementation-plan.md)
- Archive: [2026-05-12-journal-harness-core-archives.md](../../archives/2026-05/2026-05-12-journal-harness-core-archives.md)
- Related Problems:
  - None yet.
- Code or Test:
  - [JournalHarnessService.cs](../../../../src/Journal.Infrastructure/Harness/JournalHarnessService.cs)
  - [JournalHarnessServiceTests.cs](../../../../tests/Journal.Tests/JournalHarnessServiceTests.cs)
