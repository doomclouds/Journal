# Journal 阶段 5：真实 AI Provider 接入设计

> 日期：2026-05-10
> 状态：待用户复核
> 对应愿景：`PROJECT_VISION.md` 阶段 5：真实 AI Provider
> 上一阶段：[阶段 3：JMF 编辑模式与结构校验](./2026-05-09-phase-3-jmf-editor-design.md)
> 界面原型：[2026-05-10-ai-provider-integration-prototype.html](./2026-05-10-ai-provider-integration-prototype.html)

## 1. 背景

阶段 2 已经打通“自然语言输入 -> Mock AI JSON -> JMF Markdown 草稿 -> 用户确认 -> 正式 Markdown 文件”。阶段 3 又补上 JMF 块编辑、源码编辑和结构校验。现在主链路已经能保护原始表达、保护 JMF 结构，也能保证正式日记只在用户确认后写入。

下一步要把 `MockAiProvider` 替换为真实可配置模型，让 Journal 能用 OpenAI-compatible 大模型完成“自然表达 -> 结构化日记 JSON”的整理任务。真实模型接入不能改变已有产品原则：模型不能直接写 Markdown，不能绕过 JSON 校验，不能在失败时污染正式 entry，也不能把 API Key、base URL 或调试响应扩散到日记文件里。

本阶段采用 **OpenAI-compatible Runtime + Microsoft Agent Framework 1.5.0 轻量调用**。Provider 配置支持 `Mock`、`OpenAI`、`DeepSeek`、`智谱 GLM`、`Custom OpenAI-Compatible`。真实 Provider 走统一 OpenAI-compatible 调用路径；Mock 继续作为未配置时的默认能力和测试能力。

## 2. 目标

阶段 5 目标是打通：

```text
raw inputs
  -> active AI provider
  -> faithful prompt
  -> OpenAI-compatible chat completion
  -> JSON object response
  -> JournalAiJson
  -> JournalAiJsonValidator
  -> JMF Markdown renderer
  -> reviewing draft
  -> user confirmation
  -> formal Markdown entry
```

完成后，用户应该可以：

- 不配置任何 Key 时继续使用 Mock 生成草稿。
- 通过顶部 `AI` 状态入口查看当前 Provider。
- 配置并启用 `OpenAI`、`DeepSeek`、`智谱 GLM` 或自定义 OpenAI-compatible Provider。
- 使用环境变量覆盖本机配置。
- 手填模型名，不依赖 Provider 一定支持 `/models`。
- 点击“测试连接”发送最小 JSON 请求验证 base URL、API Key、model 和 JSON 输出能力。
- 点击“重新整理草稿”用当前 Provider 重新生成当天 draft。
- 在真实 Provider 失败时看到 `attention` 状态、友好摘要和可展开安全技术详情。

## 3. 非目标

本阶段明确不做：

- 多 Agent 协作或 Agent workflow 编排。
- Streaming UI。
- Embedding、索引、向量库和检索增强。
- 语音转写和语音模型调用。
- AI 对话式改写、按反馈多轮重写。
- 自动模型列表强依赖。
- 价格统计、token 成本估算和账单报表。
- API Key 加密存储强制要求。
- 版本快照。
- 生产期 Electron 托管 .NET 后端进程。

本阶段只让真实模型替换 Mock 生成 `JournalAiJson`，并把配置、健康检查、失败状态和重新整理草稿的第一条闭环做稳。

## 4. 已确认取舍

### 4.1 使用 OpenAI-compatible 单运行时

所有真实 Provider 统一走 OpenAI-compatible chat completion 调用。`OpenAI`、`DeepSeek`、`智谱 GLM` 是预设配置，`Custom OpenAI-Compatible` 是用户自定义配置。这样可以避免一开始为每家 Provider 写重复 adapter，也方便后续接入 Ollama、OpenRouter、硅基流动或本地代理。

### 4.2 轻量使用 Microsoft Agent Framework

使用 `Microsoft.Agents.AI`、`Microsoft.Agents.AI.OpenAI` 和 OpenAI .NET SDK 作为调用抽象和未来扩展基础。Journal 的业务流程仍由 `TodayJournalService` 和应用服务控制。

不把“生成、校验、修复、渲染”交给 Agent 自主规划。这个流程是确定业务流，不需要模型或 workflow 接管。Agent Framework 在首版只是 Provider runtime 的技术底座。

### 4.3 Mock 是未配置默认，不是真实失败的静默兜底

未配置真实 Provider 时，系统默认使用 `MockAiProvider`，保证本地演示、自动测试和无 Key 使用不被卡住。

一旦用户启用真实 Provider，真实调用失败必须进入 `attention`，不能静默回退 Mock。静默回退会让用户误以为真实模型生成成功，破坏长期日记的来源可信度。

### 4.4 统一 `json_object`

首版统一使用：

```json
{ "type": "json_object" }
```

Prompt 明确要求只输出 `JournalAiJson` JSON。后端继续使用 `JournalAiJsonValidator` 做强校验。OpenAI 官方模型未来可以通过能力开关升级 `json_schema`，但本阶段不引入分支。

### 4.5 默认 faithful 风格

Prompt 首版使用 `faithful` 风格：保留原话优先，只做分块、提取重点和轻度整理，不虚构事实，不把晨间日记写成鸡汤总结或工作报告。内部保留 `stylePreset` 扩展点，但 UI 首版不暴露复杂风格编辑。

## 5. 配置模型

### 5.1 配置来源优先级

配置读取优先级：

```text
1. JOURNAL_AI_PROVIDER
   JOURNAL_AI_BASE_URL
   JOURNAL_AI_MODEL
   JOURNAL_AI_API_KEY

2. 常见 Provider Key
   OPENAI_API_KEY
   DEEPSEEK_API_KEY
   ZHIPU_API_KEY

3. %LocalAppData%/Journal/.journal/settings/ai-providers.json

4. Mock
```

环境变量优先于配置文件。环境变量来源在 UI 中显示为“来自环境变量”，但不回写配置文件。配置文件第一版允许明文 API Key，原因是这是本机个人工具，当前更重要的是简单、可检查、可迁移。安全边界放在“Key 不扩散”。

### 5.2 配置文件位置

配置文件位置：

```text
%LocalAppData%/Journal/.journal/settings/ai-providers.json
```

配置文件存完整 Provider 列表，包括预设 Provider。这样用户可以修改默认模型名、display name、启用状态和自定义 base URL，文件结构也更直观。

示例：

```json
{
  "activeProvider": "mock",
  "providers": [
    {
      "id": "mock",
      "type": "mock",
      "displayName": "Mock",
      "model": "mock-journal",
      "isEnabled": true
    },
    {
      "id": "deepseek",
      "type": "openai-compatible",
      "displayName": "DeepSeek",
      "preset": "deepseek",
      "baseUrl": "https://api.deepseek.com",
      "model": "deepseek-v4-flash",
      "apiKey": "",
      "responseFormat": "json_object",
      "timeoutSeconds": 45,
      "temperature": 0.2,
      "maxTokens": 1200,
      "stylePreset": "faithful"
    }
  ]
}
```

### 5.3 预设 Provider

首版预设：

| Provider | 类型 | 默认模型 | 默认 base URL |
| --- | --- | --- | --- |
| Mock | `mock` | `mock-journal` | 无 |
| OpenAI / ChatGPT | `openai-compatible` | `gpt-5.4` | OpenAI 官方 endpoint |
| DeepSeek | `openai-compatible` | `deepseek-v4-flash` | `https://api.deepseek.com` |
| 智谱 GLM | `openai-compatible` | `glm-5.1` | 智谱 OpenAI-compatible endpoint |
| Custom | `openai-compatible` | 用户手填 | 用户手填 |

2026-05-10 默认模型校准：

- OpenAI / ChatGPT 默认使用 `gpt-5.4`，这是本阶段明确选定的目标模型。
- DeepSeek 默认使用 `deepseek-v4-flash`；旧的 `deepseek-chat` / `deepseek-reasoner` 已被官方标记为 deprecated，并计划在 2026-07-24 停止使用。
- 智谱 GLM 默认使用 `glm-5.1`。
- 参考资料：[DeepSeek API docs](https://api-docs.deepseek.com/)、[DeepSeek updates](https://api-docs.deepseek.com/zh-cn/updates)、[智谱 GLM-5.1](https://docs.bigmodel.cn/cn/guide/models/text/glm-5.1)，检索日期为 2026-05-10；OpenAI / ChatGPT 默认模型按本次产品决策指定为 `gpt-5.4`。

模型名首版以“默认值 + 手填”为主，不强依赖模型列表接口。Provider 支持 `/models` 时，后续可以增加模型列表拉取，但不是本阶段验收条件。

## 6. 安全边界

API Key 可以存在于环境变量或本机配置文件，但不能出现在：

```text
entries/yyyy/MM/yyyy-MM-dd.md
.journal/drafts/yyyy/MM/yyyy-MM-dd.md
.journal/drafts/yyyy/MM/yyyy-MM-dd.meta.json
错误摘要
连接测试结果
日志
前端状态展示
JMF front matter
```

错误信息必须是安全结构：

```text
summary: "DeepSeek 调用失败，请检查 API Key 或模型名。"
stage: "provider-call"
httpStatus: 401
providerErrorCode: "invalid_api_key"
providerRequestId: "..."
safeResponseSnippet: "..."
localCorrelationId: "..."
```

禁止保存或展示：

```text
apiKey
Authorization header
完整请求 headers
完整请求体
完整 provider response
可能包含 secret 的 exception.ToString()
```

## 7. AI 生成流程

### 7.1 应用服务边界

建议新增 `JournalAiGenerationService`，负责：

- 读取 active Provider。
- 基于当天 raw inputs 构造 faithful prompt。
- 调用 active Provider。
- 解析 JSON。
- 反序列化为 `JournalAiJson`。
- 调用 `JournalAiJsonValidator`。
- 返回成功结果或安全失败结果。

`TodayJournalService` 继续负责：

- 追加 raw input。
- 创建 reviewing draft。
- 创建 attention draft。
- 确认 draft 写 entry。
- 重新整理草稿。

Provider runtime 不能直接写文件。

### 7.2 Provider 接口

现有 `IJournalAiProvider.Generate(...)` 是同步接口，只适合 Mock。真实 Provider 需要异步和失败结构。建议升级为：

```csharp
public interface IJournalAiProvider
{
    string ProviderId { get; }

    Task<JournalAiProviderResult> GenerateAsync(
        JournalAiGenerationRequest request,
        CancellationToken cancellationToken);

    Task<JournalAiProviderHealthResult> CheckAsync(
        JournalAiProviderSettings settings,
        CancellationToken cancellationToken);
}
```

Mock 可以同步实现但暴露异步接口。真实 OpenAI-compatible Provider 通过 Agent Framework / OpenAI SDK 调用模型。

### 7.3 Prompt 约束

Prompt 必须表达：

- 只输出 JSON，不输出 Markdown。
- JSON 必须匹配 `JournalAiJson` 字段。
- `schema` 固定为 `journal-entry/v1`。
- `rawInputs` 必须完整保留用户原始输入。
- `yesterdayReview` 和 `todayFocus` 至少各有一条。
- 不虚构用户没说过的事实。
- 可以轻度整理，但优先保留原话。
- 不写鸡汤、夸张总结或营销文案。

Prompt 版本：

```text
journal-entry-json-v1
```

### 7.4 成功路径

```text
raw inputs
  -> JournalAiGenerationService
  -> active provider
  -> JSON object
  -> JournalAiJson
  -> JournalAiJsonValidator
  -> JmfMarkdownRenderer
  -> reviewing draft
```

JMF front matter 保留：

```yaml
provider: deepseek
model: deepseek-v4-flash
prompt_version: journal-entry-json-v1
generated_at: "2026-05-10T..."
```

不写 `base_url`、`api_key`、`request_id`、`raw_response`。

### 7.5 失败路径

以下任一失败都进入 `attention` draft：

- base URL 无效。
- API Key 缺失或鉴权失败。
- 模型不存在。
- 请求超时。
- 限流或额度不足。
- provider 返回空内容。
- provider 返回非法 JSON。
- JSON 反序列化失败。
- `JournalAiJsonValidator` 不通过。

真实 Provider 失败不自动回退 Mock。UI 可以提供“用 Mock 生成一次”手动兜底。

## 8. 配置界面设计

界面入口放在今日工作台顶部状态区：

```text
[reviewing] [API ok] [AI Mock ▾]
```

点击 `AI Mock ▾` 打开 LLM 配置面板。该面板遵循现有 Phase 2/3 原型的桌面工具气质：日记纸面仍是产品中心，模型配置是低频设置，不抢主工作流。

布局：

- 左侧：Provider 列表。
- 中间：普通配置。
- 右侧：高级配置和测试结果。

Provider 列表显示：

- 当前启用。
- 未配置。
- 来自环境变量。
- 来自配置文件。
- 最近测试成功或失败。

普通配置包含：

- Provider 名称。
- API Key 状态。
- Model 手填框。
- 启用 Provider。
- 测试连接。
- 重新整理今日草稿。

高级配置包含：

- Base URL。
- JSON 输出模式，首版固定 `json_object`。
- Timeout。
- Temperature。
- Max tokens。
- Style preset，首版固定 `faithful`。
- 技术详情和最近测试结果。

## 9. 连接测试

连接测试发送一次最小 JSON 请求：

```text
system: "Return a JSON object only."
user: "Return { \"ok\": true }"
response_format: { "type": "json_object" }
```

测试目标：

- base URL 可访问。
- API Key 可用。
- model 存在。
- Provider 支持 JSON object 输出。
- 返回能解析为 JSON。

UI 必须提示：“测试会向当前 Provider 发送一次最小请求，可能产生少量 token 消耗。”

结果分类：

- `success`
- `unauthorized`
- `forbidden`
- `model_not_found`
- `rate_limited`
- `timeout`
- `invalid_json`
- `provider_error`

## 10. 重新整理草稿

用户配置真实 Provider 后，可以点击“重新整理草稿”。流程：

```text
today raw inputs
  -> active provider
  -> JournalAiJson
  -> JMF Markdown
  -> reviewing draft
```

不会做：

- 不写 formal entry。
- 不改 raw inputs。
- 不创建版本快照。
- 不静默覆盖正式日记。

如果当前 draft 可能包含用户编辑内容，点击前提示：

```text
这会覆盖当前草稿内容，但不会影响正式日记。
```

确认后只覆盖 `.journal/drafts/`。

## 11. API 设计

建议新增配置 API：

```text
GET  /settings/ai
PUT  /settings/ai
POST /settings/ai/test
POST /journal/today/draft/regenerate
```

`GET /settings/ai` 返回安全视图：

- active provider。
- Provider 列表。
- 每个 Provider 的配置来源。
- API Key 是否存在。
- 最近测试状态。
- 当前运行时来源。

不返回 API Key 明文。

`PUT /settings/ai` 保存配置文件。环境变量来源字段不应被回写覆盖。

`POST /settings/ai/test` 执行最小 JSON 健康检查。

`POST /journal/today/draft/regenerate` 使用当前 active Provider 重新生成当天 draft。

## 12. 测试

后端测试覆盖：

- 环境变量优先于配置文件。
- 常见 Provider Key 能兜底。
- 未配置时使用 Mock。
- 环境变量来源不回写配置文件。
- OpenAI-compatible 请求包含 model、messages、response_format。
- API Key 不出现在错误对象。
- timeout、401、429、invalid JSON 映射为安全失败。
- `json_object` 返回能反序列化为 `JournalAiJson`。
- 未配置时 `AddInputAsync` 仍使用 Mock 生成 reviewing draft。
- 真实 Provider 成功时生成 reviewing draft。
- 真实 Provider 失败时生成 attention draft，不写 entry。
- 重新整理草稿只覆盖 draft，不写 entry。

前端测试覆盖：

- 顶部状态显示当前 AI Provider。
- 点击 AI 状态打开配置面板。
- 五个 Provider 入口可见。
- 环境变量来源显示为只读或“已加载”。
- 手填 model、base URL、API Key 后可保存。
- 测试连接显示成功或失败。
- 重新整理草稿有覆盖提示。
- attention 显示摘要，可展开技术详情。

默认自动测试不调用真实 Provider。真实 API 只作为手工 smoke test，避免测试依赖外网、Key 和 token 消耗。

验收命令：

```powershell
dotnet test Journal.slnx
npm test --prefix apps/desktop
npm run build --prefix apps/desktop
```

## 13. 完成标准

阶段 5 完成时必须满足：

- `Mock`、`OpenAI`、`DeepSeek`、`智谱 GLM`、`Custom OpenAI-Compatible` 五个 Provider 入口可见。
- 未配置时默认 Mock。
- 真实 Provider 可通过环境变量或配置文件启用。
- 配置页能保存本机配置文件。
- 配置页能测试连接并展示安全结果。
- `AddInputAsync` 能使用 active Provider 生成 draft。
- 真实 Provider 失败进入 attention，不自动回退 Mock。
- 用户可手动用 Mock 生成一次。
- 用户可用当前 active Provider 重新整理今日草稿。
- AI 输出只进入 `JournalAiJson`，不能直接写 Markdown。
- JMF renderer 写入 provider、model、prompt_version、generated_at。
- API Key 不出现在 Markdown、draft meta、日志、错误详情和前端状态中。
- 后端测试、前端测试和前端构建通过。

## 14. 后续扩展

后续可以继续做：

- OpenAI 官方 Provider 的 `json_schema` 严格模式。
- 模型列表拉取。
- Provider token 成本统计。
- 重试策略和重试按钮。
- 按用户反馈重新生成。
- Prompt style preset UI。
- 本机 Key 加密或 Windows Credential Manager。
- Ollama、本地模型、OpenRouter 等更多 OpenAI-compatible 预设。
- 真实 AI 生成后的版本快照和来源审计。
