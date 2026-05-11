# LLM Settings UX Polish

- Date: `2026-05-10`
- Topic slug: `llm-settings-ux-polish`
- Status: `Archived`
- Scope: `UI`
- Tags: `llm`, `settings`, `api-key`, `ux`, `desktop`

## Summary

本轮把 LLM 设置面板从工程调试面板收口成可理解、可操作的配置界面：用户可以明确看到 Provider 是否可用、API Key 来自哪里、当前表单测试了什么，以及只有通过连接测试后配置才会保存并启用；重新整理草稿也回到今日页主工作流，避免设置页直接覆盖日记内容。

## Delivered Scope

- 后端安全设置视图补充 `apiKeyPreview`、`canRevealApiKey` 和 file-backed Key reveal endpoint；`GET /settings/ai` 仍不返回完整 Key，环境变量 Key 不可 reveal。
- `POST /settings/ai/test` 支持候选配置，`POST /settings/ai/activate` 先做最小连接测试，成功才保存并启用，失败不污染当前可用配置。
- 前端 LLM 设置面板改为 Provider 状态、基础配置、候选测试、保存并启用、诊断与高级摘要的三列配置界面，并使用 `lucide-react` 的 Eye/EyeOff/LockKeyhole 图标。
- API Key 输入避免把遮罩串当作可编辑真实值；file-backed Key 默认隐藏、可临时查看，保存成功后清除明文状态。
- “重新整理今日草稿”移到今日页，并补齐编辑、输入、切换面板等动作会重置二次确认的交互保护。
- 2026-05-11 跟进：LLM 配置面板收敛为带遮罩的居中模态弹窗，关闭、测试连接、保存启用和高级参数展开改为图标按钮；Provider 列表删除多余标题说明，降低后台表单感。
- 2026-05-11 跟进：高级参数保留专业命名 `temperature`、`max tokens`、`timeout`、`JSON mode`，默认用 chip 摘要展示，展开后仅保留输入框与 `title` tooltip 说明，不再在字段下方重复显示解释文字。

## Out of Scope

- 不新增 Provider preset、模型列表在线拉取、API Key 加密、token 成本统计或差异预览。
- 不改动 LLM prompt、JMF validator、AI 输出合同或正式日记写入规则。
- 不处理生产 Electron 后端托管、安装器或云同步。

## Verification Snapshot

- `dotnet test Journal.slnx`：137/137 .NET tests passed。
- `npm test --prefix apps/desktop`：63/63 frontend tests passed。
- `npm run build --prefix apps/desktop`：TypeScript + Vite build passed。
- Final review 修复：补上环境变量只覆盖非 Key 字段时 file-backed Key 仍可 reveal 的边界；补上遮罩 API Key 不进入 editable value、reveal 后编辑显示值与 candidate 同步的回归。
- Visual smoke：临时启动 API `http://127.0.0.1:5058` 和 Vite `http://localhost:5173`，Playwright 打开 LLM 设置面板并截图检查高级摘要换行、Key 行、测试与保存按钮可见。
- Responsive follow-up：小窗口单列布局改为只保留 settings grid 一个滚动容器，避免 provider 列表、主表单、侧栏和页面本体同时出现滚动条；补充 CSS 合同测试和 Playwright 960×620 运行态检查。
- 2026-05-11 模态与高级参数跟进：`npm test --prefix apps/desktop -- src/App.test.tsx src/styles.test.ts` 通过，`91 passed`；`npm run build --prefix apps/desktop` 通过 TypeScript 与 Vite 构建。

## Source Documents

- Spec: [2026-05-10-llm-settings-ux-polish-design.md](../../specs/2026-05-10-llm-settings-ux-polish-design.md)
- Visual: [2026-05-10-llm-settings-ux-polish-prototype.html](../../specs/2026-05-10-llm-settings-ux-polish-prototype.html)
- Visual follow-up: [2026-05-10-today-workbench-command-surface-prototype.html](../../specs/2026-05-10-today-workbench-command-surface-prototype.html)
- Plan: [2026-05-10-llm-settings-ux-polish-implementation-plan.md](../../plans/2026-05-10-llm-settings-ux-polish-implementation-plan.md)

## Related Problems

- [Vite Loopback Origin CORS Drift](../../problems/2026-05/2026-05-07-vite-loopback-cors-problem.md)

## Notes

- 历史 AI Provider 设计与原型中仍保留旧文案，这是需求历史，不作为本轮运行态清理项；当前 LLM settings polish spec 和实现已经替换运行时交互合同。
