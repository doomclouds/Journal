# Phase 3 JMF Editor

- Date: `2026-05-09`
- Topic slug: `phase-3-jmf-editor`
- Status: `Archived`
- Scope: `Feature`
- Tags: `phase-3`, `jmf`, `editor`, `validation`, `desktop`

## Summary

阶段 3 交付了 JMF 的安全编辑层：今日工作台从只读预览升级为块编辑和源码编辑双模式，后端把 draft / entry Markdown 解析为 JMF document，保存前执行结构校验，成功只写 reviewing draft，失败进入 attention，正式 entry 仍必须由用户确认后才更新。

## Delivered Scope

- 新增 JMF v1 section catalog、parser、validator、composer，覆盖 front matter、schema、必需块、未知块、重复块、marker 成对、孤立/嵌套 marker 和 readonly section 请求。
- `TodayJournalService` 增加 `GetTodayEditorAsync`、`SaveBlockDraftAsync`、`SaveSourceDraftAsync`，保存成功只写 draft，校验失败写 attention draft，`raw-inputs` 和系统块在块编辑中从 baseline 保留。
- API 新增 `GET /journal/today/editor`、`PUT /journal/today/editor/blocks`、`PUT /journal/today/editor/source`，并将坏请求稳定映射为 400 / 409。
- 前端新增 JMF editor API client、块编辑组件、源码模式、可选单例块插入菜单、校验提示和 Today Workbench 集成。
- 编辑器将保存动作固定在顶部工具条，pending 时禁用 raw input、block/source textarea 和保存/确认按钮，避免旧响应覆盖用户输入。
- README 补充阶段 3 API、编辑边界和未包含能力。

## Out of Scope

- 不包含版本快照、SQLite 索引、全文搜索、真实 AI Provider 或 AI 改写。
- 不包含富文本/WYSIWYG 编辑器、拖拽排序、自定义 JMF 外 section、自动保存、多日期浏览或多窗口冲突处理。
- 不包含安装包、生产期 Electron 托管后端进程和 Provider 配置页。

## Verification Snapshot

- `dotnet test Journal.slnx`：83/83 .NET tests passed。
- `npm test --prefix apps/desktop`：30/30 frontend tests passed。
- `npm run build --prefix apps/desktop`：TypeScript + Vite build passed。
- Playwright 运行态验证：临时 API `http://localhost:5091` + Vite `http://127.0.0.1:5173` 加载 Today Workbench，JMF block editor 可见，顶部保存动作进入首屏。
- Code review checkpoints：Task 1-7 均经过 spec compliance 和 code quality review；parser 补齐孤立 end marker、嵌套 start marker、非法 section id 回归，前端补齐 pending/stale response/editor buffer 回归，后端补齐 source editor baseline 回归。

## Source Documents

- Spec: [2026-05-09-phase-3-jmf-editor-design.md](../../specs/2026-05-09-phase-3-jmf-editor-design.md)
- Visual: [2026-05-09-phase-3-jmf-editor-prototype.html](../../specs/2026-05-09-phase-3-jmf-editor-prototype.html)
- Plan: [2026-05-09-phase-3-jmf-editor-implementation-plan.md](../../plans/2026-05-09-phase-3-jmf-editor-implementation-plan.md)

## Related Problems

- None.

## Notes

- 实施过程中澄清了 system readonly section 的错误码：`raw-inputs` 使用 `raw-inputs-is-readonly`，`keywords` / `metadata-note` 使用 `readonly-section`。
- 运行态验证时临时使用 `5091` 和 `5173`；控制台唯一错误为 `favicon.ico` 404，不影响应用功能。
