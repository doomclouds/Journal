# Phase 1 Skeleton Design

> 日期：2026-05-07  
> 状态：已评审通过
> 对应愿景：`PROJECT_VISION.md` 阶段 1：应用框架骨架  
> 已确认方案：B，工程化薄壳闭环

## 1. 目标

阶段 1 的目标是搭出 Journal 后续功能可以持续生长的工程骨架，而不是提前实现日记业务。

完成后，项目应具备：

- Electron 桌面窗口可以启动。
- React + TypeScript 第一屏可以渲染。
- .NET 本地 API 可以独立运行。
- React 可以调用 .NET `/health` 并显示在线状态。
- 仓库具备清晰目录、启动脚本、最小测试和 README 启动说明。

阶段 1 只证明一件事：**前端桌面壳和后端本地服务能稳定联通，后续功能有明确落点。**

## 2. 不做范围

阶段 1 明确不做：

- 不做真实日记输入与保存。
- 不做 AI Provider 接入。
- 不做 Mock AI 到 JMF Markdown 的生成链路。
- 不做 Markdown 文件写入、解析或版本快照。
- 不做 SQLite 索引。
- 不做应用内语音、录音或转写。
- 不做安装包。
- 不做 Electron 自动启动、托管和关闭 .NET 后端进程。

这些能力分别进入后续阶段。阶段 1 不提前把业务复杂度塞进骨架里。

## 3. 总体架构

阶段 1 采用开发期双进程模式：

```text
Electron main process
  -> 打开 Vite dev server 页面
  -> 承载 React UI

React UI
  -> fetch http://localhost:<api-port>/health
  -> 显示 API 状态

.NET API
  -> 提供 GET /health
  -> 返回应用名、状态、版本、环境、服务端时间
```

这个模式牺牲一点启动便利性，换取早期可观察性和简单性。用户可以分别看到 API 是否启动、前端是否启动、Electron 是否打开，这对第一阶段排错更友好。

生产打包时 Electron 托管 .NET 后端进程是后续任务，不在阶段 1 里处理。

## 4. 工程目录

阶段 1 建议落地目录：

```text
Journal/
  apps/
    desktop/                    # Electron + React + TypeScript + Vite
  src/
    Journal.Api/                # ASP.NET Core Minimal API
    Journal.Domain/             # 领域模型与阶段 1 应用描述
    Journal.Infrastructure/     # 基础设施占位，后续接文件、SQLite、AI Provider
  tests/
    Journal.Tests/              # .NET 最小测试
  docs/
    product/
    superpowers/
      specs/
        2026-05-07-phase-1-skeleton-design.md
  PROJECT_VISION.md
  README.md
```

目录设计原则：

- `Journal.Api` 只负责本地 HTTP API 和启动配置。
- `Journal.Domain` 放稳定领域概念，阶段 1 可以很薄，但不能让 API 直接承载所有未来模型。
- `Journal.Infrastructure` 暂时可以没有业务实现，但先建立后续文件存储、SQLite、AI Provider 的落点。
- `apps/desktop` 只负责桌面壳和前端界面，不直接读写日记文件。

## 5. 后端设计

### 5.1 技术选择

- .NET 10
- ASP.NET Core Minimal API
- xUnit 测试

实际项目创建时以本机 SDK 可用版本为准；当前已验证本机可用 `.NET SDK 10.0.203`。

### 5.2 `/health` 合同

阶段 1 只定义一个后端接口：

```http
GET /health
```

响应示例：

```json
{
  "app": "Journal.Api",
  "status": "ok",
  "version": "0.1.0",
  "environment": "Development",
  "serverTime": "2026-05-07T20:30:00+08:00"
}
```

字段语义：

- `app`：服务名，固定为 `Journal.Api`。
- `status`：健康状态，阶段 1 固定为 `ok`。
- `version`：应用版本，阶段 1 使用 `0.1.0`。
- `environment`：当前运行环境，例如 `Development`。
- `serverTime`：服务端当前时间，使用 ISO 8601 字符串。

### 5.3 错误处理

阶段 1 的错误处理只覆盖联通状态：

- API 未启动时，前端显示 `offline`。
- `/health` 返回非 2xx 时，前端显示 `degraded` 或 `offline`。
- 请求超时或网络异常时，前端显示简短错误原因。

不设计业务错误码，因为阶段 1 没有业务 API。

## 6. 前端与 Electron 设计

### 6.1 技术选择

- Electron
- React
- TypeScript
- Vite
- Vitest

### 6.2 Electron 职责

阶段 1 的 Electron 只做三件事：

- 创建桌面窗口。
- 开发模式加载 Vite dev server。
- 生产预览模式加载前端构建产物。

阶段 1 不让 Electron 启动 .NET API，不做托盘、菜单、自动更新、安装包和日志聚合。

### 6.3 React 首页

React 第一屏是工程状态面板，不是最终日记工作台。

页面内容：

- 产品名：Journal。
- 今日日期。
- 当前阶段：Phase 1 Skeleton。
- API 状态：`checking`、`online`、`offline`。
- `/health` 返回的应用名、版本、环境、服务端时间。
- 下一阶段提示：文本输入到 JMF Markdown MVP。

视觉原则：

- 安静、克制、工具感优先。
- 不做营销式 Hero。
- 不提前设计完整日记编辑器。
- 不让阶段 1 UI 暗示已经具备日记保存能力。

## 7. 数据流

阶段 1 数据流很短：

```text
用户启动 .NET API
  -> 用户启动 Electron/Vite
  -> React 首屏加载
  -> React 请求 GET /health
  -> API 返回健康信息
  -> React 渲染状态面板
```

没有本地日记文件写入，没有数据库，没有 AI 请求，没有 Markdown 生成。

## 8. 测试策略

阶段 1 的测试只验证骨架合同：

- .NET 测试验证 `/health` 返回 200。
- .NET 测试验证 `/health` JSON 包含关键字段。
- 前端测试验证页面能显示阶段标题。
- 前端测试验证 API online/offline 状态能被渲染。
- 构建检查验证 React/Vite 能成功 build。

执行顺序应遵循需求驱动开发：

1. 先写失败测试。
2. 确认测试因功能缺失而失败。
3. 写最小实现。
4. 确认测试通过。
5. 再进入下一个小任务。

## 9. 验收标准

阶段 1 完成标准：

- `dotnet test` 通过。
- 前端测试通过。
- 前端构建通过。
- `.NET API` 可以启动并响应 `/health`。
- Electron 桌面窗口可以打开 React 页面。
- React 页面可以显示 API 在线状态。
- README 写清楚开发期双进程启动方式。
- 仓库目录与本设计文档一致。

只有这些全部满足，阶段 1 才算完成。

## 10. 后续衔接

阶段 1 完成后，阶段 2 才开始处理：

- 今日自然语言文本输入。
- 原始输入保存。
- Mock AI JSON。
- JMF v1 Markdown 预览。
- 用户确认后写正式 Markdown。

阶段 1 不为这些业务提前做半成品页面，只保留清晰扩展点。

## 11. 设计决策记录

- 选择 B：工程化薄壳闭环。
- 开发期采用双进程启动，不做 Electron 托管 .NET。
- `/health` 是阶段 1 唯一 API 合同。
- 首页只做工程状态面板，不做日记编辑器。
- 第一阶段优先验证工程联通和测试链路，不追求产品功能完整度。
