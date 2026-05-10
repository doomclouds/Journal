# LLM Prompt And Validator Contract Mismatch

- Date: `2026-05-10`
- Topic slug: `llm-validator-contract-mismatch`
- Status: `Captured`
- Scope: `Feature`
- Tags: `llm`, `validator`, `prompt-contract`, `jmf`

## Symptom

真实 LLM 生成草稿后进入 `attention`，草稿内容显示：

```md
# LLM generation failed

## Errors

- yesterdayReview must contain at least one item. todayFocus must contain at least one item.
- Code: validation_failed
```

正式 entry 没有被写入，但用户无法从真实模型得到 reviewing draft。

## Trigger / Context

- 用户输入没有明确包含昨日复盘或今日重点。
- `JournalAiPrompt.SystemInstructions` 要求信息不足时使用空数组，不要猜测。
- 真实 LLM 按提示词输出 `yesterdayReview: []` 或 `todayFocus: []`。
- `JournalAiJsonValidator` 仍沿用早期 mock 阶段的非空结构块约束。

## Root Cause

提示词合同、JMF 渲染能力和 AI JSON validator 的合同发生漂移。当前产品原则允许模型只忠实整理已有信息，信息不足时保留 `rawInputs` 并让结构化块为空；JMF renderer/composer 也能输出带 marker 的空 required section。但 `JournalAiJsonValidator` 仍把 `yesterdayReview` 和 `todayFocus` 当成必须至少一条，导致真实 LLM 的合规输出被判定为 `validation_failed`。

## Fix

- `JournalAiJsonValidator` 只继续强制 `schema`、`date`、`monthDay` 和 `rawInputs` 等真正必要字段。
- 允许 `yesterdayReview`、`todayFocus` 和 `inspiration` 为空数组。
- 增加回归测试，覆盖 raw input 被保留时结构化块为空仍然通过校验。
- 修正阶段 5 实施计划中的过时提示词片段，明确不够信息时输出空数组。

## Why This Fix

强迫 LLM 至少生成一条会诱导它编造内容，违背 Journal 的核心原则：原始表达是源材料，AI 只能整理不能替用户补事实。把空块兜底成占位文本也会污染正式 Markdown。保留 raw input 非空约束，同时允许结构化块为空，能维持可追溯性和 JMF 结构完整性。

## Recognition Clues

- 错误码是 `validation_failed`，错误字段集中在 `yesterdayReview` 或 `todayFocus` 的非空约束。
- prompt 中出现“没有足够信息时使用空数组”，但 validator 报“must contain at least one item”。
- JMF renderer 或 composer 已能生成空 section marker，说明失败发生在 AI JSON 边界，不是 Markdown 结构边界。

## Applicability / Non-Applicability

### Applies When

- 真实 LLM 接入后，模型按 faithful prompt 输出空数组，却被后端字段数量校验拒绝。
- 产品原则要求 AI 不猜测、不硬凑条目，原始输入仍必须保留。
- 下游 JMF 结构允许 required section 存在但内容为空。

### Does Not Apply When

- `rawInputs` 为空或被模型改写；这仍应被拒绝或由服务端覆盖保护。
- JSON schema、日期、字段名或类型不符合合同；这些仍属于有效的 validator 失败。
- 用户明确要求产品层面必须生成每日固定任务清单；那是新的产品合同，需要先改设计和提示词。

## Related Artifacts

- Spec: [2026-05-10-ai-provider-integration-design.md](../../specs/2026-05-10-ai-provider-integration-design.md)
- Plan: [2026-05-10-ai-provider-integration-implementation-plan.md](../../plans/2026-05-10-ai-provider-integration-implementation-plan.md)
- Archive: [2026-05-10-ai-provider-integration-archives.md](../../archives/2026-05/2026-05-10-ai-provider-integration-archives.md)
- Related Problems:
  - None yet.
- Code or Test:
  - [JournalAiJsonValidator.cs](../../../../src/Journal.Infrastructure/Jmf/JournalAiJsonValidator.cs)
  - [MockAiAndJmfTests.cs](../../../../tests/Journal.Tests/MockAiAndJmfTests.cs)
  - [JournalAiPrompt.cs](../../../../src/Journal.Infrastructure/Ai/JournalAiPrompt.cs)
