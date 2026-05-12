# Journal Generated Content Blank Line Inflation

- Date: `2026-05-13`
- Topic slug: `journal-generated-content-blank-line-inflation`
- Status: `Captured`
- Scope: `UX`
- Tags: `harness-core`, `journal-editor`, `formatting`, `frontend`

## Symptom

AI harness 插入或创建 section 后，今日工作台阅读态出现明显的大块空白：同一 section 内的条目之间、AI 新插入内容和原有内容之间衔接很突兀。用户添加很多 raw inputs 后，`raw-inputs` / 今日材料区也会变得很长，挡住下面真正要看的日记正文。

## Trigger / Context

- Harness tool call 的 `content` 参数包含多余空行，例如 `\n\n- item\n\n\n- item`。
- `JournalHarnessOperationExecutor` 原样保留 AI 新内容里的空行，只做整体 `Trim()`。
- `JournalBlockCard` 预览层把每一行都渲染成一个 `<p>`，空行也会渲染成 `&nbsp;` 段落。
- `raw-inputs` 是系统材料区，内容会随用户输入增长，但阅读态默认完整展开。

## Root Cause

后端和前端都把模型输出当成已经排版好的 Markdown 片段：

- 后端只用 `AppendContent(existing, operation.Content)` 做拼接，没有对 AI 片段内部的空行和行首尾空白做规范化。
- 前端预览层没有压缩连续空行，导致一个模型生成的空行序列被放大成多个可见段落。
- 今日材料区和正文 section 采用同样的默认展开策略，系统上下文材料越多，越容易压住用户真正关心的日记内容。

## Fix

当前修复已落地：

- 后端对 harness AI 生成的新 `content` 做行级规范化：统一换行、去掉空行、去掉每行首尾空白。
- 仅规范化 AI 工具新内容，不裁剪用户已有 section 内容，避免破坏用户手写 Markdown。
- 前端阅读态压缩连续空行，空白段落使用更小的视觉间距。
- `raw-inputs` / 今日材料默认折叠，只保留标题和展开按钮；用户需要看原始材料时再展开。
- 补回归测试覆盖 AI append/upsert 空行规范化、阅读态连续空行压缩、今日材料默认折叠和展开。

## Why This Fix

只在 CSS 上缩小段落间距不够，因为 Markdown 文件里仍会持续积累模型带来的多余空行。只在后端清理也不够，因为历史 draft/entry 里已经存在的空行仍会被前端预览放大。两层一起收口可以同时修复新数据和已有数据。

今日材料默认折叠是布局层保护：raw inputs 是审计和上下文材料，不应默认抢占正文阅读空间。

## Recognition Clues

- UI 截图里同一 section 的 bullet 之间出现多倍空白。
- Markdown section content 中可以看到连续空行。
- `JournalBlockCard.renderPreview` 对空行生成多个 `<p>`。
- 用户说“今日材料很长，挡住下面日记内容”。

## Applicability / Non-Applicability

### Applies When

- AI/harness/LLM 输出的 section content 被直接写入 JMF。
- 阅读态按行渲染 Markdown 内容，而不是完整 Markdown parser 渲染。
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
