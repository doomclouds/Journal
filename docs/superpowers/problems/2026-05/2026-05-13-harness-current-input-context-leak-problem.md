# Harness Current Input Context Leak

- Date: `2026-05-13`
- Topic slug: `harness-current-input-context-leak`
- Status: `Captured`
- Scope: `Feature`
- Tags: `harness-core`, `prompt-context`, `raw-input`, `draft`, `audit`

## Symptom

`append-input` 已经把当前用户输入从 `historicalRawInputs` 中排除，但 spec review 发现 planner 仍可能在 `ProtectedContext.currentDraftMarkdown` 里看到同一条当前输入。

这会让模型同时把当前输入看成本轮 user message 和已经存在的历史草稿内容，破坏“当前输入不是 historical context”的核心边界。

## Trigger / Context

- Harness Run 需要在新增输入时保存 raw input，供后续运行和正式 JMF raw-inputs 使用。
- Planner prompt 要求本轮 current user message 不进入 historical raw inputs。
- 服务端为了构造执行基线和最终 draft，会读取当天 raw inputs 并重新渲染 baseline Markdown。
- 如果 prompt context 与最终写入基线共用同一份“包含当前输入”的 raw input 集合，就会出现隐性泄漏。

## Root Cause

修复前的实现只在 `ProtectedContext.historicalRawInputs` 字段层面排除了当前输入，但 `currentDraftMarkdown` 是从已追加当前输入后的 baseline 派生出来的。

也就是说，字段名看起来干净，派生 Markdown 却已经包含当前输入。Prompt 分层问题不只发生在显式 raw input 列表，也会通过 draft、summary、baseline 等派生上下文绕回来。

## Fix

- 在 `JournalHarnessService` 中拆分两组输入：
  - `allInputs`：用于最终 draft raw-inputs、executor 可用 source ids 和落盘事实。
  - `promptContextInputs`：排除 `run.CurrentRawInputId`，只用于 planner protected context 和 `currentDraftMarkdown`。
- `append-input` 的 planner prompt 使用 `promptContextInputs` 构造 dynamic Journal Context。
- `reorganize-existing` 继续使用当天已有 raw inputs，不排除任何当前输入，因为它不会追加 raw input。
- 补回归测试：断言 protected context 的 historical raw inputs 和 currentDraftMarkdown 都不包含本次输入，user message 包含本次输入，最终 draft 的 raw-inputs 仍包含本次输入。

## Why This Fix

只在 prompt JSON 的 raw input 列表上过滤不够，因为模型会阅读整个 protected context。把“planner 可见上下文”和“最终写入事实集合”拆成两个明确变量，能同时满足用户输入被保存、最终草稿可显示 raw-inputs、以及 planner 本轮不把当前输入当作历史材料这三件事。

相比延后 raw input 持久化，这个修法对现有 run/audit/草稿写入顺序影响更小，也能保留当前接口的可靠落盘语义。

## Recognition Clues

- 测试只检查 `historicalRawInputs` 不含当前输入，但没有检查 `currentDraftMarkdown`。
- 代码中同一份 raw input 集合同时用于 prompt context、baseline render 和最终 draft。
- 模型在 append-input 场景里表现得像“这句话已经整理过”，或把当前输入当成重复内容。
- spec review 抓到“字段过滤正确，但派生上下文仍污染”的不一致。

## Applicability / Non-Applicability

### Applies When

- 同一条用户输入既要持久化，又要作为当前 user message 驱动本轮 planner。
- Prompt context 包含 raw input 列表之外的派生内容，例如 draft Markdown、summary、baseline 或 indexed snippets。
- 需求要求“本轮之前已有材料”和“本轮当前意图”严格分层。

### Does Not Apply When

- 重新整理模式没有追加 raw input，本轮 user message 是固定操作指令。
- 模型只接收当前 user message，不接收任何派生 historical context。
- 问题是最终 draft 没有包含 raw-inputs；那应检查写入基线和 JMF composer，而不是 prompt context 泄漏。

## Related Artifacts

- Spec: [2026-05-13-harness-unified-planner-prompt-design.md](../../specs/2026-05-13-harness-unified-planner-prompt-design.md)
- Plan: [2026-05-13-harness-unified-planner-prompt-implementation-plan.md](../../plans/2026-05-13-harness-unified-planner-prompt-implementation-plan.md)
- Archive: [2026-05-13-harness-unified-planner-prompt-archives.md](../../archives/2026-05/2026-05-13-harness-unified-planner-prompt-archives.md)
- Related Problems:
  - [Harness Submit Not Wired Leaves Audit Empty](./2026-05-13-harness-submit-not-wired-audit-empty-problem.md)
- Code or Test:
  - [JournalHarnessService.cs](../../../../src/Journal.Infrastructure/Harness/JournalHarnessService.cs)
  - [JournalHarnessServiceTests.cs](../../../../tests/Journal.Tests/JournalHarnessServiceTests.cs)
