# DeepSeek Thinking Tool Calls Require Reasoning Content Replay

- Date: `2026-05-13`
- Topic slug: `deepseek-thinking-tool-call-reasoning-content`
- Status: `Captured`
- Scope: `Runtime`
- Tags: `llm`, `deepseek`, `agent-framework`, `tool-calls`

## Symptom

Harness run 调用真实 DeepSeek provider 时失败：

```text
LLM request failed.
Code: provider_error
Technical details: HTTP 400 (invalid_request_error: invalid_request_error) The `reasoning_content` in the thinking mode must be passed back to the API.
```

失败发生在 Microsoft Agent Framework 工具调用链路中，而不是普通 mock 运行、JMF 执行器或审计页渲染。

## Trigger / Context

- 当前 runtime 使用 `Microsoft.Agents.AI` + `OpenAI.Chat.ChatClient.AsAIAgent(...)`。
- Harness planner 暴露 side-effect-free collector tools。
- DeepSeek V4 thinking mode 默认开启。
- 模型在 thinking mode 中执行 tool call 后，后续请求继续沿用 OpenAI-compatible chat history。

## Root Cause

DeepSeek 官方 thinking mode 规定：一旦 thinking mode 的轮次发生 tool call，后续请求必须完整回传 assistant message 中的 `reasoning_content`。

本项目当前通过 OpenAI .NET SDK / `Microsoft.Extensions.AI.OpenAI` 适配层进入 Agent Framework。该适配层可以把响应里的 `reasoning_content` 读成 `TextReasoningContent`，但在把 assistant history 重新序列化为 OpenAI chat messages 时，不会把 `TextReasoningContent` 写回 DeepSeek 的 top-level `reasoning_content` 字段。工具调用后的下一轮请求缺字段，DeepSeek 因此返回 400。

## Fix

当前修复已落地为保守兼容层：

- 对 `providerId == deepseek` 或 host 为 `api.deepseek.com` 的请求，向 `ChatCompletionOptions.Patch` 写入 top-level `thinking: { "type": "disabled" }`。
- JSON 生成和 Harness planner 两条 OpenAI-compatible runtime 路径共用同一兼容判断。
- OpenAI provider 不写入 DeepSeek 专属字段，避免污染标准 OpenAI 请求。
- 补回归测试验证 DeepSeek JSON / Harness planner options 都带 `thinking` disabled，OpenAI options 不带。

后续更完整的方案是增加 DeepSeek 专用 `IChatClient` 或 adapter：在 request message 构建时把 `TextReasoningContent` 序列化回 `reasoning_content`，并在 streaming/non-streaming 响应中稳定保留该内容。这样可以在明确需要时重新启用 thinking。

## Why This Fix

短期关闭 thinking 是最小风险恢复路径：不改 Agent Framework tool loop，不自行接管所有 function calling 和 SSE 解析，也不会影响 OpenAI、智谱或 Custom provider。

直接“总是保留 thinking”需要替换或包裹 `IChatClient`，否则现有 OpenAI-compatible 适配层仍会丢失 DeepSeek 扩展字段。这个方向适合做成后续独立增强，不应在恢复当前日记操作时半路硬塞。

## Recognition Clues

- 错误只在 DeepSeek thinking mode + tool call / agent loop 后出现。
- 报错包含 `reasoning_content`、`thinking mode`、`must be passed back`。
- 普通 JSON 生成可能正常，但 Harness planner 或多轮工具调用失败。
- `Microsoft.Extensions.AI.OpenAI` 响应解析能看到 `TextReasoningContent`，但 request 序列化不写回 DeepSeek 的 `reasoning_content`。

## Applicability / Non-Applicability

### Applies When

- 使用 DeepSeek V4 thinking mode。
- 使用 Agent Framework、Microsoft.Extensions.AI、OpenAI-compatible provider 或类似抽象层进行 tool call。
- 框架没有确认支持 DeepSeek `reasoning_content` 的持久化与回传。

### Does Not Apply When

- 使用非 thinking 模型或已显式 `thinking: disabled` 且服务端确认生效。
- 使用 DeepSeek 专用 client，并已确认 assistant tool-call history 会完整回传 `reasoning_content`。
- 报错是 API key、模型名、quota、tool schema 或 network 问题。

## Related Artifacts

- Spec: [2026-05-12-journal-harness-core-design.md](../../specs/2026-05-12-journal-harness-core-design.md)
- Plan: [2026-05-12-journal-harness-core-implementation-plan.md](../../plans/2026-05-12-journal-harness-core-implementation-plan.md)
- Archive: [2026-05-12-journal-harness-core-archives.md](../../archives/2026-05/2026-05-12-journal-harness-core-archives.md)
- Related Problems:
  - [2026-05-13-harness-submit-not-wired-audit-empty-problem.md](./2026-05-13-harness-submit-not-wired-audit-empty-problem.md)
- References:
  - [DeepSeek Thinking Mode](https://api-docs.deepseek.com/guides/thinking_mode)
  - [Microsoft Extensions AI TextReasoningContent](https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.textreasoningcontent)
- Code or Test:
  - [OpenAiCompatibleAgentRuntime.cs](../../../../src/Journal.Infrastructure/Ai/OpenAiCompatibleAgentRuntime.cs)
  - [OpenAiCompatibleAgentRuntimeTests.cs](../../../../tests/Journal.Tests/OpenAiCompatibleAgentRuntimeTests.cs)
