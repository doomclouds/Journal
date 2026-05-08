# Journal 阶段 2：JMF 生成确认 MVP 设计

> 日期：2026-05-08
> 状态：待实施计划
> 对应愿景：`PROJECT_VISION.md` 阶段 2：JMF 生成确认 MVP
> 上一阶段：[阶段 1：应用框架骨架](./2026-05-07-phase-1-skeleton-design.md)

## 1. 背景

阶段 1 已经完成 Electron + React + .NET 本地 API 的工程骨架，前端可以通过 `/health` 与后端联通。阶段 2 开始进入 Journal 的第一条真实产品链路，但不把完整编辑器、版本、索引和真实 AI Provider 一次塞进来。

本阶段采用纵向切片优先：让用户从今日工作台输入一段自然语言，后端保存原始输入，用规则型 `MockAiProvider` 生成结构化 JSON，校验后渲染成 JMF v1 Markdown 草稿，用户确认后写入当天正式 Markdown 文件。

这意味着原路线图中的“块编辑模式”和“源码模式基础校验”从阶段 2 拆出，进入新的阶段 3。阶段 2 不缩水，它只聚焦主链路闭环。

## 2. 目标

阶段 2 的目标是打通：

```text
自然语言输入
  -> 追加 raw input
  -> 规则型 Mock AI
  -> 结构化 JSON
  -> JSON 校验
  -> JMF Markdown 草稿落盘
  -> 前端预览
  -> 用户确认
  -> 更新当天正式 Markdown
```

完成后，用户应该能在今日工作台输入一段中文晨间日记，看到 JMF v1 Markdown 预览，并确认保存为当天正式日记文件。

## 3. 非目标

阶段 2 明确不做：

- 块编辑模式。
- 源码模式。
- 草稿正文手动编辑。
- 版本快照。
- SQLite 索引。
- Markdown -> JSON 反向解析。
- 真实 AI Provider。
- Provider 配置页。
- 多日期浏览。
- 同日年轮。
- 未来日记提醒。
- 安装包与生产期 Electron 托管后端进程。

这些能力分别进入后续阶段，避免阶段 2 同时承担数据链路、编辑器交互和长期可靠性三类复杂度。

## 4. 阶段拆分调整

阶段路线从原来的：

```text
阶段 2：文本输入到 JMF Markdown MVP
阶段 3：版本与索引
阶段 4：真实 AI Provider
阶段 5：同日年轮与未来日记
阶段 6：长期可靠性
```

调整为：

```text
阶段 2：JMF 生成确认 MVP
阶段 3：JMF 编辑模式与结构校验
阶段 4：版本与索引
阶段 5：真实 AI Provider
阶段 6：同日年轮与未来日记
阶段 7：长期可靠性
```

阶段 3 专门处理块编辑、源码模式、保存前结构保护和 JMF 异常修复提示。这样阶段 2 的验收标准更清楚，阶段 3 的编辑器质量也不会被主链路实现挤压。

## 5. 数据目录

开发期和阶段 2 默认写入 Windows 本地用户应用目录：

```text
%LocalAppData%/Journal/
```

在当前 Windows 用户下，实际路径形态为：

```text
C:/Users/<user>/AppData/Local/Journal/
```

阶段 2 使用以下目录：

```text
%LocalAppData%/Journal/
  entries/
    2026/
      05/
        2026-05-08.md
  .journal/
    raw-inputs/
      2026/
        05/
          2026-05-08.jsonl
    drafts/
      2026/
        05/
          2026-05-08.md
```

目录职责：

- `entries/` 保存用户确认过的正式 Markdown，一天一个文件。
- `.journal/raw-inputs/` 保存原始输入 JSONL，每次输入追加一行。
- `.journal/drafts/` 保存当前 Markdown 预览草稿，应用重启后可恢复。

阶段 2 不创建 `.journal/versions/` 和 `.journal/index/`，版本与索引留给阶段 4。

## 6. 领域模型

`Journal.Domain` 承担稳定概念，不依赖文件系统：

- `JournalDate`：封装日记日期、年份、月份、文件名和 `month_day`。
- `RawInput`：一次原始输入，包含 id、date、createdAt、source、text。
- `JournalStatus`：`empty`、`draft`、`reviewing`、`processed`、`updated`、`attention`。
- `JournalAiJson`：Mock AI 输出的结构化日记草案。
- `JournalDraft`：当前日期的草稿状态、Markdown 内容、错误信息和来源 raw input ids。
- `JournalEntry`：当天正式 Markdown 文件状态。
- `JournalSection` / `JournalSectionId`：JMF v1 section 定义。

阶段 2 的模型要能表达失败状态。失败不是 500 之后消失，而是可呈现的 `attention` 草稿。

## 7. 基础设施服务

`Journal.Infrastructure` 承担实现细节：

- `LocalJournalPaths`：解析 `%LocalAppData%/Journal`，生成 entries、raw-inputs、drafts 路径。
- `RawInputStore`：按日期追加和读取 JSONL。
- `DraftStore`：写入和读取当天 Markdown 草稿及草稿元信息。
- `EntryStore`：写入和读取当天正式 Markdown。
- `MockAiProvider`：按规则从 raw inputs 生成 `JournalAiJson`。
- `JournalAiJsonValidator`：校验必需字段和 JMF 可渲染性。
- `JmfMarkdownRenderer`：将合法 JSON 渲染为 JMF v1 Markdown。
- `TodayJournalService`：编排今日工作流，供 API 调用。

`Journal.Api` 不直接拼路径、不直接写文件、不直接实现 Mock AI 规则。`Program.cs` 只负责注册服务和映射 endpoint。

## 8. 规则型 Mock AI

`MockAiProvider` 不追求智能，只追求稳定、可测试、输出随输入变化。

规则建议：

- 包含“昨天”“昨晚”“上次”“完成了”等词句时，倾向写入 `yesterdayReview`。
- 包含“今天”“接下来”“准备”“要做”“计划”等词句时，倾向写入 `todayFocus`。
- 包含“想到”“灵感”“应该”“可以”“原则”等词句时，倾向生成 `inspiration` 可选块。
- 包含“累”“焦虑”“开心”“有推进感”“平静”等词句时，提取 `mood`。
- 显式 `#标签` 优先进入 `tags`。
- 未提取到内容时，也必须生成合法最小 JSON，保证必需块存在。

Mock AI 必须基于当天全部 raw inputs 生成当天草稿，而不是只看最后一次输入。

## 9. JSON 校验与错误策略

JSON 校验失败或 Mock AI 生成失败时：

1. 原始输入已经保留在 raw inputs JSONL。
2. 后端生成 `attention` 草稿状态。
3. 草稿记录错误原因和相关 raw input ids。
4. 前端展示错误和原始输入。
5. 不写入或覆盖正式 `entries/` Markdown。

阶段 2 的原则是：宁可明确进入 `attention`，不要用坏数据污染正式日记。

## 10. JMF v1 Markdown 渲染

阶段 2 渲染的 Markdown 必须包含：

- YAML front matter。
- `schema: journal-entry/v1`。
- `date`。
- `month_day`。
- `status`。
- `tags`。
- `topics`。
- `mood`。
- `version`，阶段 2 固定可从 `1` 开始。
- `provider: mock`。
- `model: mock-journal`。
- `prompt_version: mock-journal-entry-v1`。
- `generated_at`。

必需 section：

- `raw-inputs`
- `yesterday-review`
- `today-focus`

可选 section：

- `mood`
- `work`
- `learning`
- `health`
- `relationship`
- `money`
- `inspiration`
- `future-notes`
- `gratitude`
- `keywords`
- `metadata-note`

阶段 2 的 Markdown 可以预览和确认，但不能在 UI 中手动编辑。编辑能力进入阶段 3。

## 11. 今日工作流 API

阶段 2 只暴露今日主流程 API：

```text
GET  /journal/today
POST /journal/today/inputs
POST /journal/today/draft/confirm
```

### 11.1 `GET /journal/today`

返回今日状态：

- `date`
- `status`
- `rawInputs`
- `draft`
- `entry`
- `errors`

当没有输入、草稿和正式文件时，状态为 `empty`。

### 11.2 `POST /journal/today/inputs`

请求：

```json
{
  "text": "昨天把阶段 1 跑通了，今天准备做 JMF 主链路。",
  "source": "text"
}
```

处理流程：

1. 校验文本非空。
2. 追加 raw input。
3. 读取当天全部 raw inputs。
4. 调用规则型 `MockAiProvider`。
5. 校验 `JournalAiJson`。
6. 成功时渲染 JMF Markdown 并写入 `.journal/drafts/`。
7. 失败时写入 `attention` 草稿，不写正式 entry。
8. 返回最新今日状态。

### 11.3 `POST /journal/today/draft/confirm`

处理流程：

1. 读取当前 draft。
2. 校验 draft 存在且状态可确认。
3. 将 draft Markdown 写入或覆盖当天正式 entry。
4. 如果之前没有正式 entry，状态为 `processed`。
5. 如果之前已有正式 entry，状态为 `updated`。
6. 返回最新今日状态。

阶段 2 确认时直接更新当天正式文件，不创建版本快照。

## 12. 前端今日工作台

前端从阶段 1 的状态页升级为单页今日工作台。

前端高保真原型：

- [2026-05-08-phase-2-today-workbench-prototype.html](./2026-05-08-phase-2-today-workbench-prototype.html)

界面包含：

- 今日日期。
- API 状态。
- 日记状态。
- 自然语言输入框。
- 提交按钮。
- raw inputs 简要列表。
- Markdown 草稿预览。
- attention 错误展示。
- 确认保存按钮。
- 正式文件状态。

交互规则：

- 页面加载时调用 `/health` 和 `/journal/today`。
- 提交输入时调用 `POST /journal/today/inputs`。
- 有合法 draft 且状态为 `reviewing` 时显示确认保存。
- `attention` 时显示错误，不显示确认保存。
- 确认保存后调用 `POST /journal/today/draft/confirm` 并刷新状态。

阶段 2 不做 Markdown 编辑器。预览区只读。

### 12.1 前端响应式窗口规则

阶段 2 的前端不能只按宽屏三栏设计实现。当前 Electron 默认窗口是 `1180 x 780`，最小窗口是 `960 x 640`，因此今日工作台采用三档布局：

- `1360px+` 宽屏态：左侧日期与 raw inputs，中间日记纸面，右侧输入与 Mock AI Dock。
- `1180px` 默认态：左侧日期与 raw inputs 压缩为顶部上下文条，中间日记纸面保持主区域，右侧保留输入与 Mock AI Dock。
- `960px` 最小态：只保留日记纸面作为首屏重心，输入、Mock AI 和确认能力降为底部/抽屉式辅助区；首屏仍必须提供“补充输入”和“确认保存”的快捷操作。

实现时以“日记纸面优先”为约束。任何窗口尺寸下，输入框、AI 提取结果和文件路径都不能抢占日记正文的视觉中心。

## 13. 同日多次输入

同一天可以多次提交输入。每次提交都：

1. 追加一条 raw input。
2. 基于当天全部 raw inputs 重新生成当天草稿。
3. 覆盖 `.journal/drafts/yyyy/MM/yyyy-MM-dd.md`。
4. 用户确认后覆盖 `entries/yyyy/MM/yyyy-MM-dd.md`。

这样正式文件始终表示当天最新版，raw inputs 记录完整表达历史。

## 14. 测试

后端测试覆盖：

- `POST /journal/today/inputs` 会追加 raw input JSONL。
- 规则型 `MockAiProvider` 会根据输入提取 yesterday/today/inspiration/mood/tags。
- JSON 校验失败会进入 `attention`，不会写正式 entry。
- JMF 渲染结果包含 front matter、必需 section marker、原始输入、昨日回顾、今日重点。
- `POST /journal/today/draft/confirm` 会写入或覆盖当天正式 Markdown。
- 同一天多次输入会追加 raw inputs，并重生成当天 draft。

前端测试覆盖：

- 今日工作台能展示 `empty`、`reviewing`、`attention`、`processed` 等状态。
- 提交输入后会调用 `/journal/today/inputs`。
- 有 draft 时显示 Markdown 预览和确认按钮。
- `attention` 时显示错误，不显示确认保存。
- 确认保存后刷新今日状态。

集成验证命令：

```powershell
dotnet test Journal.slnx
npm test --prefix apps/desktop
npm run build --prefix apps/desktop
```

手工验证：

1. 启动 API。
2. 启动 Electron/Vite 桌面前端。
3. 在今日工作台输入一段中文。
4. 检查前端出现 Markdown 草稿。
5. 点击确认保存。
6. 检查 `%LocalAppData%/Journal/entries/yyyy/MM/yyyy-MM-dd.md` 出现或更新。

## 15. 完成标准

阶段 2 完成时必须满足：

- 今日工作台可以提交自然语言输入。
- raw input 以 JSONL 追加保存。
- draft Markdown 落盘到 `.journal/drafts/`。
- 合法草稿可以在前端预览。
- 用户确认后正式 Markdown 写入 `entries/`。
- 同日多次输入会重生成当天草稿。
- 失败进入 `attention`，不覆盖正式 entry。
- 后端测试、前端测试和前端构建通过。
- README 更新阶段 2 启动与验证说明。

## 16. 后续衔接

阶段 2 完成后，阶段 3 继续处理：

- 块编辑模式。
- 源码模式。
- JMF marker 成对校验。
- 必需 section 校验。
- 未知 section 的 `attention` 状态。
- 保存前结构保护。
- 结构修复提示。

阶段 4 再处理版本、索引、FTS 和 Markdown 反向解析。
