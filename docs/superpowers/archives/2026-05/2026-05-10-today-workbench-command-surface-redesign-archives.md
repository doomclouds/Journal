# Today Workbench Command Surface Redesign

- Date: `2026-05-10`
- Topic slug: `today-workbench-command-surface-redesign`
- Status: `Archived`
- Scope: `UI`
- Tags: `today-workbench`, `command-surface`, `llm-settings`, `desktop-ui`

## Summary

本次交付把 Journal 今日主界面从上一轮“日记纸面优先”的产品化雏形推进为更完整的桌面命令界面：真实运行界面直接贴合 Electron 内容区，不再内嵌原型展示用的假窗口 titlebar 或模拟菜单栏；窗口菜单改为 Electron 原生中文菜单，内容区顶部保留今日上下文，左侧沉淀原始对话上下文，中间保持日记纸面与段落编辑，右侧提供今日助手和整理状态；同时把 LLM 配置面板对齐同一套视觉语言，但不把日记下一步工作流塞进配置页。

## Delivered Scope

- 主界面形成 `Electron 原生中文菜单 + 顶部今日上下文 + 左侧今日上下文 + 中间日记纸面 + 右侧今日助手` 的 command-surface 布局，并明确排除 HTML 原型外层窗口壳和 React 内模拟菜单栏。
- `只看日记 / 日记 + 助手` 视图切换已接入真实状态：只看日记时右侧 Today Assistant 隐藏，日记纸面获得更宽的阅读空间。
- 原始输入改为左侧可折叠原始对话，同时右侧保留今日材料摘要，避免把 JMF 源码或调试字段暴露给日常用户。
- 原始对话正文和今日材料恢复原型里的圆角加重引用符号，而不是简单裸直线。
- 原生菜单完成中文化，`文件 -> LLM 配置` 通过 preload bridge 打开 LLM 配置面板；保存日记、重新整理和插入段落仍保留在日记工作台内部按钮，不实现额外业务快捷键，也不显示 `Ctrl+` / `Alt+` 提示。
- 空白日记状态不再把空 Markdown 的 JMF 校验诊断展示成“需要处理”，而是稳定显示“待开始”。
- LLM 配置页只处理供应商、模型参数、API Key、配置来源、诊断和高级参数，整理方式仅静态展示 `忠实整理`。
- LLM 配置页恢复供应商字母圆标和当前 LLM 字母圆标，保持与 HTML 原型一致的视觉锚点。
- 反馈消息被固定到独立 grid 行，错误或校验提示出现时不会把主工作台挤到隐式行。
- `stop-journal-dev.ps1` 修复进程停止竞态：只吞掉 PID 已消失的良性情况，仍存在但停止失败会继续抛错。

## Out of Scope

- 不实现快捷键监听、快捷键说明或菜单命令体系的全量功能。
- 不恢复 JMF 源码检查器、源码模式、完整 Markdown 源码编辑或高级源码入口。
- 不实现 AI 整理风格切换；`忠实整理` 只是当前固定提示词能力的界面表达。
- 不改变后端 JMF、raw input、正式 Markdown 存储、LLM provider 调用合同。

## Verification Snapshot

- `npm test --prefix apps/desktop`：`96 passed`
- `npm run build --prefix apps/desktop`：通过 TypeScript 与 Vite 构建。
- `dotnet test Journal.slnx`：`132 passed`
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests\stop-journal-dev.Tests.ps1`：通过 stop 脚本错误处理合同。
- `git diff --check`：通过。
- Playwright CLI 对 `http://127.0.0.1:5173` 做桌面和 960px 窄屏截图：`output/playwright/today-workbench-command-surface-fixed-1440.png`、`output/playwright/today-workbench-command-surface-fixed-960.png`。截图确认内容区贴合原型：左侧上下文、中间日记纸面、右侧 Today Assistant，且无 React 内假窗口壳。
- 最终只读 code review 发现的两个 Important 已修复：feedback grid 行分配和 stop 脚本 catch 过宽。

## Source Documents

- Spec: [2026-05-10-today-workbench-command-surface-redesign-design.md](../../specs/2026-05-10-today-workbench-command-surface-redesign-design.md)
- Visual: [2026-05-10-today-workbench-command-surface-prototype.html](../../specs/2026-05-10-today-workbench-command-surface-prototype.html)
- Plan: [2026-05-10-today-workbench-command-surface-redesign-implementation-plan.md](../../plans/2026-05-10-today-workbench-command-surface-redesign-implementation-plan.md)

## Related Problems

- None.

## Notes

- 这是对上一份 `today-workbench-productized-ux` 归档的后续升级：前一版解决“像调试面板”，本轮进一步确立菜单层、左侧上下文、右侧助手和 LLM 配置视觉一致性。
