# Phase 2 JMF Generation Confirmation

- Date: `2026-05-08`
- Topic slug: `phase-2-jmf-generation-confirmation`
- Status: `Archived`
- Scope: `Feature`
- Tags: `phase-2`, `jmf`, `journal-workflow`, `mock-ai`, `desktop`

## Summary

阶段 2 交付了 Journal 第一条真实日记主链路：用户在今日工作台输入自然语言，后端追加 raw input JSONL，规则型 Mock AI 生成结构化 JSON，校验后渲染 JMF v1 Markdown 草稿，前端只读预览，用户确认后写入当天正式 Markdown。

这次实现把原本可能膨胀的编辑器能力继续留给阶段 3，只关闭“生成、预览、确认、落盘”的纵向切片。V3 原型中的“日记纸面优先”也落入前端：默认桌面窗口以日记预览为视觉中心，输入和文件状态作为辅助 Dock。

## Delivered Scope

- 新增阶段 2 领域模型、raw input JSONL 存储、draft/entry 文件存储和今日工作流编排服务。
- 提供 `GET /journal/today`、`POST /journal/today/inputs`、`POST /journal/today/draft/confirm` 三个 Minimal API endpoint。
- 实现规则型 `MockAiProvider`、`JournalAiJsonValidator` 与 JMF v1 Markdown renderer，包含 front matter、section marker、YAML 转义和 section 内容防 marker 注入。
- 前端升级为 Today Workbench，支持今日状态加载、自然语言提交、Markdown 只读预览、attention 错误呈现、确认写入正式文件和 entry path 展示。
- 前端实现 V3 响应式布局：宽屏三栏、默认桌面两栏、最小窗口保留日记纸面和紧凑操作入口。
- Markdown 预览隐藏 YAML front matter 和 JMF 机器 marker，避免用户把只读预览误认为源码编辑器。
- README 补充阶段 2 API、开发期数据文件路径和“阶段 2 不做块编辑/源码编辑”的边界。

## Out of Scope

- 不包含块编辑模式、源码模式、草稿正文手动编辑或保存前 JMF 结构保护。
- 不包含版本快照、SQLite 索引、Markdown 反向解析、真实 AI Provider 或 Provider 配置页。
- 不包含多日期浏览、同日年轮、未来日记提醒、安装包或 Electron 托管 .NET 后端进程。

## Verification Snapshot

- `dotnet test Journal.slnx`：31/31 .NET tests passed。
- `npm test --prefix apps/desktop`：11/11 frontend tests passed。
- `npm run build --prefix apps/desktop`：TypeScript + Vite build passed。
- `git diff --check`：无 whitespace error。
- API 冒烟：`GET /journal/today` 返回 `empty`，`POST /journal/today/inputs` 返回 `reviewing`，`POST /journal/today/draft/confirm` 返回 `processed` / `updated`。
- 文件冒烟：`%LocalAppData%/Journal/entries/2026/05/2026-05-08.md`、raw input JSONL、draft Markdown、draft meta JSON 均已写出。
- Playwright 截图验证：`1180x780` 与 `960x640` 下 Today Workbench 可加载 `API ok`，日记纸面显示中文 section 标题，未暴露 YAML front matter 或 `journal:section` marker。
- JMF renderer 回归验证：YAML front matter 会 quote `#` 注释、flow indicator、alias/anchor、布尔/null、首尾空白、数字和日期形态的字符串标量。

## Source Documents

- Spec: [2026-05-08-phase-2-jmf-generation-confirmation-design.md](../../specs/2026-05-08-phase-2-jmf-generation-confirmation-design.md)
- Visual: [2026-05-08-phase-2-today-workbench-prototype.html](../../specs/2026-05-08-phase-2-today-workbench-prototype.html)
- Plan: [2026-05-08-phase-2-jmf-generation-implementation-plan.md](../../plans/2026-05-08-phase-2-jmf-generation-implementation-plan.md)

## Related Problems

- None.

## Notes

- 运行态验证时 `5057` 已被本机其他进程占用，改用临时 API 端口验证主链路。
- Chromium 会阻止部分 unsafe port；`5061` 会导致浏览器 `fetch` 报 `net::ERR_UNSAFE_PORT`，最终使用 `5077` 完成前端运行态验证。
