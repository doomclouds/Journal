# Harness Unified Planner Prompt Delivery

- Date: `2026-05-13`
- Topic slug: `harness-unified-planner-prompt`
- Status: `Archived`
- Scope: `Feature`
- Tags: `harness-core`, `planner-prompt`, `raw-input`, `reorganize`, `sse`, `jmf`

## Summary

本轮把今日工作台的新增输入和“重新整理”统一到 Harness Run：模型不再走旧的整篇 regenerate 路径，而是在同一套 Markdown system instructions 与动态 Journal Context 下，通过受控工具规划 JMF section 操作。设计重点是把稳定方法论、动态日记上下文和本轮 user message 分层，避免当前输入被提前混入历史 raw inputs。

## Delivered Scope

- `JournalHarnessPrompt` 升级为 `journal-harness-v2`，System Instructions 使用 Markdown contract 表达 Core Principle、Priority Order、Protected Context Boundary、Green Path、Red Lines、工具选择、正反例和写作风格。
- Journal Context 动态携带 mode、historical raw inputs、current draft、confirmed entry、section catalog 和 available tools；section catalog 来自 `JmfSectionCatalog`。
- Harness Run 新增 `append-input` 与 `reorganize-existing` 两种模式；新增输入会保存为后续 raw input，但本轮 planner 只把它当作 user message。
- `reorganize-existing` 使用服务端固定 user prompt，不追加 raw input，并继续通过 Harness tools 与 SSE 写 draft / audit。
- 今日页“重新整理”改为调用 `/journal/today/harness/runs` + SSE，普通输入和重新整理都能产生 audit run。
- README 与 AGENTS 更新为当前真实链路，明确旧 `/journal/today/draft/regenerate` 只是兼容接口。

## Out of Scope

- 不改动今日页视觉设计、布局或交互文案。
- 不新增删除、隐藏、整段替换用户内容、diff、rollback 或多日期编辑。
- 不新增聊天式 follow-up UI，也不让前端自由传入重新整理 prompt。
- 不把真实 LLM 决策结果作为自动化测试判定标准。

## Verification Snapshot

- `dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JournalHarnessPromptTests|JournalHarnessServiceTests|TodayJournalEndpointTests"`：75/75 passed。
- `npm test --prefix apps/desktop -- App.test.tsx`：94/94 passed。
- Task 1/2/3/4 均完成 subagent spec review 与 code quality review；Task 2 的 context 泄漏问题已补测试并修复。
- `dotnet test Journal.slnx`：277/277 passed。
- `npm test --prefix apps/desktop`：136/136 passed。
- `npm run build --prefix apps/desktop`：TypeScript build 和 Vite production build passed。
- Archive / problem / index validators passed；最终 code review 仅发现归档字段过度声明，已修正。

## Source Documents

- Spec: [2026-05-13-harness-unified-planner-prompt-design.md](../../specs/2026-05-13-harness-unified-planner-prompt-design.md)
- Visual: None found for this topic.
- Plan: [2026-05-13-harness-unified-planner-prompt-implementation-plan.md](../../plans/2026-05-13-harness-unified-planner-prompt-implementation-plan.md)

## Related Problems

- [Harness Current Input Context Leak](../../problems/2026-05/2026-05-13-harness-current-input-context-leak-problem.md)

## Notes

- 旧 `POST /journal/today/draft/regenerate` 没有从 API 删除，但今日产品路径已经不再依赖它；后续若要清理旧接口，应单独设计兼容策略。
