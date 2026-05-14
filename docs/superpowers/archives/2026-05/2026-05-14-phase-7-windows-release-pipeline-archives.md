# Phase 7 Windows Release Pipeline Delivery

- Date: `2026-05-14`
- Topic slug: `phase-7-windows-release-pipeline`
- Status: `Archived`
- Scope: `Feature`
- Tags: `phase-7`, `release`, `installer`, `electron`, `backup`, `github-actions`

## Summary

Phase 7 把 Journal 从开发态双进程应用推进到 Windows 本地发布闭环：安装版 Electron 能加载打包前端、托管内置 `.NET` backend，并通过 About、导入导出、法律声明、图标资产、Inno Setup 和 GitHub Actions release workflow 形成可验证的 0.1.0 发布路径。

最终验收修复补齐了 packaged Electron 的两个阻塞点：Vite 产物使用相对 `./assets/...` 路径，`file://` renderer 的 `Origin: null` CORS 预检被后端精确允许，同时 release metadata 注入真实 frontend version，避免 About 面板显示 `0.1.0-dev`。

## Delivered Scope

- About 面板展示 release、frontend、backend、commit、build time 和数据目录，前后端都从 release metadata 读取构建身份。
- Electron 生产态拥有本机 backend lifecycle：安装包携带 self-contained `Journal.Api.exe`，renderer 通过受信任 bridge 使用本次启动的 loopback API。
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

- `dotnet test Journal.slnx`：312/312 backend tests passed。
- `npm test --prefix apps/desktop`：193/193 frontend tests passed。
- `npm run build --prefix apps/desktop`：TypeScript build 和 Vite production build passed，`dist/index.html` 使用 `./assets/...`。
- `.\scripts\release\build-installer.ps1 -ReleaseVersion 0.1.0`：Inno Setup compile successful，生成 `artifacts/installer/dist/Journal-Setup-0.1.0.exe` 与 `.sha256`。
- `.\scripts\release\verify-installer.ps1 -ReleaseVersion 0.1.0`：installer artifact verified。
- `git diff --check`：exit 0，仅有工作区 CRLF 规范化提示。

## Source Documents

- Spec: [2026-05-14-phase-7-windows-release-pipeline-design.md](../../specs/2026-05-14-phase-7-windows-release-pipeline-design.md)
- Visual: no separate visual artifact was created for this release pipeline.
- Plan: [2026-05-14-phase-7-windows-release-pipeline-implementation-plan.md](../../plans/2026-05-14-phase-7-windows-release-pipeline-implementation-plan.md)

## Related Problems

- [Vite Loopback Origin CORS Drift](../../problems/2026-05/2026-05-07-vite-loopback-cors-problem.md)

## Notes

- Packaged Electron keeps `webSecurity` enabled. The production fix is not `AllowAnyOrigin`; it is a narrow CORS predicate for the two dev origins plus packaged `null` origin.
- Release staging now treats frontend asset path shape and frontend version metadata as build-time gates, so future installer builds fail before shipping a white-screen package.
