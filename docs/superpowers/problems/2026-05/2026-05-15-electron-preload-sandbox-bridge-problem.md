# Electron Preload Sandbox Bridge

- Date: `2026-05-15`
- Topic slug: `electron-preload-sandbox-bridge`
- Status: `Captured`
- Scope: `Desktop`
- Tags: `electron`, `preload`, `ipc`, `data-backup`, `release`

## Symptom

数据与备份面板在桌面端里可以完成后端导出，但两个原生能力表现异常：

- 点击“打开导出路径”显示“当前环境不支持打开本地路径。”
- 点击“选择导入包”没有弹出文件选择窗口，看起来像按钮无效。

这类现象容易被误判为普通浏览器或 Vite 页面缺少 Electron 能力，但用户重新打开桌面端后仍复现，说明必须先确认当前进程到底是不是 Electron、preload 是否注入、IPC bridge 是否存在。

## Trigger / Context

- 本地运行的是开发版 Electron：`apps/desktop/node_modules/electron/dist/electron.exe .`，不是已安装的 `Journal.exe`。
- renderer 进程命令行包含 `--enable-sandbox`。
- `BrowserWindow` 配置了 `preload: path.join(__dirname, "preload.cjs")`、`contextIsolation: true`、`nodeIntegration: false`，但之前没有显式设置 `sandbox: false`。
- `preload.cjs` 通过 CommonJS 组合本地 bridge 模块，并把 `selectImportPackage`、`openPath` 暴露到 `window.journalDesktop`。
- App 的 HTTP API 仍能通过 `localhost:5057` 工作，所以“导出成功”会掩盖 Electron bridge 断开的事实。

## Root Cause

桌面端依赖 preload 注入的 `window.journalDesktop` 来调用 Electron 主进程 IPC。当前 preload 是模块化 CommonJS 写法，会加载本地 bridge 文件。Electron 新版本下 renderer sandbox 默认更容易开启；在 sandbox renderer 里，preload 对本地 CommonJS 模块的加载能力受限，导致 bridge 可能没有完整注入。

于是页面主体仍能通过 fetch 调后端 API，但所有需要 Electron 原生桥的能力都会缺失：文件选择器打不开、本地路径打开能力被前端判定不可用。

同时，`openSafeJournalPath` 之前直接把导出的 zip 路径传给 `shell.openPath`。即使 bridge 正常，用户语义上点击的是“打开导出路径”，更合理的是打开 zip 所在目录，而不是尝试打开 zip 文件本身。

## Fix

- 在 `apps/desktop/electron/main.cjs` 的 `BrowserWindow.webPreferences` 中显式设置 `sandbox: false`，保持现有模块化 preload/IPC bridge 可用。
- 在 `apps/desktop/electron/dataBackupIpc.cjs` 中把安全路径打开逻辑改为：
  - 目标是目录：直接打开目录。
  - 目标是文件：打开父目录。
  - 目标不在 `.journal/exports` 或 `.journal/import-backups` 白名单内：拒绝打开。
- 在 `apps/desktop/src/App.tsx` 中给 `selectImportPackage` bridge 缺失增加明确错误提示，避免按钮无声失败。
- 增加回归测试：
  - `releasePackaging.test.ts` 静态守护 Electron preload 配置包含 `sandbox: false`。
  - `electronMenu.test.ts` 覆盖文件目标打开父目录、目录目标直接打开、非白名单路径拒绝。
  - `App.test.tsx` 覆盖原生文件选择器不可用时显示明确提示。

## Why This Fix

长期更强的方案是把 preload 打成单文件、减少 CommonJS 本地加载，并逐步收紧 Electron sandbox。但这会改变 preload 构建链，超出当前 bug 修复范围。

本次修复选择显式 `sandbox: false`，因为当前应用已经把主业务安全边界放在 `contextIsolation: true`、`nodeIntegration: false`、最小 IPC surface 和路径白名单上。它能最小化变更面，直接恢复现有 bridge 合同，并通过测试固定“不要无意间重新打开 sandbox 导致 bridge 消失”的发布风险。

把打开文件改为打开父目录，是为了对齐按钮文案和用户预期：导出结果是 zip 文件，但用户点击的是“打开导出路径”，应该进入可见文件所在文件夹。

## Recognition Clues

- 桌面端里 HTTP API 正常，只有文件选择器、打开路径、原生菜单这类 Electron IPC 能力失效。
- `window.journalDesktop` 缺失或只有部分方法，前端显示“当前环境不支持打开本地路径”。
- 进程列表显示是 Electron dev app，但 renderer 命令行有 `--enable-sandbox`。
- `BrowserWindow` 有 preload，但没有显式 `sandbox: false`，且 preload 不是单文件 bundle，而是 `require` 本地 CommonJS 模块。
- 用户说“我已经重新打开桌面端”，说明不能再用“你可能在浏览器里打开”作为主要解释。

## Applicability / Non-Applicability

### Applies When

- Electron preload 负责暴露 `contextBridge` API，并且 preload 加载本地 CommonJS 模块。
- 应用主体 fetch/React 功能正常，但 IPC、dialog、shell、native menu 等原生桥能力缺失。
- 升级 Electron 或使用 `electron: latest` 后，原本可用的 preload bridge 变成不稳定或缺失。

### Does Not Apply When

- preload 已经由构建工具打成 sandbox-safe 单文件，并且不依赖本地 CommonJS require。
- `window.journalDesktop` 存在且 IPC handler 被调用，但主进程 handler 内部报错。
- 用户确实在普通浏览器或 Vite preview 中打开页面，此时缺少 Electron 原生能力是预期边界，应显示 fallback 提示而不是修 preload。

## Related Artifacts

- Spec: [2026-05-14-phase-7-windows-release-pipeline-design.md](../../specs/2026-05-14-phase-7-windows-release-pipeline-design.md)
- Plan: [2026-05-14-phase-7-windows-release-pipeline-implementation-plan.md](../../plans/2026-05-14-phase-7-windows-release-pipeline-implementation-plan.md)
- Archive: [2026-05-14-phase-7-windows-release-pipeline-archives.md](../../archives/2026-05/2026-05-14-phase-7-windows-release-pipeline-archives.md)
- Related Problems:
  - [Phase 7 Problem Gate Omission](./2026-05-15-phase-7-problem-gate-omission-problem.md)
- Code or Test:
  - [apps/desktop/electron/main.cjs](../../../../apps/desktop/electron/main.cjs)
  - [apps/desktop/electron/dataBackupIpc.cjs](../../../../apps/desktop/electron/dataBackupIpc.cjs)
  - [apps/desktop/src/App.tsx](../../../../apps/desktop/src/App.tsx)
  - [apps/desktop/src/App.test.tsx](../../../../apps/desktop/src/App.test.tsx)
  - [apps/desktop/src/electronMenu.test.ts](../../../../apps/desktop/src/electronMenu.test.ts)
  - [apps/desktop/src/releasePackaging.test.ts](../../../../apps/desktop/src/releasePackaging.test.ts)
