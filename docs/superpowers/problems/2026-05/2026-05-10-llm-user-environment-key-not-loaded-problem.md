# Windows User Environment LLM Key Not Loaded

- Date: `2026-05-10`
- Topic slug: `llm-user-environment-key-not-loaded`
- Status: `Captured`
- Scope: `Environment`
- Tags: `llm`, `settings`, `environment`, `windows`, `api-key`

## Symptom

用户已经在本机配置了 `DEEPSEEK_API_KEY`，但 LLM 设置页没有自动加载：DeepSeek 显示为 active，却显示没有 API key，真实模型不可用。

运行态证据表现为：

- `GET /settings/ai` 返回 `activeProviderId: deepseek`。
- DeepSeek provider 的 `hasApiKey` 是 `false`，`source` 仍是 `file`。
- PowerShell 里读取 User 级环境变量能看到 `DEEPSEEK_API_KEY` 已设置，但当前进程环境变量为空。

## Trigger / Context

- Windows 桌面开发环境。
- 用户把 key 配在 Windows User 级环境变量里，而不是当前 PowerShell/Codex 进程环境里。
- API 进程可能早于环境变量配置启动，或者由一个没有继承新 User 环境变量的父进程启动。
- LLM 设置服务只读进程环境时，这类 User/Machine 级配置不会被发现。

## Root Cause

`SystemJournalAiEnvironment.Get()` 使用 `Environment.GetEnvironmentVariable(name)`，这只读取当前进程环境变量。Windows User/Machine 级环境变量存储在系统环境配置中，已启动的父进程不会自动把这些值刷新进自己的 process environment。

结果是：用户明明在系统里配置了 `DEEPSEEK_API_KEY`，但从当前 API 进程视角看该变量不存在，`JournalAiSettingsService` 只能回退到配置文件视图，最终显示 DeepSeek 没有 key。

## Fix

- 将 `SystemJournalAiEnvironment` 改为优先读取 Process 环境变量。
- Windows 下如果 Process 为空，再读取 User 环境变量。
- User 为空时继续读取 Machine 环境变量。
- 保持配置文件为最后兜底来源，环境变量来源的 key 仍不可通过 UI reveal。
- 增加回归测试：
  - 进程变量缺失时能读取 User 级变量。
  - Process 与 User 同时存在时 Process 优先。

## Why This Fix

这个修复保留了显式进程环境变量的最高优先级，适合临时调试和 CI；同时支持用户在 Windows 系统环境变量面板里配置 key 后，应用主动读取 User/Machine 级配置，不要求重启 Codex、终端或父进程。

相比在启动脚本里手工把 User 环境变量复制到 Process，这个修复更接近产品语义：LLM 配置层自己负责解析“全局环境变量”，不会把环境加载规则散落到不同启动入口。

## Recognition Clues

- `/settings/ai` 中目标 provider active，但 `hasApiKey=false`。
- `[Environment]::GetEnvironmentVariable("DEEPSEEK_API_KEY", "User")` 有值，但 `[Environment]::GetEnvironmentVariable("DEEPSEEK_API_KEY", "Process")` 或 `$env:DEEPSEEK_API_KEY` 为空。
- API 进程 `StartTime` 早于环境变量配置，或由长期运行的父进程启动。
- 重新从同一个旧终端启动 API 仍看不到 key，但新开系统终端或重启父进程后可能恢复。

## Applicability / Non-Applicability

### Applies When

- Windows 本地桌面应用需要读取用户级或机器级环境变量作为配置来源。
- 环境变量用于 LLM API key、provider、model 或 base URL 等本地配置。
- 用户确认 key 已配置在系统环境变量中，但应用运行态安全视图仍显示未配置。

### Does Not Apply When

- 环境变量名称写错，例如把 DeepSeek key 写成未支持的变量名。
- `/settings/ai` 已显示 `source: environment` 且 `hasApiKey=true`，但连接测试失败；那属于 provider URL、模型、key 权限或网络问题。
- 非 Windows 平台；当前修复只在 Windows 下额外读取 User/Machine，非 Windows 仍使用进程环境。
- 文件配置中的 key 没有 reveal；环境变量 key 按设计不可 reveal，不是未加载。

## Related Artifacts

- Spec: [2026-05-10-ai-provider-integration-design.md](../../specs/2026-05-10-ai-provider-integration-design.md)
- Plan: [2026-05-10-ai-provider-integration-implementation-plan.md](../../plans/2026-05-10-ai-provider-integration-implementation-plan.md)
- Archive: [2026-05-10-ai-provider-integration-archives.md](../../archives/2026-05/2026-05-10-ai-provider-integration-archives.md)
- Related Problems:
  - [2026-05-10-llm-prompt-field-scope-too-narrow-problem.md](./2026-05-10-llm-prompt-field-scope-too-narrow-problem.md)
- Code or Test:
  - [JournalAiEnvironment.cs](../../../../src/Journal.Infrastructure/Ai/JournalAiEnvironment.cs)
  - [JournalAiSettingsService.cs](../../../../src/Journal.Infrastructure/Ai/JournalAiSettingsService.cs)
  - [JournalAiSettingsTests.cs](../../../../tests/Journal.Tests/JournalAiSettingsTests.cs)
