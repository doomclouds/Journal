# LLM Prompt Field Scope Too Narrow

- Date: `2026-05-10`
- Topic slug: `llm-prompt-field-scope-too-narrow`
- Status: `Captured`
- Scope: `Feature`
- Tags: `llm`, `prompt`, `journal-semantics`, `today-focus`

## Symptom

用户输入了当天发生的开心事、母亲节和值得纪念的内容，真实 LLM 没有失败，draft 也进入 `reviewing`，但正文里的 `today-focus` 为空。模型只把这些内容提取到了 front matter 的 `tags`、`topics` 和 `mood`：

- `tags`: 家庭、母亲节、外出
- `topics`: 家庭聚餐、母亲节
- `mood`: 开心

从用户视角看，这像是 LLM 没有帮忙整理“今天发生了什么”和“值得庆祝什么”。

## Trigger / Context

- 原始输入表达的是今天发生的生活事件、情绪、节日或纪念日，而不是明确的计划、待办或下一步。
- prompt 把 `todayFocus` 写成“今日计划、重点、待办或下一步”，模型倾向把它理解为任务清单。
- 当前 AI JSON 正文合同只有 `yesterdayReview`、`todayFocus` 和 `inspiration` 三项，没有单独的 `celebrations`、`gratitude` 或生活事件字段。

## Root Cause

prompt 字段语义过窄，和晨间日记的真实使用语境不一致。`todayFocus` 在用户心智里是“今天值得放进正文的重点”，既包括计划，也包括已经发生的重要事件、家庭生活片段、节日和值得纪念/庆祝的事情；但 prompt 更像工作待办提取器。真实 LLM 按这个狭义合同执行后，把生活事件安全地放进 metadata，却没有写进正文块。

## Fix

- 将 prompt version 提升到 `journal-entry-json-v1.1`，让生成痕迹可追踪。
- 扩展 `todayFocus`：允许今日计划、待办、正在推进的事情、已经发生的重要事件、家庭/生活片段、节日、值得记录、纪念或庆祝的事情。
- 扩展 `yesterdayReview`：允许昨天、过去、最近已完成、正在复盘、遗留或值得回顾的事情，也可以包含家庭生活或情绪回看。
- 扩展 `inspiration`：允许无法自然归入昨日回顾或今日重点的观察、感受、感恩和值得保留的片段。
- 增加 prompt 合同测试，要求系统提示词明确包含这些字段范围。
- 用真实 DeepSeek 重新生成当天 draft，确认 `today-focus` 写入“一家出去吃饭”“出去玩了，很开心”“母亲节值得纪念”“晚上计划去跑步”等条目。
- 2026-05-15 JMF 分类软合并后，将普通 AI JSON prompt version 提升到 `journal-entry-json-v1.2`，并把旧三正文字段扩展为 active sections：`work`、`relationship`、`health`、`money` 和 `inspiration`。这次修复还要求 Mock provider 先归入具体 active section，再让 `todayFocus` 跳过已归类事实，避免同一句同时出现在 `today-focus` 与 `work`。

## Why This Fix

不新增 `celebrations` 字段可以保持当前 JSON、validator、renderer、JMF 和 UI 合同稳定，避免一次 prompt 修复扩张成数据结构迁移。把 `todayFocus` 调整为“今日正文重点”更符合现有块名和用户期望，同时仍保留“不虚构事实、空数组允许、raw input 必须保留”的安全边界。

## Recognition Clues

- draft 状态是 `reviewing`，没有 `validation_failed`。
- front matter 的 `tags`、`topics` 或 `mood` 已经识别出生活事件和情绪，但 `today-focus` 为空。
- 用户输入包含“今天”“开心”“值得纪念”“出去玩”“一家吃饭”等日记事件词，而不是任务/待办词。
- prompt 中 `todayFocus` 的说明偏“计划、重点、待办、下一步”，缺少已发生事件、纪念、庆祝、生活片段。
- 做 JMF 分类调整时，只改 Harness prompt 或前端新增块是不够的；还要检查 `JournalAiPrompt`、`JournalAiJson`、`JmfMarkdownRenderer` 和 Mock provider 这条兼容生成链是否继续固化旧字段合同。

## Applicability / Non-Applicability

### Applies When

- 真实 LLM 可以识别用户输入主题，但正文块没有承接生活事件或纪念性内容。
- 产品目标是晨间日记整理，而不是纯工作任务提取。
- 当前不想扩展 AI JSON schema，只想通过 prompt 调整字段语义。

### Does Not Apply When

- 模型返回非法 JSON、空 `rawInputs` 或字段类型错误；那属于 provider/runtime/validator 问题。
- 产品明确决定新增或合并正文 section；那需要同步 schema、prompt、renderer、Mock provider、Harness 和 UI，而不是只改单个 prompt。
- 用户输入完全没有可整理的正文信息；此时正文块为空是合理结果。

## Related Artifacts

- Spec: [2026-05-10-ai-provider-integration-design.md](../../specs/2026-05-10-ai-provider-integration-design.md)
- Plan: [2026-05-10-ai-provider-integration-implementation-plan.md](../../plans/2026-05-10-ai-provider-integration-implementation-plan.md)
- Archive: [2026-05-10-ai-provider-integration-archives.md](../../archives/2026-05/2026-05-10-ai-provider-integration-archives.md)
- Follow-up Archive: [2026-05-15-jmf-soft-section-consolidation-archives.md](../../archives/2026-05/2026-05-15-jmf-soft-section-consolidation-archives.md)
- Related Problems:
  - [2026-05-10-llm-validator-contract-mismatch-problem.md](./2026-05-10-llm-validator-contract-mismatch-problem.md)
- Code or Test:
  - [JournalAiPrompt.cs](../../../../src/Journal.Infrastructure/Ai/JournalAiPrompt.cs)
  - [OpenAiCompatibleJournalAiProviderTests.cs](../../../../tests/Journal.Tests/OpenAiCompatibleJournalAiProviderTests.cs)
  - [MockAiAndJmfTests.cs](../../../../tests/Journal.Tests/MockAiAndJmfTests.cs)
