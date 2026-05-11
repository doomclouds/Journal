import { createRequire } from "node:module";
import { describe, expect, test, vi } from "vitest";

const require = createRequire(import.meta.url);
const { createNativeMenuBridge } = require("../electron/nativeMenuBridge.cjs");

describe("native menu bridge", () => {
  test("replays a menu command that arrives before React subscribes", () => {
    let listener: (_event: unknown, command: string) => void = () => undefined;
    const ipcRenderer = {
      on: vi.fn((_channel: string, callback: (_event: unknown, command: string) => void) => {
        listener = callback;
      })
    };

    const bridge = createNativeMenuBridge(ipcRenderer, "journal:native-menu");
    listener?.({}, "open-llm-settings");

    const handler = vi.fn();
    bridge.onNativeMenuCommand(handler);

    expect(handler).toHaveBeenCalledWith("open-llm-settings");
  });

  test("stops delivering menu commands after unsubscribe", () => {
    let listener: (_event: unknown, command: string) => void = () => undefined;
    const ipcRenderer = {
      on: vi.fn((_channel: string, callback: (_event: unknown, command: string) => void) => {
        listener = callback;
      })
    };

    const bridge = createNativeMenuBridge(ipcRenderer, "journal:native-menu");
    const handler = vi.fn();
    const unsubscribe = bridge.onNativeMenuCommand(handler);

    unsubscribe();
    listener?.({}, "open-llm-settings");

    expect(handler).not.toHaveBeenCalled();
  });
});
