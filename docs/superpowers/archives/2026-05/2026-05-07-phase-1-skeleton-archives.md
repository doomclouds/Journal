# Phase 1 Skeleton

- Date: `2026-05-07`
- Topic slug: `phase-1-skeleton`
- Status: `Archived`
- Scope: `Feature`
- Tags: `phase-1`, `skeleton`, `electron`, `react`, `.net`

## Summary

阶段 1 交付了 Journal 的工程化薄壳闭环：Electron/Vite/React 桌面前端、.NET 10 Minimal API、本地 `/health` 联通合同、最小测试与启动文档。这个切片只证明前端桌面壳和后端本地服务能稳定联通，为后续日记业务、AI 和 Markdown/JMF 流程提供清晰落点。

## Delivered Scope

- 建立 `Journal.slnx`，包含 `Journal.Api`、`Journal.Domain`、`Journal.Infrastructure` 与 `Journal.Tests`。
- 提供 `GET /health`，返回 `app/status/version/environment/serverTime`，并为 Vite 开发 origin 配置窄 CORS 白名单。
- 建立 `apps/desktop` Electron + React + TypeScript + Vite 桌面壳，React 首屏展示 API `checking/online/offline` 状态。
- 补齐后端 xUnit 测试、前端 Vitest 测试、Vite build、README 启动说明和根 `.gitignore`。

## Out of Scope

- 不包含日记输入、保存、文件写入、SQLite 索引或 Markdown/JMF 生成。
- 不包含 AI Provider、语音转写、安装包、自动更新或 Electron 托管 .NET 后端进程。
- 不包含生产级打包生命周期、托盘菜单、日志聚合或业务错误码体系。

## Verification Snapshot

- `dotnet test Journal.slnx`：4/4 .NET tests passed。
- `npm test --prefix apps/desktop`：4/4 frontend tests passed。
- `npm run build --prefix apps/desktop`：TypeScript + Vite build passed。
- `dotnet run --project src/Journal.Api --urls http://localhost:5057`：API 监听 `http://localhost:5057`。
- `Invoke-RestMethod http://localhost:5057/health`：返回 `Journal.Api`、`ok`、`0.1.0`、`Development`。
- Playwright 打开 `http://127.0.0.1:5173/`：页面显示 `API 状态 online`、`Journal.Api`、`0.1.0`。

## Source Documents

- Spec: [2026-05-07-phase-1-skeleton-design.md](../../specs/2026-05-07-phase-1-skeleton-design.md)
- Visual: None found for this topic.
- Plan: [2026-05-07-phase-1-skeleton-implementation-plan.md](../../plans/2026-05-07-phase-1-skeleton-implementation-plan.md)

## Related Problems

- [2026-05-07-vite-loopback-cors-problem.md](../../problems/2026-05/2026-05-07-vite-loopback-cors-problem.md)

## Notes

- 运行验证期间发现 Vite 输出 `127.0.0.1:5173`，而 Electron 和部分脚本使用 `localhost:5173`；最终 API CORS 同时白名单这两个 loopback origin，但仍未放宽到 `AllowAnyOrigin`。
