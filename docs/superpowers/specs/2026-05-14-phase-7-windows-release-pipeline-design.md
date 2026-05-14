# Phase 7 Windows Release Pipeline & Local Data Portability 设计

> 日期：2026-05-14
> 状态：待审查
> 对应方向：Windows 安装包、GitHub Actions 发布流水线、版本中心、图标与声明、导入导出、生产运行闭环

## 1. 背景

Journal 当前已经交付到 Phase 6B：

```text
自然语言输入
  -> Raw input 持久化
  -> Mock / 真实 LLM JSON 或 Harness Core 工具计划
  -> JMF Markdown 草稿
  -> 用户确认
  -> 覆盖前版本快照
  -> 正式 Markdown
  -> 可重建 SQLite/FTS5 历史索引
  -> 历史搜索 / 同日年轮 / 今日版本恢复为草稿
```

这条产品链路已经能证明 Journal 的核心价值，但它仍然是开发态应用：

- Electron 生产态只加载前端 `dist`，还不会托管 `.NET` 后端。
- 运行依赖开发命令和固定端口。
- 没有 Windows 安装包、开始菜单入口、卸载入口或升级策略。
- 没有正式应用图标。
- 没有成体系的 License、隐私声明、数据安全声明和个人声明展示。
- 没有导入导出数据包，换机器时不能形成完整搬迁闭环。
- 没有 GitHub Actions 发布流水线。
- 前端和后端版本号存在，但用户不能在应用里统一看到。

Phase 7 的目标不是再加一组日记功能，而是把 Journal 推进到 **Windows 本地生产可用**：用户可以安装、打开、写日记、备份、迁移、升级、卸载，并能在应用内确认版本与数据位置。

## 2. 当前版本基线

当前代码中的版本事实：

- 后端版本：`src/Journal.Domain/Application/ApplicationInfo.cs` 中 `ApplicationInfo.Version = "0.1.0"`。
- 前端版本：`apps/desktop/package.json` 中 `version = "0.1.0"`。

Phase 7 以当前 API 版本为发布基线：

```text
Journal release: 0.1.0
Backend: Journal.Api 0.1.0
Frontend: @journal/desktop 0.1.0
```

后续版本策略：

- 后端版本继续由 `ApplicationInfo.Version` 或等价 MSBuild version source 管理。
- 前端版本继续由 `apps/desktop/package.json` 管理。
- Release tag 使用整体应用版本，例如 `v0.1.0`。
- About 面板同时显示整体 release、frontend、backend、commit 和 build time。
- 如果未来前后端版本不同步，整体 release 仍由 tag 决定，About 面板显示真实组件版本。

## 3. 目标

Phase 7 采用 **Production-Ready Local App** 方案：

1. **生产运行闭环**：安装后的 Electron 应用能自动启动内置 `.NET` 后端，不再要求用户手动运行 `dotnet run`。
2. **版本中心**：前端、后端、release、commit、build time 和数据目录在 About 面板中可见。
3. **正式图标与品牌资产**：生成 Journal 应用图标，并用于窗口、快捷方式、安装包和 GitHub Release。
4. **法律与声明表面**：提供 License、Notice、隐私声明、数据安全声明、AI 使用声明、个人声明和免责声明。
5. **导入导出 / 备份恢复**：支持导出完整本地数据包，支持导入前自动备份当前数据，导入后重建索引。
6. **Windows 安装包**：使用 Inno Setup 生成正式安装包，支持安装、升级和卸载。
7. **GitHub Actions 发布流水线**：本地安装包验证通过后，再通过 `workflow_dispatch` 或 `v*` tag 在 GitHub Actions 上自动构建安装包和 Release assets。
8. **本地优先验证门槛**：先在本地跑通安装、启动、导出、导入、升级、卸载，再启用 GitHub Release 流水线。

完成后，最低验收场景是：

```text
换一台 Windows 机器
  -> 安装 Journal-Setup-0.1.0.exe
  -> 打开应用
  -> 看到前后端版本和数据目录
  -> 导入旧数据包
  -> 重建索引
  -> 继续写今天的日记
```

## 4. 非目标

本阶段不做：

- 云同步。
- 自动更新。
- Windows Service 常驻后台运行。
- 多用户账户。
- 加密保险箱或主密码。
- DPAPI / Windows Credential Manager 的完整密钥迁移。
- 完整 API Key 导出导入。
- 自动定时备份计划任务。
- macOS / Linux 安装包。
- Microsoft Store / MSIX 发布。
- 代码签名证书采购和签名流水线。
- AI 改写聊天、Future Notes、diff / rollback。

这些能力重要，但会把 Phase 7 从“本地生产可用”拖成平台工程。首版先把 Windows 单机发布闭环做稳。

## 5. 总体方案

Phase 7 分成七个工作面：

```text
Versioning & About
  -> Icon & Release Identity
  -> Legal / Privacy / Data Safety Docs
  -> Production Runtime
  -> Export / Import / Backup
  -> Windows Installer
  -> GitHub Actions Release Pipeline
```

依赖顺序：

1. 版本中心和图标先行，因为安装包和 Release 都要引用。
2. 生产运行先行，因为安装包不能解决后端托管问题。
3. 导入导出与数据目录稳定后，再定义安装/卸载/升级数据策略。
4. 本地安装包验证通过后，再把同样的 staging 和 Inno 编译流程搬进 GitHub Actions。

## 6. Versioning & About

### 6.1 后端信息

新增或扩展后端应用信息接口：

```text
GET /app/info
```

响应建议：

```json
{
  "name": "Journal.Api",
  "version": "0.1.0",
  "releaseVersion": "0.1.0",
  "commit": "aeea0bc",
  "buildTimeUtc": "2026-05-14T12:00:00Z",
  "environment": "Production",
  "dataRoot": "%LocalAppData%/Journal",
  "indexPath": "%LocalAppData%/Journal/.journal/index/journal.db"
}
```

`/health` 可以继续保留轻量健康检查；`/app/info` 负责 About 面板和诊断信息。

构建元数据来源：

- 本地构建时由脚本写入 generated metadata。
- GitHub Actions 构建时从 `GITHUB_SHA`、tag、UTC time 注入。
- 没有 CI metadata 时 fallback 为 `dev` 或 `unknown`，但不能让接口失败。

### 6.2 前端信息

前端构建时注入：

```text
VITE_JOURNAL_FRONTEND_VERSION
VITE_JOURNAL_RELEASE_VERSION
VITE_JOURNAL_COMMIT
VITE_JOURNAL_BUILD_TIME_UTC
```

来源：

- `apps/desktop/package.json` 的 `version`。
- release tag，例如 `v0.1.0`。
- git commit。
- 构建时 UTC 时间。

开发态没有注入时，前端显示：

```text
Frontend: 0.1.0-dev
Commit: dev
Build: local
```

### 6.3 About 面板

当前 Electron 原生 About 只显示固定文案。Phase 7 改为应用内 About 面板或增强后的桌面弹窗，建议优先应用内面板。

About 显示：

- Journal release version。
- Frontend version。
- Backend version。
- Git commit。
- Build time。
- Data root。
- Index path。
- License。
- Privacy / Data Safety / AI Notice 链接。
- Open source notices。

菜单入口：

- `帮助 -> 关于 Journal`
- 今日页或设置页的 `关于` 入口。

## 7. Icon & Release Identity

### 7.1 图标方向

Journal 图标不做普通笔记本模板。建议方向：

```text
晨间纸页 + 年轮弧线 + 本地可信日记
```

视觉关键词：

- 温暖纸张底色。
- 简洁展开日记或单页轮廓。
- 一条日升弧线或年轮弧线。
- 小尺寸下仍可识别。
- 不使用复杂文字。
- 不使用明显云、聊天气泡或通用 AI 星星。

### 7.2 图标资产

图标由 Codex 图像生成先生成源图，再本地处理成 Windows 多尺寸 icon。

推荐路径：

```text
assets/app-icon/
  journal-icon-source.png
  journal-icon-16.png
  journal-icon-24.png
  journal-icon-32.png
  journal-icon-48.png
  journal-icon-64.png
  journal-icon-128.png
  journal-icon-256.png
  journal.ico
```

使用位置：

- Electron `BrowserWindow` icon。
- Windows taskbar / shortcut icon。
- Inno Setup `SetupIconFile`。
- 开始菜单快捷方式。
- GitHub Release 展示图。
- README 或产品故事页面。

图像生成不放进 CI。CI 使用仓库里已提交的确定性图标资产。

## 8. Legal / Privacy / Data Safety / Personal Statement

Phase 7 要把安装包做得正式，需要把声明类内容变成仓库资产和安装包界面的一部分。

推荐新增文件：

```text
docs/legal/
  LICENSE-NOTES.md
  PRIVACY.md
  DATA_SAFETY.md
  AI_NOTICE.md
  PERSONAL_STATEMENT.md
  DISCLAIMER.md
docs/release/
  RELEASE_NOTES.md
  GITHUB_RELEASE_TEMPLATE.md
```

根目录当前已有 `LICENSE` 和 `NOTICE`，继续作为正式基础文件。

### 8.1 License

安装向导必须展示 `LICENSE`，用户接受后才能继续安装。

如果当前 License 需要调整，单独处理，不在 Phase 7 实现中临时改许可证含义。

### 8.2 Privacy

隐私声明必须写清：

- Journal 默认本地优先。
- 日记 Markdown、raw inputs、drafts、versions、audit、index 默认在本机用户目录。
- 应用本身不提供云同步。
- 用户配置真实 LLM Provider 后，用户主动提交的文本会发送给对应模型供应商。
- API Key 不写入 Markdown、版本快照、普通日志、GitHub Release 或安装包。
- 环境变量来源的 API Key 不通过 UI reveal。

### 8.3 Data Safety

数据安全声明必须写清：

- Markdown、raw-input jsonl、version files 是源材料。
- SQLite index 是可重建缓存。
- 卸载默认不删除用户数据。
- 导入前会自动备份当前数据目录。
- 导入后重建 SQLite index。
- 导出默认不包含完整 API Key。

### 8.4 AI Notice

AI 使用声明必须写清：

- AI 负责整理，不是事实保证。
- 用户原始表达是源材料。
- AI 结果进入 draft，用户确认后才写正式 entry。
- 真实 LLM Provider 由用户配置。
- AI 错误或 JSON/JMF 校验失败会进入 `attention`，不直接覆盖正式日记。

### 8.5 Personal Statement

个人声明可以作为产品气质的一部分，但不应该变成营销长文。建议聚焦：

- 这是一个为长期个人记录设计的本地优先工具。
- 它尊重原始表达，不把 AI 摘要当成人生真相。
- 它尽量让用户拥有自己的 Markdown 数据。
- 它不会承诺医疗、心理、法律或财务建议。

### 8.6 Disclaimer

免责声明必须明确：

- Journal 是个人日记整理与本地记录工具。
- AI 输出仅供整理和回看，不构成专业建议。
- 用户应自行判断是否将敏感内容发送给第三方 LLM Provider。
- 用户应自行妥善备份重要数据。

## 9. Installer Customization

安装包使用 Inno Setup。首版不追求完全自绘安装器，采用官方 wizard + 定制图标、图片、文案和声明页面。

### 9.1 Wizard 页面

推荐页面顺序：

```text
Welcome
  -> License
  -> Privacy / Data Safety Info
  -> Select Destination
  -> Select Start Menu Folder
  -> Additional Tasks
  -> Ready
  -> Installing
  -> Finish
```

页面内容：

- Welcome：Journal 图标、版本、简短说明。
- License：根目录 `LICENSE`。
- InfoBefore：隐私声明、数据安全和 AI Notice 摘要。
- Additional Tasks：
  - 创建桌面快捷方式。
  - 安装后启动 Journal。
  - 打开数据目录，默认不勾选。
- Finish：
  - 启动 Journal。
  - 打开 Release Notes。

### 9.2 安装器视觉资产

推荐路径：

```text
installer/windows/assets/
  wizard-large.bmp
  wizard-small.bmp
  setup-icon.ico
  license.rtf
  info-before.rtf
  info-after.rtf
```

RTF 由 Markdown/legal 文档生成，或维护一份安装器专用简短版本。不要在 `.iss` 里硬塞大段中文文案。

### 9.3 安装目录

程序文件：

```text
%ProgramFiles%/Journal/
  Journal.exe
  resources/
  backend/
    Journal.Api.exe
    *.dll
  app/
    dist/
```

用户数据继续使用当前路径：

```text
%LocalAppData%/Journal/
  entries/
  .journal/
```

选择 `%LocalAppData%` 的原因：

- 当前代码已经使用 `JournalStorageOptions.FromLocalAppData()`。
- Journal 是单用户本地日记，不需要首版共享到所有 Windows 用户。
- 升级安装不会触碰用户数据。

安装器可以创建诊断日志目录，但应用运行数据仍由应用自己按当前路径创建。

### 9.4 升级与卸载

升级：

- 关闭正在运行的 Journal。
- 替换 `%ProgramFiles%/Journal` 下程序文件。
- 保留 `%LocalAppData%/Journal`。
- 升级后首次启动执行健康检查和 index scan。

卸载：

- 删除程序文件。
- 删除桌面和开始菜单快捷方式。
- 默认保留 `%LocalAppData%/Journal`。
- 卸载界面或文档提示用户如需删除个人数据，可手动删除数据目录。

首版不做“卸载时删除用户数据”选项，避免误删日记。

## 10. Production Runtime

### 10.1 生产态后端托管

Electron 生产态必须托管 `.NET` 后端。

开发态：

```text
npm run desktop
  -> Vite dev server
  -> Electron loadURL(http://localhost:5173)
  -> API 仍可由 dev script 或 dotnet run 启动
```

生产态：

```text
Journal.exe
  -> Electron main process
  -> detect reusable owned backend or spawn bundled backend/Journal.Api.exe
  -> wait for /health
  -> renderer loadFile(dist/index.html)
  -> renderer API baseUrl from preload bridge
```

### 10.2 端口策略

首版推荐：

- 生产态由 Electron main process 选择空闲 loopback port。
- 通过环境变量传给后端，例如 `ASPNETCORE_URLS=http://127.0.0.1:{port}`。
- 通过 preload bridge 暴露给前端。

避免生产态固定 `5057`，否则端口冲突会让安装版看起来像坏了。

开发态可以继续默认 `5057`，减少对当前测试和脚本的冲击。

### 10.3 后端生命周期

Electron main process 负责：

- 启动后端进程。
- 检测上次残留的自有后端进程。
- 捕获 stdout/stderr 写日志。
- 轮询 `/health`。
- renderer ready 前显示 loading 或错误页。
- app quit 时终止后端进程。
- 后端异常退出时提示用户并保留日志路径。

日志路径建议：

```text
%LocalAppData%/Journal/.journal/logs/
  app-YYYY-MM-DD.log
  backend-YYYY-MM-DD.log
```

### 10.4 残留后端恢复

生产态后端是 Electron 拥有的 child process。正常关闭时，Electron 应终止后端；但如果 Electron 崩溃、机器断电或被任务管理器强杀，`Journal.Api.exe` 可能残留。下一次启动不能盲目再开一个新后端，也不能随便复用端口上任意服务。

新增运行期 lock 文件：

```text
%LocalAppData%/Journal/.journal/runtime/backend.lock.json
```

内容建议：

```json
{
  "pid": 12345,
  "port": 51234,
  "startedAtUtc": "2026-05-14T12:00:00Z",
  "backendVersion": "0.1.0",
  "releaseVersion": "0.1.0",
  "dataRoot": "C:\\Users\\小陌\\AppData\\Local\\Journal",
  "owner": "electron",
  "exePath": "C:\\Program Files\\Journal\\backend\\Journal.Api.exe"
}
```

启动策略：

```text
Electron 启动
  -> 读取 backend.lock.json
  -> 如果 pid 不存在或进程已退出
      -> 启动新的后端
  -> 如果 pid 仍存活
      -> 请求 lock.port 上的 /app/info
      -> 校验身份、版本、数据目录和 exePath
      -> 满足条件则复用
      -> 不满足且确认是安装目录里的自有 Journal.Api.exe，则终止残留并启动新的后端
      -> 无法确认归属则不杀进程，启动失败并提示用户处理
```

复用条件必须同时满足：

- `pid` 仍存在。
- `exePath` 指向当前安装目录下的 `backend/Journal.Api.exe`。
- `owner = "electron"`。
- lock 文件中的 `port` 可访问。
- `/app/info` 返回 `name = "Journal.Api"`。
- `backendVersion` 与当前 release 兼容。
- `dataRoot` 与当前用户数据目录一致。

不能只因为端口可访问就复用。端口上可能是开发态 API，也可能是其他程序。必须通过 `/app/info` 和 lock 文件共同确认身份。

生产态可以清理自己上次留下的残留后端；开发态不应杀死手动启动的 `dotnet run` 或其他 Journal.Api 进程。

### 10.5 前端本地服务状态

前端需要把后端连接状态作为“本地服务状态”展示，而不是让用户只能看到 API 失败。

建议状态：

```text
启动中
已连接
复用上次残留进程
连接失败
后端异常退出
```

显示内容：

- 本地服务状态。
- API 地址。
- PID。
- 后端版本。
- 数据目录。
- 最近健康检查时间。
- 日志路径。

失败操作：

- 重试启动。
- 查看日志。
- 打开数据目录。
- 打开诊断说明。

这块状态可以出现在顶部状态区、About 面板和错误页中。首版不做常驻托盘，也不做 Windows Service。

## 11. Export / Import / Backup

### 11.1 导出数据包

导出目标：

```text
Journal-Export-2026-05-14-120000.zip
```

内容：

```text
manifest.json
entries/
.journal/raw-inputs/
.journal/drafts/
.journal/versions/
.journal/audit/
.journal/settings/ai-providers.safe.json
```

默认不导出：

- `.journal/index/journal.db`
- 完整 API Key
- 临时 logs
- installer artifacts

manifest 示例：

```json
{
  "format": "journal-export/v1",
  "createdAt": "2026-05-14T12:00:00+08:00",
  "appVersion": "0.1.0",
  "backendVersion": "0.1.0",
  "frontendVersion": "0.1.0",
  "entryCount": 12,
  "rawInputCount": 34,
  "versionCount": 8,
  "containsFullApiKeys": false
}
```

### 11.2 导入数据包

导入规则：

1. 校验 zip 内必须有 `manifest.json`。
2. 校验 `format = journal-export/v1`。
3. 检查目标数据目录。
4. 导入前创建当前数据目录备份：

```text
%LocalAppData%/Journal/.journal/import-backups/yyyyMMdd-HHmmss/
```

5. 解压 entries 和 `.journal` 源材料。
6. 不导入旧 SQLite index。
7. 导入完成后执行 index rebuild。
8. 返回导入摘要。

失败规则：

- manifest invalid：不修改当前数据。
- 解压失败：恢复导入前备份。
- rebuild 失败：导入的源材料保留，但 UI 标记 index warning，并允许手动 rebuild。

### 11.3 UI 入口

首版入口放在 About / 设置式面板里，命名为 `数据与备份`：

- 打开数据目录。
- 导出数据包。
- 导入数据包。
- 重建索引。
- 查看最近导入备份。

不要把导入导出塞进今日写作主按钮区，避免干扰晨间写作。

## 12. Windows Installer

### 12.1 工具选择

首版使用 Inno Setup，而不是 electron-builder 的 installer 输出。

原因：

- 当前应用是 Electron + 自托管 `.NET` 后端，发布产物需要明确 staging。
- Inno Setup 对 Program Files、快捷方式、License、InfoBefore、升级卸载控制更直接。
- GitHub Actions 上可以用命令行编译器生成安装包。

### 12.2 Staging 目录

构建流程不直接从 `bin/Release` 或 `apps/desktop/dist` 打包，必须先 staging：

```text
artifacts/installer/publish/Journal/
  Journal.exe
  resources/
  backend/
    Journal.Api.exe
    *.dll
  legal/
    LICENSE
    NOTICE
    PRIVACY.md
    DATA_SAFETY.md
    AI_NOTICE.md
  assets/
    journal.ico
```

安装包输出：

```text
artifacts/installer/dist/
  Journal-Setup-0.1.0.exe
  Journal-Setup-0.1.0.sha256
```

### 12.3 本地构建脚本

推荐脚本：

```text
scripts/release/build-installer.ps1
scripts/release/stage-installer.ps1
scripts/release/write-build-metadata.ps1
scripts/release/verify-installer.ps1
```

脚本职责：

- `write-build-metadata`：生成版本、commit、build time。
- `stage-installer`：清理并重建 `artifacts/installer/publish/Journal`。
- `build-installer`：调用 dotnet publish、npm build、Electron packaging/staging 和 Inno `ISCC.exe`。
- `verify-installer`：执行本地安装验证清单中的可自动化部分。

## 13. GitHub Actions Release Pipeline

### 13.1 触发策略

首版采用两类触发：

```yaml
workflow_dispatch:
push:
  tags:
    - "v*"
```

不要每次 push 都打完整 Windows installer。普通 push 可以跑测试；正式安装包只在手动触发或 tag 发布时构建，避免浪费 GitHub Actions 免费额度。

### 13.2 Job 流程

运行环境：

```text
runs-on: windows-latest
```

步骤：

```text
checkout
setup-dotnet
setup-node
npm ci --prefix apps/desktop
dotnet test Journal.slnx
npm test --prefix apps/desktop
npm run build --prefix apps/desktop
dotnet publish src/Journal.Api -c Release -r win-x64 --self-contained true
stage installer publish folder
install or locate Inno Setup
ISCC.exe installer/windows/Journal.iss
generate sha256
upload artifact
if tag: create/update GitHub Release and upload setup exe + checksum
```

### 13.3 Release 产物

GitHub Release assets：

```text
Journal-Setup-0.1.0.exe
Journal-Setup-0.1.0.sha256
RELEASE_NOTES.md
```

Release notes 包含：

- 版本号。
- 前端版本。
- 后端版本。
- commit。
- 安装说明。
- 升级说明。
- 数据保留说明。
- 已知限制。

## 14. Local-First Validation Gates

Phase 7 必须先本地验证，再接 GitHub Actions。

### 14.1 本地验证

本地必须完成：

1. `dotnet test Journal.slnx`
2. `npm test --prefix apps/desktop`
3. `npm run build --prefix apps/desktop`
4. `scripts/release/build-installer.ps1`
5. 安装 `Journal-Setup-0.1.0.exe`
6. 从开始菜单启动 Journal。
7. About 面板显示前后端版本、commit、build time、数据目录。
8. 写一篇测试日记并确认正式 Markdown。
9. 导出数据包。
10. 卸载应用，确认用户数据默认保留。
11. 重新安装应用。
12. 导入数据包，确认索引重建。
13. 历史搜索和同日年轮可用。

### 14.2 GitHub Actions 验证

CI 必须完成：

- backend tests passed。
- frontend tests passed。
- frontend build passed。
- backend publish passed。
- installer build passed。
- setup exe uploaded as artifact。
- tag build creates Release assets。

CI 不负责真实安装交互验证；真实安装验证仍以本地 Windows 机器为准。

## 15. 错误处理与边界

### 15.1 后端启动失败

Electron 显示错误页：

- 后端未启动。
- 端口绑定失败。
- `/health` timeout。
- 检测到旧后端进程仍在运行但无法确认归属。
- 旧后端属于当前安装版但无法终止。
- 日志路径。
- 重新启动按钮。

不能让用户只看到空白窗口。

### 15.2 残留后端处理失败

如果检测到 lock 文件中的 pid 仍存活，但 `/app/info` 不可访问或身份不匹配：

- 若 `exePath` 不是当前安装目录下的 `Journal.Api.exe`，不终止该进程。
- 若无法确认 owner，不终止该进程。
- 若确认是自有残留进程但终止失败，显示诊断错误和 pid。
- UI 提供 `重试`、`查看日志`、`打开数据目录`，并提示用户可在任务管理器中结束旧进程。

这条规则优先保护用户机器上的其他进程，避免安装版误杀开发态服务或无关程序。

### 15.3 导入失败

导入失败时必须保护当前数据：

- manifest 校验失败：不动当前数据。
- 解压中途失败：恢复导入前备份。
- rebuild 失败：保留源材料，给出 index warning。

### 15.4 安装失败

安装失败不应删除用户数据。升级安装失败时，应避免破坏旧版本已安装程序；如果无法保证 rollback，至少要在安装前关闭应用并清晰提示失败。

### 15.5 卸载

卸载默认保留 `%LocalAppData%/Journal`。任何删除个人数据的动作都必须显式、二次确认，并且首版不实现这个选项。

## 16. 测试策略

### 16.1 后端测试

- `/app/info` 返回版本、环境和数据目录。
- build metadata 缺失时仍返回 fallback。
- 导出 manifest 正确。
- 导出不包含完整 API Key。
- 导入前创建备份。
- 导入 invalid manifest 不修改当前数据。
- 导入后触发 index rebuild。
- 导入失败能恢复备份。

### 16.2 前端测试

- About 面板显示 frontend/backend/release 版本。
- 后端信息加载失败时显示可读错误。
- 数据与备份面板可以触发导出/导入/rebuild。
- 导入确认提示明确说明会先备份当前数据。
- Privacy / Data Safety / AI Notice 链接可达。

### 16.3 Electron 测试

- 开发态继续 load Vite。
- 生产态 spawn backend。
- 生产态发现可复用的自有残留 backend 时复用。
- 生产态发现不兼容的自有残留 backend 时清理并重启。
- 生产态发现无法确认归属的进程时不误杀。
- backend ready 后 renderer 获得 API base URL。
- app quit 时终止 backend。
- backend 启动失败显示错误页。
- 前端显示本地服务状态、PID、端口和日志路径。

### 16.4 Installer 验证

- 安装完成。
- 程序文件进入 `%ProgramFiles%/Journal`。
- 快捷方式创建。
- About 显示安装版版本。
- 升级保留 `%LocalAppData%/Journal`。
- 卸载保留 `%LocalAppData%/Journal`。
- 重新安装后能继续读取旧数据。

## 17. 验收标准

Phase 7 完成后必须满足：

1. 本地能生成 `Journal-Setup-0.1.0.exe`。
2. 安装包有正式图标、License 页、隐私/数据安全说明和完成页。
3. 安装后能从开始菜单启动 Journal。
4. Electron 生产态能自动启动 `.NET` 后端。
5. Electron 启动时能检测、复用或清理上次残留的自有后端。
6. Electron 不会误杀开发态 API 或无法确认归属的进程。
7. 前端能显示本地服务状态、PID、端口、后端版本、日志路径和数据目录。
8. About 面板能看到前端版本、后端版本、release、commit、build time 和数据目录。
9. 用户能导出完整 Journal 数据包。
10. 用户能导入数据包，导入前自动备份当前数据。
11. 导入后 SQLite index 从源材料重建。
12. 升级安装保留用户数据。
13. 卸载默认保留用户数据。
14. GitHub Actions 能在 `windows-latest` 上生成安装包 artifact。
15. `v0.1.0` tag 能生成 GitHub Release assets。

## 18. 后续扩展

Phase 7 完成后，可以继续：

- Phase 4B：diff / rollback / 外部修改处理 UI。
- Future Notes：未来提醒和长期锚点。
- 自动备份计划任务。
- 加密与敏感内容保护。
- 代码签名证书与安装包签名。
- 自动更新。
- 云同步或跨设备同步。

## 19. Spec 自审

- Placeholder scan：无待补占位、未完成标记或空章节。
- Internal consistency：安装、版本、导入导出、GitHub Actions 都围绕 Windows 本地生产可用；卸载默认保留用户数据。
- Scope check：范围包含安装包和发布流水线，但排除云同步、自动更新、加密、代码签名和 Windows Service。
- Ambiguity check：版本来源、图标路径、声明文档、安装目录、用户数据目录、后端残留恢复、导入失败策略、CI 触发策略均已明确。
