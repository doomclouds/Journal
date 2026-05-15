# Phase 7 Windows Release Pipeline Delivery

- Date: `2026-05-14`
- Topic slug: `phase-7-windows-release-pipeline`
- Status: `Archived`
- Scope: `Feature`
- Tags: `phase-7`, `release`, `installer`, `electron`, `backup`, `github-actions`

## Summary

Phase 7 把 Journal 从开发态双进程应用推进到 Windows 本地发布闭环：安装版 Electron 能加载打包前端、托管内置 `.NET` backend，并通过 About、导入导出、法律声明、图标资产、Inno Setup 和 GitHub Actions release workflow 形成可验证的 0.1.0 发布路径。

最终验收修复补齐了 packaged Electron 的发布阻塞点：Vite 产物使用相对 `./assets/...` 路径，`file://` renderer 的 `Origin: null` 预检被后端精确允许，实际请求必须携带本次 Electron 启动分配的 desktop access token。Electron 主窗口禁止外链导航接管 renderer，packaged backend 启动时强制 `ASPNETCORE_ENVIRONMENT=Production`，同时 release metadata 注入真实 frontend version，避免 About 面板和导出 manifest 显示漂移版本。

## Delivered Scope

- About 面板展示 release、frontend、backend、commit、build time 和数据目录，前后端都从 release metadata 读取构建身份。
- Electron 生产态拥有本机 backend lifecycle：安装包携带 self-contained `Journal.Api.exe`，renderer 通过受信任 bridge 使用本次启动的 loopback API，并对 packaged `Origin: null` 实际请求加 desktop access token 防护。
- Packaged renderer 安全边界收口：token IPC 校验 sender frame URL，Markdown 外链和新窗口交给系统浏览器，主窗口不允许外部页面接管 preload bridge。
- Windows release package 使用提交的图标、legal/privacy/data-safety/AI notice 资料和 Inno Setup 生成 `Journal-Setup-0.1.0.exe` 与 `.sha256`。
- 数据导出/导入提供本地迁移闭环：导出完整本地数据包，导入前备份当前 source material，导入失败时不破坏原数据。
- GitHub Actions release workflow 接入：`workflow_dispatch` 构建安装包 artifact，推送 `v*` tag 才发布 GitHub Release assets。
- Final packaged smoke 固化在脚本与测试里：`stage-installer.ps1` 检查 build metadata 必含 frontend version，并拒绝 root-relative `/assets/...` 产物。

## Out of Scope

- 不包含自动更新、代码签名证书采购、Microsoft Store/MSIX、macOS/Linux 安装包、Windows Service 常驻后台或云同步。
- 不包含完整 API Key 导出导入、Credential Manager/DPAPI 密钥迁移、加密保险箱、主密码或计划任务备份。
- 不包含 AI 改写聊天、Future Notes、draft diff、entry rollback UI、自动保存或多日期编辑。
- SQLite 仍是可重建索引缓存；Markdown entries、raw-input jsonl 和 version/source material 文件仍是事实源。

## Verification Snapshot

- `dotnet test Journal.slnx`：317/317 backend tests passed。
- `npm test --prefix apps/desktop`：201/201 frontend tests passed。
- `npm run build --prefix apps/desktop`：TypeScript build 和 Vite production build passed，`dist/index.html` 使用 `./assets/...`。
- `.\scripts\release\build-installer.ps1 -ReleaseVersion 0.1.0`：Inno Setup compile successful，生成 `artifacts/installer/dist/Journal-Setup-0.1.0.exe` 与 `.sha256`。
- `.\scripts\release\verify-installer.ps1 -ReleaseVersion 0.1.0`：installer artifact verified。
- `git diff --check`：exit 0，仅有工作区 CRLF 规范化提示。

## Source Documents

- Spec: [2026-05-14-phase-7-windows-release-pipeline-design.md](../../specs/2026-05-14-phase-7-windows-release-pipeline-design.md)
- Visual: no separate visual artifact was created for this release pipeline.
- Plan: [2026-05-14-phase-7-windows-release-pipeline-implementation-plan.md](../../plans/2026-05-14-phase-7-windows-release-pipeline-implementation-plan.md)

## Related Problems

- [Phase 7 Problem Gate Omission](../../problems/2026-05/2026-05-15-phase-7-problem-gate-omission-problem.md)
- [Electron Preload Sandbox Bridge](../../problems/2026-05/2026-05-15-electron-preload-sandbox-bridge-problem.md)
- [Vite Loopback Origin CORS Drift](../../problems/2026-05/2026-05-07-vite-loopback-cors-problem.md)
- Inbox: [Phase 7 Post-Install Validation Gaps](../../inbox/2026-05/2026-05-15-phase-7-post-install-validation-gaps-inbox.md)

## Notes

- Packaged Electron keeps `webSecurity` enabled. The production fix is not `AllowAnyOrigin`; it is a narrow CORS predicate: dev origins are development-only, packaged `null` preflight is allowed, and actual browser `Origin` requests require the Electron-owned desktop access token whenever that token is configured.
- `JOURNAL_DESKTOP_ACCESS_TOKEN` is an internal packaged runtime variable. If it is manually left in a development shell, dev browser requests without the token will be rejected by design.
- Release staging now treats frontend asset path shape and frontend version metadata as build-time gates, so future installer builds fail before shipping a white-screen package.
- The automated verification snapshot proves build artifacts, checksum, frontend build, and test suite health. It does not replace manual installed-app UX checks for About, legal notices, native path opening, and other Electron bridge surfaces.
- 2026-05-15 数据备份 UX 回访：数据与备份面板新增固定的当前数据概览区，通过 `GET /journal/data/summary` 读取本地条目/原始材料/版本计数；导入和导出分区补齐操作前提示，成功后用稳定状态行反馈完成结果，避免统计信息跟随导入/导出结果块漂移。
