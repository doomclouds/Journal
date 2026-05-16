# Release Script UTF-8 Template Boundary Inbox

- Date: `2026-05-16`
- Topic slug: `release-script-utf8-template-boundary`
- Status: `Inbox`
- Lifecycle: `Open`
- Revisit trigger: `再次修改 scripts/release/*.ps1 的中文输出、GitHub Release 正文生成逻辑，或 Windows PowerShell 本地测试出现解析乱码时复查。`
- Scope: `Journal Windows release scripts and GitHub Release notes generation`
- Confidence: `Medium`
- Route candidate: `new-problem`

## Signal

优化 GitHub Release 正文模板时，曾把中文发布正文直接写进 `scripts/release/write-github-release-notes.ps1`。`npm test --prefix apps/desktop -- releasePackaging.test.ts` 调用 Windows PowerShell 执行脚本后，PowerShell 按非 UTF-8 解析 BOM-less `.ps1`，中文字符串变成乱码并触发 parser error。

2026-05-16 follow-up：`v0.1.1` tag 推送后，GitHub Actions `build-windows-installer` 的前端测试失败。原因是 build job 的 `actions/checkout` 仍是 shallow checkout，测试调用 release notes 脚本时拿不到 `v0.1.0`，生成正文退回 `repository history`。修复方向是 build job 也设置 `fetch-depth: 0`，并让测试显式传入 `-PreviousTag v0.1.0`，避免 release notes 测试隐式依赖当前 checkout 形态。

## Why It Might Matter

Journal 的发布脚本需要同时服务本机 Windows PowerShell、GitHub Actions `pwsh` 和用户手动执行场景。中文 release 文案以后还会继续调整，如果直接把中文正文塞回 `.ps1`，同类错误很容易复发，而且失败表现会像语法错误，不像编码错误。Release notes 还依赖 tag range；CI 里 shallow checkout 会让“上一版本 tag”消失，导致测试或 release 正文悄悄退化。

## What Is Missing

目前只在 `write-github-release-notes.ps1` 中复现并修复了一次。还没有系统审计所有 `.ps1` 是否都需要保持 ASCII-only，也没有确认 Windows PowerShell 5 与 PowerShell 7 在项目所有发布脚本上的编码边界。

## Likely Next Route

如果后续再次出现 `.ps1` 中文解析失败，或需要长期支持 Windows PowerShell 5，本信号应提升为正式 problem：建议规则是 PowerShell 控制脚本尽量保持 ASCII-only，面向用户的中文长文放在 UTF-8 `.md` / `.json` / `.resx` 等数据文件中，再由脚本显式 UTF-8 读取。

## Related Assets

- [Phase 7 Windows release pipeline design](../../specs/2026-05-14-phase-7-windows-release-pipeline-design.md)
- [Phase 7 Windows release pipeline implementation plan](../../plans/2026-05-14-phase-7-windows-release-pipeline-implementation-plan.md)
- [Phase 7 Windows release pipeline archive](../../archives/2026-05/2026-05-14-phase-7-windows-release-pipeline-archives.md)
- [Post-install validation gaps inbox](./2026-05-15-phase-7-post-install-validation-gaps-inbox.md)
