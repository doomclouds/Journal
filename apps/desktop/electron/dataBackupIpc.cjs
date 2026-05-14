const fs = require("node:fs");
const path = require("node:path");

function hasProtocolPrefix(value) {
  return /^[a-zA-Z][a-zA-Z\d+.-]*:/.test(value) && !/^[a-zA-Z]:[\\/]/.test(value);
}

function isUncPath(value) {
  return value.startsWith("\\\\") || value.startsWith("//");
}

function normalizeForCompare(value) {
  return path.normalize(path.resolve(value));
}

function isPathInside(candidatePath, rootPath) {
  const candidate = normalizeForCompare(candidatePath);
  const root = normalizeForCompare(rootPath);
  const rootWithSeparator = root.endsWith(path.sep) ? root : `${root}${path.sep}`;
  const comparableCandidate = candidate.toLowerCase();
  const comparableRoot = root.toLowerCase();
  const comparableRootWithSeparator = rootWithSeparator.toLowerCase();

  return comparableCandidate === comparableRoot || comparableCandidate.startsWith(comparableRootWithSeparator);
}

function getDefaultAllowedOpenRoots(dataRoot) {
  return [
    path.join(dataRoot, ".journal", "exports"),
    path.join(dataRoot, ".journal", "import-backups")
  ];
}

function getDefaultExportDirectory(dataRoot) {
  return path.join(dataRoot, ".journal", "exports");
}

function isSafeJournalOpenPath(targetPath, options = {}) {
  const { dataRoot, allowedRoots, exists = fs.existsSync } = options;
  if (typeof targetPath !== "string" || typeof dataRoot !== "string") {
    return false;
  }

  const trimmedPath = targetPath.trim();
  if (!trimmedPath || hasProtocolPrefix(trimmedPath) || isUncPath(trimmedPath) || !path.isAbsolute(trimmedPath)) {
    return false;
  }

  const resolvedPath = path.resolve(trimmedPath);
  if (!exists(resolvedPath)) {
    return false;
  }

  const openRoots = Array.isArray(allowedRoots) && allowedRoots.length > 0
    ? allowedRoots
    : getDefaultAllowedOpenRoots(dataRoot);
  return openRoots.some(rootPath => typeof rootPath === "string" && isPathInside(resolvedPath, rootPath));
}

async function openSafeJournalPath(targetPath, options) {
  const { shell, dataRoot, exists, stat = fs.statSync } = options;
  if (!isSafeJournalOpenPath(targetPath, { dataRoot, exists })) {
    return false;
  }

  const resolvedPath = path.resolve(targetPath.trim());
  let openTargetPath;
  try {
    openTargetPath = stat(resolvedPath).isDirectory() ? resolvedPath : path.dirname(resolvedPath);
  } catch {
    return false;
  }

  const errorMessage = await shell.openPath(openTargetPath);
  return errorMessage === "";
}

function createDataBackupIpcHandlers(options) {
  const { ipcMain, dialog, shell, dataRoot, exists = fs.existsSync, stat = fs.statSync } = options;
  ipcMain.handle("journal:select-import-package", async () => {
    const result = await dialog.showOpenDialog({
      title: "选择导入包",
      defaultPath: getDefaultExportDirectory(dataRoot),
      properties: ["openFile"],
      filters: [{ name: "Journal 数据包", extensions: ["zip"] }]
    });

    if (result.canceled || result.filePaths.length === 0) {
      return null;
    }

    return result.filePaths[0];
  });

  ipcMain.handle("journal:open-path", (_event, targetPath) =>
    openSafeJournalPath(targetPath, { shell, dataRoot, exists, stat }));
}

module.exports = {
  createDataBackupIpcHandlers,
  getDefaultExportDirectory,
  isSafeJournalOpenPath,
  openSafeJournalPath
};
