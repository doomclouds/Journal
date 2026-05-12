# Generated Draft Missing AI Provenance

- Date: `2026-05-13`
- Topic slug: `generated-draft-missing-ai-provenance`
- Status: `Captured`
- Scope: `Feature`
- Tags: `harness-core`, `provenance`, `jmf`, `revise`

## Symptom

Harness planner 正确调用 `revise-ai-generated-section` 修改 `today-focus`，但执行阶段进入 `attention`，审计记录显示：

`Section 'today-focus' is not a pure AI section.`

用户看到的表现是 AI 明明要修改自己生成的句子，却被 JMF validation 拒绝。

## Trigger / Context

- 草稿由 LLM/Mock JSON 通过 `JmfMarkdownRenderer` 生成。
- 生成的 section marker 只有 `<!-- journal:section today-focus -->`，没有 `origin`、`created_by`、`last_touched_by`、`last_operation`。
- Harness executor 对 `revise-ai-generated-section` 保持严格校验，只允许 `origin=ai` 且 `created_by=ai` 且未被用户触碰的 section。
- 用户要求把 AI 生成的“可能看《第一性原理》这本书（但不确定）”改成更柔和俏皮的描述。

## Root Cause

JMF provenance 的读写能力是在 harness 阶段补上的，但原始 AI 草稿生成入口没有同步写入 section 级 provenance。Parser 对缺失 provenance 的旧 marker 按 `unknown` 处理，这是正确的兼容策略；问题在于真正由 AI 生成的结构段落也落成了 `unknown`。

因此 executor 无法区分“旧 AI 生成段落漏标”与“用户/source 手动写过但没有来源信息”的段落，只能按安全边界拒绝 revise。

## Fix

- `JmfMarkdownRenderer` 对 AI 结构段落输出 `origin="ai" created_by="ai" last_touched_by="ai" last_operation="create"`。
- `raw-inputs` 继续不标记为 AI provenance，保持用户原始材料的来源边界。
- Harness baseline 构建时，对带有 `provider`、`model`、`prompt_version`、`generated_at` front matter 的旧生成文档执行窄范围迁移：
  - 只修复 catalog 内可编辑、非 system、非 `raw-inputs` 的 section。
  - 只修复 provenance 仍为全 unknown 的 section。
  - 已被用户块编辑标为 `last_touched_by=user` 的 section 不会被回填成 AI。
- 增加回归测试覆盖新生成 marker 和旧 DeepSeek 生成草稿 revise 通过。

## Why This Fix

不能把 executor 改成“unknown 也允许 revise”，那会破坏用户内容保护边界。正确修复点是让生成入口写出真实 provenance，并对明确可识别的 legacy generated draft 做一次窄范围补标。

这种做法保留了安全阀：用户编辑或混合来源 section 仍不能走 `revise-ai-generated-section`，模型只能 append。

## Recognition Clues

- 审计页出现 `reviseAiGeneratedSection rejected`，拒绝原因为 `not a pure AI section`。
- 目标 section marker 没有 provenance attributes，但 front matter 里有 `provider`、`model`、`prompt_version`、`generated_at`。
- 用户要改的内容看起来来自 AI 生成 draft，而不是用户手动 block edit。
- Parser 看到 section provenance 为 `unknown/unknown/unknown/unknown`。

## Applicability / Non-Applicability

### Applies When

- 新增了依赖 provenance 的 harness 操作，但旧生成入口尚未写 provenance。
- 需要允许 AI revise 自己生成且未被用户触碰的段落。
- 旧 draft 可以通过生成 front matter 明确识别为 AI/Mock 生成产物。

### Does Not Apply When

- section 已经被块编辑标记为 `last_touched_by=user`；这类内容应拒绝 revise，只允许 append。
- 文档没有生成 front matter，或来源无法确认；这类 unknown 不应自动升格为 AI。
- 需要 item 级细粒度修改；当前修复仍是 section 级 provenance。

## Related Artifacts

- Spec: [2026-05-12-journal-harness-core-design.md](../../specs/2026-05-12-journal-harness-core-design.md)
- Plan: [2026-05-12-journal-harness-core-implementation-plan.md](../../plans/2026-05-12-journal-harness-core-implementation-plan.md)
- Archive: [2026-05-12-journal-harness-core-archives.md](../../archives/2026-05/2026-05-12-journal-harness-core-archives.md)
- Related Problems:
  - [Harness Provenance Attribute Pollution](./2026-05-12-harness-provenance-attribute-pollution-problem.md)
- Code or Test:
  - [JmfMarkdownRenderer.cs](../../../../src/Journal.Infrastructure/Jmf/JmfMarkdownRenderer.cs)
  - [JournalHarnessService.cs](../../../../src/Journal.Infrastructure/Harness/JournalHarnessService.cs)
  - [MockAiAndJmfTests.cs](../../../../tests/Journal.Tests/MockAiAndJmfTests.cs)
  - [JournalHarnessServiceTests.cs](../../../../tests/Journal.Tests/JournalHarnessServiceTests.cs)
