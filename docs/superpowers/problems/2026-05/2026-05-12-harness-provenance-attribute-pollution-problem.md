# Harness Provenance Attribute Pollution

- Date: `2026-05-12`
- Topic slug: `harness-provenance-attribute-pollution`
- Status: `Captured`
- Scope: `Feature`
- Tags: `harness-core`, `provenance`, `jmf`, `security`

## Symptom

LLM 工具参数里的 `basedOnRawInputIds` 可以被原样写入 JMF section marker 的 `based_on_raw_inputs="..."` 属性。恶意或失控参数包含引号、`-->`、`<script>` 或未知 raw input id 时，可能污染 marker 属性，甚至破坏 comment 结构。

## Trigger / Context

- Harness planner 收集模型工具调用。
- Operation executor 将 operation 的 `BasedOnRawInputIds` 写入 section provenance。
- JMF composer 把 provenance 字段拼进 marker attribute。
- 最终审查发现正文内容有 escaping，但 provenance attribute 缺少校验和转义。

## Root Cause

系统把模型提供的 raw input id 当成可信 provenance 数据。`basedOnRawInputIds` 表面是“来源 id”，实质仍是 LLM 可控输入，不能直接进入 JMF marker attribute。

同时 composer 没有对 marker attribute 做 defense-in-depth escaping，导致即使上层传入异常值，下层也无法兜底保护 JMF comment。

## Fix

- Service 层把当天服务端已知 raw input ids 传给 executor。
- Executor 将 model-provided `BasedOnRawInputIds` 与 allowed raw input ids 取交集，未知或恶意 id 不进入 provenance。
- Composer 对 provenance attribute value 做 escaping，避免引号和 `>` 等字符破坏 marker。
- 增加测试覆盖：
  - 未知/恶意 raw input id 不会出现在执行后 provenance。
  - composer 即使收到异常 provenance attribute value，也输出安全 marker。

## Why This Fix

provenance 是系统审计边界，不能让模型自证来源。只接受服务端已知 raw input id 能保持 lineage 可信；composer 再做转义兜底，避免未来其他调用路径绕过 executor 时破坏 JMF 文件。

## Recognition Clues

- `based_on_raw_inputs` 中出现不属于当天 raw inputs 的 id。
- marker 行里出现未转义的 `"`、`-->` 或 HTML/script 片段。
- parser/composer roundtrip 后 section marker 结构异常。
- 安全问题不是日记正文 escaping，而是 comment marker attribute escaping。

## Applicability / Non-Applicability

### Applies When

- LLM 工具参数会进入 JMF marker、front matter、metadata 或审计结构。
- 字段看起来像 id/source/provenance，但来源仍可由模型控制。
- 需要保持 raw input lineage 可信。

### Does Not Apply When

- 字段完全由服务端生成，且不接受模型输入。
- 用户正文内容进入 Markdown body；那应走正文 escaping 或 Markdown 规则，不是 provenance attribute 规则。
- 审计 JSON 保存模型原始请求用于调试；那应隔离为审计数据，不应混入 JMF marker。

## Related Artifacts

- Spec: [2026-05-12-journal-harness-core-design.md](../../specs/2026-05-12-journal-harness-core-design.md)
- Plan: [2026-05-12-journal-harness-core-implementation-plan.md](../../plans/2026-05-12-journal-harness-core-implementation-plan.md)
- Archive: [2026-05-12-journal-harness-core-archives.md](../../archives/2026-05/2026-05-12-journal-harness-core-archives.md)
- Related Problems:
  - None yet.
- Code or Test:
  - [JournalHarnessOperationExecutor.cs](../../../../src/Journal.Infrastructure/Harness/JournalHarnessOperationExecutor.cs)
  - [JmfMarkdownComposer.cs](../../../../src/Journal.Infrastructure/Jmf/JmfMarkdownComposer.cs)
  - [JournalHarnessOperationExecutorTests.cs](../../../../tests/Journal.Tests/JournalHarnessOperationExecutorTests.cs)
  - [JmfMarkdownComposerTests.cs](../../../../tests/Journal.Tests/JmfMarkdownComposerTests.cs)
