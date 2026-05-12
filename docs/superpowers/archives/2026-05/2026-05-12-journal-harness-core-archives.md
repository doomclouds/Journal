# Journal Harness Core Delivery

- Date: `2026-05-12`
- Topic slug: `journal-harness-core`
- Status: `Archived`
- Scope: `Feature`
- Tags: `phase-6`, `harness-core`, `llm`, `audit`, `jmf`, `desktop`

## Summary

阶段 6 将 Journal 的真实 LLM 从“整篇生成器”收束为 Harness Core：模型读取 protected context 和当前 user message，只能通过 side-effect-free 工具规划 append / upsert / revise AI section / no-op 操作；服务端负责执行、校验、写 draft 和记录审计，正式 Markdown entry 仍由用户确认。

## Delivered Scope

- JMF section marker 支持 section 级 provenance；parser/composer 能读取和写回来源属性，块编辑会把用户触碰过的 section 标记为 user edit。
- Harness operation executor 落地 append、upsert、revise AI-generated section 和 no-op 约束，保护 `raw-inputs`、system section、只读 section 和用户已编辑内容不被删除、清空或替换。
- Prompt split 已实现：历史 raw inputs、draft、entry 进入 protected context，当前输入作为 user message；Agent Framework planner 工具只收集计划，不产生副作用。
- 新增 harness run API、SSE 事件流和 audit store；工具执行只写 `reviewing` / `attention` draft，不直接写 `entries/`。
- 桌面端在现有三栏 command workspace 内新增 AI 审计工作台，支持按日期查看 harness run、工具调用、拒绝原因和运行摘要。
- Task 7/8 已补 SSE disconnect/concurrent execution hardening、重复执行 gate、audit workbench dirty guard、stale guard 和 SSE parse handling。

## API List

- `POST /journal/today/harness/runs`：追加当前 raw input，创建 queued harness run，返回 run id 和今日状态。
- `GET /journal/harness/runs/{runId}`：按 run id 查询持久化 audit run。
- `GET /journal/harness/runs/{runId}/events`：通过 `TypedResults.ServerSentEvents(...)` / `SseItem<T>` 执行并推送 run 状态事件。
- `GET /journal/audit?date=yyyy-MM-dd`：按日期列出当天 harness run。

## Storage Paths

- `%LocalAppData%/Journal/.journal/audit/yyyy/MM/yyyy-MM-dd/<runId>.json`：每次 harness run 的审计记录。
- `%LocalAppData%/Journal/.journal/raw-inputs/yyyy/MM/yyyy-MM-dd.jsonl`：当前输入仍先追加到 raw inputs。
- `%LocalAppData%/Journal/.journal/drafts/yyyy/MM/yyyy-MM-dd.md` 和 `.meta.json`：harness 执行结果只写 draft 与 meta。
- `%LocalAppData%/Journal/entries/yyyy/MM/yyyy-MM-dd.md`：正式 entry 仍只由用户确认流程写入。

## Out of Scope

- 不包含 item 级 provenance、用户授权删除/隐藏、draft diff、rollback、版本快照、SQLite 索引/搜索或多日期日记浏览。
- 不包含自由 Markdown patch、AI 直接写正式 entry、完整 Agent workflow 记忆系统、对话式改写 UI 或语音/录音能力。
- 不包含生产 Electron 托管 .NET 后端、安装包或跨进程持久队列恢复。

## Verification Snapshot

- `dotnet test Journal.slnx`：172/172 .NET tests passed。
- `npm test --prefix apps/desktop`：120/120 frontend tests passed。首次运行暴露 `styles.css` 工作区 mixed line endings 导致的 Windows 字符串断言失败；规范回 LF 后无内容 diff，复跑通过。
- `npm run build --prefix apps/desktop`：TypeScript build 和 Vite production build passed。
- `git diff --check`：Task 9 文档 diff whitespace check passed。
- Archive validator：`validate_archive_asset.py docs/superpowers/archives/2026-05/2026-05-12-journal-harness-core-archives.md` passed。

## Known Follow-up Items

- item 级 provenance。
- 用户授权删除/隐藏流程。
- draft diff 和单次 harness run rollback。
- 多日期日记浏览后，审计日期与日记日期的联动。

## Source Documents

- Spec: [2026-05-12-journal-harness-core-design.md](../../specs/2026-05-12-journal-harness-core-design.md)
- Visual: [2026-05-12-journal-harness-audit-workbench-prototype.html](../../specs/2026-05-12-journal-harness-audit-workbench-prototype.html)
- Plan: [2026-05-12-journal-harness-core-implementation-plan.md](../../plans/2026-05-12-journal-harness-core-implementation-plan.md)

## Related Problems

- None.

## Notes

- 本归档记录 Phase 6 第一阶段交付边界；旧 `POST /journal/today/draft/regenerate` 仍可保留为既有整篇整理路径，但新用户工作流已具备 harness run 入口。
