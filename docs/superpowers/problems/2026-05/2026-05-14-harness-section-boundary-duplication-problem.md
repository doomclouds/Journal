# Harness Section Boundary Duplication

- Date: `2026-05-14`
- Topic slug: `harness-section-boundary-duplication`
- Status: `Captured`
- Scope: `Feature`
- Tags: `harness-core`, `planner-prompt`, `section-catalog`, `deduplication`, `jmf`

## Symptom

晨间日记里同一条事实会被 AI 同时写进相近 section，例如“今天继续优化晨间日记 / 处理 Harness 提示词问题”同时出现在 `today-focus` 和 `work`。用户阅读时会感觉九宫格不是分工，而是在重复堆内容。

同一类边界问题也可能反向表现为过度保守：用户只说“调整日记结构”时，Planner 把它当成“不够具体”并 no-op，要求用户说明具体怎么调，而不是主动检查重复、错分、整段内容和相近 section 边界。

## Trigger / Context

- Planner prompt 只要求“分配到最合适的 section”，但没有给相近 section 的互斥边界。
- `sectionCatalog` 只暴露 id、title、order、kind 和 editable 标记，缺少 `semanticHint` / `avoidWhen` 这种可执行语义。
- 模型看到 `today-focus` 与 `work` 都像“今天要推进的事”，于是可能把同一事实拆成两份相似表述。
- 服务端 executor 原先只按工具调用顺序执行，不会识别同一 normalized fact 被重复写入多个 section。

## Root Cause

九宫格 section catalog 是产品语义的一部分，但实现里把它当成了简单显示目录。缺少主题边界时，LLM 会用自然语言近义关系自行猜测；`today-focus`、`work`、`learning`、`health` 等 section 又都可能承载“今天要做的事”，于是相近主题之间容易重复。

仅靠 prompt 说“不要重复”不够，因为模型仍可能生成重复工具调用；仅靠后端去重也不够，因为相似但不完全相同的内容需要 planner 先做语义选择。

## Fix

- `JmfSectionDefinition` 增加 `SemanticHint` 和 `AvoidWhen`。
- `JmfSectionCatalog` 为九宫格每个主题补语义边界，尤其明确：
  - `today-focus` 只放今天总体优先级、关键行动或日程重心。
  - `work` 放具体工作项目、开发、会议、交付或排障。
  - `learning`、`health`、`relationship`、`money`、`inspiration`、`future-notes`、`gratitude` 都有各自的优先归属。
- Harness prompt 增加 Section Boundary Rules，要求同一事实只进入一个最合适的 section，并列出相近 section 的边界。
- Journal Context 的 `sectionCatalog` 额外序列化 `semanticHint` 和 `avoidWhen`，让模型拿到和代码同源的主题说明。
- `JournalHarnessOperationExecutor` 对完全相同的 normalized fact 做最后防线去重，保留语义更具体的 section。例如同一事实同时写入 `today-focus` 和 `work` 时，保留 `work`。
- 补测试覆盖 prompt 中的边界规则、catalog 语义字段，以及 executor 对重复事实的去重行为。

## Why This Fix

把语义边界放进 `JmfSectionCatalog`，比在 prompt 里维护一份独立说明更不容易漂移；prompt 负责让模型理解边界，executor 负责阻断完全重复的工具调用。这是两层防线：语义相似靠 planner 减少，完全重复靠服务端兜底。

不直接删除 `work` section，是因为 `today-focus` 和 `work` 的产品价值不同：前者是今天总体重心，后者是具体工作项目和排障记录。问题在边界，不在 section 本身存在。

## Recognition Clues

- 同一句或近义句出现在两个相邻主题下。
- `today-focus` 里塞入大量具体开发、会议或排障细节。
- `work` 和 `today-focus` 的 tool call `content` 高度相似，甚至完全一样。
- Prompt 里没有相近 section 的互斥说明，`sectionCatalog` 也没有语义 hint。
- 用户输入“调整日记结构”“优化分类”“重新分配 section”这类短命令后，audit 显示 no-op 原因是“用户没有说明具体要调整什么”。

## Applicability / Non-Applicability

### Applies When

- LLM 负责把自然语言材料分配到多个固定主题 section。
- 两个 section 都可能解释同一事实，但产品上希望只保留最合适的一处。
- 工具调用是 append/upsert/revise 这类 section 级操作。

### Does Not Apply When

- 用户明确要求同一事实在多个 section 中以不同视角出现。
- 两个 section 内容只共享关键词，但事实和意图不同。
- 重复来自历史旧 draft 已经存在的内容；当前 executor 只处理本轮工具调用中的完全重复 normalized fact。

## Related Artifacts

- Spec: [2026-05-13-harness-unified-planner-prompt-design.md](../../specs/2026-05-13-harness-unified-planner-prompt-design.md)
- Plan: [2026-05-13-harness-unified-planner-prompt-implementation-plan.md](../../plans/2026-05-13-harness-unified-planner-prompt-implementation-plan.md)
- Archive: [2026-05-13-harness-unified-planner-prompt-archives.md](../../archives/2026-05/2026-05-13-harness-unified-planner-prompt-archives.md)
- Related Problems:
  - [Journal Generated Content Blank Line Inflation](./2026-05-13-journal-generated-content-blank-line-inflation-problem.md)
- Code or Test:
  - [JmfSectionCatalog.cs](../../../../src/Journal.Domain/Entries/JmfSectionCatalog.cs)
  - [JmfSectionDefinition.cs](../../../../src/Journal.Domain/Entries/JmfSectionDefinition.cs)
  - [JournalHarnessPrompt.cs](../../../../src/Journal.Infrastructure/Harness/JournalHarnessPrompt.cs)
  - [JournalHarnessOperationExecutor.cs](../../../../src/Journal.Infrastructure/Harness/JournalHarnessOperationExecutor.cs)
  - [JournalHarnessPromptTests.cs](../../../../tests/Journal.Tests/JournalHarnessPromptTests.cs)
  - [JournalHarnessOperationExecutorTests.cs](../../../../tests/Journal.Tests/JournalHarnessOperationExecutorTests.cs)
