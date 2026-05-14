# Phase 7 Windows Release Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a production-ready Windows release flow for Journal: version-visible installed app, owned local backend lifecycle, formal icon/legal surfaces, data export/import, Inno Setup installer, and GitHub Actions release packaging.

**Architecture:** Keep Journal local-first. Electron owns the production `Journal.Api` child process and exposes runtime status to React through preload IPC; backend owns app info, export/import, storage paths, and index rebuild; installer scripts stage immutable binaries separately from user data. Local installer validation comes before CI release automation.

**Tech Stack:** .NET 10 minimal API, xUnit, Electron CommonJS main/preload, React + TypeScript + Vite, Vitest, PowerShell release scripts, Inno Setup, GitHub Actions `windows-latest`.

---

## File Map

- Backend version/info:
  - Modify: `src/Journal.Domain/Application/ApplicationInfo.cs`
  - Create: `src/Journal.Domain/Application/ApplicationBuildInfo.cs`
  - Modify: `src/Journal.Api/Program.cs`
  - Test: `tests/Journal.Tests/HealthEndpointTests.cs`

- Frontend About and service status:
  - Modify: `apps/desktop/src/api.ts`
  - Modify: `apps/desktop/src/App.tsx`
  - Modify: `apps/desktop/src/styles.css`
  - Modify: `apps/desktop/electron/menu.cjs`
  - Modify: `apps/desktop/electron/nativeMenuBridge.cjs`
  - Modify: `apps/desktop/electron/preload.cjs`
  - Test: `apps/desktop/src/App.test.tsx`
  - Test: `apps/desktop/src/electronMenu.test.ts`
  - Test: `apps/desktop/src/nativeMenuBridge.test.ts`

- Electron production backend lifecycle:
  - Create: `apps/desktop/electron/backendRuntime.cjs`
  - Modify: `apps/desktop/electron/main.cjs`
  - Modify: `apps/desktop/electron/preload.cjs`
  - Create: `apps/desktop/src/serviceStatus.ts`
  - Test: `apps/desktop/src/serviceStatus.test.ts`
  - Test: `apps/desktop/src/App.test.tsx`

- Icon and legal/release identity:
  - Create: `assets/app-icon/`
  - Create: `docs/legal/PRIVACY.md`
  - Create: `docs/legal/DATA_SAFETY.md`
  - Create: `docs/legal/AI_NOTICE.md`
  - Create: `docs/legal/PERSONAL_STATEMENT.md`
  - Create: `docs/legal/DISCLAIMER.md`
  - Create: `docs/release/RELEASE_NOTES.md`
  - Create: `docs/release/GITHUB_RELEASE_TEMPLATE.md`

- Data export/import:
  - Modify: `src/Journal.Infrastructure/Storage/LocalJournalPaths.cs`
  - Create: `src/Journal.Domain/Entries/JournalDataPortabilityModels.cs`
  - Create: `src/Journal.Infrastructure/Storage/JournalDataExportService.cs`
  - Create: `src/Journal.Infrastructure/Storage/JournalDataImportService.cs`
  - Modify: `src/Journal.Api/Program.cs`
  - Test: `tests/Journal.Tests/JournalDataExportServiceTests.cs`
  - Test: `tests/Journal.Tests/JournalDataImportServiceTests.cs`
  - Test: `tests/Journal.Tests/TodayJournalEndpointTests.cs`

- Installer and release automation:
  - Create: `scripts/release/write-build-metadata.ps1`
  - Create: `scripts/release/stage-installer.ps1`
  - Create: `scripts/release/build-installer.ps1`
  - Create: `scripts/release/verify-installer.ps1`
  - Create: `installer/windows/Journal.iss`
  - Create: `installer/windows/assets/`
  - Modify: `.gitignore`
  - Modify: `apps/desktop/package.json`
  - Modify: `apps/desktop/package-lock.json`
  - Create: `.github/workflows/release-windows.yml`
  - Modify: `README.md`
  - Modify: `AGENTS.md`

## Task 1: Backend Version Center And `/app/info`

**Files:**
- Modify: `src/Journal.Domain/Application/ApplicationInfo.cs`
- Create: `src/Journal.Domain/Application/ApplicationBuildInfo.cs`
- Modify: `src/Journal.Api/Program.cs`
- Test: `tests/Journal.Tests/HealthEndpointTests.cs`

- [ ] **Step 1: Add failing endpoint test**

Append this test to `tests/Journal.Tests/HealthEndpointTests.cs`:

```csharp
[Fact]
public async Task GetAppInfo_ReturnsVersionBuildAndStoragePaths()
{
    using var workspace = TempWorkspace.Create();
    using var factory = TodayJournalEndpointTests.CreateFactory(workspace.Root);
    using var client = factory.CreateClient();

    using var response = await client.GetAsync("/app/info");

    response.EnsureSuccessStatusCode();
    using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    var root = document.RootElement;
    Assert.Equal("Journal.Api", root.GetProperty("name").GetString());
    Assert.Equal("0.1.0", root.GetProperty("version").GetString());
    Assert.Equal("0.1.0", root.GetProperty("releaseVersion").GetString());
    Assert.True(root.TryGetProperty("commit", out _));
    Assert.True(root.TryGetProperty("buildTimeUtc", out _));
    Assert.Equal("Development", root.GetProperty("environment").GetString());
    Assert.Equal(workspace.Root, root.GetProperty("dataRoot").GetString());
    Assert.EndsWith(Path.Combine(".journal", "index", "journal.db"), root.GetProperty("indexPath").GetString());
}
```

If `CreateFactory` is private, change it to `internal static` in `TodayJournalEndpointTests.cs`.

- [ ] **Step 2: Run failing test**

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "HealthEndpointTests|GetAppInfo"
```

Expected: build or test fails because `/app/info` and build info model do not exist.

- [ ] **Step 3: Add build info model**

Create `src/Journal.Domain/Application/ApplicationBuildInfo.cs`:

```csharp
namespace Journal.Domain.Application;

public sealed record ApplicationBuildInfo(
    string ReleaseVersion,
    string Commit,
    string BuildTimeUtc)
{
    public static ApplicationBuildInfo Current { get; } = new(
        GetValue("JOURNAL_RELEASE_VERSION", ApplicationInfo.Version),
        GetValue("JOURNAL_BUILD_COMMIT", "dev"),
        GetValue("JOURNAL_BUILD_TIME_UTC", "local"));

    private static string GetValue(string name, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
```

- [ ] **Step 4: Add app info endpoint**

In `src/Journal.Api/Program.cs`, after `/health`, add:

```csharp
app.MapGet("/app/info", (
    IHostEnvironment environment,
    JournalStorageOptions storageOptions,
    LocalJournalPaths paths) =>
{
    var build = ApplicationBuildInfo.Current;
    return Results.Ok(new AppInfoResponse(
        ApplicationInfo.Name,
        ApplicationInfo.Version,
        build.ReleaseVersion,
        build.Commit,
        build.BuildTimeUtc,
        environment.EnvironmentName,
        storageOptions.RootDirectory,
        paths.IndexPath()));
});
```

Add this response record near `HealthResponse`:

```csharp
public sealed record AppInfoResponse(
    string Name,
    string Version,
    string ReleaseVersion,
    string Commit,
    string BuildTimeUtc,
    string Environment,
    string DataRoot,
    string IndexPath);
```

- [ ] **Step 5: Run focused tests**

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "HealthEndpointTests|TodayJournalEndpointTests"
```

Expected: tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src/Journal.Domain/Application/ApplicationBuildInfo.cs src/Journal.Api/Program.cs tests/Journal.Tests/HealthEndpointTests.cs tests/Journal.Tests/TodayJournalEndpointTests.cs
git commit -m "feat: expose application build info"
```

## Task 2: Frontend About Panel And Version Display

**Files:**
- Modify: `apps/desktop/src/api.ts`
- Modify: `apps/desktop/electron/menu.cjs`
- Modify: `apps/desktop/electron/nativeMenuBridge.cjs`
- Modify: `apps/desktop/src/App.tsx`
- Modify: `apps/desktop/src/styles.css`
- Test: `apps/desktop/src/App.test.tsx`
- Test: `apps/desktop/src/electronMenu.test.ts`
- Test: `apps/desktop/src/nativeMenuBridge.test.ts`

- [ ] **Step 1: Add failing API and App tests**

In `apps/desktop/src/App.test.tsx`, add an `appInfo` fixture:

```ts
const appInfo = {
  name: "Journal.Api",
  version: "0.1.0",
  releaseVersion: "0.1.0",
  commit: "abc1234",
  buildTimeUtc: "2026-05-14T12:00:00Z",
  environment: "Production",
  dataRoot: "C:\\Users\\10062\\AppData\\Local\\Journal",
  indexPath: "C:\\Users\\10062\\AppData\\Local\\Journal\\.journal\\index\\journal.db"
};
```

Add this test:

```tsx
test("opens about panel from native menu and shows frontend and backend versions", async () => {
  const fetchMock = vi
    .fn()
    .mockResolvedValueOnce(mockJsonResponse(healthResponse))
    .mockResolvedValueOnce(mockJsonResponse(createEditorState(processedToday())))
    .mockResolvedValueOnce(mockJsonResponse(aiSettings))
    .mockResolvedValueOnce(mockJsonResponse(appInfo));
  vi.stubGlobal("fetch", fetchMock);
  const handlers: Array<(command: string) => void> = [];
  window.journalDesktop = {
    platform: "win32",
    onNativeMenuCommand: handler => {
      handlers.push(handler);
      return () => undefined;
    }
  };

  render(<App />);
  await screen.findByText("本地优先晨间日记");
  act(() => handlers[0]("open-about"));

  expect(await screen.findByRole("dialog", { name: "关于 Journal" })).toBeInTheDocument();
  expect(screen.getByText("Backend 0.1.0")).toBeInTheDocument();
  expect(screen.getByText(/Frontend/)).toBeInTheDocument();
  expect(screen.getByText(/abc1234/)).toBeInTheDocument();
  expect(screen.getByText(/AppData\\Local\\Journal/)).toBeInTheDocument();
});
```

In `apps/desktop/src/electronMenu.test.ts`, update the expected command list to include `open-about`.

- [ ] **Step 2: Run failing tests**

```powershell
npm test --prefix apps/desktop -- App.test.tsx electronMenu.test.ts nativeMenuBridge.test.ts
```

Expected: tests fail because `open-about` and app info API do not exist.

- [ ] **Step 3: Add frontend types and API call**

In `apps/desktop/src/api.ts`, add:

```ts
export type AppInfo = {
  name: string;
  version: string;
  releaseVersion: string;
  commit: string;
  buildTimeUtc: string;
  environment: string;
  dataRoot: string;
  indexPath: string;
};

export const frontendBuildInfo = {
  frontendVersion: import.meta.env.VITE_JOURNAL_FRONTEND_VERSION ?? "0.1.0-dev",
  releaseVersion: import.meta.env.VITE_JOURNAL_RELEASE_VERSION ?? "0.1.0-dev",
  commit: import.meta.env.VITE_JOURNAL_COMMIT ?? "dev",
  buildTimeUtc: import.meta.env.VITE_JOURNAL_BUILD_TIME_UTC ?? "local"
};

export function getAppInfo(): Promise<AppInfo> {
  return requestJson<AppInfo>("/app/info");
}
```

- [ ] **Step 4: Add native menu command**

In `apps/desktop/electron/menu.cjs`, change the Help menu item:

```js
{
  label: "关于 Journal",
  click: () => sendCommand(mainWindow, "open-about")
}
```

In `apps/desktop/src/App.tsx`, change:

```ts
type NativeMenuCommand = "open-llm-settings";
```

to:

```ts
type NativeMenuCommand = "open-llm-settings" | "open-about";
```

- [ ] **Step 5: Add About panel state and UI**

In `App.tsx`, import `getAppInfo`, `frontendBuildInfo`, and `type AppInfo`. Add state:

```ts
const [isAboutOpen, setIsAboutOpen] = useState(false);
const [appInfo, setAppInfo] = useState<AppInfo | null>(null);
const [aboutError, setAboutError] = useState("");
```

Add:

```ts
async function openAboutPanel() {
  setIsAboutOpen(true);
  setAboutError("");
  try {
    setAppInfo(await getAppInfo());
  } catch (caught) {
    setAboutError(getErrorMessage(caught));
  }
}
```

Handle menu command:

```ts
if (command === "open-about") {
  void openAboutPanel();
}
```

Render near `LlmSettingsPanel`:

```tsx
{isAboutOpen ? (
  <div className="llm-settings-backdrop" role="presentation">
    <section className="about-panel" role="dialog" aria-modal="true" aria-label="关于 Journal">
      <header>
        <h2>关于 Journal</h2>
        <button type="button" onClick={() => setIsAboutOpen(false)}>关闭</button>
      </header>
      <dl>
        <dt>Release</dt>
        <dd>{appInfo?.releaseVersion ?? frontendBuildInfo.releaseVersion}</dd>
        <dt>Frontend</dt>
        <dd>Frontend {frontendBuildInfo.frontendVersion}</dd>
        <dt>Backend</dt>
        <dd>{appInfo ? `Backend ${appInfo.version}` : "Backend 未连接"}</dd>
        <dt>Commit</dt>
        <dd>{appInfo?.commit ?? frontendBuildInfo.commit}</dd>
        <dt>Build</dt>
        <dd>{appInfo?.buildTimeUtc ?? frontendBuildInfo.buildTimeUtc}</dd>
        <dt>Data</dt>
        <dd>{appInfo?.dataRoot ?? "本地服务未连接"}</dd>
      </dl>
      {aboutError ? <p className="api-error" role="alert">{aboutError}</p> : null}
      <footer>
        <span>License</span>
        <span>Privacy</span>
        <span>Data Safety</span>
        <span>AI Notice</span>
      </footer>
    </section>
  </div>
) : null}
```

- [ ] **Step 6: Add styles**

Append to `apps/desktop/src/styles.css`:

```css
.about-panel {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 8px;
  box-shadow: var(--shadow-lg);
  color: var(--text-primary);
  margin: 8vh auto;
  max-width: 620px;
  padding: 22px;
}

.about-panel header,
.about-panel footer {
  align-items: center;
  display: flex;
  justify-content: space-between;
  gap: 12px;
}

.about-panel dl {
  display: grid;
  gap: 10px 16px;
  grid-template-columns: 120px minmax(0, 1fr);
  margin: 18px 0;
}

.about-panel dt {
  color: var(--text-secondary);
  font-weight: 700;
}

.about-panel dd {
  margin: 0;
  overflow-wrap: anywhere;
}
```

- [ ] **Step 7: Run tests**

```powershell
npm test --prefix apps/desktop -- App.test.tsx electronMenu.test.ts nativeMenuBridge.test.ts
```

Expected: tests pass.

- [ ] **Step 8: Commit**

```powershell
git add apps/desktop/src/api.ts apps/desktop/src/App.tsx apps/desktop/src/styles.css apps/desktop/electron/menu.cjs apps/desktop/electron/nativeMenuBridge.cjs apps/desktop/src/App.test.tsx apps/desktop/src/electronMenu.test.ts apps/desktop/src/nativeMenuBridge.test.ts
git commit -m "feat: add about version panel"
```

## Task 3: Electron Production Backend Runtime And Orphan Recovery

**Files:**
- Create: `apps/desktop/electron/backendRuntime.cjs`
- Modify: `apps/desktop/electron/main.cjs`
- Modify: `apps/desktop/electron/preload.cjs`
- Create: `apps/desktop/src/serviceStatus.ts`
- Test: `apps/desktop/src/serviceStatus.test.ts`
- Test: `apps/desktop/src/App.test.tsx`

- [ ] **Step 1: Add service status formatter tests**

Create `apps/desktop/src/serviceStatus.test.ts`:

```ts
import { describe, expect, it } from "vitest";
import { getLocalServiceStatusLabel } from "./serviceStatus";

describe("getLocalServiceStatusLabel", () => {
  it("labels reused backend processes explicitly", () => {
    expect(getLocalServiceStatusLabel("reused")).toBe("复用上次残留进程");
  });

  it("labels failed startup as a connection failure", () => {
    expect(getLocalServiceStatusLabel("failed")).toBe("连接失败");
  });
});
```

- [ ] **Step 2: Add runtime module test by static smoke**

Add a script-level smoke check to `apps/desktop/src/nativeMenuBridge.test.ts`:

```ts
test("backend runtime module exports lifecycle functions", async () => {
  const runtime = await import("../electron/backendRuntime.cjs");
  expect(typeof runtime.createBackendRuntime).toBe("function");
});
```

- [ ] **Step 3: Run failing tests**

```powershell
npm test --prefix apps/desktop -- serviceStatus.test.ts nativeMenuBridge.test.ts
```

Expected: tests fail because files/exports do not exist.

- [ ] **Step 4: Add frontend service status helper**

Create `apps/desktop/src/serviceStatus.ts`:

```ts
export type LocalServiceStatus = "starting" | "connected" | "reused" | "failed" | "exited";

export function getLocalServiceStatusLabel(status: LocalServiceStatus) {
  switch (status) {
    case "starting":
      return "启动中";
    case "connected":
      return "已连接";
    case "reused":
      return "复用上次残留进程";
    case "failed":
      return "连接失败";
    case "exited":
      return "后端异常退出";
  }
}
```

- [ ] **Step 5: Create backend runtime module**

Create `apps/desktop/electron/backendRuntime.cjs`:

```js
const { spawn } = require("node:child_process");
const fs = require("node:fs");
const http = require("node:http");
const net = require("node:net");
const path = require("node:path");

function ensureDirectory(directory) {
  fs.mkdirSync(directory, { recursive: true });
}

function isProcessAlive(pid) {
  try {
    process.kill(pid, 0);
    return true;
  } catch {
    return false;
  }
}

function requestJson(url, timeoutMs = 3000) {
  return new Promise((resolve, reject) => {
    const request = http.get(url, response => {
      let body = "";
      response.setEncoding("utf8");
      response.on("data", chunk => { body += chunk; });
      response.on("end", () => {
        try {
          resolve(JSON.parse(body));
        } catch (error) {
          reject(error);
        }
      });
    });
    request.setTimeout(timeoutMs, () => {
      request.destroy(new Error("request timed out"));
    });
    request.on("error", reject);
  });
}

function findFreePort() {
  return new Promise((resolve, reject) => {
    const server = net.createServer();
    server.listen(0, "127.0.0.1", () => {
      const address = server.address();
      const port = typeof address === "object" && address ? address.port : 0;
      server.close(() => resolve(port));
    });
    server.on("error", reject);
  });
}

function createBackendRuntime(options) {
  const lockPath = options.lockPath;
  const backendExePath = options.backendExePath;
  const dataRoot = options.dataRoot;
  const logDirectory = options.logDirectory;
  let child = null;

  async function tryReuseLockedBackend() {
    if (!fs.existsSync(lockPath)) {
      return null;
    }

    const lock = JSON.parse(fs.readFileSync(lockPath, "utf8"));
    if (!lock.pid || !isProcessAlive(lock.pid)) {
      return null;
    }

    if (lock.owner !== "electron" || path.normalize(lock.exePath) !== path.normalize(backendExePath)) {
      return { status: "foreign", lock };
    }

    const info = await requestJson(`http://127.0.0.1:${lock.port}/app/info`);
    if (info.name === "Journal.Api" && info.dataRoot === dataRoot) {
      return { status: "reused", lock, info };
    }

    return { status: "stale", lock };
  }

  async function start() {
    ensureDirectory(path.dirname(lockPath));
    ensureDirectory(logDirectory);
    const reusable = await tryReuseLockedBackend().catch(() => null);
    if (reusable?.status === "reused") {
      return { status: "reused", port: reusable.lock.port, pid: reusable.lock.pid, info: reusable.info };
    }

    if (reusable?.status === "foreign") {
      return {
        status: "failed",
        reason: "Existing backend ownership could not be verified.",
        lock: reusable.lock
      };
    }

    if (reusable?.status === "stale" && reusable.lock.pid) {
      process.kill(reusable.lock.pid);
    }

    const port = await findFreePort();
    const stdout = fs.openSync(path.join(logDirectory, "backend-current.log"), "a");
    child = spawn(backendExePath, [], {
      env: {
        ...process.env,
        ASPNETCORE_URLS: `http://127.0.0.1:${port}`,
        JOURNAL_DATA_ROOT: dataRoot
      },
      stdio: ["ignore", stdout, stdout],
      windowsHide: true
    });

    const lock = {
      pid: child.pid,
      port,
      startedAtUtc: new Date().toISOString(),
      backendVersion: options.backendVersion,
      releaseVersion: options.releaseVersion,
      dataRoot,
      owner: "electron",
      exePath: backendExePath
    };
    fs.writeFileSync(lockPath, JSON.stringify(lock, null, 2), "utf8");
    return { status: "connected", port, pid: child.pid };
  }

  function stop() {
    if (child && !child.killed) {
      child.kill();
    }
  }

  return { start, stop };
}

module.exports = { createBackendRuntime, isProcessAlive };
```

- [ ] **Step 6: Wire production runtime in main/preload**

In `apps/desktop/electron/main.cjs`, import and use the runtime in packaged mode:

```js
const { createBackendRuntime } = require("./backendRuntime.cjs");
```

In `createWindow`, before `mainWindow.loadFile`, start runtime when `!isDev` and expose status via preload global. Keep dev path unchanged.

In `preload.cjs`, add `getLocalServiceStatus` and `getApiBaseUrl` fields to `journalDesktop`.

- [ ] **Step 7: Run Electron-related tests**

```powershell
npm test --prefix apps/desktop -- serviceStatus.test.ts nativeMenuBridge.test.ts App.test.tsx
```

Expected: tests pass.

- [ ] **Step 8: Commit**

```powershell
git add apps/desktop/electron/backendRuntime.cjs apps/desktop/electron/main.cjs apps/desktop/electron/preload.cjs apps/desktop/src/serviceStatus.ts apps/desktop/src/serviceStatus.test.ts apps/desktop/src/App.test.tsx apps/desktop/src/nativeMenuBridge.test.ts
git commit -m "feat: manage packaged backend runtime"
```

## Task 4: Icon Asset Generation And Wiring

**Files:**
- Create: `assets/app-icon/`
- Modify: `apps/desktop/electron/main.cjs`
- Modify: `README.md`

- [ ] **Step 1: Generate app icon source**

Use the `imagegen` skill with this prompt:

```text
Use case: logo-brand
Asset type: Windows desktop app icon source
Primary request: Create a polished app icon for "Journal", a local-first morning journal desktop app.
Subject: a warm paper journal page with a subtle sunrise arc and annual tree-ring curve, symbolizing morning writing and long-term memory.
Style/medium: clean modern icon, softly dimensional, vector-friendly, no text.
Composition/framing: centered object, square composition, strong silhouette at small sizes.
Color palette: warm paper ivory, muted sage, soft gold, graphite accent.
Constraints: no letters, no words, no cloud symbol, no generic AI sparkle, no busy details, no watermark.
```

Save selected output as `assets/app-icon/journal-icon-source.png`.

- [ ] **Step 2: Create icon conversion script**

Create `scripts/release/build-icon.ps1`:

```powershell
param(
    [string]$Source = "assets/app-icon/journal-icon-source.png",
    [string]$OutputDirectory = "assets/app-icon"
)

$ErrorActionPreference = "Stop"
$sizes = @(16, 24, 32, 48, 64, 128, 256)
Add-Type -AssemblyName System.Drawing
[System.IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null

function Write-IcoFile {
    param(
        [string[]]$PngPaths,
        [string]$IcoPath
    )

    $images = foreach ($pngPath in $PngPaths) {
        $image = [System.Drawing.Image]::FromFile((Resolve-Path $pngPath))
        try {
            [pscustomobject]@{
                Width = $image.Width
                Height = $image.Height
                Bytes = [System.IO.File]::ReadAllBytes((Resolve-Path $pngPath))
            }
        }
        finally {
            $image.Dispose()
        }
    }

    $stream = [System.IO.File]::Create($IcoPath)
    $writer = New-Object System.IO.BinaryWriter $stream
    try {
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]$images.Count)
        $offset = 6 + (16 * $images.Count)

        foreach ($item in $images) {
            $writer.Write([byte]($(if ($item.Width -eq 256) { 0 } else { $item.Width })))
            $writer.Write([byte]($(if ($item.Height -eq 256) { 0 } else { $item.Height })))
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$item.Bytes.Length)
            $writer.Write([UInt32]$offset)
            $offset += $item.Bytes.Length
        }

        foreach ($item in $images) {
            $writer.Write($item.Bytes)
        }
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

$sourceImage = [System.Drawing.Image]::FromFile((Resolve-Path $Source))
foreach ($size in $sizes) {
    $bitmap = New-Object System.Drawing.Bitmap $size, $size
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.DrawImage($sourceImage, 0, 0, $size, $size)
    $pngPath = Join-Path $OutputDirectory "journal-icon-$size.png"
    $bitmap.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}
$sourceImage.Dispose()

$pngPaths = $sizes | ForEach-Object { Join-Path $OutputDirectory "journal-icon-$_.png" }
Write-IcoFile -PngPaths $pngPaths -IcoPath (Join-Path $OutputDirectory "journal.ico")
```

- [ ] **Step 3: Generate icons locally**

```powershell
.\scripts\release\build-icon.ps1
```

Expected: `assets/app-icon/journal.ico` and size PNGs exist.

- [ ] **Step 4: Wire Electron window icon**

In `apps/desktop/electron/main.cjs`, add:

```js
icon: path.join(__dirname, "../../assets/app-icon/journal.ico"),
```

to `new BrowserWindow({ ... })`.

- [ ] **Step 5: Update README release identity note**

Add a short note to `README.md` stating that committed icon assets live under `assets/app-icon/`, and that CI uses the committed deterministic icon files rather than regenerating AI imagery during release builds.

- [ ] **Step 6: Commit**

```powershell
git add assets/app-icon scripts/release/build-icon.ps1 apps/desktop/electron/main.cjs README.md
git commit -m "feat: add journal app icon"
```

## Task 5: Legal, Privacy, Data Safety, And Release Identity Docs

**Files:**
- Create: `docs/legal/PRIVACY.md`
- Create: `docs/legal/DATA_SAFETY.md`
- Create: `docs/legal/AI_NOTICE.md`
- Create: `docs/legal/PERSONAL_STATEMENT.md`
- Create: `docs/legal/DISCLAIMER.md`
- Create: `docs/release/RELEASE_NOTES.md`
- Create: `docs/release/GITHUB_RELEASE_TEMPLATE.md`
- Modify: `README.md`

- [ ] **Step 1: Create privacy document**

Create `docs/legal/PRIVACY.md`:

```markdown
# Journal Privacy

Journal is local-first. By default, journal entries, raw inputs, drafts, version snapshots, audit records, and the rebuildable SQLite index are stored under the current Windows user's local application data directory.

Journal does not provide cloud sync in this version.

When a real LLM provider is enabled, text submitted by the user for journal organization may be sent to the configured provider. The provider is chosen and configured by the user.

Journal does not intentionally write full API keys to Markdown entries, version snapshots, release artifacts, GitHub Actions logs, or normal application logs.
```

- [ ] **Step 2: Create data safety document**

Create `docs/legal/DATA_SAFETY.md`:

```markdown
# Journal Data Safety

Journal treats Markdown entries, raw-input jsonl files, and version snapshot files as source material.

The SQLite history index is a rebuildable cache. It can be rebuilt from local source material.

Uninstalling the app should preserve `%LocalAppData%/Journal` by default.

Imports create a backup of the current data directory before replacing source material. Imports rebuild the SQLite index after source files are restored.

Exports do not include full API keys by default.
```

- [ ] **Step 3: Create AI notice, personal statement, and disclaimer**

Create `docs/legal/AI_NOTICE.md`:

```markdown
# Journal AI Notice

AI output is an organization aid, not a source of truth.

Journal preserves raw user input and writes AI output to draft boundaries first. Formal Markdown entries are updated only after user confirmation.

If a configured LLM response fails JSON or JMF validation, Journal creates an attention draft and does not overwrite the formal entry.
```

Create `docs/legal/PERSONAL_STATEMENT.md`:

```markdown
# Personal Statement

Journal is built as a local-first morning journal for long-term personal memory.

The product favors raw expression, readable Markdown, careful confirmation, and user-owned local data over opaque automation.
```

Create `docs/legal/DISCLAIMER.md`:

```markdown
# Disclaimer

Journal is a personal journaling and organization tool.

AI-generated content is for review and reflection only. It is not medical, psychological, legal, financial, or professional advice.

Users are responsible for deciding what sensitive information they send to third-party LLM providers and for keeping backups of important data.
```

- [ ] **Step 4: Create release documents**

Create `docs/release/RELEASE_NOTES.md`:

```markdown
# Journal 0.1.0 Release Notes

## Highlights

- Local-first morning journal workflow.
- JMF Markdown draft, validation, and confirmation flow.
- OpenAI-compatible provider settings.
- Harness Core audit trail.
- Local history search, version snapshots, and same-day anniversary wheel.
- Windows installer release pipeline groundwork.

## Data

Journal stores user data under `%LocalAppData%/Journal` by default. Uninstalling the app preserves that directory.
```

Create `docs/release/GITHUB_RELEASE_TEMPLATE.md`:

```markdown
# Journal v0.1.0

## Assets

- `Journal-Setup-0.1.0.exe`
- `Journal-Setup-0.1.0.sha256`

## Install

Download and run the setup executable on Windows x64.

## Data Safety

The installer preserves `%LocalAppData%/Journal` during upgrade and uninstall.
```

- [ ] **Step 5: Link documents from README**

Add a short `法律与数据声明` section to `README.md` linking all new docs.

- [ ] **Step 6: Commit**

```powershell
git add docs/legal docs/release README.md
git commit -m "docs: add release legal notices"
```

## Task 6: Backend Data Export

**Files:**
- Modify: `src/Journal.Infrastructure/Storage/LocalJournalPaths.cs`
- Create: `src/Journal.Domain/Entries/JournalDataPortabilityModels.cs`
- Create: `src/Journal.Infrastructure/Storage/JournalDataExportService.cs`
- Modify: `src/Journal.Api/Program.cs`
- Test: `tests/Journal.Tests/JournalDataExportServiceTests.cs`
- Test: `tests/Journal.Tests/TodayJournalEndpointTests.cs`

- [ ] **Step 1: Add failing export service tests**

Create `tests/Journal.Tests/JournalDataExportServiceTests.cs`:

```csharp
using System.IO.Compression;
using System.Text.Json;
using Journal.Infrastructure.Storage;

namespace Journal.Tests;

public sealed class JournalDataExportServiceTests
{
    [Fact]
    public async Task ExportAsync_WritesManifestAndSourceMaterialWithoutIndexOrFullApiKey()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        Directory.CreateDirectory(Path.GetDirectoryName(paths.AiSettingsPath())!);
        await File.WriteAllTextAsync(paths.AiSettingsPath(), """
        {"providers":[{"id":"openai","apiKey":"sk-secret"}]}
        """);
        var entryPath = Path.Combine(workspace.Root, "entries", "2026", "05", "2026-05-14.md");
        Directory.CreateDirectory(Path.GetDirectoryName(entryPath)!);
        await File.WriteAllTextAsync(entryPath, "# entry");
        Directory.CreateDirectory(paths.IndexDirectory());
        await File.WriteAllTextAsync(paths.IndexPath(), "cache");
        var service = new JournalDataExportService(paths);

        var result = await service.ExportAsync(Path.Combine(workspace.Root, "export.zip"), CancellationToken.None);

        Assert.True(File.Exists(result.ExportPath));
        using var archive = ZipFile.OpenRead(result.ExportPath);
        Assert.Contains(archive.Entries, entry => entry.FullName == "manifest.json");
        Assert.Contains(archive.Entries, entry => entry.FullName == "entries/2026/05/2026-05-14.md");
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.Contains("journal.db", StringComparison.OrdinalIgnoreCase));
        var manifestEntry = archive.GetEntry("manifest.json")!;
        using var stream = manifestEntry.Open();
        using var document = await JsonDocument.ParseAsync(stream);
        Assert.False(document.RootElement.GetProperty("containsFullApiKeys").GetBoolean());
    }
}
```

- [ ] **Step 2: Run failing test**

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JournalDataExportServiceTests
```

Expected: build fails because export service and models do not exist.

- [ ] **Step 3: Add portability models**

Create `src/Journal.Domain/Entries/JournalDataPortabilityModels.cs`:

```csharp
namespace Journal.Domain.Entries;

public sealed record JournalDataExportManifest(
    string Format,
    DateTimeOffset CreatedAt,
    string AppVersion,
    string BackendVersion,
    string FrontendVersion,
    int EntryCount,
    int RawInputCount,
    int VersionCount,
    bool ContainsFullApiKeys);

public sealed record JournalDataExportResult(
    string ExportPath,
    JournalDataExportManifest Manifest);
```

- [ ] **Step 4: Add path helpers**

In `LocalJournalPaths.cs`, add:

```csharp
public string RootDirectory() => _rootDirectory;

public string RawInputRootDirectory() =>
    Path.Combine(_rootDirectory, ".journal", "raw-inputs");

public string DraftRootDirectory() =>
    Path.Combine(_rootDirectory, ".journal", "drafts");

public string VersionRootDirectory() =>
    Path.Combine(_rootDirectory, ".journal", "versions");

public string AuditRootDirectory() =>
    Path.Combine(_rootDirectory, ".journal", "audit");
```

- [ ] **Step 5: Implement export service**

Create `src/Journal.Infrastructure/Storage/JournalDataExportService.cs`:

```csharp
using System.IO.Compression;
using System.Text.Json;
using Journal.Domain.Application;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Storage;

public sealed class JournalDataExportService(LocalJournalPaths paths)
{
    public async Task<JournalDataExportResult> ExportAsync(string exportPath, CancellationToken cancellationToken)
    {
        LocalJournalPaths.EnsureParentDirectory(exportPath);
        if (File.Exists(exportPath))
        {
            File.Delete(exportPath);
        }

        var manifest = new JournalDataExportManifest(
            "journal-export/v1",
            DateTimeOffset.Now,
            ApplicationInfo.Version,
            ApplicationInfo.Version,
            "0.1.0",
            CountFiles(paths.EntryRootDirectory(), "*.md"),
            CountFiles(paths.RawInputRootDirectory(), "*.jsonl"),
            CountFiles(paths.VersionRootDirectory(), "*.md"),
            false);

        await using var stream = File.Create(exportPath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        await AddManifestAsync(archive, manifest, cancellationToken);
        AddDirectory(archive, paths.EntryRootDirectory(), "entries");
        AddDirectory(archive, paths.RawInputRootDirectory(), ".journal/raw-inputs");
        AddDirectory(archive, paths.DraftRootDirectory(), ".journal/drafts");
        AddDirectory(archive, paths.VersionRootDirectory(), ".journal/versions");
        AddDirectory(archive, paths.AuditRootDirectory(), ".journal/audit");
        AddSafeSettings(archive);
        return new JournalDataExportResult(exportPath, manifest);
    }

    private static int CountFiles(string directory, string pattern) =>
        Directory.Exists(directory) ? Directory.GetFiles(directory, pattern, SearchOption.AllDirectories).Length : 0;

    private static async Task AddManifestAsync(ZipArchive archive, JournalDataExportManifest manifest, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry("manifest.json");
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, manifest, cancellationToken: cancellationToken);
    }

    private static void AddDirectory(ZipArchive archive, string sourceDirectory, string archiveRoot)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, file).Replace('\\', '/');
            archive.CreateEntryFromFile(file, $"{archiveRoot}/{relative}");
        }
    }

    private static void AddSafeSettings(ZipArchive archive)
    {
        var entry = archive.CreateEntry(".journal/settings/ai-providers.safe.json");
        using var writer = new StreamWriter(entry.Open());
        writer.Write("{\"containsFullApiKeys\":false}");
    }
}
```

- [ ] **Step 6: Wire service and endpoint**

Register in `Program.cs`:

```csharp
builder.Services.AddSingleton<JournalDataExportService>();
```

Add endpoint:

```csharp
app.MapPost("/journal/data/export", async (
    JournalDataExportService service,
    CancellationToken cancellationToken) =>
{
    var exportPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Journal",
        ".journal",
        "exports",
        $"Journal-Export-{DateTimeOffset.Now:yyyy-MM-dd-HHmmss}.zip");
    return Results.Ok(await service.ExportAsync(exportPath, cancellationToken));
});
```

- [ ] **Step 7: Run tests**

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JournalDataExportServiceTests|TodayJournalEndpointTests"
```

Expected: tests pass.

- [ ] **Step 8: Commit**

```powershell
git add src/Journal.Domain/Entries/JournalDataPortabilityModels.cs src/Journal.Infrastructure/Storage/JournalDataExportService.cs src/Journal.Infrastructure/Storage/LocalJournalPaths.cs src/Journal.Api/Program.cs tests/Journal.Tests/JournalDataExportServiceTests.cs
git commit -m "feat: export journal data package"
```

## Task 7: Backend Data Import With Pre-Import Backup

**Files:**
- Modify: `src/Journal.Domain/Entries/JournalDataPortabilityModels.cs`
- Create: `src/Journal.Infrastructure/Storage/JournalDataImportService.cs`
- Modify: `src/Journal.Api/Program.cs`
- Test: `tests/Journal.Tests/JournalDataImportServiceTests.cs`

- [ ] **Step 1: Add failing import tests**

Create `tests/Journal.Tests/JournalDataImportServiceTests.cs`:

```csharp
using System.IO.Compression;
using System.Text.Json;
using Journal.Infrastructure.Storage;

namespace Journal.Tests;

public sealed class JournalDataImportServiceTests
{
    [Fact]
    public async Task ImportAsync_CreatesBackupAndRestoresEntries()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var existing = Path.Combine(workspace.Root, "entries", "2026", "05", "2026-05-14.md");
        Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
        await File.WriteAllTextAsync(existing, "old");
        var zipPath = Path.Combine(workspace.Root, "import.zip");
        CreateImportZip(zipPath);
        var service = new JournalDataImportService(paths, new JournalIndexingService(paths, new JournalIndexStore(paths)));

        var result = await service.ImportAsync(zipPath, CancellationToken.None);

        Assert.True(Directory.Exists(result.BackupDirectory));
        Assert.Equal("journal-export/v1", result.Manifest.Format);
        Assert.Equal("new", await File.ReadAllTextAsync(existing));
    }

    [Fact]
    public async Task ImportAsync_WithInvalidManifest_DoesNotModifyCurrentData()
    {
        using var workspace = TempWorkspace.Create();
        var paths = new LocalJournalPaths(new JournalStorageOptions(workspace.Root));
        var existing = Path.Combine(workspace.Root, "entries", "2026", "05", "2026-05-14.md");
        Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
        await File.WriteAllTextAsync(existing, "old");
        var zipPath = Path.Combine(workspace.Root, "bad.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var manifest = archive.CreateEntry("manifest.json");
            await using var stream = manifest.Open();
            await JsonSerializer.SerializeAsync(stream, new { format = "bad" });
        }
        var service = new JournalDataImportService(paths, new JournalIndexingService(paths, new JournalIndexStore(paths)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ImportAsync(zipPath, CancellationToken.None));
        Assert.Equal("old", await File.ReadAllTextAsync(existing));
    }

    private static void CreateImportZip(string zipPath)
    {
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        var manifest = archive.CreateEntry("manifest.json");
        using (var writer = new StreamWriter(manifest.Open()))
        {
            writer.Write("""{"format":"journal-export/v1","createdAt":"2026-05-14T12:00:00+08:00","appVersion":"0.1.0","backendVersion":"0.1.0","frontendVersion":"0.1.0","entryCount":1,"rawInputCount":0,"versionCount":0,"containsFullApiKeys":false}""");
        }
        var entry = archive.CreateEntry("entries/2026/05/2026-05-14.md");
        using var entryWriter = new StreamWriter(entry.Open());
        entryWriter.Write("new");
    }
}
```

- [ ] **Step 2: Run failing tests**

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter JournalDataImportServiceTests
```

Expected: build fails because import service/model does not exist.

- [ ] **Step 3: Add import result model**

Append to `JournalDataPortabilityModels.cs`:

```csharp
public sealed record JournalDataImportResult(
    string BackupDirectory,
    JournalDataExportManifest Manifest);
```

- [ ] **Step 4: Implement import service**

Create `src/Journal.Infrastructure/Storage/JournalDataImportService.cs`:

```csharp
using System.IO.Compression;
using System.Text.Json;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Storage;

public sealed class JournalDataImportService(
    LocalJournalPaths paths,
    JournalIndexingService indexingService)
{
    public async Task<JournalDataImportResult> ImportAsync(string zipPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(zipPath))
        {
            throw new FileNotFoundException("import package was not found", zipPath);
        }

        using var archive = ZipFile.OpenRead(zipPath);
        var manifest = await ReadManifestAsync(archive, cancellationToken);
        if (!string.Equals(manifest.Format, "journal-export/v1", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("import package format is invalid.");
        }

        var backupDirectory = CreateBackup();
        try
        {
            ExtractSourceMaterial(archive);
            await indexingService.RebuildAsync(DateTimeOffset.Now, cancellationToken);
            return new JournalDataImportResult(backupDirectory, manifest);
        }
        catch
        {
            RestoreBackup(backupDirectory);
            throw;
        }
    }

    private static async Task<JournalDataExportManifest> ReadManifestAsync(ZipArchive archive, CancellationToken cancellationToken)
    {
        var entry = archive.GetEntry("manifest.json")
            ?? throw new InvalidOperationException("manifest.json is missing.");
        await using var stream = entry.Open();
        return await JsonSerializer.DeserializeAsync<JournalDataExportManifest>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken)
            ?? throw new InvalidOperationException("manifest.json is invalid.");
    }

    private string CreateBackup()
    {
        var backupDirectory = Path.Combine(paths.RootDirectory(), ".journal", "import-backups", DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(backupDirectory);
        CopyIfExists(paths.EntryRootDirectory(), Path.Combine(backupDirectory, "entries"));
        CopyIfExists(Path.Combine(paths.RootDirectory(), ".journal"), Path.Combine(backupDirectory, ".journal"));
        return backupDirectory;
    }

    private void ExtractSourceMaterial(ZipArchive archive)
    {
        foreach (var entry in archive.Entries.Where(item =>
            item.FullName.StartsWith("entries/", StringComparison.Ordinal)
            || item.FullName.StartsWith(".journal/raw-inputs/", StringComparison.Ordinal)
            || item.FullName.StartsWith(".journal/drafts/", StringComparison.Ordinal)
            || item.FullName.StartsWith(".journal/versions/", StringComparison.Ordinal)
            || item.FullName.StartsWith(".journal/audit/", StringComparison.Ordinal)))
        {
            var destination = Path.Combine(paths.RootDirectory(), entry.FullName.Replace('/', Path.DirectorySeparatorChar));
            LocalJournalPaths.EnsureParentDirectory(destination);
            entry.ExtractToFile(destination, overwrite: true);
        }
    }

    private void RestoreBackup(string backupDirectory)
    {
        CopyIfExists(Path.Combine(backupDirectory, "entries"), paths.EntryRootDirectory(), overwrite: true);
        CopyIfExists(Path.Combine(backupDirectory, ".journal"), Path.Combine(paths.RootDirectory(), ".journal"), overwrite: true);
    }

    private static void CopyIfExists(string source, string destination, bool overwrite = false)
    {
        if (!Directory.Exists(source))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            LocalJournalPaths.EnsureParentDirectory(target);
            File.Copy(file, target, overwrite);
        }
    }
}
```

- [ ] **Step 5: Wire service and endpoint**

Register:

```csharp
builder.Services.AddSingleton<JournalDataImportService>();
```

Add endpoint:

```csharp
app.MapPost("/journal/data/import", async Task<IResult> (
    DataImportRequest request,
    JournalDataImportService service,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.PackagePath))
    {
        return Results.BadRequest(new { error = "packagePath is required" });
    }

    try
    {
        return Results.Ok(await service.ImportAsync(request.PackagePath, cancellationToken));
    }
    catch (Exception exception) when (exception is IOException or InvalidOperationException or FileNotFoundException)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

public sealed record DataImportRequest(string PackagePath);
```

- [ ] **Step 6: Run tests**

```powershell
dotnet test tests/Journal.Tests/Journal.Tests.csproj --filter "JournalDataImportServiceTests|TodayJournalEndpointTests"
```

Expected: tests pass.

- [ ] **Step 7: Commit**

```powershell
git add src/Journal.Domain/Entries/JournalDataPortabilityModels.cs src/Journal.Infrastructure/Storage/JournalDataImportService.cs src/Journal.Api/Program.cs tests/Journal.Tests/JournalDataImportServiceTests.cs
git commit -m "feat: import journal data package"
```

## Task 8: Data And Backup UI

**Files:**
- Modify: `apps/desktop/src/api.ts`
- Modify: `apps/desktop/src/App.tsx`
- Modify: `apps/desktop/src/styles.css`
- Modify: `apps/desktop/electron/main.cjs`
- Modify: `apps/desktop/electron/preload.cjs`
- Modify: `apps/desktop/electron/menu.cjs`
- Test: `apps/desktop/src/App.test.tsx`
- Test: `apps/desktop/src/electronMenu.test.ts`

- [ ] **Step 1: Add failing UI tests**

In `App.test.tsx`, add:

```tsx
test("opens data and backup panel and can trigger export", async () => {
  const fetchMock = vi
    .fn()
    .mockResolvedValueOnce(mockJsonResponse(healthResponse))
    .mockResolvedValueOnce(mockJsonResponse(createEditorState(processedToday())))
    .mockResolvedValueOnce(mockJsonResponse(aiSettings))
    .mockResolvedValueOnce(mockJsonResponse({ exportPath: "C:\\Exports\\Journal.zip", manifest: { format: "journal-export/v1" } }));
  vi.stubGlobal("fetch", fetchMock);

  render(<App />);
  fireEvent.click(await screen.findByRole("button", { name: "数据与备份" }));
  fireEvent.click(await screen.findByRole("button", { name: "导出数据包" }));

  await waitFor(() => expect(fetchMock).toHaveBeenCalledWith(
    "http://localhost:5057/journal/data/export",
    expect.objectContaining({ method: "POST" })
  ));
  expect(await screen.findByText(/Journal.zip/)).toBeInTheDocument();
});
```

- [ ] **Step 2: Run failing test**

```powershell
npm test --prefix apps/desktop -- App.test.tsx electronMenu.test.ts
```

Expected: tests fail because panel and API functions do not exist.

- [ ] **Step 3: Add API functions**

In `api.ts`, add:

```ts
export type JournalDataExportResult = {
  exportPath: string;
  manifest: Record<string, unknown>;
};

export type JournalDataImportResult = {
  backupDirectory: string;
  manifest: Record<string, unknown>;
};

export function exportJournalData(): Promise<JournalDataExportResult> {
  return requestJson<JournalDataExportResult>("/journal/data/export", { method: "POST" });
}

export function importJournalData(packagePath: string): Promise<JournalDataImportResult> {
  return requestJson<JournalDataImportResult>("/journal/data/import", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ packagePath })
  });
}
```

- [ ] **Step 4: Add desktop file and folder bridge**

In `apps/desktop/electron/main.cjs`, add IPC handlers:

```js
const { app, BrowserWindow, dialog, ipcMain, shell } = require("electron");

ipcMain.handle("journal:select-import-package", async () => {
  const result = await dialog.showOpenDialog({
    title: "选择 Journal 导入包",
    filters: [{ name: "Journal Export", extensions: ["zip"] }],
    properties: ["openFile"]
  });
  return result.canceled ? null : result.filePaths[0];
});

ipcMain.handle("journal:open-path", async (_event, targetPath) => {
  if (typeof targetPath !== "string" || targetPath.trim().length === 0) {
    return false;
  }
  await shell.openPath(targetPath);
  return true;
});
```

In `apps/desktop/electron/preload.cjs`, expose:

```js
selectImportPackage: () => ipcRenderer.invoke("journal:select-import-package"),
openPath: targetPath => ipcRenderer.invoke("journal:open-path", targetPath)
```

Keep the manual path input as a fallback for tests and for users who paste a path directly.

- [ ] **Step 5: Add UI panel**

In `App.tsx`, add button `数据与备份` to the assistant panel or About panel footer. Add state:

```ts
const [isDataPanelOpen, setIsDataPanelOpen] = useState(false);
const [dataPanelMessage, setDataPanelMessage] = useState("");
const [importPackagePath, setImportPackagePath] = useState("");
```

Add handlers:

```ts
async function handleExportJournalData() {
  setDataPanelMessage("");
  const result = await exportJournalData();
  setDataPanelMessage(`已导出：${result.exportPath}`);
}

async function handleImportJournalData() {
  setDataPanelMessage("");
  const result = await importJournalData(importPackagePath);
  setDataPanelMessage(`已导入，备份目录：${result.backupDirectory}`);
}

async function handleSelectImportPackage() {
  const selected = await window.journalDesktop?.selectImportPackage?.();
  if (selected) {
    setImportPackagePath(selected);
  }
}
```

Render dialog:

```tsx
{isDataPanelOpen ? (
  <div className="llm-settings-backdrop" role="presentation">
    <section className="about-panel" role="dialog" aria-modal="true" aria-label="数据与备份">
      <header>
        <h2>数据与备份</h2>
        <button type="button" onClick={() => setIsDataPanelOpen(false)}>关闭</button>
      </header>
      <p>导入前会先备份当前数据。导出默认不包含完整 API Key。</p>
      <button type="button" onClick={() => void handleExportJournalData()}>导出数据包</button>
      <label>
        导入包路径
        <input value={importPackagePath} onChange={event => setImportPackagePath(event.target.value)} />
      </label>
      <button type="button" onClick={() => void handleSelectImportPackage()}>选择导入包</button>
      <button type="button" onClick={() => void handleImportJournalData()} disabled={!importPackagePath.trim()}>
        导入数据包
      </button>
      {dataPanelMessage ? <p>{dataPanelMessage}</p> : null}
    </section>
  </div>
) : null}
```

- [ ] **Step 6: Run tests**

```powershell
npm test --prefix apps/desktop -- App.test.tsx electronMenu.test.ts
```

Expected: tests pass.

- [ ] **Step 7: Commit**

```powershell
git add apps/desktop/src/api.ts apps/desktop/src/App.tsx apps/desktop/src/styles.css apps/desktop/electron/main.cjs apps/desktop/electron/preload.cjs apps/desktop/electron/menu.cjs apps/desktop/src/App.test.tsx apps/desktop/src/electronMenu.test.ts
git commit -m "feat: add data backup panel"
```

## Task 9: Local Release Staging Scripts

**Files:**
- Create: `scripts/release/write-build-metadata.ps1`
- Create: `scripts/release/stage-installer.ps1`
- Create: `scripts/release/build-installer.ps1`
- Modify: `.gitignore`
- Modify: `apps/desktop/package.json`
- Modify: `apps/desktop/package-lock.json`

- [ ] **Step 1: Add release scripts to package.json**

Install the Electron app packager from the repository root:

```powershell
npm install --prefix apps/desktop --save-dev @electron/packager
```

Then add this script to `apps/desktop/package.json`:

```json
"package:win": "electron-packager . Journal --platform=win32 --arch=x64 --out=../../artifacts/installer/electron --overwrite --ignore=\"src|node_modules/.cache\""
```

Run:

```powershell
npm install --prefix apps/desktop
```

- [ ] **Step 2: Create build metadata script**

Create `scripts/release/write-build-metadata.ps1`:

```powershell
param(
    [string]$ReleaseVersion = "0.1.0",
    [string]$OutputPath = "artifacts/installer/build-metadata.env"
)

$ErrorActionPreference = "Stop"
$commit = (git rev-parse --short HEAD).Trim()
$buildTime = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($OutputPath)) | Out-Null
@"
JOURNAL_RELEASE_VERSION=$ReleaseVersion
JOURNAL_BUILD_COMMIT=$commit
JOURNAL_BUILD_TIME_UTC=$buildTime
VITE_JOURNAL_RELEASE_VERSION=$ReleaseVersion
VITE_JOURNAL_COMMIT=$commit
VITE_JOURNAL_BUILD_TIME_UTC=$buildTime
"@ | Set-Content -Encoding utf8 $OutputPath
```

- [ ] **Step 3: Create staging script**

Create `scripts/release/stage-installer.ps1`:

```powershell
param(
    [string]$ReleaseVersion = "0.1.0",
    [string]$PublishRoot = "artifacts/installer/publish/Journal"
)

$ErrorActionPreference = "Stop"
if (Test-Path $PublishRoot) { Remove-Item -LiteralPath $PublishRoot -Recurse -Force }
[System.IO.Directory]::CreateDirectory($PublishRoot) | Out-Null
[System.IO.Directory]::CreateDirectory("$PublishRoot/backend") | Out-Null
[System.IO.Directory]::CreateDirectory("$PublishRoot/legal") | Out-Null
[System.IO.Directory]::CreateDirectory("$PublishRoot/assets") | Out-Null

dotnet publish src/Journal.Api/Journal.Api.csproj -c Release -r win-x64 --self-contained true -o "$PublishRoot/backend"
npm run build --prefix apps/desktop
npm run package:win --prefix apps/desktop
Copy-Item -LiteralPath "artifacts/installer/electron/Journal-win32-x64" -Destination "$PublishRoot/app" -Recurse
Copy-Item -LiteralPath "LICENSE" -Destination "$PublishRoot/legal/LICENSE"
Copy-Item -LiteralPath "NOTICE" -Destination "$PublishRoot/legal/NOTICE"
Copy-Item -LiteralPath "docs/legal/PRIVACY.md" -Destination "$PublishRoot/legal/PRIVACY.md"
Copy-Item -LiteralPath "docs/legal/DATA_SAFETY.md" -Destination "$PublishRoot/legal/DATA_SAFETY.md"
Copy-Item -LiteralPath "docs/legal/AI_NOTICE.md" -Destination "$PublishRoot/legal/AI_NOTICE.md"
Copy-Item -LiteralPath "assets/app-icon/journal.ico" -Destination "$PublishRoot/assets/journal.ico"
```

- [ ] **Step 4: Create top-level build script**

Create `scripts/release/build-installer.ps1`:

```powershell
param(
    [string]$ReleaseVersion = "0.1.0",
    [switch]$SkipInno
)

$ErrorActionPreference = "Stop"
.\scripts\release\write-build-metadata.ps1 -ReleaseVersion $ReleaseVersion
.\scripts\release\stage-installer.ps1 -ReleaseVersion $ReleaseVersion
if (-not $SkipInno) {
    & "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" "installer/windows/Journal.iss" "/DAppVersion=$ReleaseVersion"
}
```

- [ ] **Step 5: Update `.gitignore`**

Add:

```gitignore
artifacts/installer/publish/
artifacts/installer/dist/
artifacts/installer/electron/
artifacts/installer/build-metadata.env
```

- [ ] **Step 6: Run staging without Inno**

```powershell
.\scripts\release\build-installer.ps1 -ReleaseVersion 0.1.0 -SkipInno
```

Expected: `artifacts/installer/publish/Journal` contains `backend`, `app`, `legal`, and `assets`.

- [ ] **Step 7: Commit**

```powershell
git add scripts/release/write-build-metadata.ps1 scripts/release/stage-installer.ps1 scripts/release/build-installer.ps1 .gitignore apps/desktop/package.json apps/desktop/package-lock.json
git commit -m "build: add local installer staging"
```

## Task 10: Inno Setup Installer

**Files:**
- Create: `installer/windows/Journal.iss`
- Create: `installer/windows/assets/wizard-large.bmp`
- Create: `installer/windows/assets/wizard-small.bmp`
- Create: `installer/windows/assets/info-before.rtf`
- Create: `installer/windows/assets/info-after.rtf`
- Modify: `scripts/release/build-installer.ps1`

- [ ] **Step 1: Create Inno setup script**

Create `installer/windows/Journal.iss`:

```ini
#define AppName "Journal"
#ifndef AppVersion
#define AppVersion "0.1.0"
#endif
#define Publisher "panyonglin"
#define PublishRoot "..\..\artifacts\installer\publish\Journal"
#define OutputRoot "..\..\artifacts\installer\dist"

[Setup]
AppId={{B82A4615-4424-4C2C-9C66-35C1E8968F10}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#Publisher}
DefaultDirName={autopf}\Journal
DefaultGroupName=Journal
OutputDir={#OutputRoot}
OutputBaseFilename=Journal-Setup-{#AppVersion}
SetupIconFile=..\..\assets\app-icon\journal.ico
WizardImageFile=assets\wizard-large.bmp
WizardSmallImageFile=assets\wizard-small.bmp
UninstallDisplayIcon={app}\app\Journal.exe
LicenseFile=..\..\LICENSE
InfoBeforeFile=assets\info-before.rtf
InfoAfterFile=assets\info-after.rtf
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："
Name: "launch"; Description: "安装完成后启动 Journal"; GroupDescription: "附加任务："

[Files]
Source: "{#PublishRoot}\app\*"; DestDir: "{app}\app"; Flags: recursesubdirs ignoreversion
Source: "{#PublishRoot}\backend\*"; DestDir: "{app}\backend"; Flags: recursesubdirs ignoreversion
Source: "{#PublishRoot}\legal\*"; DestDir: "{app}\legal"; Flags: recursesubdirs ignoreversion
Source: "{#PublishRoot}\assets\journal.ico"; DestDir: "{app}\assets"; Flags: ignoreversion

[Icons]
Name: "{group}\Journal"; Filename: "{app}\app\Journal.exe"; IconFilename: "{app}\assets\journal.ico"
Name: "{autodesktop}\Journal"; Filename: "{app}\app\Journal.exe"; IconFilename: "{app}\assets\journal.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\app\Journal.exe"; Description: "启动 Journal"; Flags: nowait postinstall skipifsilent; Tasks: launch

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
```

- [ ] **Step 2: Create installer visual assets**

Create `installer/windows/assets/wizard-large.bmp` and `installer/windows/assets/wizard-small.bmp` from `assets/app-icon/journal-icon-source.png` using PowerShell and `System.Drawing`:

```powershell
Add-Type -AssemblyName System.Drawing
[System.IO.Directory]::CreateDirectory("installer/windows/assets") | Out-Null
$source = [System.Drawing.Image]::FromFile((Resolve-Path "assets/app-icon/journal-icon-source.png"))
try {
    foreach ($target in @(
        @{ Path = "installer/windows/assets/wizard-large.bmp"; Width = 164; Height = 314 },
        @{ Path = "installer/windows/assets/wizard-small.bmp"; Width = 55; Height = 55 }
    )) {
        $bitmap = New-Object System.Drawing.Bitmap $target.Width, $target.Height
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $graphics.Clear([System.Drawing.Color]::FromArgb(248, 244, 235))
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $size = [Math]::Min($target.Width - 16, $target.Height - 16)
        $x = [Math]::Floor(($target.Width - $size) / 2)
        $y = [Math]::Floor(($target.Height - $size) / 2)
        $graphics.DrawImage($source, $x, $y, $size, $size)
        $bitmap.Save($target.Path, [System.Drawing.Imaging.ImageFormat]::Bmp)
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}
finally {
    $source.Dispose()
}
```

Expected: both BMP files exist and can be opened by Windows Photos.

- [ ] **Step 3: Create installer RTF info pages**

Create `installer/windows/assets/info-before.rtf`:

```text
{\rtf1\ansi\deff0
{\fonttbl{\f0 Segoe UI;}}
\f0\fs20 Journal 是本地优先的晨间日记应用。\par
\par
默认情况下，日记、原始输入、草稿、版本快照和索引数据保存在当前 Windows 用户的本地应用数据目录。\par
\par
如果你启用真实 LLM Provider，主动提交的文本可能会发送给你配置的模型供应商。\par
}
```

Create `installer/windows/assets/info-after.rtf`:

```text
{\rtf1\ansi\deff0
{\fonttbl{\f0 Segoe UI;}}
\f0\fs20 安装完成。\par
\par
卸载 Journal 默认不会删除你的个人日记数据。数据目录通常位于 %LocalAppData%\\Journal。\par
\par
建议在正式使用前先通过应用内“数据与备份”导出一次数据包。\par
}
```

- [ ] **Step 4: Build installer locally**

```powershell
.\scripts\release\build-installer.ps1 -ReleaseVersion 0.1.0
```

Expected: `artifacts/installer/dist/Journal-Setup-0.1.0.exe` exists.

- [ ] **Step 5: Generate checksum**

Add to `build-installer.ps1` after ISCC:

```powershell
$setup = "artifacts/installer/dist/Journal-Setup-$ReleaseVersion.exe"
$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $setup
"$($hash.Hash)  $(Split-Path $setup -Leaf)" | Set-Content -Encoding ascii "artifacts/installer/dist/Journal-Setup-$ReleaseVersion.sha256"
```

- [ ] **Step 6: Commit**

```powershell
git add installer/windows/Journal.iss installer/windows/assets/wizard-large.bmp installer/windows/assets/wizard-small.bmp installer/windows/assets/info-before.rtf installer/windows/assets/info-after.rtf scripts/release/build-installer.ps1
git commit -m "build: add windows installer"
```

## Task 11: Local Installer Validation Script And Documentation

**Files:**
- Create: `scripts/release/verify-installer.ps1`
- Modify: `README.md`
- Modify: `AGENTS.md`

- [ ] **Step 1: Create verification script**

Create `scripts/release/verify-installer.ps1`:

```powershell
param(
    [string]$ReleaseVersion = "0.1.0"
)

$ErrorActionPreference = "Stop"
$setup = "artifacts/installer/dist/Journal-Setup-$ReleaseVersion.exe"
$checksum = "artifacts/installer/dist/Journal-Setup-$ReleaseVersion.sha256"

if (-not (Test-Path -LiteralPath $setup)) {
    throw "Installer not found: $setup"
}

if (-not (Test-Path -LiteralPath $checksum)) {
    throw "Checksum not found: $checksum"
}

$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $setup).Hash
$expected = (Get-Content -Encoding ascii -LiteralPath $checksum)[0].Split(" ")[0]
if ($hash -ne $expected) {
    throw "Checksum mismatch."
}

Write-Host "Installer artifact verified: $setup"
```

- [ ] **Step 2: Run verification script**

```powershell
.\scripts\release\verify-installer.ps1 -ReleaseVersion 0.1.0
```

Expected: prints `Installer artifact verified`.

- [ ] **Step 3: Update README release section**

Add:

````markdown
## Windows 安装包

本地构建安装包：

```powershell
.\scripts\release\build-installer.ps1 -ReleaseVersion 0.1.0
.\scripts\release\verify-installer.ps1 -ReleaseVersion 0.1.0
```

安装包输出到 `artifacts/installer/dist/`。卸载默认保留 `%LocalAppData%/Journal`。
````

- [ ] **Step 4: Update AGENTS commands**

Add focused release commands:

```powershell
.\scripts\release\build-installer.ps1 -ReleaseVersion 0.1.0 -SkipInno
.\scripts\release\build-installer.ps1 -ReleaseVersion 0.1.0
.\scripts\release\verify-installer.ps1 -ReleaseVersion 0.1.0
```

- [ ] **Step 5: Commit**

```powershell
git add scripts/release/verify-installer.ps1 README.md AGENTS.md
git commit -m "docs: add installer validation flow"
```

## Task 12: GitHub Actions Windows Release Workflow

**Files:**
- Create: `.github/workflows/release-windows.yml`
- Modify: `docs/release/GITHUB_RELEASE_TEMPLATE.md`
- Modify: `README.md`
- Modify: `AGENTS.md`

- [ ] **Step 1: Create workflow**

Create `.github/workflows/release-windows.yml`:

```yaml
name: Windows Release

on:
  workflow_dispatch:
    inputs:
      release_version:
        description: "Release version"
        required: true
        default: "0.1.0"
  push:
    tags:
      - "v*"

permissions:
  contents: write

jobs:
  build-windows-installer:
    runs-on: windows-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v5
        with:
          dotnet-version: "10.0.x"

      - name: Setup Node
        uses: actions/setup-node@v5
        with:
          node-version: "24"
          cache: "npm"
          cache-dependency-path: "apps/desktop/package-lock.json"

      - name: Install frontend dependencies
        run: npm ci --prefix apps/desktop

      - name: Test backend
        run: dotnet test Journal.slnx

      - name: Test frontend
        run: npm test --prefix apps/desktop

      - name: Install Inno Setup
        run: choco install innosetup --yes

      - name: Build installer
        shell: pwsh
        run: |
          $version = "${{ github.event.inputs.release_version }}"
          if ([string]::IsNullOrWhiteSpace($version)) {
            $version = "${{ github.ref_name }}".TrimStart("v")
          }
          .\scripts\release\build-installer.ps1 -ReleaseVersion $version

      - name: Upload installer artifact
        uses: actions/upload-artifact@v4
        with:
          name: journal-windows-installer
          path: |
            artifacts/installer/dist/*.exe
            artifacts/installer/dist/*.sha256
          retention-days: 14

      - name: Create GitHub Release
        if: startsWith(github.ref, 'refs/tags/v')
        uses: softprops/action-gh-release@v2
        with:
          files: |
            artifacts/installer/dist/*.exe
            artifacts/installer/dist/*.sha256
          body_path: docs/release/GITHUB_RELEASE_TEMPLATE.md
```

- [ ] **Step 2: Run local workflow syntax sanity**

```powershell
Get-Content -Encoding utf8 .github\workflows\release-windows.yml
```

Expected: file contains `workflow_dispatch`, tag trigger, `windows-latest`, and installer artifact upload.

- [ ] **Step 3: Update docs**

In `README.md`, add:

```markdown
## GitHub Actions 发布

首版完整 Windows 安装包不在每次 push 构建。使用：

- `workflow_dispatch` 手动构建测试安装包 artifact。
- `v*` tag 构建正式 GitHub Release assets。
```

In `AGENTS.md`, add the release workflow path and trigger rule.

- [ ] **Step 4: Commit**

```powershell
git add .github/workflows/release-windows.yml docs/release/GITHUB_RELEASE_TEMPLATE.md README.md AGENTS.md
git commit -m "ci: add windows release workflow"
```

## Final Verification Matrix

Run after Task 12:

```powershell
dotnet test Journal.slnx
npm test --prefix apps/desktop
npm run build --prefix apps/desktop
.\scripts\release\build-installer.ps1 -ReleaseVersion 0.1.0
.\scripts\release\verify-installer.ps1 -ReleaseVersion 0.1.0
git diff --check
```

Expected:

- Backend tests pass.
- Frontend tests pass.
- Vite build passes.
- Installer exe and checksum exist.
- Verification script confirms checksum.
- No whitespace errors.

## Spec Coverage Review

- Versioning & About: Tasks 1 and 2.
- Icon & Release Identity: Task 4.
- Legal / Privacy / Data Safety: Task 5.
- Production Runtime and orphan recovery: Task 3.
- Export / Import / Backup: Tasks 6, 7, and 8.
- Windows Installer: Tasks 9, 10, and 11.
- GitHub Actions Release Pipeline: Task 12.
- Local-first validation gates: Tasks 10, 11, and Final Verification Matrix.

## Deferred Work Scan

This plan intentionally avoids deferred work markers. Any implementation ambiguity should be handled by updating this plan before execution rather than improvising during execution.
