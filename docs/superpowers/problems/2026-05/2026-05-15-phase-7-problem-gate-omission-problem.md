# Phase 7 Problem Gate Omission

- Date: `2026-05-15`
- Topic slug: `phase-7-problem-gate-omission`
- Status: `Captured`
- Scope: `Repo`
- Tags: `superpowers`, `asset-compounding`, `phase-7`, `release`, `verification`

## Symptom

Phase 7 Windows release pipeline 已经完成 spec、plan、实现、验证和 requirement archive，但没有同步形成对应的问题归档或 inbox 记录。随后本地安装验证继续暴露 About 面板透明、About 声明区像假链接、模态关闭按钮不统一、导入导出打开路径异常等问题，说明发布收尾时的可复用问题信号没有被完整捕获。

## Trigger / Context

- Phase 7 是大范围交付，覆盖 packaged Electron、backend lifecycle、数据导入导出、Inno Setup、GitHub Actions、About 和 legal surface。
- 计划末尾的 Final Verification Matrix 只列出自动测试、构建、安装包生成、checksum 和 `git diff --check`。
- 仓库 `AGENTS.md` 明确要求 meaningful development work 在实现、spec alignment、code-quality review 和验证后执行 problem-archiving gate。
- 收尾对话里曾把资产复利判断为 `none`，理由是“同一任务的窄补丁、测试已固定、额外写 docs 超范围”。这个判断适合单个小补丁，不适合整个发布阶段的最终收口。

## Root Cause

Phase 7 的计划和执行把 requirement archive 当成了唯一的资产收口，没有把 problem gate 作为和 archive 分离的硬关卡写进 Final Verification Matrix。实际执行时，主 agent 只在单个补丁粒度上判断“是否要再写资产”，而没有回到整个 Phase 7 交付范围收集问题候选。

这导致 `none` 的判断粒度错误：局部补丁看起来不值得写文档，但整个发布阶段已经产生了可复用的 failure classes 和验收缺口。

## Fix

- 新增本 problem 资产，明确记录 Phase 7 收尾漏跑/误判 problem gate 的流程失败模式。
- 回填 Phase 7 archive 的 `Related Problems`，把本问题和后续安装版人工验收 inbox 链接到完成记录。
- 新增 `phase-7-post-install-validation-gaps` inbox，暂存已经观察到但还需要随 bug 修复继续诊断的安装版验收信号。
- 后续 Phase 7 bug 修复完成时，必须把根因、排查路径、修复选择和验证结果更新到正式 problem 或从 inbox 提升为 problem，而不是只在聊天里说明。

## Why This Fix

把所有信号都直接写成正式 problem 会过度归档，因为部分安装版 UI/路径问题还需要进一步复现和根因确认。只更新 archive 又会丢失“problem gate 漏跑”这个流程层 failure class。

因此采用正式 problem 加 inbox 的组合：流程失败已经稳定，适合正式 problem；安装版验收问题仍在继续排查，先进入 inbox，等 bug 修复后再提升或更新。

## Recognition Clues

- 大阶段有 spec、plan、archive 和完整验证记录，但 `docs/superpowers/problems/` / `docs/superpowers/inbox/` 没有相关问题或候选信号。
- 最终计划只列自动验证命令，没有列 `using-asset-compounding` / problem gate / route decision。
- 收尾理由把“单个窄补丁不值得归档”套用到“整个阶段交付”。
- 用户在人工验收中连续发现问题，但 archive 仍显示交付已完整闭环。

## Applicability / Non-Applicability

### Applies When

- 一个阶段或大功能已经完成 requirement archive，但过程中出现过发布阻塞、人工验收缺口、测试覆盖缺口或可复用 debugging pattern。
- agent 使用 subagent-driven / executing-plans 推进多个任务，最终合并时容易只看最后一个小补丁。
- 用户要求保留 bug 的解决思路、排查过程或复盘结论。

### Does Not Apply When

- 只是纯格式、文案、颜色、间距等低价值小改，且没有产生可复用诊断信号。
- 已有同主题 problem/inbox 覆盖，并且 archive 已经回链。
- 工作尚未完成到可 review 的边界，此时应先继续实现和验证，而不是提前写正式 problem。

## Related Artifacts

- Spec: [2026-05-14-phase-7-windows-release-pipeline-design.md](../../specs/2026-05-14-phase-7-windows-release-pipeline-design.md)
- Plan: [2026-05-14-phase-7-windows-release-pipeline-implementation-plan.md](../../plans/2026-05-14-phase-7-windows-release-pipeline-implementation-plan.md)
- Archive: [2026-05-14-phase-7-windows-release-pipeline-archives.md](../../archives/2026-05/2026-05-14-phase-7-windows-release-pipeline-archives.md)
- Related Problems:
  - [Vite Loopback Origin CORS Drift](./2026-05-07-vite-loopback-cors-problem.md)
- Code or Test:
  - [AGENTS.md](../../../../AGENTS.md)
  - [2026-05-14-phase-7-windows-release-pipeline-implementation-plan.md](../../plans/2026-05-14-phase-7-windows-release-pipeline-implementation-plan.md)
