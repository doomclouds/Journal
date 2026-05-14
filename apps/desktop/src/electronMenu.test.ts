import { createRequire } from "node:module";
import { describe, expect, test, vi } from "vitest";

const require = createRequire(import.meta.url);
const { createApplicationMenuTemplate, nativeMenuChannel } = require("../electron/menu.cjs");

describe("Electron native menu", () => {
  test("uses Chinese top-level menu labels instead of Electron defaults", () => {
    const template = createApplicationMenuTemplate();

    expect(template.map((item: { label: string }) => item.label)).toEqual([
      "文件",
      "编辑",
      "帮助"
    ]);
    expect(template.map((item: { label: string }) => item.label)).not.toContain("File");
    expect(template.map((item: { label: string }) => item.label)).not.toContain("Edit");
    expect(template.map((item: { label: string }) => item.label)).not.toContain("View");
    expect(template.map((item: { label: string }) => item.label)).not.toContain("Window");
    expect(template.map((item: { label: string }) => item.label)).not.toContain("Help");
  });

  test("keeps only product-useful menu groups and removes development view actions", () => {
    const template = createApplicationMenuTemplate();
    const labels = JSON.stringify(template);

    expect(labels).not.toContain("切换开发者工具");
    expect(labels).not.toContain("重新加载");
    expect(labels).not.toContain("视图");
    expect(labels).not.toContain("窗口");
  });

  test("routes LLM settings command through the native menu channel", () => {
    const sent: Array<[string, string]> = [];
    const executeJavaScript = vi.fn();
    const template = createApplicationMenuTemplate({
      mainWindow: {
        isDestroyed: () => false,
        webContents: {
          send: (channel: string, command: string) => sent.push([channel, command]),
          executeJavaScript
        }
      }
    });

    const fileMenu = template.find((item: { label: string }) => item.label === "文件");
    const llmMenuItem = fileMenu.submenu.find((item: { label?: string }) => item.label === "LLM 配置");
    llmMenuItem.click();

    expect(sent).toEqual([[nativeMenuChannel, "open-llm-settings"]]);
    expect(executeJavaScript).toHaveBeenCalledWith(expect.stringContaining("journal:native-menu-command"));
    expect(executeJavaScript).toHaveBeenCalledWith(expect.stringContaining("open-llm-settings"));
  });

  test("custom menu actions have useful click handlers", () => {
    const app = { quit: vi.fn() };
    const showAbout = vi.fn();
    const sent: Array<[string, string]> = [];
    const executeJavaScript = vi.fn();
    const template = createApplicationMenuTemplate({
      app,
      showAbout,
      mainWindow: {
        isDestroyed: () => false,
        webContents: {
          send: (channel: string, command: string) => sent.push([channel, command]),
          executeJavaScript
        }
      }
    });

    const fileMenu = template.find((item: { label: string }) => item.label === "文件");
    fileMenu.submenu.find((item: { label?: string }) => item.label === "LLM 配置").click();
    fileMenu.submenu.find((item: { label?: string }) => item.label === "退出").click();
    template
      .find((item: { label: string }) => item.label === "帮助")
      .submenu.find((item: { label?: string }) => item.label === "关于 Journal")
      .click();

    expect(sent).toEqual([
      [nativeMenuChannel, "open-llm-settings"],
      [nativeMenuChannel, "open-about"]
    ]);
    expect(app.quit).toHaveBeenCalledTimes(1);
    expect(showAbout).not.toHaveBeenCalled();
  });
});
