import { createRequire } from "node:module";
import { describe, expect, test, vi } from "vitest";

const require = createRequire(import.meta.url);
const { createApplicationMenuTemplate, nativeMenuChannel } = require("../electron/menu.cjs");
const {
  createDataBackupIpcHandlers,
  isSafeJournalOpenPath,
  openSafeJournalPath
} = require("../electron/dataBackupIpc.cjs");

let legalDocumentIpc: {
  createLegalDocumentIpcHandlers?: (...args: unknown[]) => void;
  readLegalDocument?: (...args: unknown[]) => Promise<{ fileName: string; content: string } | null>;
  resolveLegalDocumentPath?: (...args: unknown[]) => string | null;
} | null = null;

try {
  legalDocumentIpc = require("../electron/legalDocumentIpc.cjs");
} catch {
  legalDocumentIpc = null;
}

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

  test("routes data backup command through the native menu channel", () => {
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
    const dataMenuItem = fileMenu.submenu.find((item: { label?: string }) => item.label === "数据与备份");
    dataMenuItem.click();

    expect(sent).toEqual([[nativeMenuChannel, "open-data-backup"]]);
    expect(executeJavaScript).toHaveBeenCalledWith(expect.stringContaining("journal:native-menu-command"));
    expect(executeJavaScript).toHaveBeenCalledWith(expect.stringContaining("open-data-backup"));
  });

  test("custom menu actions have useful click handlers", () => {
    const app = { quit: vi.fn() };
    const sent: Array<[string, string]> = [];
    const executeJavaScript = vi.fn();
    const template = createApplicationMenuTemplate({
      app,
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
    fileMenu.submenu.find((item: { label?: string }) => item.label === "数据与备份").click();
    fileMenu.submenu.find((item: { label?: string }) => item.label === "退出").click();
    template
      .find((item: { label: string }) => item.label === "帮助")
      .submenu.find((item: { label?: string }) => item.label === "关于 Journal")
      .click();

    expect(sent).toEqual([
      [nativeMenuChannel, "open-llm-settings"],
      [nativeMenuChannel, "open-data-backup"],
      [nativeMenuChannel, "open-about"]
    ]);
    expect(app.quit).toHaveBeenCalledTimes(1);
  });

  test("rejects unsafe open path targets before calling Electron shell", () => {
    const dataRoot = "C:\\Users\\10062\\AppData\\Local\\Journal";
    const exportPath = "C:\\Users\\10062\\AppData\\Local\\Journal\\.journal\\exports\\Journal.zip";
    const backupPath = "C:\\Users\\10062\\AppData\\Local\\Journal\\.journal\\import-backups\\20260515";
    const settingsPath = "C:\\Users\\10062\\AppData\\Local\\Journal\\.journal\\settings\\ai-providers.json";
    const exists = (targetPath: string) =>
      targetPath === exportPath || targetPath === backupPath || targetPath === settingsPath;

    expect(isSafeJournalOpenPath(exportPath, { dataRoot, exists })).toBe(true);
    expect(isSafeJournalOpenPath(backupPath, { dataRoot, exists })).toBe(true);
    expect(isSafeJournalOpenPath("", { dataRoot, exists })).toBe(false);
    expect(isSafeJournalOpenPath(null, { dataRoot, exists })).toBe(false);
    expect(isSafeJournalOpenPath("https://example.com/file.zip", { dataRoot, exists })).toBe(false);
    expect(isSafeJournalOpenPath("file:///C:/Users/10062/AppData/Local/Journal/file.zip", { dataRoot, exists })).toBe(false);
    expect(isSafeJournalOpenPath("\\\\server\\share\\Journal.zip", { dataRoot, exists })).toBe(false);
    expect(isSafeJournalOpenPath("exports\\Journal.zip", { dataRoot, exists })).toBe(false);
    expect(isSafeJournalOpenPath("C:\\Temp\\Journal.zip", { dataRoot, exists })).toBe(false);
    expect(isSafeJournalOpenPath(settingsPath, { dataRoot, exists })).toBe(false);
    expect(isSafeJournalOpenPath("C:\\Users\\10062\\AppData\\Local\\Journal\\.journal\\exports\\missing.zip", {
      dataRoot,
      exists
    })).toBe(false);
  });

  test("data backup IPC uses zip-only package selection and safe open path", async () => {
    const handlers = new Map<string, (...args: unknown[]) => unknown>();
    const ipcMain = {
      handle: vi.fn((channel: string, handler: (...args: unknown[]) => unknown) => {
        handlers.set(channel, handler);
      })
    };
    const dialog = {
      showOpenDialog: vi.fn().mockResolvedValue({
        canceled: false,
        filePaths: ["C:\\Backups\\Journal.zip"]
      })
    };
    const shell = {
      openPath: vi.fn().mockResolvedValue("")
    };
    const dataRoot = "C:\\Users\\10062\\AppData\\Local\\Journal";
    const exportPath = "C:\\Users\\10062\\AppData\\Local\\Journal\\.journal\\exports\\Journal.zip";
    const exportDirectory = "C:\\Users\\10062\\AppData\\Local\\Journal\\.journal\\exports";

    createDataBackupIpcHandlers({
      ipcMain,
      dialog,
      shell,
      dataRoot,
      exists: (targetPath: string) => targetPath === exportPath,
      stat: () => ({ isDirectory: () => false })
    });

    await handlers.get("journal:select-import-package")?.();
    expect(dialog.showOpenDialog).toHaveBeenCalledWith({
      title: "选择导入包",
      defaultPath: exportDirectory,
      properties: ["openFile"],
      filters: [{ name: "Journal 数据包", extensions: ["zip"] }]
    });

    await expect(handlers.get("journal:open-path")?.({}, exportPath)).resolves.toBe(true);
    await expect(handlers.get("journal:open-path")?.({}, "C:\\Temp\\Journal.zip")).resolves.toBe(false);
    expect(shell.openPath).toHaveBeenCalledTimes(1);
    expect(shell.openPath).toHaveBeenCalledWith(exportDirectory);
  });

  test("safe open path opens directories directly and files through their parent folder", async () => {
    const dataRoot = "C:\\Users\\10062\\AppData\\Local\\Journal";
    const exportPath = "C:\\Users\\10062\\AppData\\Local\\Journal\\.journal\\exports\\Journal.zip";
    const exportDirectory = "C:\\Users\\10062\\AppData\\Local\\Journal\\.journal\\exports";
    const backupDirectory = "C:\\Users\\10062\\AppData\\Local\\Journal\\.journal\\import-backups\\20260515";
    const shell = {
      openPath: vi.fn().mockResolvedValue("")
    };
    const exists = (targetPath: string) => targetPath === exportPath || targetPath === backupDirectory;
    const stat = (targetPath: string) => ({
      isDirectory: () => targetPath === backupDirectory
    });

    await expect(openSafeJournalPath(exportPath, { shell, dataRoot, exists, stat })).resolves.toBe(true);
    await expect(openSafeJournalPath(backupDirectory, { shell, dataRoot, exists, stat })).resolves.toBe(true);

    expect(shell.openPath).toHaveBeenNthCalledWith(1, exportDirectory);
    expect(shell.openPath).toHaveBeenNthCalledWith(2, backupDirectory);
  });

  test("reads only known legal documents from the packaged legal folder", async () => {
    expect(legalDocumentIpc).not.toBeNull();
    const { readLegalDocument, resolveLegalDocumentPath } = legalDocumentIpc!;
    const legalRoot = "C:\\Program Files\\Journal\\legal";
    const privacyPath = "C:\\Program Files\\Journal\\legal\\PRIVACY.md";
    const exists = (targetPath: string) => targetPath === privacyPath;
    const readFile = vi.fn().mockResolvedValue("# Journal 隐私声明");

    expect(resolveLegalDocumentPath?.("privacy", { legalRoot })).toBe(privacyPath);
    expect(resolveLegalDocumentPath?.("..\\secrets", { legalRoot })).toBeNull();
    await expect(readLegalDocument?.("privacy", { legalRoot, exists, readFile })).resolves.toEqual({
      fileName: "PRIVACY.md",
      content: "# Journal 隐私声明"
    });
    await expect(readLegalDocument?.("missing", { legalRoot, exists, readFile })).resolves.toBeNull();
    expect(readFile).toHaveBeenCalledWith(privacyPath, "utf8");
    expect(readFile).toHaveBeenCalledTimes(1);
  });
});
