import { createRequire } from "node:module";
import path from "node:path";
import { describe, expect, test, vi } from "vitest";

const require = createRequire(import.meta.url);
const security = require("../electron/windowSecurity.cjs");

describe("window security", () => {
  test("trusts only the packaged renderer entry file in packaged mode", () => {
    const packagedIndexPath = path.resolve("C:\\Program Files\\Journal\\app\\resources\\app\\dist\\index.html");
    const trustedUrl = security.toFileUrl(packagedIndexPath);
    const untrustedUrl = security.toFileUrl("C:\\Users\\Public\\attacker.html");

    expect(security.isTrustedDesktopFrameUrl(trustedUrl, {
      isDev: false,
      packagedIndexPath
    })).toBe(true);
    expect(security.isTrustedDesktopFrameUrl(untrustedUrl, {
      isDev: false,
      packagedIndexPath
    })).toBe(false);
  });

  test("trusts Vite renderer origins only in dev mode", () => {
    expect(security.isTrustedDesktopFrameUrl("http://localhost:5173/today", {
      isDev: true
    })).toBe(true);
    expect(security.isTrustedDesktopFrameUrl("http://127.0.0.1:5173/today", {
      isDev: true
    })).toBe(true);
    expect(security.isTrustedDesktopFrameUrl("http://localhost:5174/today", {
      isDev: true
    })).toBe(false);
  });

  test("navigation guard denies external window opens and delegates safe URLs to the OS", () => {
    const openExternal = vi.fn();
    const setWindowOpenHandler = vi.fn();
    const webContents = {
      on: vi.fn(),
      setWindowOpenHandler
    };

    security.installNavigationGuards({ webContents }, { openExternal }, {
      isDev: false,
      packagedIndexPath: path.resolve("C:\\Program Files\\Journal\\app\\resources\\app\\dist\\index.html")
    });

    const handler = setWindowOpenHandler.mock.calls[0][0];
    expect(handler({ url: "https://example.com" })).toEqual({ action: "deny" });
    expect(openExternal).toHaveBeenCalledWith("https://example.com");
  });

  test("navigation guard blocks main-frame navigation away from the trusted app", () => {
    const preventDefault = vi.fn();
    const openExternal = vi.fn();
    const on = vi.fn();
    const webContents = {
      on,
      setWindowOpenHandler: vi.fn()
    };

    security.installNavigationGuards({ webContents }, { openExternal }, {
      isDev: false,
      packagedIndexPath: path.resolve("C:\\Program Files\\Journal\\app\\resources\\app\\dist\\index.html")
    });

    const willNavigateRegistration = on.mock.calls.find(([eventName]) => eventName === "will-navigate");
    expect(willNavigateRegistration).toBeDefined();
    const willNavigateHandler = willNavigateRegistration![1];
    willNavigateHandler({ preventDefault }, "https://example.com/phish");

    expect(preventDefault).toHaveBeenCalled();
    expect(openExternal).toHaveBeenCalledWith("https://example.com/phish");
  });
});
