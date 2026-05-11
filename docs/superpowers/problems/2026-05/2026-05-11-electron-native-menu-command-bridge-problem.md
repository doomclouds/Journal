# Electron Native Menu Command Bridge Drift

- Date: `2026-05-11`
- Topic slug: `electron-native-menu-command-bridge`
- Status: `Captured`
- Scope: `UI`
- Tags: `electron`, `native-menu`, `preload`, `ipc`, `react-ui`

## Symptom

Electron 原生菜单中已经出现 `文件 -> LLM 配置`，但用户点击后没有打开 LLM 配置面板；修复前同一配置面板通过 React 页面内顶部入口可以打开，说明面板本身没有坏。

## Trigger / Context

- 今日工作台重构后，菜单栏从 React 内模拟菜单改为 Electron 原生中文菜单。
- `文件 -> LLM 配置` 通过 Electron main process 发送命令给 renderer。
- React 侧依赖 preload 暴露的 `window.journalDesktop.onNativeMenuCommand` 接收命令并打开配置面板。
- 后续产品收敛已删除 React 顶部重复 LLM 配置入口，`文件 -> LLM 配置` 成为设置面板的唯一主入口；右侧 Today Assistant 只保留当前 LLM 供应商元信息。

## Root Cause

原实现把菜单命令视为单一路径事件：`MenuItem.click -> webContents.send -> preload ipcRenderer.on -> React subscription`。这个链路在测试中只验证了“菜单点击会 send”和“React 收到 stub command 会打开面板”，没有覆盖真实运行时的桥接边界。

当 renderer 侧订阅尚未挂上、preload 桥接未提前缓存命令，或 Electron/React 之间任一侧监听时序漂移时，菜单命令可能已经发出但没有可见效果。结果就是菜单项看起来是产品入口，实际点击却像失效按钮。

## Fix

- 增加 `nativeMenuBridge.cjs`，让 preload 尽早注册 IPC 监听，并在 React 订阅前缓存 pending commands。
- Electron main process 发送菜单命令时，同时保留 IPC 发送，并通过 `executeJavaScript` 派发 `journal:native-menu-command` DOM fallback 事件。
- React 侧同时监听 preload bridge 和 DOM fallback，两条路径都统一进入 `open-llm-settings` 命令处理。
- 补测试覆盖：
  - Electron 菜单模板点击会触发 IPC 和 DOM fallback。
  - preload bridge 能重放 React 订阅前到达的命令。
  - React 能通过 native menu bridge 和 DOM fallback 打开 LLM 配置面板。

## Why This Fix

相比只把菜单 click 重新绑一次，桥接层缓存 + renderer fallback 更适合 Electron 原生命令：菜单、快捷键、托盘这类命令入口都在 React 生命周期之外，不能假设 React 已经完成订阅。保留 IPC 是主路径，DOM fallback 让开发期和测试期更容易验证真实 UI 行为，避免菜单项成为“看起来能点”的假入口。

## Recognition Clues

- 原生菜单项显示正常，点击后没有任何 UI 变化。
- 修复前页面内同类入口可以打开目标面板，说明业务状态本身没有坏；如果后续页面内入口已被删除，可改用 DOM fallback 或测试 helper 直接验证 renderer 打开逻辑。
- 单测只验证 main process `send` 或 React stub handler，没有端到端覆盖菜单命令被 renderer 消费。
- Electron preload / main / React 三层代码都参与同一个行为，但没有一个测试同时约束命令发送、桥接缓存和 UI 打开。

## Applicability / Non-Applicability

### Applies When

- Electron 原生菜单、快捷键、托盘菜单或窗口级命令需要驱动 React UI。
- 命令来源在 main process，目标行为在 renderer/React state。
- 失效现象是“菜单能点但 UI 不动”，而页面内按钮或直接调用同一 React handler 正常。

### Does Not Apply When

- 问题来自菜单项没有注册、菜单模板未安装、菜单被操作系统隐藏，或 click handler 根本没有执行。
- 目标面板自身渲染失败、接口报错或 React 状态机拒绝打开。
- 生产打包环境禁止 `executeJavaScript` fallback，此时应保留 preload 早监听和 pending replay，但需要重新设计 fallback 策略。

## Related Artifacts

- Spec: [2026-05-10-today-workbench-command-surface-redesign-design.md](../../specs/2026-05-10-today-workbench-command-surface-redesign-design.md)
- Plan: [2026-05-10-today-workbench-command-surface-redesign-implementation-plan.md](../../plans/2026-05-10-today-workbench-command-surface-redesign-implementation-plan.md)
- Archive: [2026-05-10-today-workbench-command-surface-redesign-archives.md](../../archives/2026-05/2026-05-10-today-workbench-command-surface-redesign-archives.md)
- Related Problems:
  - None yet.
- Code or Test:
  - [menu.cjs](../../../../apps/desktop/electron/menu.cjs)
  - [preload.cjs](../../../../apps/desktop/electron/preload.cjs)
  - [nativeMenuBridge.cjs](../../../../apps/desktop/electron/nativeMenuBridge.cjs)
  - [App.tsx](../../../../apps/desktop/src/App.tsx)
  - [electronMenu.test.ts](../../../../apps/desktop/src/electronMenu.test.ts)
  - [nativeMenuBridge.test.ts](../../../../apps/desktop/src/nativeMenuBridge.test.ts)
  - [App.test.tsx](../../../../apps/desktop/src/App.test.tsx)
