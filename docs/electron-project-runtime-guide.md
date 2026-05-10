# Electron 项目目录与运行机制说明

## 先给结论

当前 Journal 的 Electron 不是一个已经打包好的最终 EXE 应用，而是开发模式下的桌面壳。

当前启动方式是：

```text
.NET API 单独启动
        ↓
Vite 启动 React 开发服务器
        ↓
Electron 打开一个桌面窗口
        ↓
窗口里加载 React 页面
        ↓
React fetch 调用 .NET 本地 API
```

也就是说，Electron 现在仍然是开发模式桌面窗口容器，真正页面是 React，后端是 .NET API。当前 React 已经不是阶段 1 占位页，而是包含今日工作台、JMF 编辑器和 LLM 设置页的真实应用界面。

## 目录为什么这样设计

当前桌面端放在：

```text
apps/desktop
```

这样设计是为了把“桌面壳”和“.NET 后端”分开：

```text
Journal/
  apps/
    desktop/        # Electron + React + Vite 桌面前端
  src/
    Journal.Api/    # .NET 本地 API
    Journal.Domain/
    Journal.Infrastructure/
  tests/
    Journal.Tests/
```

核心思路是：

```text
apps/desktop 负责用户界面和桌面窗口
src/Journal.Api 负责本地 HTTP API
Domain / Infrastructure 留给后续业务和存储
```

这样后期不会把所有东西塞进 Electron 里，也不会让 React 直接操作本地文件、数据库、AI 这些复杂东西。

## Electron 是怎么启动的

入口在：

```text
apps/desktop/package.json
```

核心脚本是：

```json
"desktop": "concurrently -k \"npm run dev\" \"npm run electron\""
```

运行：

```powershell
npm run desktop --prefix apps/desktop
```

实际会同时启动两件事：

```text
npm run dev       -> 启动 Vite，提供 React 页面
npm run electron  -> 等 Vite 启动后，再启动 Electron
```

Electron 这一段是：

```json
"electron": "wait-on http://localhost:5173 && electron ."
```

意思是：

```text
先等 http://localhost:5173 可以访问
然后执行 electron .
```

`electron .` 会读取 `package.json` 里的：

```json
"main": "electron/main.cjs"
```

所以 Electron 主进程入口就是：

```text
apps/desktop/electron/main.cjs
```

## Electron 的运行机制

Electron 大体有三层：

```text
Main Process
  Electron 主进程，负责创建窗口、管理应用生命周期

Renderer Process
  窗口里的网页，也就是 React 页面

Preload Script
  安全桥梁，让网页有限访问桌面能力
```

在当前项目里：

```text
electron/main.cjs
  创建 BrowserWindow
  开发模式加载 http://localhost:5173

src/main.tsx
  挂载 React 应用

src/App.tsx
  调用 http://localhost:5057/health、/journal/today、/settings/ai 等本地 API
  显示今日日记工作台、JMF 编辑器、LLM 设置和在线状态
```

当前窗口创建逻辑在：

```text
apps/desktop/electron/main.cjs
```

开发模式下加载：

```js
mainWindow.loadURL("http://localhost:5173");
```

也就是 Electron 窗口里显示的其实是 Vite 提供的 React 页面。

## 当前为什么要先启动 .NET API

因为当前仍采用“双进程开发模式”。

也就是说 Electron 目前不会自动启动 .NET 后端。你需要先运行：

```powershell
dotnet run --project src/Journal.Api
```

然后再运行：

```powershell
npm run desktop --prefix apps/desktop
```

React 页面会请求：

```text
http://localhost:5057/health
http://localhost:5057/journal/today
http://localhost:5057/settings/ai
```

如果 API 正常，页面显示 `online` 并可使用今日日记链路。API 没开时会显示 `offline` 或接口加载失败。

## preload 是干什么的

当前 preload 在：

```text
apps/desktop/electron/preload.cjs
```

它现在只暴露了一个很小的对象：

```js
contextBridge.exposeInMainWorld("journalDesktop", {
  platform: process.platform
});
```

为什么不让 React 直接用 Node？

因为窗口配置了：

```js
contextIsolation: true,
nodeIntegration: false
```

这是 Electron 推荐的安全边界。简单说：

```text
React 页面不能直接访问 Node / 文件系统
需要通过 preload 暴露安全 API
```

后期如果要让前端读取本地文件、选择目录、调用系统能力，应该通过 preload + IPC 做，不建议直接打开 `nodeIntegration`。

## 后期可以怎么改

### 1. 继续完善真实页面功能

主要改：

```text
apps/desktop/src/
```

当前已经有：

```text
日记输入页
Markdown 预览页
设置页
AI 处理状态
JMF 块编辑
JMF 源码编辑
LLM 参数配置
```

后面可以继续引入：

```text
React Router
TanStack Query
Zustand / Redux
组件库
```

但当前仍应保持桌面工具的密度和克制，不需要做成营销式页面。

### 2. 扩展 .NET API

主要改：

```text
src/Journal.Api
src/Journal.Domain
src/Journal.Infrastructure
```

当前已经包含今日链路、编辑器和 LLM 设置；后续可以新增：

```text
GET /journal/recent
GET /journal/by-date
GET /journal/search
POST /journal/index/rebuild
```

React 继续通过 HTTP 调用本地 API。

### 3. 让 Electron 自动启动 .NET 后端

这是后续很关键的一步。

现在是手动双进程：

```text
手动启动 dotnet
手动启动 Electron
```

未来可以改成：

```text
Electron 启动
  -> 自动启动 Journal.Api.exe
  -> 等待 /health 成功
  -> 打开 React 页面
  -> 关闭窗口时自动停止 API
```

这会涉及：

```text
child_process.spawn
.NET publish
进程生命周期管理
端口冲突处理
日志输出
异常恢复
```

### 4. 打包成真正的安装包或 EXE

当前看到的：

```text
apps/desktop/node_modules/electron/dist/electron.exe
```

只是 Electron 开发运行时，不是最终应用 EXE。

后期要做真正安装包，可以引入：

```text
electron-builder
electron-forge
Inno Setup
```

最终目标会变成：

```text
Journal.exe
安装包
桌面快捷方式
开始菜单
卸载程序
```

### 5. 扩展本地存储

当前已经有 Markdown 日记、原始输入、草稿和 LLM 配置文件。后续还要补：

```text
SQLite 索引
版本快照
备份 / 导出
```

比较适合这个项目的是：

```text
Markdown 作为长期可读数据
SQLite 做搜索、索引、元数据
```

### 6. 接 AI Provider

当前已经按这个方向实现：React 不直接调 AI，API Key、重试、模型切换和 LLM 调用都留在 .NET 后端。

更稳的是：

```text
React
  -> 调 .NET API
    -> .NET 调 AI Provider
      -> 返回结构化 JSON
        -> 生成 JMF Markdown
```

这样 API key、重试、日志、模型切换都在后端控制。

## 当前项目启动链路图

```text
你执行：
npm run desktop --prefix apps/desktop

          ┌────────────────────┐
          │ concurrently        │
          └─────────┬──────────┘
                    │
        ┌───────────┴───────────┐
        │                       │
        ▼                       ▼
  npm run dev             npm run electron
  启动 Vite               等 Vite ready
        │                       │
        ▼                       ▼
http://127.0.0.1:5173     electron .
                                │
                                ▼
                         electron/main.cjs
                                │
                                ▼
                         BrowserWindow
                                │
                                ▼
                         loadURL localhost:5173
                                │
                                ▼
                         React App
                                │
                                ▼
                   fetch localhost:5057/health
                   fetch localhost:5057/journal/today
                   fetch localhost:5057/settings/ai
                                │
                                ▼
                         .NET Journal.Api
```

## 你现在最该记住的点

这个 Electron 项目不是“Electron 里写所有业务”。

它是：

```text
Electron = 桌面外壳
React = UI
.NET API = 本地业务服务
preload = 安全桥
Vite = 开发期前端服务器
```

这个结构后期比较好扩，不容易乱。继续做功能时，优先思路应该是：

```text
UI 放 React
业务接口放 .NET API
本地文件/数据库/AI 放 .NET 后端
Electron 只管桌面生命周期和系统集成
```
