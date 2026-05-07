# Vite Loopback Origin CORS Drift

- Date: `2026-05-07`
- Topic slug: `vite-loopback-cors`
- Status: `Captured`
- Scope: `Feature`
- Tags: `cors`, `vite`, `loopback`, `health`, `runtime-verification`

## Symptom

React 状态页在 `http://localhost:5173/` 下显示 `online`，但在 Vite 输出的 `http://127.0.0.1:5173/` 下显示 `offline`，浏览器控制台提示 CORS 拦截 `/health` 请求。

## Trigger / Context

- Vite dev server 配置绑定 `127.0.0.1` 并在控制台展示 `http://127.0.0.1:5173/`。
- React 默认请求 `http://localhost:5057/health`。
- 后端 CORS policy 起初只允许 `http://localhost:5173`。

## Root Cause

浏览器 CORS 按完整 origin 判断来源，`http://localhost:5173` 和 `http://127.0.0.1:5173` 是两个不同 origin。后端只对白名单中的 `localhost` 回写 `Access-Control-Allow-Origin`，因此从 `127.0.0.1` 页面发起的健康检查被浏览器拦截。

## Fix

- 在 `DesktopDevelopment` CORS policy 中显式允许两个开发 origin：`http://localhost:5173` 和 `http://127.0.0.1:5173`。
- 保留 `AllowAnyHeader` 与 `AllowAnyMethod`，没有使用 `AllowAnyOrigin`。
- 添加后端回归测试，分别验证 `localhost:5173` 与 `127.0.0.1:5173` origin 都能拿到对应的 `Access-Control-Allow-Origin`。

## Why This Fix

这次问题只来自本地 loopback 名称漂移，不需要把 CORS 放宽到任意来源。显式列出两个开发期 origin 可以覆盖 Vite 控制台链接、Electron dev URL 和手工浏览器验证，同时保留 Phase 1 的安全边界。

## Recognition Clues

- 同一个 React 页面在 `localhost` 下 online，在 `127.0.0.1` 下 offline。
- API 直接 `Invoke-RestMethod http://localhost:5057/health` 返回正常，但浏览器 fetch 报 CORS。
- ASP.NET Core 日志出现 `Request origin http://127.0.0.1:5173 does not have permission`。
- 浏览器控制台出现 `No 'Access-Control-Allow-Origin' header is present`。

## Applicability / Non-Applicability

### Applies When

- 本地前后端分别运行在 loopback 地址，且工具链可能在 `localhost` 与 `127.0.0.1` 之间切换。
- API 使用精确 CORS origin 白名单，而前端实际打开的 origin 与白名单字符串不完全一致。
- 验证目标是开发期联通，不是生产公网 CORS 策略。

### Does Not Apply When

- 问题来自 API 未启动、端口错误、DNS 解析失败或代理层拒绝连接。
- 生产环境需要真实域名、HTTPS、鉴权或更严格的 CORS/CSRF 策略。
- 前端和 API 同源部署，不经过跨源请求。

## Related Artifacts

- Spec: [2026-05-07-phase-1-skeleton-design.md](../../specs/2026-05-07-phase-1-skeleton-design.md)
- Plan: [2026-05-07-phase-1-skeleton-implementation-plan.md](../../plans/2026-05-07-phase-1-skeleton-implementation-plan.md)
- Archive: [2026-05-07-phase-1-skeleton-archives.md](../../archives/2026-05/2026-05-07-phase-1-skeleton-archives.md)
- Related Problems:
  - None yet.
- Code or Test:
  - [Program.cs](../../../../src/Journal.Api/Program.cs)
  - [HealthEndpointTests.cs](../../../../tests/Journal.Tests/HealthEndpointTests.cs)
  - [vite.config.ts](../../../../apps/desktop/vite.config.ts)
