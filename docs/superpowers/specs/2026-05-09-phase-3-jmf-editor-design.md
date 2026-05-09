# Journal 阶段 3：JMF 编辑模式与结构校验设计

> 日期：2026-05-09
> 状态：待实施计划
> 对应愿景：`PROJECT_VISION.md` 阶段 3：JMF 编辑模式与结构校验
> 上一阶段：[阶段 2：JMF 生成确认 MVP](./2026-05-08-phase-2-jmf-generation-confirmation-design.md)
> 高保真原型：[2026-05-09-phase-3-jmf-editor-prototype.html](./2026-05-09-phase-3-jmf-editor-prototype.html)

## 1. 背景

阶段 1 已完成 Electron + React + .NET 本地 API 的工程骨架。阶段 2 已打通今日晨间日记主链路：自然语言输入、raw input JSONL、Mock AI JSON、JMF Markdown 草稿、只读预览、用户确认、正式 Markdown 写入。

阶段 3 不继续扩张 AI、索引或版本能力，而是补上 JMF 的安全编辑层。用户需要能修改日记内容，但系统必须保护 JMF 的可解析结构：普通用户在块编辑模式里只编辑内容，不接触 `<!-- journal:section ... -->` marker；高级用户可以进入源码模式编辑完整 Markdown，但保存前必须通过 JMF 结构校验。

本阶段的核心原则来自项目愿景：**结构由系统保护，内容保持自由**。编辑器不能退化成普通 Markdown 文本框，也不能在没有版本快照的阶段直接覆盖正式日记。

## 2. 目标

阶段 3 目标是打通：

```text
读取今日 draft / entry Markdown
  -> 解析为 JMF document
  -> 块编辑或源码编辑
  -> JMF 结构校验
  -> 成功保存为 reviewing draft
  -> 用户确认
  -> 更新当天正式 Markdown
```

完成后，用户应该可以在今日工作台中：

- 用默认块编辑模式修改 `yesterday-review`、`today-focus`、`mood`、`inspiration` 等内容块。
- 查看但不直接改写 `raw-inputs` 原始表达。
- 从 JMF 可选单例块菜单中新增尚未出现的块。
- 切换源码模式查看和编辑完整 Markdown。
- 在保存前得到明确的结构校验结果。
- 校验失败时看到 `attention` 状态和修复提示，且正式 Markdown 不被覆盖。

## 3. 非目标

阶段 3 明确不做：

- 版本快照。
- SQLite 索引。
- Markdown -> JSON 的完整业务反向生成。
- 真实 AI Provider。
- AI 对话式改写。
- 富文本/WYSIWYG 编辑器。
- 拖拽排序 section。
- 自由创建 JMF 外的新 section。
- 多日期浏览。
- 文件删除、隐藏、加密。
- 自动保存、协同编辑、多窗口冲突处理。
- 安装包与生产期 Electron 托管后端进程。

版本快照与索引继续留给阶段 4。真实 AI Provider 继续留给阶段 5。阶段 3 只做安全编辑和结构保护。

## 4. 产品原则

### 4.1 原始表达不可丢

`raw-inputs` 是原始表达底片。块编辑模式里它默认只读，用户可以查看、展开、复制，但不能在块编辑器里改写。用户要补充原始表达，仍走阶段 2 的“补充今天” raw input 链路。

源码模式可以看到完整 Markdown，包括 `raw-inputs`，但保存时必须经过结构校验。阶段 3 不提供“修改 raw input JSONL 历史”的能力。

### 4.2 Markdown 是可信源

阶段 3 的 parser 和 block model 都从 Markdown 得到。正式 `entries/yyyy/MM/yyyy-MM-dd.md` 仍是用户确认后的可信源；`.journal/drafts/` 是编辑和预览的草稿层。

### 4.3 结构由系统保护

普通块编辑模式不暴露 YAML front matter 和 marker。新增块只能从 JMF 已知可选单例块中选择。系统按固定顺序重组 Markdown，不允许用户拖拽改变 section 顺序。

### 4.4 内容保持自由

块内容内部允许自然段和 Markdown 列表。阶段 3 不做复杂富文本，但不能把用户限制成表单字段。普通块编辑可以用 textarea/list-like editing 表达多行内容。

### 4.5 失败不污染正式文件

任何 JMF 解析或校验失败，都只能写入 `attention` draft 和错误信息，不能覆盖正式 entry。用户可以回到块编辑修复，也可以在源码模式修复 marker/front matter。

## 5. JMF v1 块规则

阶段 3 以项目愿景中的 JMF v1 规则为准。

必需块，永远存在，不可删除：

- `raw-inputs`：原始输入，块编辑只读。
- `yesterday-review`：昨日回顾，可编辑内容，不可删除。
- `today-focus`：今日重点，可编辑内容，不可删除。

可选单例块，每篇最多一个：

- `mood`：情绪状态。
- `inspiration`：灵感。
- `health`：健康与精力。
- `relationship`：关系与家庭。
- `work`：工作推进。
- `learning`：学习与思考。
- `money`：财务。
- `future-notes`：未来提醒。
- `gratitude`：感恩。

系统块，普通块编辑默认不直接编辑：

- `keywords`：关键词。
- `metadata-note`：生成信息和模型信息。

固定顺序：

1. 原始输入
2. 情绪状态
3. 昨日回顾
4. 今日重点
5. 工作推进
6. 学习与思考
7. 健康与精力
8. 关系与家庭
9. 财务
10. 灵感
11. 未来提醒
12. 感恩
13. 关键词
14. 生成信息

可选块如果没有内容就不渲染。一旦出现，系统按固定顺序插入，而不是按用户点击位置、AI 输出顺序或拖拽顺序插入。

## 6. 后端设计

### 6.1 JMF section 定义

`Journal.Domain` 增加稳定的 JMF section 定义：

- `JournalSectionId`：封装已知 section id。
- `JournalSectionKind`：`Required`、`OptionalSingleton`、`System`。
- `JournalSectionDefinition`：id、标题、顺序、kind、是否块编辑可编辑。
- `JmfDocument`：front matter、sections、parse warnings。
- `JmfSection`：id、title、content、kind、isEditableInBlockMode。
- `JmfValidationResult`：isValid、errors、repairHints。

这些类型表达 JMF 结构，不依赖文件系统。

### 6.2 JMF parser

新增 `JmfMarkdownParser`，负责从 Markdown 解析：

- YAML front matter。
- `schema`。
- `date`、`month_day`、`status`、`tags`、`topics`、`mood`、`version`、provider/model/prompt metadata。
- `<!-- journal:section <id> -->` 和 `<!-- /journal:section <id> -->` marker。
- section title。
- section content。

parser 需要保留 section 内容的 Markdown 文本，不做复杂 Markdown AST。阶段 3 的目标是 JMF 结构安全，不是 Markdown 完整语法编辑器。

### 6.3 JMF validator

新增 `JmfMarkdownValidator`，保存前至少校验：

- front matter 必须存在。
- `schema: journal-entry/v1` 必须存在。
- 必需块 `raw-inputs`、`yesterday-review`、`today-focus` 必须存在。
- section marker 必须成对。
- 开始 marker 和结束 marker 的 id 必须一致。
- 同一个 section id 不能重复。
- 未知 section id 进入 `attention`。
- 可选单例块最多出现一次。
- 块编辑模式提交不能修改 `raw-inputs` 内容。

校验失败返回结构化错误和修复提示。错误示例：

- `missing-required-section: today-focus`
- `unknown-section: random-note`
- `duplicate-section: inspiration`
- `unmatched-section-marker: yesterday-review`
- `raw-inputs-is-readonly`

### 6.4 JMF composer

新增 `JmfMarkdownComposer`，负责从 `JmfDocument` 或块编辑请求重组 Markdown。composer 必须：

- 保留合法 front matter。
- 使用固定 section 顺序。
- 跳过空的可选块。
- 对 section content 进行 marker 注入防护。
- 复用阶段 2 已验证的 YAML quoting 规则或抽成共享 helper。

块编辑模式保存时，不允许前端提交完整 Markdown 直接覆盖。前端提交的是 block model，后端由 composer 生成 Markdown。

### 6.5 TodayJournalService 扩展

`TodayJournalService` 增加：

- `GetTodayEditorAsync`：读取今日 draft 优先，其次 entry，解析为 editor state。
- `SaveBlockDraftAsync`：接收块编辑请求，校验、compose、写入 reviewing draft。
- `SaveSourceDraftAsync`：接收完整 Markdown，校验、写入 reviewing draft 或 attention draft。

保存成功只写 `.journal/drafts/`，状态为 `reviewing`。用户确认仍走阶段 2 已有 `ConfirmDraftAsync`，正式 entry 只在确认后更新。

## 7. API 设计

新增今日编辑 API：

```text
GET  /journal/today/editor
PUT  /journal/today/editor/blocks
PUT  /journal/today/editor/source
```

### 7.1 `GET /journal/today/editor`

返回当前可编辑状态。读取优先级：

1. 如果存在 draft，解析 draft。
2. 否则如果存在 entry，解析 entry。
3. 否则返回 empty editor state。

响应包含：

- `date`
- `status`
- `mode`
- `markdown`
- `sections`
- `availableOptionalSections`
- `validation`
- `canConfirm`

### 7.2 `PUT /journal/today/editor/blocks`

请求包含当前块编辑内容：

```json
{
  "sections": [
    { "id": "mood", "content": "清醒，有推进感。" },
    { "id": "yesterday-review", "content": "- 完成阶段 2。" },
    { "id": "today-focus", "content": "- 写阶段 3 spec。" },
    { "id": "inspiration", "content": "- 结构由系统保护。" }
  ]
}
```

处理流程：

1. 读取当前 draft/entry 作为基线，保留 front matter 和 `raw-inputs`。
2. 校验请求里不存在未知 section。
3. 校验请求里不存在重复 section。
4. 校验请求没有尝试修改 `raw-inputs`。
5. 将请求 section 合并到 JMF document。
6. 按固定顺序 compose Markdown。
7. 校验 composed Markdown。
8. 成功时写入 reviewing draft。
9. 失败时写入 attention draft，不覆盖 entry。
10. 返回 editor state 和 today state 所需信息。

### 7.3 `PUT /journal/today/editor/source`

请求包含完整 Markdown：

```json
{
  "markdown": "---\nschema: journal-entry/v1\n---\n..."
}
```

处理流程：

1. 解析 Markdown。
2. 运行 JMF validator。
3. 成功时写入 reviewing draft。
4. 失败时写入 attention draft，保留用户源码内容和错误。
5. 不覆盖正式 entry。

## 8. 前端设计

阶段 3 继续沿用阶段 2 V3 原型的桌面工具布局：日记纸面为中心，工具 Dock 为辅助。

高保真原型：

- [2026-05-09-phase-3-jmf-editor-prototype.html](./2026-05-09-phase-3-jmf-editor-prototype.html)

### 8.1 块编辑模式

默认显示块编辑模式。纸面中每个 JMF section 是一个 block card：

- `raw-inputs`：只读，展示原始表达和时间。
- `mood`：单段文本编辑。
- `yesterday-review`：多行 Markdown 内容编辑。
- `today-focus`：多行 Markdown 内容编辑。
- `inspiration` 等可选块：多行 Markdown 内容编辑。

用户看不到 marker。标题可以显示中文 section 标题，但 marker id 只作为小标签或调试信息弱展示。

### 8.2 新增块菜单

右侧 Dock 提供“新增块”菜单：

- 只展示尚未出现的可选单例块。
- 已出现的可选块显示 disabled 或不显示。
- 必需块不在新增菜单里。
- 系统块默认不在新增菜单里。
- 插入后按 JMF 固定顺序出现在纸面中。

### 8.3 源码模式

顶部提供 `块编辑 / 源码模式` 切换。源码模式显示完整 Markdown，包括 front matter 和 marker。

源码模式保存按钮文案使用“保存为草稿”，不使用“写入正式日记”。保存后如果校验通过，进入 reviewing draft；如果失败，进入 attention draft 并展示错误。

### 8.4 校验失败态

校验失败时展示：

- 错误列表。
- 修复提示。
- 不覆盖正式文件的提示。
- 回到块编辑模式。
- 回到源码模式。

可自动修复的错误可以提供按钮，例如“插入缺失的今日重点块”。阶段 3 首版只需要提供修复提示，不要求自动修复所有错误。

### 8.5 确认写入正式文件

当 draft 状态为 `reviewing` 且校验通过时，显示阶段 2 已有“确认写入正式日记”按钮。确认后才写入 `entries/`。

## 9. 状态与错误策略

阶段 3 继续使用现有状态：

- `reviewing`：编辑草稿已通过校验，等待确认。
- `processed`：首次确认写入正式 entry。
- `updated`：已有 entry 后再次确认更新。
- `attention`：结构校验失败或不能安全写入。

新增行为：

- block 保存成功：draft status = `reviewing`。
- source 保存成功：draft status = `reviewing`。
- block/source 保存失败：draft status = `attention`，正式 entry 不变。
- `GET /journal/today/editor` 在 attention 状态下也要返回可修复信息。

## 10. 数据保存策略

阶段 3 坚持“先草稿，后正式”：

```text
编辑 block/source
  -> 后端解析并校验 JMF
  -> 保存为 reviewing draft
  -> 前端预览
  -> 用户确认
  -> 写入 entries 正式 Markdown
```

校验失败：

```text
编辑内容
  -> 校验失败
  -> attention draft + 修复提示
  -> 不覆盖正式 entries
```

阶段 3 不做自动保存。用户点击“保存为草稿”才写 `.journal/drafts/`。

## 11. 测试

后端测试覆盖：

- JMF parser 能解析 front matter、必需 section、可选 section 和 section content。
- parser 能发现 marker 未成对、未知 section、重复 section。
- validator 能拒绝缺失 front matter、缺失 schema、缺失必需块。
- block save 不能修改 `raw-inputs`。
- block save 会按固定顺序 compose Markdown。
- block save 成功写 reviewing draft，不覆盖正式 entry。
- source save 校验失败写 attention draft，不覆盖正式 entry。
- confirm 仍复用阶段 2 行为，只有 reviewing draft 可以写 entry。
- API endpoint 返回 camelCase JSON 和正确错误码。

前端测试覆盖：

- 页面加载 editor state 后显示块编辑模式。
- `raw-inputs` 显示只读，不出现可编辑控件。
- 用户编辑 `today-focus` 后点击“保存为草稿”，调用 `/journal/today/editor/blocks`。
- 新增块菜单只显示尚未出现的可选单例块。
- 源码模式保存调用 `/journal/today/editor/source`。
- attention 返回时显示错误和修复提示，不显示确认写入按钮。
- reviewing draft 时显示确认写入按钮。

集成验证命令：

```powershell
dotnet test Journal.slnx
npm test --prefix apps/desktop
npm run build --prefix apps/desktop
```

手工验证：

1. 启动 API 和桌面前端。
2. 生成或打开今天的 reviewing draft。
3. 在块编辑模式修改 `今日重点`。
4. 保存为草稿。
5. 确认正式文件未立即更新。
6. 点击确认写入正式日记。
7. 检查 `%LocalAppData%/Journal/entries/yyyy/MM/yyyy-MM-dd.md` 更新。
8. 切到源码模式，故意破坏 marker。
9. 保存后看到 attention 和修复提示，正式 entry 不被覆盖。

## 12. 完成标准

阶段 3 完成时必须满足：

- 后端有 JMF parser、validator、composer。
- 块编辑模式能从 JMF Markdown 加载 section model。
- `raw-inputs` 在块编辑中只读。
- 用户能编辑必需内容块和已存在可选内容块。
- 用户能从 JMF 可选单例块菜单新增块。
- 新增块按固定顺序插入。
- 源码模式能保存完整 Markdown 到 draft。
- 保存前结构校验覆盖 front matter、schema、必需块、marker 成对、重复块、未知块。
- 校验失败进入 `attention`，不覆盖正式 entry。
- 校验成功只保存 reviewing draft，确认后才写正式 entry。
- 后端测试、前端测试和前端构建通过。
- README 或阶段文档更新阶段 3 启动与边界说明。

## 13. 后续衔接

阶段 4 继续处理：

- 版本快照。
- SQLite 索引库。
- FTS5 全文搜索。
- 文件 hash 扫描与索引重建。
- Markdown -> JSON/索引模型解析。
- 外部文件异常检测。

阶段 5 再接入真实 AI Provider。
