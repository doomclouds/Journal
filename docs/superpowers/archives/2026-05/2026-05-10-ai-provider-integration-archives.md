# Phase 5 LLM Provider Integration

- Date: `2026-05-10`
- Topic slug: `ai-provider-integration`
- Status: `Archived`
- Scope: `Feature`
- Tags: `phase-5`, `llm`, `ai-provider`, `openai-compatible`, `desktop`

## Summary

阶段 5 把 Journal 从纯 Mock 整理推进到真实可配置 LLM：后端保留 `AI Provider` 技术抽象和 JMF 校验边界，真实模型统一走 OpenAI-compatible runtime，前端提供 `LLM` 状态入口和配置面板；未配置时仍默认 Mock，真实 LLM 失败时进入 attention，不静默回退、不写正式 entry。

## Delivered Scope

- 新增异步 AI Provider 合同、动态 provider/model/prompt metadata、settings store、环境变量覆盖、默认 LLM 预设，以及 OpenAI-compatible Agent Framework runtime。
- `TodayJournalService` 改为通过 generation service 选择 active LLM，AI 输出仍只生成 `JournalAiJson`，并经过 validator、服务端 `raw-inputs` 覆盖保护和 JMF renderer 后进入 reviewing/attention draft。
- API 新增 `GET/PUT /settings/ai`、`POST /settings/ai/test`、`POST /journal/today/draft/regenerate`，并补齐 malformed body、unknown active provider、safe view 和 regenerate 不写 entry 的回归。
- 桌面端新增 `LlmSettingsPanel`、当时的顶部 `LLM <provider>` 状态入口、保存/测试已保存配置/重新整理草稿流程，并处理 settings save 竞态、连接测试竞态、masked API Key 保留和 settings refresh 失败不阻断 editor 更新。后续 command-surface 优化已将配置入口收敛到 Electron 原生菜单，顶部重复入口不再保留。
- Prompt 固定 faithful 风格，只整理 `yesterdayReview`、`todayFocus`、`inspiration` 三项；九宫格等 JMF 可选块暂不纳入本轮模型输出合同。

## Out of Scope

- 不包含多 Agent workflow、streaming UI、模型列表拉取、token 价格统计、账单报表、API Key 加密、版本快照、SQLite 索引或 Electron 托管后端进程。
- 不包含语音转写、RAG、向量库、对话式改写、按反馈多轮重写。
- 不包含让 AI 直接写 Markdown、直接写正式 entry，或自动整理九宫格/JMF 可选块。

## Verification Snapshot

- `dotnet test Journal.slnx`：123/123 .NET tests passed。
- `npm test --prefix apps/desktop`：51/51 frontend tests passed。
- `npm run build --prefix apps/desktop`：TypeScript + Vite build passed。
- No-key API smoke：临时 API `http://127.0.0.1:5097` 下 `GET /settings/ai` 返回 `activeProviderId=mock`、5 个 providers，且没有明文 `apiKey` 属性。
- Secret/template scan：`%LocalAppData%/Journal` 未扫到 `apiKey`、`api_key`、`Authorization`、`raw_response`、`request_id` 等敏感字段；原型和计划未发现 `visual-spec`、`Will Specs`、`TODO`、`TBD` 残留。
- Code review checkpoints：Task 5/6/7 均经过 spec compliance 和 code quality review；修正了 malformed regenerate body 500、无效 active provider 写坏配置、masked API Key 被空串覆盖、连接测试 provider 切换竞态、settings save 旧响应覆盖、settings refresh failure 阻断 editor 等问题。
- Final review fixes：补上真实 LLM 不得改写 `raw-inputs`、未知 `JOURNAL_AI_PROVIDER` 不再静默 fallback、用户可见术语统一到 `LLM`、测试连接 reject 在面板内安全展示失败结果。

## Source Documents

- Spec: [2026-05-10-ai-provider-integration-design.md](../../specs/2026-05-10-ai-provider-integration-design.md)
- Visual: [2026-05-10-ai-provider-integration-prototype.html](../../specs/2026-05-10-ai-provider-integration-prototype.html)
- Plan: [2026-05-10-ai-provider-integration-implementation-plan.md](../../plans/2026-05-10-ai-provider-integration-implementation-plan.md)

## Related Problems

- None.

## Notes

- 用户界面统一使用 `LLM` 术语；后端和 API 保留 `AI Provider` 作为既有技术抽象。
- `测试已保存配置` 明确只验证已保存 LLM 配置，不验证当前未保存表单草稿，避免 UI 语义误导。
