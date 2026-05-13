# Journal Generated Content Blank Line Inflation

- Date: `2026-05-13`
- Topic slug: `journal-generated-content-blank-line-inflation`
- Status: `Captured`
- Scope: `UX`
- Tags: `harness-core`, `journal-editor`, `formatting`, `frontend`

## Symptom

AI harness 插入或创建 section 后，今日工作台阅读态出现明显的大块空白：同一 section 内的条目之间、AI 新插入内容和原有内容之间衔接很突兀。用户添加很多 raw inputs 后，`raw-inputs` / 今日材料区也会变得很长，挡住下面真正要看的日记正文。后续还观察到模型会把目标 section 标题（例如 `## 情绪状态`）放进工具 `content` 参数，导致 UI 里同一个标题出现两次。历史版本改用 Markdown 渲染后，又暴露出当前日记和 Today 阅读态仍按普通文本逐行渲染，导致同一份日记在不同入口视觉风格不一致。2026-05-14 又观察到模型把新增内容作为整段 paragraph 传入工具参数，section 里没有形成一条一条的 bullet item。

## Trigger / Context

- Harness tool call 的 `content` 参数包含多余空行，例如 `\n\n- item\n\n\n- item`。
- Harness tool call 的 `content` 参数可能是一整段自然语言，而不是 `- item` 列表。
- `JournalHarnessOperationExecutor` 原样保留 AI 新内容里的空行，只做整体 `Trim()`。
- `AppendContent` 在已有 section 和 AI append 内容之间主动使用 `\n\n` 拼接，导致即使 AI 内容已规范化，仍会额外插入一个空白分隔行。
- Harness tool call 的 `content` 参数可能包含目标 section 的 Markdown 标题，例如 `## 情绪状态\n\n- ...`。
- `HistoryWorkbench` 当前日记详情优先渲染 parsed sections，而历史版本详情走 Markdown 内容，两个预览入口视觉不同。
- `JournalBlockCard` 预览层把每一行都渲染成一个 `<p>`，空行也会渲染成 `&nbsp;` 段落；列表项会显示成带 `-` 的普通文本，而不是 Markdown 列表。
- `raw-inputs` 是系统材料区，内容会随用户输入增长，但阅读态默认完整展开。

## Root Cause

后端和前端都把模型输出当成已经排版好的 Markdown 片段：

- 后端只用 `AppendContent(existing, operation.Content)` 做拼接，没有对 AI 片段内部的空行和行首尾空白做规范化。
- `AppendContent` 把 append 当成 Markdown 段落拼接，默认加空白分隔行；但 Journal 的 section 内容主要是 bullet list，这会让列表被拆成视觉上不连续的两段。
- Prompt 要求模型对 section 操作，但没有足够强地说明 `content` 只能是 section body，不能包含 JMF 标题；执行器也没有把“目标 section 标题”视为可剥离的模型包装文本。
- 前端预览层没有压缩连续空行，导致一个模型生成的空行序列被放大成多个可见段落。
- 今日材料区和正文 section 采用同样的默认展开策略，系统上下文材料越多，越容易压住用户真正关心的日记内容。

## Fix

当前修复已落地：

- 后端对 harness AI 生成的新 `content` 做行级规范化：统一换行、去掉空行、去掉每行首尾空白。
- 后端把 harness AI 生成的新 `content` 规范为 Markdown bullet list；整段 paragraph、`* item`、`• item` 和短编号列表都会进入统一的 `- item` 形态。
- 后端在规范化 AI 工具新内容时剥离与目标 section 匹配的 leading Markdown heading，例如 `## 情绪状态` 或 `## mood`，避免模型把 block 标题写入正文。
- 仅规范化 AI 工具新内容，不裁剪用户已有 section 内容，避免破坏用户手写 Markdown。
- 后端 append 拼接改为单换行，并压掉已有内容末尾的空白行，避免“修完 AI 内容后又由拼接器插入一行空白”。
- 前端阅读态压缩连续空行，空白段落使用更小的视觉间距。
- `raw-inputs` / 今日材料默认折叠，只保留标题和展开按钮；用户需要看原始材料时再展开。
- 历史工作台中间主预览在选中版本时渲染指定版本 Markdown；未选版本但已加载当前详情时，也用同一个 `MarkdownPreview` 渲染当前正式日记 Markdown。
- Today 页 `JournalBlockCard` 阅读态改用共享 `MarkdownPreview` 渲染 section content，并用局部 CSS 重置 padding、宽度、列表间距，保留 block 编辑按钮和 raw inputs 折叠行为。
- 补回归测试覆盖 AI append/upsert 空行规范化、阅读态连续空行压缩、今日材料默认折叠和展开。
- 补前端回归测试覆盖历史当前详情 Markdown 渲染、历史版本主预览渲染、Today section Markdown list 渲染。

## Why This Fix

只在 CSS 上缩小段落间距不够，因为 Markdown 文件里仍会持续积累模型带来的多余空行。只在后端清理也不够，因为历史 draft/entry 里已经存在的空行仍会被前端预览放大。两层一起收口可以同时修复新数据和已有数据。

今日材料默认折叠是布局层保护：raw inputs 是审计和上下文材料，不应默认抢占正文阅读空间。

## Recognition Clues

- UI 截图里同一 section 的 bullet 之间出现多倍空白。
- UI 截图里 section 标题下方又出现一行同名 Markdown 标题，例如 `情绪状态` 标题下显示 `## 情绪状态`。
- Markdown section content 中可以看到连续空行。
- Harness audit/tool call 中 `content` 第一行是目标 section 的 `## <title>`。
- 最新 AI append 内容本身已经没有空行，但已有内容和追加内容之间仍出现一个空白行。
- AI audit/tool call 中 `content` 是整段自然语言，没有 `- ` 前缀。
- 历史版本预览比当前日记详情更像正式 Markdown，当前详情仍像搜索摘要卡片。
- `JournalBlockCard.renderPreview` 对空行生成多个 `<p>`，列表项文本里保留 literal `- `。
- 用户说“今日材料很长，挡住下面日记内容”。

## Applicability / Non-Applicability

### Applies When

- AI/harness/LLM 输出的 section content 被直接写入 JMF。
- 阅读态按行渲染 Markdown 内容，而不是完整 Markdown parser 渲染。
- 同一份 JMF Markdown 在历史版本、历史当前详情、Today block 阅读态之间走不同渲染组件。
- 系统材料区会随输入增长，并与正文出现在同一滚动阅读流里。

### Does Not Apply When

- 用户在 source mode 中明确写了用于 Markdown 段落分隔的空行；source mode 不应被前端保存时静默改写。
- 内容是代码块、表格或需要保留内部空行的专门 Markdown section；这类内容不应通过当前 harness 简单 append 工具生成。
- 问题来自 CSS 全局字号、窗口缩放或系统显示比例。

## Related Artifacts

- Spec: [2026-05-12-journal-harness-core-design.md](../../specs/2026-05-12-journal-harness-core-design.md)
- Plan: [2026-05-12-journal-harness-core-implementation-plan.md](../../plans/2026-05-12-journal-harness-core-implementation-plan.md)
- Archive: [2026-05-12-journal-harness-core-archives.md](../../archives/2026-05/2026-05-12-journal-harness-core-archives.md)
- Related Problems:
  - [2026-05-13-deepseek-thinking-tool-call-reasoning-content-problem.md](./2026-05-13-deepseek-thinking-tool-call-reasoning-content-problem.md)
- Code or Test:
  - [JournalHarnessOperationExecutor.cs](../../../../src/Journal.Infrastructure/Harness/JournalHarnessOperationExecutor.cs)
  - [JournalBlockCard.tsx](../../../../apps/desktop/src/JournalBlockCard.tsx)
- [JournalHarnessOperationExecutorTests.cs](../../../../tests/Journal.Tests/JournalHarnessOperationExecutorTests.cs)
- [App.test.tsx](../../../../apps/desktop/src/App.test.tsx)
