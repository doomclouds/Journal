# Phase 1 Skeleton Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Phase 1 Journal engineering skeleton: .NET local API, Electron + React desktop shell, `/health` connectivity, minimal tests, and runnable documentation.

**Architecture:** Development uses two visible processes: ASP.NET Core Minimal API and Electron/Vite. React fetches the local API health endpoint and renders a status panel. Phase 1 deliberately excludes diary persistence, AI, Markdown generation, SQLite, installers, and Electron-managed API lifecycle.

**Tech Stack:** .NET 10, ASP.NET Core Minimal API, xUnit, Microsoft.AspNetCore.Mvc.Testing, Electron, React, TypeScript, Vite, Vitest, Testing Library.

---

## File Structure

- `Journal.slnx`: .NET solution file created by `dotnet new sln -n Journal` with .NET 10 SDK.
- `src/Journal.Api/`: local ASP.NET Core API. Owns HTTP endpoints and startup.
- `src/Journal.Domain/`: stable domain/application metadata. Owns `ApplicationInfo`.
- `src/Journal.Infrastructure/`: reserved infrastructure assembly. Owns future file, SQLite, and AI Provider implementations.
- `tests/Journal.Tests/`: .NET tests for API and domain contracts.
- `apps/desktop/`: Electron + React + TypeScript + Vite desktop app.
- `.gitignore`: ignores .NET, Node, Electron, Vite, test, and editor outputs.
- `README.md`: explains exact development commands and Phase 1 scope.

## Task 1: Create .NET Solution Skeleton

**Files:**
- Create: `Journal.slnx`
- Create: `src/Journal.Api/Journal.Api.csproj`
- Create: `src/Journal.Api/Program.cs`
- Create: `src/Journal.Api/Properties/launchSettings.json`
- Create: `src/Journal.Domain/Journal.Domain.csproj`
- Create: `src/Journal.Infrastructure/Journal.Infrastructure.csproj`
- Create: `tests/Journal.Tests/Journal.Tests.csproj`

- [ ] **Step 1: Create projects**

Run:

```powershell
dotnet new sln -n Journal
dotnet new web -n Journal.Api -o src/Journal.Api --framework net10.0
dotnet new classlib -n Journal.Domain -o src/Journal.Domain --framework net10.0
dotnet new classlib -n Journal.Infrastructure -o src/Journal.Infrastructure --framework net10.0
dotnet new xunit -n Journal.Tests -o tests/Journal.Tests --framework net10.0
```

Expected:

```text
Journal.slnx exists
src/Journal.Api/Journal.Api.csproj exists
src/Journal.Domain/Journal.Domain.csproj exists
src/Journal.Infrastructure/Journal.Infrastructure.csproj exists
tests/Journal.Tests/Journal.Tests.csproj exists
```

- [ ] **Step 2: Add projects to solution**

Run:

```powershell
dotnet sln Journal.slnx add src/Journal.Api/Journal.Api.csproj
dotnet sln Journal.slnx add src/Journal.Domain/Journal.Domain.csproj
dotnet sln Journal.slnx add src/Journal.Infrastructure/Journal.Infrastructure.csproj
dotnet sln Journal.slnx add tests/Journal.Tests/Journal.Tests.csproj
```

Expected: each command reports that the project was added to the solution.

- [ ] **Step 3: Add project references**

Run:

```powershell
dotnet add src/Journal.Api/Journal.Api.csproj reference src/Journal.Domain/Journal.Domain.csproj
dotnet add src/Journal.Api/Journal.Api.csproj reference src/Journal.Infrastructure/Journal.Infrastructure.csproj
dotnet add src/Journal.Infrastructure/Journal.Infrastructure.csproj reference src/Journal.Domain/Journal.Domain.csproj
dotnet add tests/Journal.Tests/Journal.Tests.csproj reference src/Journal.Api/Journal.Api.csproj
dotnet add tests/Journal.Tests/Journal.Tests.csproj reference src/Journal.Domain/Journal.Domain.csproj
dotnet add tests/Journal.Tests/Journal.Tests.csproj package Microsoft.AspNetCore.Mvc.Testing
```

Expected: references and package are added successfully.

- [ ] **Step 4: Remove generated default classes**

Delete these files if generated:

```text
src/Journal.Domain/Class1.cs
src/Journal.Infrastructure/Class1.cs
tests/Journal.Tests/UnitTest1.cs
```

Run:

```powershell
Remove-Item -LiteralPath src/Journal.Domain/Class1.cs -ErrorAction SilentlyContinue
Remove-Item -LiteralPath src/Journal.Infrastructure/Class1.cs -ErrorAction SilentlyContinue
Remove-Item -LiteralPath tests/Journal.Tests/UnitTest1.cs -ErrorAction SilentlyContinue
```

- [ ] **Step 5: Build empty skeleton**

Run:

```powershell
dotnet build Journal.slnx
```

Expected: build succeeds.

- [ ] **Step 6: Commit skeleton**

Run:

```powershell
git add Journal.slnx src/Journal.Api src/Journal.Domain src/Journal.Infrastructure tests/Journal.Tests
git commit -m "chore: add dotnet solution skeleton"
```

## Task 2: Define Health Contract With Failing Tests

**Files:**
- Create: `tests/Journal.Tests/HealthEndpointTests.cs`
- Modify: `tests/Journal.Tests/Journal.Tests.csproj`

- [ ] **Step 1: Write failing API health tests**

Create `tests/Journal.Tests/HealthEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Journal.Tests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsApplicationStatus()
    {
        using var client = _factory.CreateClient();

        var payload = await client.GetFromJsonAsync<HealthResponse>("/health");

        Assert.NotNull(payload);
        Assert.Equal("Journal.Api", payload.App);
        Assert.Equal("ok", payload.Status);
        Assert.Equal("0.1.0", payload.Version);
        Assert.False(string.IsNullOrWhiteSpace(payload.Environment));
        Assert.True(payload.ServerTime > DateTimeOffset.MinValue);
    }

    private sealed record HealthResponse(
        string App,
        string Status,
        string Version,
        string Environment,
        DateTimeOffset ServerTime);
}
```

- [ ] **Step 2: Verify RED**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj
```

Expected: tests fail because `GET /health` is not implemented or because `Program` is not public yet.

The acceptable failure signals are:

```text
404 Not Found
```

or:

```text
Program is inaccessible due to its protection level
```

If the test fails for a typo or missing package restore, fix the test setup and rerun until it fails for the missing API contract.

## Task 3: Implement Minimal Health API

**Files:**
- Create: `src/Journal.Domain/Application/ApplicationInfo.cs`
- Modify: `src/Journal.Api/Program.cs`

- [ ] **Step 1: Add application metadata type**

Create `src/Journal.Domain/Application/ApplicationInfo.cs`:

```csharp
namespace Journal.Domain.Application;

public static class ApplicationInfo
{
    public const string Name = "Journal.Api";
    public const string Version = "0.1.0";
}
```

- [ ] **Step 2: Implement `/health` endpoint**

Replace `src/Journal.Api/Program.cs` with:

```csharp
using Journal.Domain.Application;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("DesktopDevelopment", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("DesktopDevelopment");

app.MapGet("/health", (IHostEnvironment environment) =>
{
    return Results.Ok(new HealthResponse(
        ApplicationInfo.Name,
        "ok",
        ApplicationInfo.Version,
        environment.EnvironmentName,
        DateTimeOffset.Now));
});

app.Run();

public partial class Program
{
}

public sealed record HealthResponse(
    string App,
    string Status,
    string Version,
    string Environment,
    DateTimeOffset ServerTime);
```

- [ ] **Step 3: Verify GREEN**

Run:

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj
```

Expected: tests pass.

- [ ] **Step 4: Verify full .NET solution**

Run:

```powershell
dotnet test Journal.slnx
```

Expected: all .NET tests pass.

- [ ] **Step 5: Commit health API**

Run:

```powershell
git add src/Journal.Api/Program.cs src/Journal.Domain/Application/ApplicationInfo.cs tests/Journal.Tests/HealthEndpointTests.cs tests/Journal.Tests/Journal.Tests.csproj
git commit -m "feat(api): add health endpoint"
```

## Task 4: Create Desktop App Skeleton

**Files:**
- Create: `apps/desktop/package.json`
- Create: `apps/desktop/index.html`
- Create: `apps/desktop/tsconfig.json`
- Create: `apps/desktop/tsconfig.node.json`
- Create: `apps/desktop/vite.config.ts`
- Create: `apps/desktop/src/main.tsx`
- Create: `apps/desktop/src/App.tsx`
- Create: `apps/desktop/src/styles.css`
- Create: `apps/desktop/src/test/setup.ts`
- Create: `apps/desktop/src/App.test.tsx`
- Create: `apps/desktop/electron/main.cjs`
- Create: `apps/desktop/electron/preload.cjs`

- [ ] **Step 1: Create Vite React TypeScript app**

Run:

```powershell
npm create vite@latest apps/desktop -- --template react-ts
```

Expected: Vite scaffolds `apps/desktop`.

- [ ] **Step 2: Replace `apps/desktop/package.json`**

Use this exact file:

```json
{
  "name": "@journal/desktop",
  "private": true,
  "version": "0.1.0",
  "type": "module",
  "main": "electron/main.cjs",
  "scripts": {
    "dev": "vite",
    "electron": "wait-on http://localhost:5173 && electron .",
    "desktop": "concurrently -k \"npm run dev\" \"npm run electron\"",
    "build": "tsc -b && vite build",
    "preview": "vite preview",
    "test": "vitest run",
    "test:watch": "vitest"
  },
  "dependencies": {
    "@vitejs/plugin-react": "latest",
    "vite": "latest",
    "typescript": "latest",
    "react": "latest",
    "react-dom": "latest"
  },
  "devDependencies": {
    "@testing-library/jest-dom": "latest",
    "@testing-library/react": "latest",
    "@types/node": "latest",
    "@types/react": "latest",
    "@types/react-dom": "latest",
    "concurrently": "latest",
    "electron": "latest",
    "jsdom": "latest",
    "vitest": "latest",
    "wait-on": "latest"
  }
}
```

- [ ] **Step 3: Install dependencies**

Run:

```powershell
npm install --prefix apps/desktop
```

Expected: `apps/desktop/package-lock.json` and `apps/desktop/node_modules` are created.

- [ ] **Step 4: Configure Vite and tests**

Replace `apps/desktop/vite.config.ts`:

```ts
import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  server: {
    host: "127.0.0.1",
    port: 5173,
    strictPort: true
  },
  test: {
    environment: "jsdom",
    setupFiles: "./src/test/setup.ts"
  }
});
```

Create `apps/desktop/src/test/setup.ts`:

```ts
import "@testing-library/jest-dom/vitest";
```

- [ ] **Step 5: Implement Electron main process**

Create `apps/desktop/electron/main.cjs`:

```js
const { app, BrowserWindow } = require("electron");
const path = require("node:path");

const isDev = !app.isPackaged;

function createWindow() {
  const mainWindow = new BrowserWindow({
    width: 1180,
    height: 780,
    minWidth: 960,
    minHeight: 640,
    title: "Journal",
    backgroundColor: "#f6efe4",
    webPreferences: {
      preload: path.join(__dirname, "preload.cjs"),
      contextIsolation: true,
      nodeIntegration: false
    }
  });

  if (isDev) {
    mainWindow.loadURL("http://localhost:5173");
  } else {
    mainWindow.loadFile(path.join(__dirname, "../dist/index.html"));
  }
}

app.whenReady().then(() => {
  createWindow();

  app.on("activate", () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow();
    }
  });
});

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") {
    app.quit();
  }
});
```

Create `apps/desktop/electron/preload.cjs`:

```js
const { contextBridge } = require("electron");

contextBridge.exposeInMainWorld("journalDesktop", {
  platform: process.platform
});
```

- [ ] **Step 6: Commit desktop skeleton**

Run:

```powershell
git add apps/desktop
git commit -m "chore(desktop): add electron react skeleton"
```

## Task 5: Implement React Health Status UI With Tests

**Files:**
- Modify: `apps/desktop/src/App.tsx`
- Modify: `apps/desktop/src/App.test.tsx`
- Modify: `apps/desktop/src/main.tsx`
- Modify: `apps/desktop/src/styles.css`

- [ ] **Step 1: Write failing React tests**

Replace `apps/desktop/src/App.test.tsx`:

```tsx
import { render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, test, vi } from "vitest";
import App from "./App";

afterEach(() => {
  vi.restoreAllMocks();
});

describe("App", () => {
  test("renders phase 1 skeleton status", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        app: "Journal.Api",
        status: "ok",
        version: "0.1.0",
        environment: "Development",
        serverTime: "2026-05-07T20:30:00+08:00"
      })
    }));

    render(<App />);

    expect(screen.getByRole("heading", { name: "Journal" })).toBeInTheDocument();
    expect(screen.getByText("Phase 1 Skeleton")).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText("online")).toBeInTheDocument());
    expect(screen.getByText("Journal.Api")).toBeInTheDocument();
    expect(screen.getByText("0.1.0")).toBeInTheDocument();
  });

  test("renders offline state when health check fails", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("connection refused")));

    render(<App />);

    await waitFor(() => expect(screen.getByText("offline")).toBeInTheDocument());
    expect(screen.getByText("connection refused")).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Verify RED**

Run:

```powershell
npm test --prefix apps/desktop
```

Expected: tests fail because `App.tsx` does not render the expected Phase 1 UI yet.

- [ ] **Step 3: Implement `App.tsx`**

Replace `apps/desktop/src/App.tsx`:

```tsx
import { useEffect, useMemo, useState } from "react";
import "./styles.css";

type ApiStatus = "checking" | "online" | "offline";

type HealthResponse = {
  app: string;
  status: string;
  version: string;
  environment: string;
  serverTime: string;
};

const apiBaseUrl = import.meta.env.VITE_JOURNAL_API_URL ?? "http://localhost:5057";

export default function App() {
  const [status, setStatus] = useState<ApiStatus>("checking");
  const [health, setHealth] = useState<HealthResponse | null>(null);
  const [error, setError] = useState<string>("");

  const today = useMemo(() => {
    return new Intl.DateTimeFormat("zh-CN", {
      dateStyle: "full"
    }).format(new Date());
  }, []);

  useEffect(() => {
    let cancelled = false;

    async function loadHealth() {
      try {
        const response = await fetch(`${apiBaseUrl}/health`);
        if (!response.ok) {
          throw new Error(`health check failed: ${response.status}`);
        }

        const payload = await response.json() as HealthResponse;
        if (!cancelled) {
          setHealth(payload);
          setStatus("online");
          setError("");
        }
      } catch (caught) {
        if (!cancelled) {
          setStatus("offline");
          setHealth(null);
          setError(caught instanceof Error ? caught.message : "unknown error");
        }
      }
    }

    loadHealth();

    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <main className="app-shell">
      <section className="hero-panel">
        <p className="eyebrow">Phase 1 Skeleton</p>
        <h1>Journal</h1>
        <p className="lead">每天 3 分钟的人生坐标系统，先从一个稳定的桌面工程骨架开始。</p>
      </section>

      <section className="status-panel" aria-label="应用状态">
        <div className="status-row">
          <span>今天</span>
          <strong>{today}</strong>
        </div>
        <div className="status-row">
          <span>API 状态</span>
          <strong className={`status status-${status}`}>{status}</strong>
        </div>
        {health ? (
          <dl className="health-grid">
            <div>
              <dt>服务</dt>
              <dd>{health.app}</dd>
            </div>
            <div>
              <dt>版本</dt>
              <dd>{health.version}</dd>
            </div>
            <div>
              <dt>环境</dt>
              <dd>{health.environment}</dd>
            </div>
            <div>
              <dt>服务端时间</dt>
              <dd>{health.serverTime}</dd>
            </div>
          </dl>
        ) : (
          <p className="error-text">{status === "checking" ? "正在检查本地 API..." : error}</p>
        )}
      </section>

      <section className="next-panel">
        <span>Next</span>
        <p>阶段 2：自然语言文本 -> Mock AI JSON -> JMF Markdown 预览。</p>
      </section>
    </main>
  );
}
```

- [ ] **Step 4: Implement `main.tsx`**

Replace `apps/desktop/src/main.tsx`:

```tsx
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import App from "./App";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App />
  </StrictMode>
);
```

- [ ] **Step 5: Implement CSS**

Replace `apps/desktop/src/styles.css`:

```css
:root {
  color: #24211d;
  background: #f4ecdf;
  font-family: Inter, "Microsoft YaHei", "PingFang SC", system-ui, sans-serif;
  font-synthesis: none;
  text-rendering: optimizeLegibility;
}

* {
  box-sizing: border-box;
}

body {
  margin: 0;
  min-width: 320px;
  min-height: 100vh;
  background:
    radial-gradient(circle at 18% 12%, rgba(255, 255, 255, 0.78), transparent 30%),
    linear-gradient(135deg, #fbf6ee 0%, #eadfce 100%);
}

button,
input,
textarea {
  font: inherit;
}

.app-shell {
  min-height: 100vh;
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(320px, 440px);
  gap: 32px;
  align-items: center;
  padding: clamp(28px, 6vw, 76px);
}

.hero-panel h1 {
  margin: 0 0 18px;
  font-size: clamp(56px, 11vw, 132px);
  line-height: 0.9;
  letter-spacing: 0;
}

.eyebrow {
  margin: 0 0 14px;
  color: #08736d;
  font-size: 13px;
  font-weight: 800;
  letter-spacing: 0.14em;
  text-transform: uppercase;
}

.lead {
  max-width: 680px;
  margin: 0;
  color: #5e574c;
  font-size: clamp(20px, 2.5vw, 30px);
  line-height: 1.55;
}

.status-panel,
.next-panel {
  border: 1px solid rgba(43, 36, 28, 0.14);
  background: rgba(255, 252, 246, 0.72);
  box-shadow: 0 24px 60px rgba(67, 52, 33, 0.12);
  backdrop-filter: blur(18px);
}

.status-panel {
  display: grid;
  gap: 18px;
  padding: 28px;
}

.status-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 20px;
  color: #6e665a;
  font-size: 15px;
}

.status-row strong {
  color: #24211d;
  text-align: right;
}

.status {
  min-width: 88px;
  padding: 7px 11px;
  text-align: center;
}

.status-checking {
  color: #7a5a18;
  background: #f3dfb0;
}

.status-online {
  color: #0c5c50;
  background: #cce9df;
}

.status-offline {
  color: #7d2c24;
  background: #f1c7bd;
}

.health-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
  margin: 4px 0 0;
}

.health-grid div {
  padding: 14px;
  background: rgba(255, 255, 255, 0.58);
  border: 1px solid rgba(43, 36, 28, 0.1);
}

.health-grid dt {
  margin-bottom: 7px;
  color: #746b5f;
  font-size: 12px;
}

.health-grid dd {
  margin: 0;
  color: #24211d;
  font-weight: 700;
  word-break: break-word;
}

.error-text {
  margin: 0;
  color: #7d2c24;
}

.next-panel {
  grid-column: 1 / -1;
  padding: 18px 22px;
  display: flex;
  align-items: center;
  gap: 14px;
}

.next-panel span {
  color: #08736d;
  font-size: 12px;
  font-weight: 800;
  letter-spacing: 0.14em;
  text-transform: uppercase;
}

.next-panel p {
  margin: 0;
  color: #5e574c;
}

@media (max-width: 860px) {
  .app-shell {
    grid-template-columns: 1fr;
    align-items: start;
  }

  .health-grid {
    grid-template-columns: 1fr;
  }

  .next-panel {
    align-items: flex-start;
    flex-direction: column;
  }
}
```

- [ ] **Step 6: Verify GREEN**

Run:

```powershell
npm test --prefix apps/desktop
```

Expected: Vitest tests pass.

- [ ] **Step 7: Verify frontend build**

Run:

```powershell
npm run build --prefix apps/desktop
```

Expected: TypeScript and Vite build pass.

- [ ] **Step 8: Commit React health UI**

Run:

```powershell
git add apps/desktop/src apps/desktop/vite.config.ts
git commit -m "feat(desktop): show api health status"
```

## Task 6: Add Repository Ignore Rules And Documentation

**Files:**
- Create: `.gitignore`
- Modify: `README.md`
- Modify: `PROJECT_VISION.md`

- [ ] **Step 1: Create `.gitignore`**

Create `.gitignore`:

```gitignore
# .NET
bin/
obj/
TestResults/
*.user
*.suo

# Node / Electron / Vite
node_modules/
dist/
dist-electron/
coverage/
*.tsbuildinfo

# Environment and secrets
.env
.env.*
!.env.example

# Logs
*.log
npm-debug.log*
yarn-debug.log*
yarn-error.log*

# OS / editor
.DS_Store
Thumbs.db
.idea/
.vscode/
```

- [ ] **Step 2: Update README**

Replace `README.md` with:

````markdown
# Journal

一个晨间日记的桌面应用，用来记录每个人的一生的日记内容。

## 项目文档

- [项目愿景](./PROJECT_VISION.md)
- [阶段 1 设计](./docs/superpowers/specs/2026-05-07-phase-1-skeleton-design.md)
- [产品故事演示](./docs/product/journal-product-story.html)

## 阶段 1：应用框架骨架

阶段 1 只验证 Electron + React + .NET 本地 API 的工程链路。

不包含：

- 日记输入和保存
- AI Provider
- Markdown/JMF 生成
- SQLite 索引
- 安装包

## 环境要求

- .NET SDK 10
- Node.js 24 或兼容版本
- npm 11 或兼容版本

## 启动 .NET API

```powershell
dotnet run --project src/Journal.Api
```

API 默认提供：

```text
GET http://localhost:5057/health
```

## 启动桌面前端

```powershell
npm install --prefix apps/desktop
npm run desktop --prefix apps/desktop
```

开发期采用双进程模式：先启动 .NET API，再启动 Electron/Vite 桌面前端。

## 验证

```powershell
dotnet test Journal.slnx
npm test --prefix apps/desktop
npm run build --prefix apps/desktop
```
````

- [ ] **Step 3: Update PROJECT_VISION phase status**

Append this short note under `### 阶段 1：应用框架骨架` after the first goal paragraph:

```markdown
> 执行策略：阶段 1 已确认采用 B 方案：工程化薄壳闭环。开发期先使用 .NET API 与 Electron/Vite 双进程联通，不在本阶段处理 Electron 托管 .NET 后端进程。
```

- [ ] **Step 4: Commit docs and ignore rules**

Run:

```powershell
git add .gitignore README.md PROJECT_VISION.md
git commit -m "docs: document phase 1 skeleton workflow"
```

## Task 7: Runtime Verification

**Files:**
- No new files required.

- [ ] **Step 1: Run all automated checks**

Run:

```powershell
dotnet test Journal.slnx
npm test --prefix apps/desktop
npm run build --prefix apps/desktop
```

Expected:

```text
.NET tests pass
Vitest tests pass
Vite build succeeds
```

- [ ] **Step 2: Start API**

Run:

```powershell
dotnet run --project src/Journal.Api --urls http://localhost:5057
```

Expected:

```text
Now listening on: http://localhost:5057
```

- [ ] **Step 3: Verify API by HTTP**

In a second terminal, run:

```powershell
Invoke-RestMethod -Uri http://localhost:5057/health
```

Expected response includes:

```text
app       : Journal.Api
status    : ok
version   : 0.1.0
```

- [ ] **Step 4: Start Electron desktop**

Run:

```powershell
npm run desktop --prefix apps/desktop
```

Expected:

```text
Electron opens a Journal window
React page shows API 状态 online
```

- [ ] **Step 5: Capture final status**

Run:

```powershell
git status --short --branch
git log --oneline -5
```

Expected:

```text
No unstaged implementation changes remain unless final verification artifacts are intentionally untracked
Recent commits show the Phase 1 skeleton commits
```

## Self-Review

- Spec coverage: Tasks cover .NET API, React/Electron desktop shell, health connectivity, tests, docs, and runtime verification.
- Scope control: No task implements diary persistence, AI Provider, JMF/Markdown generation, SQLite, voice input, installer, or Electron-managed API lifecycle.
- TDD coverage: Backend health API and React health UI both require failing tests before implementation.
- Type consistency: `ApplicationInfo`, `HealthResponse`, `/health`, `app`, `status`, `version`, `environment`, and `serverTime` are consistent across API, tests, and React UI.
