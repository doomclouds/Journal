import { createRequire } from "node:module";
import { describe, expect, test } from "vitest";

const require = createRequire(import.meta.url);
const { createApplicationMenuTemplate, nativeMenuChannel } = require("../electron/menu.cjs");

describe("Electron native menu", () => {
  test("uses Chinese top-level menu labels instead of Electron defaults", () => {
    const template = createApplicationMenuTemplate();

    expect(template.map((item: { label: string }) => item.label)).toEqual([
      "文件",
      "编辑",
      "视图",
      "窗口",
      "帮助"
    ]);
    expect(template.map((item: { label: string }) => item.label)).not.toContain("File");
    expect(template.map((item: { label: string }) => item.label)).not.toContain("Edit");
    expect(template.map((item: { label: string }) => item.label)).not.toContain("View");
    expect(template.map((item: { label: string }) => item.label)).not.toContain("Window");
    expect(template.map((item: { label: string }) => item.label)).not.toContain("Help");
  });

  test("routes LLM settings command through the native menu channel", () => {
    const sent: Array<[string, string]> = [];
    const template = createApplicationMenuTemplate({
      mainWindow: {
        isDestroyed: () => false,
        webContents: {
          send: (channel: string, command: string) => sent.push([channel, command])
        }
      }
    });

    const fileMenu = template.find((item: { label: string }) => item.label === "文件");
    const llmMenuItem = fileMenu.submenu.find((item: { label?: string }) => item.label === "LLM 配置");
    llmMenuItem.click();

    expect(sent).toEqual([[nativeMenuChannel, "open-llm-settings"]]);
  });
});
