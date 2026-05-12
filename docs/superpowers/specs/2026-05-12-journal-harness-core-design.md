# Journal LLM Harness Core 设计

> 日期：2026-05-12
> 状态：待审阅
> 对应方向：规范化大模型操作日记文件的能力边界
> 最终原型：[审计工作台推荐原型](./2026-05-12-journal-harness-audit-workbench-prototype.html)

## 1. 背景

Journal 当前 Phase 5 已经打通：

```text
自然语言输入
  -> Raw input 持久化
  -> Mock 或真实 LLM 输出 JournalAiJson
  -> 服务端校验并渲染 JMF Markdown 草稿
  -> 块编辑 / 确认
  -> 正式 Markdown entry
```

这条链路已经比“模型直接写 Markdown”安全，但仍然有一个核心问题：模型仍然倾向于生成一份完整整理结果。用户新增内容、用户手工编辑内容、AI 旧整理内容之间缺少清晰边界，模型也缺少内置工具来做局部、可审计的操作。

下一阶段的目标不是让模型更自由地写日记，而是把模型收束成受控协作者：模型可以理解历史材料、当前用户输入、当前 draft 和正式 entry，但只能通过系统提供的工具对 draft 做有限操作。正式 entry 仍然只由用户确认写入。

## 2. 目标

Harness Core 要解决五件事：

1. **用户主权**：用户原始表达和用户编辑内容不能被模型吞掉。
2. **工具边界**：模型不直接输出整篇 Markdown，也不直接写正式 entry，只能调用允许的语义工具。
3. **增量协作**：模型围绕当前输入和当前草稿做追加、创建、受限改写，而不是反复重生成整篇草稿。
4. **来源可辨识**：JMF section 带来源标记，让系统和模型区分用户生成、AI 生成、用户编辑和 AI 追加。
5. **审计可追溯**：每次 harness run 的工具调用、拒绝原因、验证结果和 provenance 可以按日期查看。

## 3. 非目标

本设计不做：

- 删除、清空、隐藏或物理移除日记内容。
- 允许 AI 直接写 `entries/` 下正式 Markdown。
- 自由 Markdown patch。
- 多轮聊天式改写 UI。
- 版本快照和差异回滚。
- SQLite 索引、搜索和多日期日记浏览。
- 完整 Agent workflow 记忆系统。
- 让 LLM 设置页触发整理动作。
- 在普通日记阅读界面展示 provenance 标签。

日期审计可以选择日期查看 harness run，但这不等于实现多日期日记浏览。多日期浏览是另一个产品能力。

## 4. 核心方案

采用 **Harness Core**：

```text
Context Builder
  -> LLM Planner
  -> Semantic Tool Calls
  -> JMF Operation Executor
  -> Validation + Provenance + Audit
  -> reviewing / attention draft
  -> user confirmation
  -> formal entry
```

### 4.1 Context Builder

构建模型可见上下文，但要分层：

- 历史 raw inputs：只读历史材料。
- 当前 draft：当前可编辑草稿。
- confirmed entry：用户已确认的正式日记，可作为上下文读取。
- section provenance：每个 section 的来源、最后修改者和最近操作。
- available tools：本次允许调用的工具和参数合同。
- 当前用户输入：作为本轮 `user message`。

模型可以读取 confirmed entry，但所有结果只能写入 draft。

### 4.2 Prompt Context Split

Prompt 组装必须区分历史材料和本次用户意图：

```text
system / developer:
  Journal Harness 工作流、禁止操作、安全边界、工具列表

protected context:
  historicalRawInputs
  currentDraft
  confirmedEntry
  sectionProvenance
  availableOperations

user:
  当前这一次用户输入
```

这样设计的原因：

- 历史 raw inputs 是背景，不是本轮命令。
- 当前输入既可能是日记内容，也可能包含本轮操作意图，例如“这段放到灵感”“这段只是备注，别整理进正文”。
- 模型更容易围绕当前 user message 规划工具调用，避免把全量 raw inputs 重新洗牌。

技术实现不强制必须使用真实 `system` role 保存全部历史内容；更准确的边界是：历史材料进入 protected context，当前输入进入 user message。

### 4.3 LLM Planner

LLM Planner 的职责是选择语义工具，不是生成 Markdown。

Planner 必须遵守：

- 不输出 JMF Markdown。
- 不输出 YAML front matter。
- 不调用禁用工具。
- 不删除、清空或替换用户内容。
- 不写正式 entry。
- 信息不足时可以返回 no-op，不要硬凑改动。

### 4.4 Operation Executor

Operation Executor 在服务端执行：

1. 校验工具名和参数。
2. 检查目标 section 是否存在、是否可编辑。
3. 检查 provenance 是否允许该操作。
4. 将语义工具映射为 JMF operation。
5. 使用 parser / composer / validator 生成新 draft。
6. 写入 `reviewing` draft 或 `attention` 状态。
7. 写入审计记录。

正式 entry 仍然只由现有确认流程写入。

### 4.5 异步 run + SSE 模型

Harness run 不应该绑定在一次长 HTTP 请求里等待 LLM 完成。用户提交当前输入后，API 必须先同步完成两件事：

1. 将当前输入追加到 raw inputs。
2. 创建一条 harness run 记录，状态为 `queued` 或 `running`。

随后由后端执行 LLM planner、operation executor、draft 写入和审计更新，并通过 SSE 把运行事件推给前端。服务端应使用 ASP.NET Core 10 的 `TypedResults.ServerSentEvents(...)` 和 `System.Net.ServerSentEvents.SseItem<T>` 返回事件流，不手写 `text/event-stream` 协议帧。SSE 是用户体验通道，不是唯一可靠性边界；run record 仍然是事实来源。客户端断线后可以用 run id 重新查询状态，必要时重新打开事件流。

这样做的边界是：

- 用户输入不会因为 LLM 超时、断网或进程重启而丢失。
- 慢模型、长文章扩写和复杂工具调用不会让“提交输入”的 API 一直等待。
- SSE 可以实时显示模型阶段、工具调用、验证结果和完成摘要。
- 审计页可以显示 `queued` / `running` / `reviewing` / `attention` / `no-change` / `failed` / `interrupted`。
- 第一阶段不承诺跨进程继续执行已启动的 LLM 请求；应用重启后仍处于 `running` 的 run 可以标记为 `interrupted`，用户可重新发起整理。

推荐交互形态：

```text
POST /journal/today/harness/runs
  -> sync append raw input
  -> create run
  -> return runId

GET /journal/harness/runs/{runId}/events
  -> TypedResults.ServerSentEvents(IAsyncEnumerable<SseItem<JournalHarnessRunEvent>>)
  -> run-started / model-token-or-step / tool-collected / tool-applied / validation / completed / failed

GET /journal/harness/runs/{runId}
  -> reconnect or fallback status query
```

如果实现上第一阶段不想引入持久队列，可以先让 SSE endpoint 承担执行流：`POST` 创建 run 后，前端立即连接 SSE；SSE 请求内运行 LLM 并持续推事件。即便这样，也必须先完成 raw input 和 run record 落盘，且断线后的最终状态要能被审计查询到。

## 5. 第一阶段工具合同

第一阶段暴露产品语义工具，对内复用 JMF operation。

### 5.1 `organizeNewInput`

主入口。根据当前 user message、历史 raw inputs、当前 draft 和 confirmed entry 判断是否需要调用后续工具。

它可以：

- 判断当前输入应该进入哪些 section。
- 选择 append / upsert / reviseAiGeneratedSection。
- 返回 no-op。

它不能：

- 直接写 Markdown。
- 直接确认 entry。
- 删除或替换用户内容。

### 5.2 `appendJournalSection`

向目标可编辑 section 追加内容。

允许：

- 追加到用户创建或用户编辑过的 section。
- 追加到 AI 创建的 section。
- 追加到已存在的 optional section。

禁止：

- 删除原内容。
- 清空原内容。
- 替换整块内容。
- 操作 `raw-inputs`、`keywords`、`metadata-note` 等系统块。

这是第一阶段最重要、最安全的工具。

### 5.3 `upsertJournalSection`

创建缺失的可选 section，或在 section 已存在时降级为 append。

规则：

- optional section 不存在：创建 section。
- optional section 已存在：只追加，不替换。
- required section 已存在：只追加，不重建。
- 目标 section 不可编辑：拒绝。

### 5.4 `reviseAiGeneratedSection`

改写 AI 自己生成且用户未编辑过的 section。

前置条件：

- `origin=ai`
- `created_by=ai`
- `last_touched_by != user`
- 目标 section 可编辑

如果 section 被用户编辑过，或者来源是 `user` / `mixed` / `unknown`，该工具必须拒绝。AI 可以改为使用 `appendJournalSection`。

### 5.5 禁用工具

第一阶段不提供：

- `deleteSection`
- `deleteItem`
- `clearSection`
- `replaceUserSection`
- `overwriteRawInputs`
- `writeFormalEntry`
- `editMetadataNote`
- `editKeywords`
- `freeformMarkdownPatch`

如果模型试图调用这些工具，Planner run 进入拒绝路径，并写入审计。

## 6. 用户内容保护规则

用户内容不是完全不可触碰，但 AI 只能协作追加。

规则：

- 用户生成 / 用户编辑过的 section：允许 append。
- 用户生成 / 用户编辑过的 section：禁止 delete / clear / replace。
- AI 认为用户内容重复、放错位置或应该删除时，只能生成建议，不能执行删除。
- `reviseAiGeneratedSection` 只允许操作纯 AI 块。
- `raw-inputs` 始终由服务端原始输入生成或保留，模型不能覆盖。

这比“用户块完全只读”更实用，也能保证用户表达不会被模型吞掉。

## 7. Provenance 设计

第一阶段采用 section 级 provenance，默认隐藏，只给 harness、审计页、测试和调试路径使用。

推荐扩展 JMF section marker 属性：

```md
<!-- journal:section today-focus
origin="mixed"
created_by="ai"
last_touched_by="ai"
last_operation="append"
based_on_raw_inputs="raw-1 raw-2" -->
## 今日重点

- 用户原来写的内容
- AI 追加的整理内容

<!-- /journal:section today-focus -->
```

### 7.1 字段

| 字段 | 值 | 说明 |
| --- | --- | --- |
| `origin` | `user` / `ai` / `mixed` / `unknown` | section 当前内容来源的粗粒度归属 |
| `created_by` | `user` / `ai` / `system` | section 最初由谁创建 |
| `last_touched_by` | `user` / `ai` | 最后一次修改者 |
| `last_operation` | `create` / `append` / `revise` / `edit` | 最近一次操作 |
| `based_on_raw_inputs` | raw input id 列表 | AI 操作依据的 raw input |

### 7.2 兼容规则

- 旧 Markdown 没有 provenance 时按 `origin=unknown` 处理。
- `unknown` section 不允许 AI replace，只允许在可编辑 section 上 append。
- 属性解析失败不应破坏正文读取，但应产生诊断并进入 attention 或审计警告。
- 普通 Markdown 预览继续隐藏 section marker 和 provenance。
- 如果用户通过块编辑保存 section，`last_touched_by=user`，`last_operation=edit`。

## 8. 状态流与失败处理

### 8.1 成功路径

```text
current user input
  -> append raw input
  -> harness run
  -> valid tool calls
  -> JMF operation executor
  -> JMF validation valid
  -> reviewing draft
  -> user confirmation
  -> formal entry
```

### 8.2 失败路径

失败包括：

- LLM 不可用。
- 模型返回非法工具调用。
- 模型调用禁用工具。
- 工具参数非法。
- provenance 不满足操作条件。
- 执行后 JMF validation 失败。

失败处理：

- 不覆盖 formal entry。
- 不把错误报告写进日记正文。
- baseline Markdown 保持干净。
- 错误和 audit 写入 meta / 审计记录 / 状态面板。
- 当前状态进入 `attention`，或保留当前状态并显示 run 失败，具体由实现阶段细化。

推荐落盘语义接近：

```text
Markdown: 保留 baseline draft / entry 内容
Meta: 记录 attention errors + audit
UI: 在助手或审计页展示失败原因
```

不要再把 `# LLM generation failed` 写成日记正文，除非没有任何 baseline 可保留。

### 8.3 No-op

如果模型判断当前输入没有可整理内容，允许 no-op：

- 不制造空 draft。
- 不覆盖已有 draft。
- 写一条审计记录说明“无有效改动”。

## 9. UI 与审计设计

### 9.1 当前真实界面约束

当前 Journal 桌面界面是三栏 command workspace：

- 左侧：`今日材料`、日期、下一步。
- 中间：`日记纸面` 和底部输入栏。
- 右侧：`Today Assistant`，显示下一步、统计、整理证据、attention、正式文件等。

没有全局 React 侧边导航。LLM 配置入口通过 Electron 原生菜单打开。

因此审计设计不能引入一套和当前界面不一致的新侧边栏产品结构。

### 9.2 入口位置

推荐入口放在右侧 `Today Assistant` 的「整理证据」卡片头部：

```text
整理证据                         查看审计
```

原因：

- `整理证据` 已经在解释 raw input 如何进入日记段落。
- 审计是“证据”的下一层，不会打断中间写作。
- 右侧助手本来就是“今天被这样整理”的承载区。

顶部不放审计入口，避免和 LLM 配置、API 健康、日期上下文混在一起。中间日记纸面不放审计入口，保持阅读和编辑干净。

### 9.3 完整审计页

完整审计不塞进右侧助手栏。右侧宽度约 300px，只适合摘要，不适合日期、run 列表、工具调用、provenance 详情。

点击 `查看审计` 后，整个主工作区切到 **AI 审计工作台**，并复用当前三栏 shell：

- 左栏：日期选择、当天 harness run 列表、状态筛选。
- 中间：工具调用时间线、摘要、未来可扩展草稿差异。
- 右栏：选中操作详情、provenance、拒绝原因、用户可见结论。

顶部或工作区显著位置提供 `返回今日`。点击后恢复今日日记界面，继续写、整理、保存。

### 9.4 右侧助手保留内容

Today Assistant 中只保留：

- 最近一次 harness run 摘要。
- 是否有拒绝操作。
- 是否可确认。
- `查看审计` 入口。

不在普通助手卡片里展示完整 provenance。

### 9.5 审计页面日期边界

审计工作台支持选日期查看 harness run。第一阶段只查看审计记录，不负责加载或编辑该日期的完整日记。

后续多日期日记浏览实现后，可以把审计日期和日记日期联动，但本阶段不要把范围扩大。

## 10. 后端数据与 API 方向

本设计不直接指定最终接口细节，但推荐形成以下抽象。

### 10.1 Harness run record

```json
{
  "id": "run-20260512-083100",
  "date": "2026-05-12",
  "createdAt": "2026-05-12T08:31:00+08:00",
  "startedAt": "2026-05-12T08:31:01+08:00",
  "completedAt": "2026-05-12T08:31:18+08:00",
  "status": "reviewing",
  "providerId": "deepseek",
  "promptVersion": "journal-harness-v1",
  "currentUserInputId": "raw-3",
  "baseline": {
    "source": "draft",
    "status": "reviewing"
  },
  "toolCalls": [],
  "validation": {
    "isValid": true,
    "issues": []
  },
  "summary": "AI 追加 1 段到灵感，没有覆盖用户内容。"
}
```

`status` 推荐值：

- `queued`：已保存 raw input，等待执行。
- `running`：LLM / 工具收集 / 执行器正在运行。
- `reviewing`：已写入可确认 draft。
- `attention`：运行完成但需要用户处理，例如工具拒绝或 validation 失败。
- `no-change`：模型判断无需修改 draft。
- `failed`：运行失败，但 raw input 已保留。
- `interrupted`：应用重启、连接中断或进程退出导致运行结果未知。

### 10.2 Tool call record

```json
{
  "id": "tool-1",
  "name": "appendJournalSection",
  "targetSectionId": "inspiration",
  "status": "applied",
  "reason": "当前输入表达的是 prompt 设计原则，适合作为灵感保留。",
  "basedOnRawInputIds": ["raw-3"],
  "resultSummary": "追加 1 段。",
  "rejectionReason": null
}
```

### 10.3 推荐 API

```text
POST /journal/today/harness/runs
GET  /journal/harness/runs/{runId}
GET  /journal/harness/runs/{runId}/events
GET  /journal/audit?date=yyyy-MM-dd
```

`POST /journal/today/harness/runs` 替代或逐步替代现在的“整篇重新整理”路径。该接口只做快速、可靠的提交：校验输入、追加 raw input、创建 run record、返回 run id。真正的 LLM 进度通过 SSE endpoint 返回。

`GET /journal/harness/runs/{runId}/events` 使用 `TypedResults.ServerSentEvents(...)` 返回 SSE，事件类型建议：

- `run-started`
- `planner-started`
- `tool-collected`
- `tool-rejected`
- `tool-applied`
- `validation-completed`
- `draft-updated`
- `run-completed`
- `run-failed`

`GET /journal/harness/runs/{runId}` 用于 SSE 断线重连后的状态恢复，也可作为不支持 SSE 时的 polling fallback。

现有 `POST /journal/today/draft/regenerate` 可以保留为旧能力或开发工具，但新用户工作流应逐步转到 harness。

## 11. 测试策略

后端重点测试：

- 当前 user input 与历史 raw inputs 分层进入 prompt request。
- 历史 raw inputs 不被当成本轮命令。
- `appendJournalSection` 可以追加用户块。
- 用户块禁止 replace / clear / delete。
- `reviseAiGeneratedSection` 只能作用于纯 AI 块。
- provenance 缺失时按 `unknown` 处理，禁止 replace。
- 工具失败不覆盖 formal entry。
- JMF validation 失败不把错误 Markdown 写进正文。
- audit record 保存 tool call、拒绝原因和 validation 结果。
- `POST /journal/today/harness/runs` 快速返回 run id，不等待 LLM 完成。
- SSE endpoint 按顺序发送 run / tool / validation / completion 事件。
- SSE 断线后可以通过 run id 查询最终状态。

前端重点测试：

- Today Assistant 的「整理证据」附近出现 `查看审计` 入口。
- 提交当前输入后立即显示 harness 正在运行，不阻塞输入框。
- SSE 事件更新右侧助手中的运行进度。
- 点击后主工作区切到 AI 审计工作台。
- `返回今日` 恢复原工作台。
- 审计工作台支持日期输入和 run 列表展示。
- 工具调用详情展示 applied / rejected。
- provenance 只在审计页显示，不在普通日记纸面显示。
- 窄屏时审计工作台按当前 command workspace 响应式规则降级。

## 12. 实施切片建议

### Slice 1：Provenance parser/composer

- 扩展 JMF parser 读取 section marker 属性。
- 扩展 composer 保留/输出 provenance。
- 块编辑保存更新 `last_touched_by=user`。
- 旧文档兼容 `unknown`。

### Slice 2：Operation executor

- 增加 JMF operation 模型。
- 实现 append / upsert / reviseAiGeneratedSection。
- 实现用户块 append、禁止 replace 的规则。
- 增加 focused 后端测试。

### Slice 3：Harness prompt 与 planner

- 实现 Prompt Context Split。
- 历史 raw inputs 进入 protected context。
- 当前输入进入 user message。
- LLM 返回语义工具调用。
- 非法工具调用进入拒绝路径。

### Slice 4：Audit store 与 API

- 保存 harness run 和 tool call。
- 提供按日期查询。
- attention / rejected 不污染正文。

### Slice 5：UI 入口与审计工作台

- Today Assistant 增加 `查看审计`。
- 工作区切换为审计工作台。
- 复用三栏 shell：左 run 列表、中时间线、右详情。
- 增加 `返回今日`。

## 13. 已确认决策

- 第一阶段选择 Harness Core，不做完整 Agent Workflow。
- AI 可读 confirmed entry，但只写 draft。
- 对模型暴露产品语义工具，后端内部映射 JMF operation。
- 第一阶段不提供删除工具。
- 用户块允许 append，但不允许 delete / clear / replace。
- AI 只允许改写纯 AI 生成且用户未编辑过的块。
- Provenance 第一阶段做 section 级。
- Provenance 默认隐藏，审计页可见。
- Prompt 分层：历史 raw inputs 进 protected context，当前输入进 user message。
- 审计入口放在 Today Assistant 的「整理证据」附近。
- 完整审计页复用当前三栏 shell，点击 `返回今日` 回到日记工作台。

## 14. 待后续设计的问题

- 是否需要 item 级 provenance。
- 是否需要用户授权删除/隐藏流程。
- 是否需要 draft diff 视图和一键撤销某次 harness run。
- 是否把旧 `regenerate` 能力降级为开发工具。
- 多日期日记浏览实现后，审计日期如何与日记日期联动。
