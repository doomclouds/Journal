# Harness SSE Run Lifecycle Re-Execution

- Date: `2026-05-12`
- Topic slug: `harness-sse-run-lifecycle`
- Status: `Captured`
- Scope: `Feature`
- Tags: `harness-core`, `sse`, `concurrency`, `audit`

## Symptom

Harness run 的 SSE endpoint 在断线、重连或并发首次连接时，存在重复执行 planner、重复写 draft/audit，或者客户端断开导致后台 run 被取消的风险。

## Trigger / Context

- `POST /journal/today/harness/runs` 先创建 queued run。
- 前端通过 `GET /journal/harness/runs/{runId}/events` 打开 SSE。
- 第一阶段让 SSE request 承担实际执行流。
- 用户刷新、网络断开、前端重复打开事件流，或两个客户端同时连接同一 queued run。

## Root Cause

SSE 是用户体验通道，但实现上也承担了执行触发。若直接把客户端 request cancellation token 传进执行链，断线会取消后台 run；若缺少 per-run gate 和 terminal state 检查，并发或重连会让同一 run 被执行多次。

这会破坏 harness 的核心事实来源：一个 run id 应对应一次工具规划和一次审计记录。

## Fix

- SSE 客户端断开只停止该客户端读取，不取消实际 run 执行。
- 对每个 run id 加执行 gate，确保 queued -> running 只有一个执行者。
- 对 terminal run 返回 `run-already-completed` / status event，不重跑 planner。
- 对并发第二连接返回当前 run status，不重复写 audit。
- 增加 service/endpoint 测试覆盖：
  - SSE 断线后后台执行继续完成。
  - 并发首次连接不会重复执行 planner。
  - 完成后重连不重复执行。

## Why This Fix

run record 才是可靠性边界，SSE 只是进度显示。把客户端连接生命周期和后端执行生命周期解耦，才能支持慢模型、长运行和用户刷新，同时避免重复工具调用污染日记。

## Recognition Clues

- 同一个 run id 出现多组 tool calls 或重复 draft updated。
- 断开 SSE 后 run 长时间停在 `running` 或变成失败，但模型/工具本应继续。
- 刷新审计页或重开事件流后，planner 再次被调用。
- 并发测试中 collector/planner 调用次数大于 1。

## Applicability / Non-Applicability

### Applies When

- SSE endpoint 同时承担进度推送和后端执行触发。
- run id 应保证幂等执行。
- 客户端可能刷新、断线或重复打开同一 run 事件流。

### Does Not Apply When

- 后端已有独立持久队列 worker，SSE 只订阅队列状态。
- 每次 SSE 连接本来就设计成新 run；Journal harness 不采用这种语义。
- 只查询 `GET /journal/harness/runs/{runId}`，没有执行副作用。

## Related Artifacts

- Spec: [2026-05-12-journal-harness-core-design.md](../../specs/2026-05-12-journal-harness-core-design.md)
- Plan: [2026-05-12-journal-harness-core-implementation-plan.md](../../plans/2026-05-12-journal-harness-core-implementation-plan.md)
- Archive: [2026-05-12-journal-harness-core-archives.md](../../archives/2026-05/2026-05-12-journal-harness-core-archives.md)
- Related Problems:
  - None yet.
- Code or Test:
  - [JournalHarnessService.cs](../../../../src/Journal.Infrastructure/Harness/JournalHarnessService.cs)
  - [TodayJournalEndpointTests.cs](../../../../tests/Journal.Tests/TodayJournalEndpointTests.cs)
  - [JournalHarnessServiceTests.cs](../../../../tests/Journal.Tests/JournalHarnessServiceTests.cs)
