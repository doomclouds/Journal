# Journal LLM 设置体验优化设计

> 日期：2026-05-10
> 状态：待实施计划
> 关联设计：[2026-05-10-ai-provider-integration-design.md](./2026-05-10-ai-provider-integration-design.md)
> 视觉原型：[2026-05-10-llm-settings-ux-polish-prototype.html](./2026-05-10-llm-settings-ux-polish-prototype.html)
> 修订范围：替换原 AI Provider 设计中第 8-10 节的配置界面、连接测试、重新整理草稿交互合同。

## 1. 背景

阶段 5 已经接入真实 OpenAI-compatible LLM Provider，并保留 Mock 作为未配置或失败时的本地备用。当前基础功能可用，但 LLM 参数配置界面仍偏工程调试面板：

- API Key 已配置时，输入框为空，用户只能从 `key ready` 推断状态。
- Provider 状态使用 `active`、`preset`、`key ready`、`no key` 等内部词，不能直接回答“现在能不能用”。
- “测试已保存配置”和“启用当前 LLM”容易让用户在编辑未保存表单时误判测试对象。
- 高级参数、连接测试、重新整理草稿混在同一个设置面板里，配置操作和内容改写操作边界不清。
- 失败信息有技术详情，但缺少清晰的“未保存、未启用、下一步怎么修”的产品反馈。

本设计只优化 LLM 设置体验，不扩张 AI 生成能力本身。目标是让用户明确知道：当前用的是谁、Key 从哪里来、改了什么、测试了什么、什么时候才真正启用。

## 2. 目标

LLM 设置页需要达到商用级可用产品的基础体验：

1. API Key 状态可理解、可操作、默认安全。
2. Provider 列表直接显示可用状态，不暴露内部状态词。
3. 编辑表单后，连接测试必须测试当前表单，而不是悄悄测试旧配置。
4. “保存并启用”必须先测试候选配置；测试成功才保存并启用，失败时不污染当前可用配置。
5. 高级参数默认折叠，但露出摘要，让用户知道当前关键参数。
6. 失败诊断技术详情优先，但必须同时说明配置未保存、未启用，以及下一步修复路径。
7. 重新整理今日草稿回到今日页执行，设置页只提供入口提示，不直接覆盖日记草稿。

## 3. 非目标

本轮不做：

- 新增 Provider 种类。
- 模型列表在线拉取。
- API Key 加密存储。
- 多用户、云同步或权限模型。
- 版本快照、差异对比或草稿历史。
- 重新设计今日工作台整体信息架构。
- 改动 LLM prompt、JMF validator 或正式日记写入规则。

API Key 仍遵循既有产品决策：环境变量优先，配置文件次之；配置文件不加密，但 Key 不在界面、日志、响应、错误详情中扩散。

## 4. 信息架构

LLM 设置入口仍在今日工作台顶部状态区，点击当前 LLM 状态打开设置面板。面板采用三列结构：

```text
左侧 Provider 列表
  -> 中间连接配置
  -> 右侧诊断与下一步
```

三列职责：

- 左侧只回答“有哪些 Provider、谁已启用、谁可用、谁需要配置”。
- 中间只编辑连接配置和高级参数。
- 右侧只展示测试结果、失败诊断和后续操作建议。

设置面板不直接承载“重新整理今日草稿”的执行动作。配置成功后，右侧显示“可以回到今日页重新整理”的提示；真正覆盖 reviewing draft 的确认动作发生在今日页主工作流中。

## 5. Provider 状态语言

Provider 卡片使用产品化状态，不直接展示内部字段名。

| 状态 | 含义 | 示例显示 |
| --- | --- | --- |
| `已启用` | 当前真实生成会使用该 Provider | `已启用 · 环境变量 · gpt-5.4` |
| `已配置` | 有 Key 或无需 Key，但当前未启用 | `已配置 · 本机配置文件` |
| `需要配置` | 缺少启用所需信息 | `需要配置 · 未填写 API Key` |
| `测试失败` | 最近一次候选配置测试失败 | `测试失败 · 401 unauthorized` |
| `备用` | Mock 本地备用，不依赖网络 | `备用 · 无需 API Key` |

来源显示规则：

- `environment` 显示为“环境变量”。
- `file` 显示为“本机配置文件”。
- `default` / `preset` 显示为“默认预设”。
- `mock` 显示为“本地备用”。

## 6. API Key 交互

API Key 分来源处理。

### 6.1 环境变量来源

环境变量 Key 不在界面显示，也不提供眼睛查看按钮。

显示文案：

```text
已从环境变量加载，不在界面显示
```

辅助说明：

```text
环境变量来源不会回写配置文件。替换 Key 后需要重启后端或重新加载环境变量。
```

### 6.2 配置文件来源

配置文件 Key 默认按密码方式遮罩：

```text
sk-••••••••••••••••••••••••4A7C
```

交互规则：

- 默认隐藏。
- 右侧显示眼睛按钮，可临时查看完整值。
- 切换 Provider、关闭面板、保存成功、测试失败后自动恢复隐藏。
- 允许“替换 Key”，输入新 Key 后进入未保存状态。
- 不在 Provider 卡片、测试结果、错误详情、日志中显示完整 Key。

### 6.3 未配置

未配置时输入框为空，文案明确：

```text
未填写 API Key
```

Mock Provider 显示：

```text
无需 API Key
```

## 7. 未保存状态与表单测试

用户修改以下字段后，当前表单进入未保存状态：

- display name
- model
- base URL
- API Key
- timeout
- temperature
- max tokens
- style preset

未保存状态规则：

- 清空或标记上一条测试结果为“已过期”。
- 主按钮显示“保存并启用”。
- 次按钮显示“测试当前表单”。
- 不再把“测试已保存配置”放在主操作位置。

`测试当前表单` 测试的是候选配置，不写入配置文件，不改变 active Provider。

`测试已保存配置` 仍可保留为次级诊断动作，只在没有未保存修改，或在诊断菜单中使用。

## 8. 保存并启用合同

“保存并启用”不是单纯保存表单。它是一个受保护的激活动作：

```text
候选配置
  -> 服务端合并当前已保存密钥和环境变量覆盖
  -> 最小 JSON 连接测试
  -> 成功：保存配置文件并切换 active Provider
  -> 失败：不保存、不启用、保留用户输入
```

成功反馈：

- 显示“连接测试通过”。
- 显示已启用 Provider 和模型。
- 刷新 Provider 列表状态。
- 右侧提示“可以回到今日页重新整理”。

失败反馈：

- 显示“测试失败，配置没有保存，当前仍使用 <原 Provider>”。
- 保留用户输入，方便修正后重试。
- Provider 卡片标记“测试失败”。
- 不覆盖已保存可用配置。
- 不自动回退写入 Mock；Mock 只作为运行时备用和显式选择项存在。

Mock Provider 的保存并启用可以走同一流程，测试结果固定为成功，或通过现有 Mock health path 返回成功。

## 9. 服务端 API 调整

保留现有 API：

```text
GET  /settings/ai
PUT  /settings/ai
POST /settings/ai/test
POST /journal/today/draft/regenerate
```

新增受保护激活 API：

```text
POST /settings/ai/activate
```

请求体复用 `AiSettingsSaveRequest`。

响应体：

```text
{
  saved: boolean,
  settings: AiSettingsView,
  testResult: AiProviderHealthResult
}
```

语义：

- 请求体先按现有保存规则校验。
- API Key 为空时，按现有规则继承当前配置文件中的 Key。
- 环境变量仍优先覆盖对应 Provider。
- 只测试候选 active Provider。
- 测试成功才写入配置文件并返回 `saved: true`。
- Provider 测试失败返回 `saved: false` 和结构化 `testResult`，HTTP 状态仍可为 `200`，因为请求本身处理成功。
- 请求格式错误、Provider 不存在、参数非法使用 `400`。

扩展 `POST /settings/ai/test`：

- 继续支持 `{ providerId }` 测试已保存配置。
- 增加可选候选配置测试，用于“测试当前表单”：

```text
{
  providerId: "openai",
  candidate: AiSettingsSaveRequest
}
```

带 `candidate` 时不保存、不启用，只返回测试结果。

## 10. 技术诊断展示

用户已选择“技术详情优先”。因此失败诊断第一层展示可定位信息：

- HTTP status。
- Provider。
- Model。
- Base URL。
- 错误分类 code。
- 测试时间。
- 请求阶段 stage。

同时必须保留产品解释：

```text
测试失败，所以配置没有保存，也没有启用。
当前仍使用：<原 Provider>
```

检查项按顺序展示：

1. Base URL 格式。
2. OpenAI-compatible endpoint 可访问。
3. API Key 授权。
4. model 是否存在。
5. JSON object 输出能力。
6. 返回 JSON 可解析。

修复建议根据错误分类生成：

- `unauthorized`：检查 Key 是否来自对应供应商，环境变量是否重启生效。
- `forbidden`：检查账号权限、额度、模型访问权限。
- `model_not_found`：检查模型名或切换默认模型。
- `rate_limited`：稍后重试或降低调用频率。
- `timeout`：检查网络、base URL、timeout。
- `invalid_json`：检查 JSON 模式或模型兼容性。
- `provider_error`：展示供应商错误摘要。

原始响应、request id、provider error code 放在可展开区域，并提供复制诊断按钮。复制内容必须经过安全清洗，不包含 API Key、Authorization header 或完整原始 prompt。

## 11. 高级参数

高级参数默认折叠，但摘要必须一直可见：

```text
高级参数：temperature 0.2 · max tokens 1800 · timeout 60s · JSON 模式开启
```

折叠区包含：

- JSON 模式：首版固定 `json_object`。
- timeout seconds。
- temperature。
- max tokens。
- style preset。

用户修改高级参数后同样进入未保存状态，并使旧测试结果过期。

## 12. 重新整理今日草稿

设置页不直接执行重新整理草稿。

配置测试并启用成功后，右侧显示下一步提示：

```text
可以回到今日页重新整理。
设置页不会直接覆盖草稿。真正改写草稿前，今日页会再次确认。
```

今日页负责执行：

```text
today raw inputs
  -> active LLM
  -> JournalAiJson
  -> JMF Markdown
  -> reviewing draft
```

今日页的重新整理交互需要保留二次确认：

```text
这会覆盖当前草稿内容，但不会影响正式日记。
```

如果当前 draft 有用户手动编辑痕迹，后续实现可以加强为差异预览，但本轮不要求。

## 13. 前端组件影响

`apps/desktop/src/LlmSettingsPanel.tsx` 应拆出更清晰的局部状态：

- `selectedProviderId`
- `draftProviders`
- `dirtyProviderIds`
- `revealedKeyProviderId`
- `testResult`
- `testResultIsStale`
- `activationResult`
- `advancedExpandedProviderIds`

可拆分子组件：

- `ProviderStatusCard`
- `ApiKeyField`
- `AdvancedSettingsSummary`
- `ConnectionDiagnosticsPanel`
- `ActivationNotice`

UI 行为重点：

- Provider 切换时重置 Key reveal。
- 编辑字段时标记 dirty 并让测试结果过期。
- 保存并启用时禁用相关按钮，显示“正在测试并启用”。
- 失败时保留输入，不关闭面板。
- 成功时刷新 settings，并清除 dirty 状态。

## 14. 验收标准

后端：

- `GET /settings/ai` 仍不返回 `apiKey`。
- 环境变量来源优先于配置文件。
- `POST /settings/ai/test` 能测试已保存配置。
- `POST /settings/ai/test` 带 candidate 时能测试当前表单且不写文件。
- `POST /settings/ai/activate` 测试成功才写配置文件和 active Provider。
- `POST /settings/ai/activate` 测试失败不改变已保存配置。
- Mock 激活不依赖网络且返回成功。

前端：

- 已配置 Key 不再表现为空白输入框。
- 配置文件 Key 默认遮罩，并可通过眼睛临时查看。
- 环境变量 Key 显示“已从环境变量加载”，不可查看，不回写。
- Provider 卡片使用产品化中文状态。
- 编辑表单后旧测试结果显示为过期或被清空。
- “测试当前表单”不保存配置。
- “保存并启用”失败后不关闭面板、不切换 active Provider。
- “保存并启用”成功后刷新状态并提示回今日页重新整理。
- 设置页不再直接显示“重新整理草稿”执行按钮。

测试：

- 后端补充 `activate` 成功、失败不写文件、candidate test 不写文件、Key 不泄露测试。
- 前端补充 API Key 遮罩、眼睛切换、环境变量不可查看、dirty 测试过期、保存并启用成功/失败、重生成入口移出设置页测试。

## 15. 与旧设计的关系

本 spec 修订并替换原 AI Provider 设计中的以下旧合同：

- 旧合同：“连接测试测试的是已保存 LLM 配置，用户需要先保存再测试。”
- 新合同：“测试当前表单不保存；保存并启用会先测试候选配置，成功才保存。”

- 旧合同：“设置页包含重新整理今日草稿按钮。”
- 新合同：“设置页只提示可回今日页重新整理，内容改写动作回到今日页确认。”

- 旧合同：“右侧包含高级配置和测试结果。”
- 新合同：“高级配置折叠摘要在中间配置区，右侧专注诊断与下一步。”

后续实现计划以本 spec 为准。
