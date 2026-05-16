# Phase 7 Post-Install Validation Gaps

- Date: `2026-05-15`
- Topic slug: `phase-7-post-install-validation-gaps`
- Status: `Inbox`
- Lifecycle: `Partially promoted`
- Revisit trigger: `重新打包或安装版 smoke 时复查 About 版本信息、内置正式文件阅读器、声明入口、Data Backup 打开路径和 release checklist 覆盖。`
- Scope: `Feature`
- Confidence: `Medium`
- Route candidate: `update-existing`

## Signal

Phase 7 归档和自动验证完成后，本地安装/运行验证继续暴露多个用户可见问题：

- About 面板曾因 CSS 变量未定义而显示成透明窗口。
- About 面板底部的 License / Privacy / Data Safety / AI Notice 曾表现为不可点击静态文字，视觉像入口但没有行为。
- About 和 Data Backup 的关闭按钮曾使用“关闭”文字按钮，和 LLM 配置面板的图标关闭按钮不一致。
- 数据导出/导入结果里的“打开路径”曾需要区分浏览器/Vite 环境缺少 Electron bridge、安装版 preload 未注入、旧安装包未更新、或后端/系统打开路径 fallback 缺失。其中 Electron preload sandbox bridge 失效已提升为正式 problem。
- `v0.1.1` 发布后 About 中 Backend 仍显示 `0.1.0`，说明 backend 不能继续只读 `ApplicationInfo.Version` 这个静态开发版本，安装包构建必须把 tag/release version 注入 backend metadata。

2026-05-16 follow-up：About 声明文案进一步从“用例/功能说明”调整为正式入口摘要，并新增白名单式 `journal:read-legal-document` IPC，让 Privacy / Data Safety / AI Notice / Personal Statement / License 从开发环境仓库路径或安装版 `{app}\legal` 读取正式文件内容，并在 About 内置阅读器中展示。`PERSONAL_STATEMENT.md` 和 `DISCLAIMER.md` 也被加入 installer staging，以避免 About 引用文件在安装包中缺失。

2026-05-16 version follow-up：`/app/info` 新增 `backendVersion`，release scripts 将同一个 tag/release version 写入 `JOURNAL_RELEASE_VERSION`、`JOURNAL_FRONTEND_VERSION` 和 `JOURNAL_BACKEND_VERSION`。Electron 后端复用判定改为读取 `backendVersion ?? version`，避免旧 packaged backend 被误判为可复用。

## Why It Might Matter

这些问题都出现在“安装包可以生成”之后，说明 Phase 7 的自动验证更偏 artifact 和构建链，未覆盖安装版的关键人工验收体验。后续如果只看 `build-installer` 和 `verify-installer`，可能再次把安装包产物可用误判为产品体验可用。

## What Is Missing

- 对“打开路径”问题的稳定复现路径：浏览器开发页、Electron 开发壳、当前安装版、旧安装版四种环境需要分开确认。
- 对已安装 Inno 版本的真实 smoke 仍需在重新打包后确认；当前已修复并验证开发版 Electron bridge 失效路径。
- 安装版 About / Data Backup 的手工或自动化 smoke 证据。
- 是否需要把 Electron preload bridge 检查、About 统一版本显示、内置正式文件阅读器和本地路径打开能力加入 release verification checklist。
- 如果后续安装包仍复现，需要另行确认是旧包未更新、安装版启动链路、权限或系统 shell 行为，而不是重复归因到 preload sandbox。

## Likely Next Route

Electron preload bridge 失效部分已提升为正式 problem。About 内置正式文件阅读器和统一版本注入已在开发侧补行为和测试，但仍缺重新打包后的安装版 smoke。剩余安装包 smoke 与 release checklist 覆盖缺口继续留在 inbox；如果重新打包后的安装版仍暴露本地路径、正式文件读取或版本显示问题，再创建或更新更具体的问题资产。

## Related Assets

- Spec: [2026-05-14-phase-7-windows-release-pipeline-design.md](../../specs/2026-05-14-phase-7-windows-release-pipeline-design.md)
- Plan: [2026-05-14-phase-7-windows-release-pipeline-implementation-plan.md](../../plans/2026-05-14-phase-7-windows-release-pipeline-implementation-plan.md)
- Archive: [2026-05-14-phase-7-windows-release-pipeline-archives.md](../../archives/2026-05/2026-05-14-phase-7-windows-release-pipeline-archives.md)
- Problems:
  - [Phase 7 Problem Gate Omission](../../problems/2026-05/2026-05-15-phase-7-problem-gate-omission-problem.md)
  - [Electron Preload Sandbox Bridge](../../problems/2026-05/2026-05-15-electron-preload-sandbox-bridge-problem.md)
