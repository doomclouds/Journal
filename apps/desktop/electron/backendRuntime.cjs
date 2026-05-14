const fs = require("node:fs");
const fsp = require("node:fs/promises");
const http = require("node:http");
const net = require("node:net");
const path = require("node:path");
const { execFile } = require("node:child_process");
const { spawn } = require("node:child_process");
const { promisify } = require("node:util");

const execFileAsync = promisify(execFile);
const lockFileName = "backend.lock.json";
const loopbackHost = "127.0.0.1";

function isProcessAlive(pid) {
  if (!Number.isInteger(pid) || pid <= 0) {
    return false;
  }

  try {
    process.kill(pid, 0);
    return true;
  } catch (error) {
    return error?.code === "EPERM";
  }
}

function normalizePath(value) {
  return path.resolve(String(value ?? "")).toLowerCase();
}

function samePath(left, right) {
  return normalizePath(left) === normalizePath(right);
}

function sameRequiredVersion(left, right) {
  return Boolean(left) && Boolean(right) && String(left) === String(right);
}

function classifyReusableBackendAppInfo(appInfo, lock, expected) {
  const matches =
    appInfo?.name === "Journal.Api"
    && samePath(appInfo.dataRoot, expected.dataRoot)
    && sameRequiredVersion(appInfo.releaseVersion, expected.releaseVersion)
    && sameRequiredVersion(appInfo.version, lock.backendVersion);

  if (matches) {
    return {
      action: "reuse",
      reason: null
    };
  }

  if (appInfo?.name === "Journal.Api") {
    return {
      action: "restart-stale",
      reason: "Existing backend /app/info belongs to Journal but not this release or data root."
    };
  }

  return {
    action: "failed",
    reason: "Existing backend /app/info did not match the current packaged backend."
  };
}

function classifySpawnedBackendAppInfo(appInfo, expected) {
  const matches =
    appInfo?.name === "Journal.Api"
    && samePath(appInfo.dataRoot, expected.dataRoot)
    && sameRequiredVersion(appInfo.releaseVersion, expected.releaseVersion)
    && Boolean(appInfo.version);

  if (matches) {
    return {
      action: "connected",
      reason: null
    };
  }

  return {
    action: "failed",
    reason: "Spawned backend /app/info did not match the current packaged backend."
  };
}

function classifyReusableBackendLock(lock, expected) {
  const apiBaseUrl = Number.isInteger(lock?.port) ? `http://${loopbackHost}:${lock.port}` : null;
  const isSelfOwned =
    lock?.owner === "electron"
    && samePath(lock.exePath, expected.backendExePath)
    && samePath(expected.actualExePath, expected.backendExePath)
    && apiBaseUrl !== null;

  if (!isSelfOwned) {
    return {
      action: "failed",
      apiBaseUrl: null,
      reason: "Existing backend lock points to a live process, but ownership could not be verified."
    };
  }

  if (!samePath(lock.dataRoot, expected.dataRoot) || !sameRequiredVersion(lock.releaseVersion, expected.releaseVersion)) {
    return {
      action: "restart-stale",
      apiBaseUrl,
      reason: "Existing backend lock is self-owned but belongs to an older release or data root."
    };
  }

  return {
    action: "check-app-info",
    apiBaseUrl,
    reason: null
  };
}

function todayLogFileName(prefix) {
  return `${prefix}-${new Date().toISOString().slice(0, 10)}.log`;
}

async function ensureDirectories(...directories) {
  await Promise.all(directories.map(directory => fsp.mkdir(directory, { recursive: true })));
}

async function chooseFreePort() {
  return await new Promise((resolve, reject) => {
    const server = net.createServer();
    server.unref();
    server.on("error", reject);
    server.listen(0, loopbackHost, () => {
      const address = server.address();
      const port = typeof address === "object" && address ? address.port : null;
      server.close(error => {
        if (error) {
          reject(error);
          return;
        }
        if (!port) {
          reject(new Error("No loopback port was assigned."));
          return;
        }
        resolve(port);
      });
    });
  });
}

async function readLock(lockPath) {
  try {
    return JSON.parse(await fsp.readFile(lockPath, "utf8"));
  } catch (error) {
    if (error?.code === "ENOENT") {
      return null;
    }
    return {
      invalid: true,
      reason: `Failed to read backend lock: ${error?.message ?? "unknown error"}`
    };
  }
}

async function writeJson(filePath, value) {
  await fsp.writeFile(filePath, `${JSON.stringify(value, null, 2)}\n`, "utf8");
}

function requestJson(url, timeoutMs = 2500) {
  return new Promise((resolve, reject) => {
    const request = http.get(url, { timeout: timeoutMs }, response => {
      let body = "";
      response.setEncoding("utf8");
      response.on("data", chunk => {
        body += chunk;
      });
      response.on("end", () => {
        if (response.statusCode < 200 || response.statusCode >= 300) {
          reject(new Error(`${url} failed with HTTP ${response.statusCode}`));
          return;
        }

        try {
          resolve(JSON.parse(body));
        } catch (error) {
          reject(new Error(`${url} returned invalid JSON: ${error?.message ?? "unknown error"}`));
        }
      });
    });

    request.on("timeout", () => {
      request.destroy(new Error(`${url} timed out.`));
    });
    request.on("error", reject);
  });
}

async function waitForAppInfo(apiBaseUrl, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  let lastError = null;

  while (Date.now() < deadline) {
    try {
      await requestJson(`${apiBaseUrl}/health`, 1200);
      return await requestJson(`${apiBaseUrl}/app/info`, 1200);
    } catch (error) {
      lastError = error;
      await new Promise(resolve => setTimeout(resolve, 250));
    }
  }

  throw lastError ?? new Error("Backend did not become healthy before the timeout.");
}

async function getProcessExecutablePath(pid) {
  if (!isProcessAlive(pid)) {
    return null;
  }

  if (process.platform === "win32") {
    try {
      const command = [
        "$ErrorActionPreference = 'Stop';",
        `$p = Get-CimInstance Win32_Process -Filter 'ProcessId = ${pid}';`,
        "if ($null -ne $p) { $p.ExecutablePath }"
      ].join(" ");
      const { stdout } = await execFileAsync("powershell.exe", ["-NoProfile", "-Command", command], {
        windowsHide: true,
        timeout: 3000
      });
      return stdout.trim() || null;
    } catch {
      return null;
    }
  }

  if (process.platform === "linux") {
    try {
      return await fsp.readlink(`/proc/${pid}/exe`);
    } catch {
      return null;
    }
  }

  return null;
}

async function terminateProcess(pid, timeoutMs = 5000) {
  if (!isProcessAlive(pid)) {
    return;
  }

  try {
    process.kill(pid);
  } catch (error) {
    if (error?.code === "ESRCH") {
      return;
    }
    throw error;
  }
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (!isProcessAlive(pid)) {
      return;
    }
    await new Promise(resolve => setTimeout(resolve, 100));
  }

  if (isProcessAlive(pid)) {
    try {
      process.kill(pid, "SIGKILL");
    } catch (error) {
      if (error?.code !== "ESRCH") {
        throw error;
      }
    }
  }
}

function formatStartupExitReason(code, signal) {
  return `Backend exited before startup completed with code ${code ?? "null"} and signal ${signal ?? "null"}.`;
}

function attachLogStream(stream, logPath, label) {
  if (!stream) {
    return;
  }

  const writer = fs.createWriteStream(logPath, { flags: "a", encoding: "utf8" });
  stream?.on("data", chunk => {
    writer.write(`[${new Date().toISOString()}] [${label}] ${chunk}`);
  });
  stream?.on("end", () => {
    writer.end();
  });
  stream?.on("error", () => {
    writer.end();
  });
}

function createBackendRuntime(options) {
  const backendExePath = path.resolve(options.backendExePath);
  const dataRoot = path.resolve(options.dataRoot);
  const runtimeDirectory = path.resolve(options.runtimeDirectory);
  const logDirectory = path.resolve(options.logDirectory);
  const releaseVersion = options.releaseVersion ?? null;
  const healthTimeoutMs = options.healthTimeoutMs ?? 15000;
  const processTools = options.processTools ?? {};
  const isAlive = processTools.isProcessAlive ?? isProcessAlive;
  const resolveProcessExecutablePath = processTools.getProcessExecutablePath ?? getProcessExecutablePath;
  const fetchJson = processTools.requestJson ?? requestJson;
  const terminateOwnedProcess = processTools.terminateProcess ?? terminateProcess;
  const selectFreePort = processTools.chooseFreePort ?? chooseFreePort;
  const spawnBackend = processTools.spawnBackend ?? ((exePath, spawnOptions) => spawn(exePath, [], spawnOptions));
  const waitForBackendAppInfo = processTools.waitForAppInfo ?? waitForAppInfo;
  const lockPath = path.join(runtimeDirectory, lockFileName);
  let child = null;
  let ownedBackendPid = null;
  let stopping = false;
  let state = {
    status: "starting",
    apiBaseUrl: null,
    port: null,
    pid: null,
    dataRoot,
    logDirectory,
    reason: null
  };

  function setState(next) {
    state = {
      ...state,
      ...next,
      dataRoot,
      logDirectory
    };
    options.onStateChange?.(getState());
    return getState();
  }

  function getState() {
    return { ...state };
  }

  async function startNewBackend() {
    await ensureDirectories(runtimeDirectory, logDirectory, dataRoot);
    const port = await selectFreePort();
    const apiBaseUrl = `http://${loopbackHost}:${port}`;
    const logPath = path.join(logDirectory, todayLogFileName("backend"));
    const startedAtUtc = new Date().toISOString();

    if (stopping) {
      return setState({
        status: "failed",
        apiBaseUrl: null,
        port,
        pid: null,
        reason: "Backend startup was cancelled."
      });
    }

    setState({
      status: "starting",
      apiBaseUrl,
      port,
      pid: null,
      reason: null
    });

    child = spawnBackend(backendExePath, {
      cwd: path.dirname(backendExePath),
      env: {
        ...process.env,
        ASPNETCORE_URLS: apiBaseUrl,
        JOURNAL_DATA_ROOT: dataRoot
      },
      windowsHide: true,
      stdio: ["ignore", "pipe", "pipe"]
    });
    const childStartupError = new Promise((_, reject) => {
      child.once("error", reject);
    });
    let rejectChildExitDuringStartup = null;
    let startupCompleted = false;
    const childExitDuringStartup = new Promise((_, reject) => {
      rejectChildExitDuringStartup = reject;
    });
    const startedPid = child.pid ?? null;
    ownedBackendPid = startedPid;
    attachLogStream(child.stdout, logPath, "stdout");
    attachLogStream(child.stderr, logPath, "stderr");
    setState({ pid: startedPid });

    child.once("exit", (code, signal) => {
      if (!startupCompleted) {
        rejectChildExitDuringStartup?.(new Error(formatStartupExitReason(code, signal)));
        return;
      }

      if (!stopping) {
        setState({
          status: "exited",
          reason: `Backend exited with code ${code ?? "null"} and signal ${signal ?? "null"}.`
        });
      }
    });

    try {
      const appInfo = await Promise.race([
        waitForBackendAppInfo(apiBaseUrl, healthTimeoutMs),
        childStartupError,
        childExitDuringStartup
      ]);
      startupCompleted = true;
      if (stopping) {
        return setState({
          status: "failed",
          apiBaseUrl: null,
          port,
          pid: startedPid,
          reason: "Backend startup was cancelled."
        });
      }

      const appInfoClassification = classifySpawnedBackendAppInfo(appInfo, {
        dataRoot,
        releaseVersion
      });
      if (appInfoClassification.action === "failed") {
        await stop();
        return setState({
          status: "failed",
          apiBaseUrl: null,
          port,
          pid: startedPid,
          reason: appInfoClassification.reason
        });
      }

      const lock = {
        pid: startedPid,
        port,
        startedAtUtc,
        backendVersion: appInfo.version,
        releaseVersion: appInfo.releaseVersion,
        dataRoot,
        owner: "electron",
        exePath: backendExePath
      };
      await writeJson(lockPath, lock);
      return setState({
        status: "connected",
        apiBaseUrl,
        port,
        pid: startedPid,
        backendVersion: lock.backendVersion,
        releaseVersion: lock.releaseVersion,
        reason: null
      });
    } catch (error) {
      startupCompleted = true;
      await stop();
      return setState({
        status: "failed",
        apiBaseUrl: null,
        port,
        pid: startedPid,
        reason: error?.message ?? "Backend startup failed."
      });
    }
  }

  async function tryReuseOrRecover(lock) {
    if (!lock || lock.invalid || !Number.isInteger(lock.pid) || !isAlive(lock.pid)) {
      return await startNewBackend();
    }

    const apiBaseUrl = Number.isInteger(lock.port) ? `http://${loopbackHost}:${lock.port}` : null;
    const actualExePath = await resolveProcessExecutablePath(lock.pid);
    const lockClassification = classifyReusableBackendLock(lock, {
      actualExePath,
      backendExePath,
      dataRoot,
      releaseVersion
    });

    if (stopping && lockClassification.action !== "failed") {
      await terminateOwnedProcess(lock.pid);
      return setState({
        status: "failed",
        apiBaseUrl: null,
        port: lock.port ?? null,
        pid: lock.pid,
        backendVersion: lock.backendVersion ?? null,
        releaseVersion: lock.releaseVersion ?? null,
        reason: "Backend startup was cancelled."
      });
    }

    if (lockClassification.action === "failed") {
      return setState({
        status: "failed",
        apiBaseUrl: null,
        port: lock.port ?? null,
        pid: lock.pid,
        backendVersion: lock.backendVersion ?? null,
        releaseVersion: lock.releaseVersion ?? null,
        reason: lockClassification.reason
      });
    }

    if (lockClassification.action === "restart-stale") {
      await terminateOwnedProcess(lock.pid);
      return await startNewBackend();
    }

    try {
      const appInfo = await fetchJson(`${apiBaseUrl}/app/info`, 2500);
      const classification = classifyReusableBackendAppInfo(appInfo, lock, {
        dataRoot,
        releaseVersion
      });

      if (stopping && classification.action !== "failed") {
        await terminateOwnedProcess(lock.pid);
        return setState({
          status: "failed",
          apiBaseUrl: null,
          port: lock.port,
          pid: lock.pid,
          backendVersion: lock.backendVersion ?? null,
          releaseVersion: lock.releaseVersion ?? null,
          reason: "Backend startup was cancelled."
        });
      }

      if (classification.action === "reuse") {
        ownedBackendPid = lock.pid;
        return setState({
          status: "reused",
          apiBaseUrl,
          port: lock.port,
          pid: lock.pid,
          backendVersion: appInfo.version ?? lock.backendVersion ?? null,
          releaseVersion: appInfo.releaseVersion ?? lock.releaseVersion ?? null,
          reason: null
        });
      }

      if (classification.action === "restart-stale") {
        await terminateOwnedProcess(lock.pid);
        return await startNewBackend();
      }

      return setState({
        status: "failed",
        apiBaseUrl: null,
        port: lock.port,
        pid: lock.pid,
        backendVersion: lock.backendVersion ?? null,
        releaseVersion: lock.releaseVersion ?? null,
        reason: classification.reason
      });
    } catch {
      // A self-owned backend that no longer answers app-info is stale.
    }

    await terminateOwnedProcess(lock.pid);
    return await startNewBackend();
  }

  async function start() {
    stopping = false;
    setState({ status: "starting", reason: null });
    try {
      await ensureDirectories(runtimeDirectory, logDirectory, dataRoot);
      const lock = await readLock(lockPath);
      return await tryReuseOrRecover(lock);
    } catch (error) {
      return setState({
        status: "failed",
        reason: error?.message ?? "Backend startup failed."
      });
    }
  }

  async function stop() {
    stopping = true;
    const pidToStop = ownedBackendPid;
    ownedBackendPid = null;
    child = null;
    if (pidToStop && isAlive(pidToStop)) {
      await terminateOwnedProcess(pidToStop);
    }
  }

  return {
    start,
    stop,
    getState
  };
}

module.exports = {
  classifyReusableBackendAppInfo,
  classifyReusableBackendLock,
  classifySpawnedBackendAppInfo,
  createBackendRuntime,
  isProcessAlive
};
