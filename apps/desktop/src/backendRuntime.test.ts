import { createRequire } from "node:module";
import { mkdtemp, mkdir, rm, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { describe, expect, test } from "vitest";

const require = createRequire(import.meta.url);
const runtime = require("../electron/backendRuntime.cjs");

describe("backend runtime classification", () => {
  test("exports lifecycle functions", () => {
    expect(typeof runtime.createBackendRuntime).toBe("function");
  });

  test("resolves installer sibling backend before resources backend", () => {
    const resourcesPath = path.join("C:\\Program Files\\Journal", "app", "resources");
    const siblingBackendPath = path.join("C:\\Program Files\\Journal", "backend", "Journal.Api.exe");
    const resourcesBackendPath = path.join(resourcesPath, "backend", "Journal.Api.exe");

    const resolved = runtime.resolvePackagedBackendExePath(
      resourcesPath,
      (candidatePath: string) => candidatePath === siblingBackendPath || candidatePath === resourcesBackendPath
    );

    expect(resolved).toBe(siblingBackendPath);
  });

  test("falls back to resources backend when sibling backend is absent", () => {
    const resourcesPath = path.join("C:\\Program Files\\Journal", "app", "resources");
    const resourcesBackendPath = path.join(resourcesPath, "backend", "Journal.Api.exe");

    const resolved = runtime.resolvePackagedBackendExePath(
      resourcesPath,
      (candidatePath: string) => candidatePath === resourcesBackendPath
    );

    expect(resolved).toBe(resourcesBackendPath);
  });

  test("reads packaged build metadata env file", async () => {
    const root = await mkdtemp(path.join(os.tmpdir(), "journal-runtime-test-"));
    const metadataPath = path.join(root, "build-metadata.env");
    await writeFile(metadataPath, [
      "JOURNAL_RELEASE_VERSION=2.3.4",
      "JOURNAL_BUILD_COMMIT=abc1234",
      "JOURNAL_BUILD_TIME_UTC=2026-05-14T18:30:00Z",
      "VITE_JOURNAL_RELEASE_VERSION=2.3.4"
    ].join("\n"), "utf8");

    try {
      expect(runtime.readBuildMetadataFile(metadataPath)).toEqual({
        JOURNAL_RELEASE_VERSION: "2.3.4",
        JOURNAL_BUILD_COMMIT: "abc1234",
        JOURNAL_BUILD_TIME_UTC: "2026-05-14T18:30:00Z",
        VITE_JOURNAL_RELEASE_VERSION: "2.3.4"
      });
    } finally {
      await rm(root, { recursive: true, force: true });
    }
  });

  test("treats mismatched app-info as uncertain without termination", () => {
    const result = runtime.classifyReusableBackendAppInfo(
      {
        name: "Another.Api",
        version: "0.1.0",
        releaseVersion: "0.1.0",
        dataRoot: "C:\\Users\\10062\\AppData\\Local\\Other"
      },
      {
        backendVersion: "0.1.0",
        releaseVersion: "0.1.0"
      },
      {
        dataRoot: "C:\\Users\\10062\\AppData\\Local\\Journal",
        releaseVersion: "0.1.0"
      }
    );

    expect(result).toEqual({
      action: "failed",
      reason: "Existing backend /app/info did not match the current packaged backend."
    });
  });

  test("does not expose an API base URL for uncertain foreign locks", () => {
    const result = runtime.classifyReusableBackendLock(
      {
        owner: "electron",
        exePath: "C:\\Tools\\Other\\Journal.Api.exe",
        dataRoot: "C:\\Users\\10062\\AppData\\Local\\Journal",
        releaseVersion: "0.1.0",
        port: 61234
      },
      {
        actualExePath: "C:\\Tools\\Other\\Journal.Api.exe",
        backendExePath: "C:\\Program Files\\Journal\\backend\\Journal.Api.exe",
        dataRoot: "C:\\Users\\10062\\AppData\\Local\\Journal",
        releaseVersion: "0.1.0"
      }
    );

    expect(result).toEqual({
      action: "failed",
      apiBaseUrl: null,
      reason: "Existing backend lock points to a live process, but ownership could not be verified."
    });
  });

  test("failed state for an uncertain lock does not expose the lock URL", async () => {
    const root = await mkdtemp(path.join(os.tmpdir(), "journal-runtime-test-"));
    const runtimeDirectory = path.join(root, "runtime");
    const logDirectory = path.join(root, "logs");
    const dataRoot = path.join(root, "data");
    const backendExePath = "C:\\Program Files\\Journal\\backend\\Journal.Api.exe";
    await mkdir(runtimeDirectory, { recursive: true });
    await writeFile(path.join(runtimeDirectory, "backend.lock.json"), JSON.stringify({
      pid: 4321,
      port: 61234,
      startedAtUtc: "2026-05-14T12:00:00Z",
      backendVersion: "0.1.0",
      releaseVersion: "0.1.0",
      dataRoot,
      owner: "electron",
      exePath: "C:\\Tools\\Other\\Journal.Api.exe"
    }), "utf8");

    try {
      const backend = runtime.createBackendRuntime({
        backendExePath,
        dataRoot,
        runtimeDirectory,
        logDirectory,
        releaseVersion: "0.1.0",
        processTools: {
          isProcessAlive: (pid: number) => pid === 4321,
          getProcessExecutablePath: async () => "C:\\Tools\\Other\\Journal.Api.exe",
          spawnBackend: () => {
            throw new Error("Foreign lock must not start a replacement backend.");
          },
          terminateProcess: async () => {
            throw new Error("Foreign lock must not be terminated.");
          }
        }
      });

      const result = await backend.start();

      expect(result.status).toBe("failed");
      expect(result.apiBaseUrl).toBeNull();
      expect(result.pid).toBe(4321);
    } finally {
      await rm(root, { recursive: true, force: true });
    }
  });

  test("classifies owned incompatible locks as stale so the runtime can restart", () => {
    const result = runtime.classifyReusableBackendLock(
      {
        owner: "electron",
        exePath: "C:\\Program Files\\Journal\\backend\\Journal.Api.exe",
        dataRoot: "C:\\Users\\10062\\AppData\\Local\\OldJournal",
        releaseVersion: "0.0.9",
        port: 51234
      },
      {
        actualExePath: "C:\\Program Files\\Journal\\backend\\Journal.Api.exe",
        backendExePath: "C:\\Program Files\\Journal\\backend\\Journal.Api.exe",
        dataRoot: "C:\\Users\\10062\\AppData\\Local\\Journal",
        releaseVersion: "0.1.0"
      }
    );

    expect(result).toEqual({
      action: "restart-stale",
      apiBaseUrl: "http://127.0.0.1:51234",
      reason: "Existing backend lock is self-owned but belongs to an older release or data root."
    });
  });

  test("classifies self-owned Journal app-info mismatch as stale so the runtime can restart", () => {
    const result = runtime.classifyReusableBackendAppInfo(
      {
        name: "Journal.Api",
        version: "0.1.0",
        releaseVersion: "0.0.9",
        dataRoot: "C:\\Users\\10062\\AppData\\Local\\OldJournal"
      },
      {
        backendVersion: "0.1.0",
        releaseVersion: "0.0.9"
      },
      {
        dataRoot: "C:\\Users\\10062\\AppData\\Local\\Journal",
        releaseVersion: "0.1.0"
      }
    );

    expect(result).toEqual({
      action: "restart-stale",
      reason: "Existing backend /app/info belongs to Journal but not this release or data root."
    });
  });

  test("classifies verified reusable backends as owned so stop can clean them up", () => {
    const result = runtime.classifyReusableBackendAppInfo(
      {
        name: "Journal.Api",
        version: "0.1.0",
        releaseVersion: "0.1.0",
        dataRoot: "C:\\Users\\10062\\AppData\\Local\\Journal"
      },
      {
        backendVersion: "0.1.0",
        releaseVersion: "0.1.0"
      },
      {
        dataRoot: "C:\\Users\\10062\\AppData\\Local\\Journal",
        releaseVersion: "0.1.0"
      }
    );

    expect(result).toEqual({
      action: "reuse",
      reason: null
    });
  });

  test("rejects reuse when required version fields are missing", () => {
    const result = runtime.classifyReusableBackendAppInfo(
      {
        name: "Journal.Api",
        dataRoot: "C:\\Users\\10062\\AppData\\Local\\Journal"
      },
      {
        backendVersion: null,
        releaseVersion: null
      },
      {
        dataRoot: "C:\\Users\\10062\\AppData\\Local\\Journal",
        releaseVersion: "0.1.0"
      }
    );

    expect(result.action).toBe("restart-stale");
  });

  test("fails and terminates a spawned backend when app-info is incomplete", async () => {
    const root = await mkdtemp(path.join(os.tmpdir(), "journal-runtime-test-"));
    const runtimeDirectory = path.join(root, "runtime");
    const logDirectory = path.join(root, "logs");
    const dataRoot = path.join(root, "data");
    const backendExePath = "C:\\Program Files\\Journal\\backend\\Journal.Api.exe";
    const terminatedPids: number[] = [];

    try {
      const backend = runtime.createBackendRuntime({
        backendExePath,
        dataRoot,
        runtimeDirectory,
        logDirectory,
        releaseVersion: "0.1.0",
        processTools: {
          chooseFreePort: async () => 61234,
          spawnBackend: () => ({
            pid: 9876,
            stdout: null,
            stderr: null,
            once: () => undefined
          }),
          waitForAppInfo: async () => ({
            name: "Journal.Api",
            releaseVersion: "0.1.0",
            dataRoot
          }),
          isProcessAlive: (pid: number) => pid === 9876,
          terminateProcess: async (pid: number) => {
            terminatedPids.push(pid);
          }
        }
      });

      const result = await backend.start();

      expect(result.status).toBe("failed");
      expect(result.apiBaseUrl).toBeNull();
      expect(result.reason).toBe("Spawned backend /app/info did not match the current packaged backend.");
      expect(terminatedPids).toEqual([9876]);
    } finally {
      await rm(root, { recursive: true, force: true });
    }
  });

  test("passes build metadata to the spawned backend process", async () => {
    const root = await mkdtemp(path.join(os.tmpdir(), "journal-runtime-test-"));
    const runtimeDirectory = path.join(root, "runtime");
    const logDirectory = path.join(root, "logs");
    const dataRoot = path.join(root, "data");
    const backendExePath = "C:\\Program Files\\Journal\\backend\\Journal.Api.exe";
    let capturedEnv: Record<string, string> | null = null;

    try {
      const backend = runtime.createBackendRuntime({
        backendExePath,
        dataRoot,
        runtimeDirectory,
        logDirectory,
        releaseVersion: "2.3.4",
        buildMetadata: {
          JOURNAL_RELEASE_VERSION: "2.3.4",
          JOURNAL_BUILD_COMMIT: "abc1234",
          JOURNAL_BUILD_TIME_UTC: "2026-05-14T18:30:00Z"
        },
        processTools: {
          chooseFreePort: async () => 61234,
          spawnBackend: (_exePath: string, spawnOptions: { env: Record<string, string> }) => {
            capturedEnv = spawnOptions.env;
            return {
              pid: 9876,
              stdout: null,
              stderr: null,
              once: () => undefined
            };
          },
          waitForAppInfo: async () => ({
            name: "Journal.Api",
            version: "0.1.0",
            releaseVersion: "2.3.4",
            dataRoot
          }),
          isProcessAlive: (pid: number) => pid === 9876
        }
      });

      const result = await backend.start();

      expect(result.status).toBe("connected");
      expect(capturedEnv).toMatchObject({
        ASPNETCORE_URLS: "http://127.0.0.1:61234",
        JOURNAL_DATA_ROOT: dataRoot,
        JOURNAL_RELEASE_VERSION: "2.3.4",
        JOURNAL_BUILD_COMMIT: "abc1234",
        JOURNAL_BUILD_TIME_UTC: "2026-05-14T18:30:00Z"
      });
    } finally {
      await rm(root, { recursive: true, force: true });
    }
  });

  test("fails promptly when a spawned backend exits before app-info validation succeeds", async () => {
    const root = await mkdtemp(path.join(os.tmpdir(), "journal-runtime-test-"));
    const runtimeDirectory = path.join(root, "runtime");
    const logDirectory = path.join(root, "logs");
    const dataRoot = path.join(root, "data");
    const backendExePath = "C:\\Program Files\\Journal\\backend\\Journal.Api.exe";
    let exitHandler: ((code: number | null, signal: string | null) => void) | null = null;
    let markSpawned: (value: unknown) => void = () => undefined;
    const spawned = new Promise(resolve => {
      markSpawned = resolve;
    });
    const emitExit = (code: number | null, signal: string | null) => {
      if (!exitHandler) {
        throw new Error("Exit handler was not registered.");
      }
      exitHandler(code, signal);
    };

    try {
      const backend = runtime.createBackendRuntime({
        backendExePath,
        dataRoot,
        runtimeDirectory,
        logDirectory,
        releaseVersion: "0.1.0",
        healthTimeoutMs: 1000,
        processTools: {
          chooseFreePort: async () => 61234,
          spawnBackend: () => {
            const child = {
              pid: 9876,
              stdout: null,
              stderr: null,
              once: (eventName: string, handler: (...args: unknown[]) => void) => {
                if (eventName === "exit") {
                  exitHandler = handler as (code: number | null, signal: string | null) => void;
                }
                return child;
              }
            };
            markSpawned({});
            return child;
          },
          waitForAppInfo: async () => {
            await new Promise((_, reject) => setTimeout(() => reject(new Error("waited for app-info timeout")), 100));
          },
          isProcessAlive: () => false
        }
      });

      const started = backend.start();
      await spawned;
      emitExit(42, null);
      const result = await started;

      expect(result.status).toBe("failed");
      expect(result.reason).toBe("Backend exited before startup completed with code 42 and signal null.");
    } finally {
      await rm(root, { recursive: true, force: true });
    }
  });

  test("stops a verified reused backend pid", async () => {
    const root = await mkdtemp(path.join(os.tmpdir(), "journal-runtime-test-"));
    const runtimeDirectory = path.join(root, "runtime");
    const logDirectory = path.join(root, "logs");
    const dataRoot = path.join(root, "data");
    const backendExePath = "C:\\Program Files\\Journal\\backend\\Journal.Api.exe";
    const terminatedPids: number[] = [];
    await mkdir(runtimeDirectory, { recursive: true });
    await writeFile(path.join(runtimeDirectory, "backend.lock.json"), JSON.stringify({
      pid: 4321,
      port: 61234,
      startedAtUtc: "2026-05-14T12:00:00Z",
      backendVersion: "0.1.0",
      releaseVersion: "0.1.0",
      dataRoot,
      owner: "electron",
      exePath: backendExePath
    }), "utf8");

    try {
      const backend = runtime.createBackendRuntime({
        backendExePath,
        dataRoot,
        runtimeDirectory,
        logDirectory,
        releaseVersion: "0.1.0",
        processTools: {
          isProcessAlive: (pid: number) => pid === 4321,
          getProcessExecutablePath: async () => backendExePath,
          requestJson: async () => ({
            name: "Journal.Api",
            version: "0.1.0",
            releaseVersion: "0.1.0",
            dataRoot
          }),
          terminateProcess: async (pid: number) => {
            terminatedPids.push(pid);
          }
        }
      });

      await backend.start();
      await backend.stop();

      expect(terminatedPids).toEqual([4321]);
    } finally {
      await rm(root, { recursive: true, force: true });
    }
  });

  test("stop during startup terminates a child that becomes ready late", async () => {
    const root = await mkdtemp(path.join(os.tmpdir(), "journal-runtime-test-"));
    const runtimeDirectory = path.join(root, "runtime");
    const logDirectory = path.join(root, "logs");
    const dataRoot = path.join(root, "data");
    const backendExePath = "C:\\Program Files\\Journal\\backend\\Journal.Api.exe";
    const terminatedPids: number[] = [];
    let markSpawned: (value: unknown) => void = () => undefined;
    const spawned = new Promise(resolve => {
      markSpawned = resolve;
    });
    let releaseStartup: (value: unknown) => void = () => undefined;
    const startupGate = new Promise(resolve => {
      releaseStartup = resolve;
    });

    try {
      const backend = runtime.createBackendRuntime({
        backendExePath,
        dataRoot,
        runtimeDirectory,
        logDirectory,
        releaseVersion: "0.1.0",
        processTools: {
          chooseFreePort: async () => 61234,
          spawnBackend: () => {
            markSpawned({});
            return {
              pid: 9876,
              stdout: null,
              stderr: null,
              once: () => undefined
            };
          },
          waitForAppInfo: async () => {
            await startupGate;
            return {
              name: "Journal.Api",
              version: "0.1.0",
              releaseVersion: "0.1.0"
            };
          },
          isProcessAlive: (pid: number) => pid === 9876,
          terminateProcess: async (pid: number) => {
            terminatedPids.push(pid);
          }
        }
      });

      const started = backend.start();
      await spawned;
      await backend.stop();
      releaseStartup({});
      await started;

      expect(terminatedPids).toEqual([9876]);
      expect(backend.getState().status).toBe("failed");
    } finally {
      await rm(root, { recursive: true, force: true });
    }
  });

  test("stop during reuse verification does not adopt a residual backend", async () => {
    const root = await mkdtemp(path.join(os.tmpdir(), "journal-runtime-test-"));
    const runtimeDirectory = path.join(root, "runtime");
    const logDirectory = path.join(root, "logs");
    const dataRoot = path.join(root, "data");
    const backendExePath = "C:\\Program Files\\Journal\\backend\\Journal.Api.exe";
    const terminatedPids: number[] = [];
    let markAppInfoRequested: (value: unknown) => void = () => undefined;
    const appInfoRequested = new Promise(resolve => {
      markAppInfoRequested = resolve;
    });
    let releaseAppInfo: (value: unknown) => void = () => undefined;
    const appInfoGate = new Promise(resolve => {
      releaseAppInfo = resolve;
    });
    await mkdir(runtimeDirectory, { recursive: true });
    await writeFile(path.join(runtimeDirectory, "backend.lock.json"), JSON.stringify({
      pid: 4321,
      port: 61234,
      startedAtUtc: "2026-05-14T12:00:00Z",
      backendVersion: "0.1.0",
      releaseVersion: "0.1.0",
      dataRoot,
      owner: "electron",
      exePath: backendExePath
    }), "utf8");

    try {
      const backend = runtime.createBackendRuntime({
        backendExePath,
        dataRoot,
        runtimeDirectory,
        logDirectory,
        releaseVersion: "0.1.0",
        processTools: {
          isProcessAlive: (pid: number) => pid === 4321,
          getProcessExecutablePath: async () => backendExePath,
          requestJson: async () => {
            markAppInfoRequested({});
            await appInfoGate;
            return {
              name: "Journal.Api",
              version: "0.1.0",
              releaseVersion: "0.1.0",
              dataRoot
            };
          },
          terminateProcess: async (pid: number) => {
            terminatedPids.push(pid);
          }
        }
      });

      const started = backend.start();
      await appInfoRequested;
      await backend.stop();
      releaseAppInfo({});
      const result = await started;

      expect(result.status).toBe("failed");
      expect(result.apiBaseUrl).toBeNull();
      expect(result.reason).toBe("Backend startup was cancelled.");
      expect(terminatedPids).toEqual([4321]);
      expect(backend.getState().status).toBe("failed");
    } finally {
      await rm(root, { recursive: true, force: true });
    }
  });

  test("stop before spawn prevents a late child process from being created", async () => {
    const root = await mkdtemp(path.join(os.tmpdir(), "journal-runtime-test-"));
    const runtimeDirectory = path.join(root, "runtime");
    const logDirectory = path.join(root, "logs");
    const dataRoot = path.join(root, "data");
    const backendExePath = "C:\\Program Files\\Journal\\backend\\Journal.Api.exe";
    let releasePort: (value: number) => void = () => undefined;
    const portGate = new Promise<number>(resolve => {
      releasePort = resolve;
    });
    let spawnCalled = false;

    try {
      const backend = runtime.createBackendRuntime({
        backendExePath,
        dataRoot,
        runtimeDirectory,
        logDirectory,
        releaseVersion: "0.1.0",
        processTools: {
          chooseFreePort: async () => await portGate,
          spawnBackend: () => {
            spawnCalled = true;
            throw new Error("Stop before spawn must prevent child creation.");
          }
        }
      });

      const started = backend.start();
      await backend.stop();
      releasePort(61234);
      await started;

      expect(spawnCalled).toBe(false);
      expect(backend.getState().status).toBe("failed");
    } finally {
      await rm(root, { recursive: true, force: true });
    }
  });
});
