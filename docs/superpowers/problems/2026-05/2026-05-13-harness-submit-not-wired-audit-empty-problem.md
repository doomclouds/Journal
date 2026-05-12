# Harness Submit Not Wired Leaves Audit Empty

- Date: `2026-05-13`
- Topic slug: `harness-submit-not-wired-audit-empty`
- Status: `Captured`
- Scope: `Feature`
- Tags: `harness-core`, `frontend`, `audit`, `workflow`

## Symptom

用户在今日工作台正常提交日记内容后，打开 AI 审计工作台，看不到任何 harness run 记录。

后端已经有 `POST /journal/today/harness/runs`、SSE 和 `GET /journal/audit?date=yyyy-MM-dd`，前端也有 `startHarnessRun` / `openHarnessRunEvents` API helper，但实际主输入提交没有产生审计记录。

## Trigger / Context

- Phase 6 交付了后端 harness run API 和前端审计工作台。
- 用户从底部输入框提交当前日记内容。
- 用户随后点击 Today Assistant 的 `查看审计`。
- 审计页只读取已有 audit run，不会反向生成审计记录。

## Root Cause

前端主提交路径仍然调用旧的 `addTodayInput(trimmedInput)`，也就是 `POST /journal/today/inputs`。该旧路径会追加 raw input 并生成/刷新草稿，但不会创建 harness run，也不会写 `.journal/audit/.../<runId>.json`。

`startHarnessRun` 和 `openHarnessRunEvents` 只作为 API client 合同存在，未接入 `handleSubmit`。因此审计工作台本身可以查询记录，但正常用户工作流不会产生记录。

## Fix

当前修复已落地：

- 将 `App.tsx` 的 `handleSubmit` 从 `addTodayInput` 切到 `startHarnessRun`。
- `POST /journal/today/harness/runs` 返回 run 后，前端立即打开 `openHarnessRunEvents(run.id, ...)`。
- SSE 完成、失败或已完成重连后刷新 `getTodayEditor()`，并让审计工作台可以读到新 run。
- 补回归测试：提交输入应请求 `/journal/today/harness/runs`，不再请求 `/journal/today/inputs`；SSE 完成后刷新日记和审计记录。
- 保留旧 `POST /journal/today/inputs` 作为兼容或开发路径时，文档必须明确它不会产生 harness audit。

## Why This Fix

审计记录的事实来源是 harness run record，不是 raw input 或 draft 本身。让审计页“猜测”旧输入会制造伪审计，反而破坏可追溯性。正确修法是把用户主提交工作流接入 harness run，让输入、工具调用、draft 写入和审计记录来自同一条链路。

## Recognition Clues

- `GET /journal/audit?date=...` 返回空数组，但当天 raw input 和 draft 都存在。
- `App.tsx` 的提交处理仍出现 `addTodayInput(trimmedInput)`。
- `api.ts` 有 `startHarnessRun`，但除测试外没有生产调用点。
- 审计工作台 UI 可打开，问题发生在“没有 run 被创建”，不是列表渲染失败。

## Applicability / Non-Applicability

### Applies When

- 功能交付了 audit viewer，但主用户路径仍走旧输入接口。
- 后端 run/audit API 存在，前端 API helper 也存在，但 UI submit 没接上。
- 用户期望“提交一次日记输入后，审计页能看到本次整理过程”。

### Does Not Apply When

- 用户手动调用了 `/journal/today/harness/runs` 但 audit 仍为空；那应查 run store 或 SSE execution。
- 后端 audit 文件存在但 UI 不显示；那应查 `getJournalAudit` 或日期筛选。
- 旧历史 raw inputs 没有审计记录；除非做迁移，否则旧数据本来没有 run lineage。

## Related Artifacts

- Spec: [2026-05-12-journal-harness-core-design.md](../../specs/2026-05-12-journal-harness-core-design.md)
- Plan: [2026-05-12-journal-harness-core-implementation-plan.md](../../plans/2026-05-12-journal-harness-core-implementation-plan.md)
- Archive: [2026-05-12-journal-harness-core-archives.md](../../archives/2026-05/2026-05-12-journal-harness-core-archives.md)
- Related Problems:
  - None yet.
- Code or Test:
  - [App.tsx](../../../../apps/desktop/src/App.tsx)
  - [api.ts](../../../../apps/desktop/src/api.ts)
