const path = require("node:path");
const { pathToFileURL, fileURLToPath } = require("node:url");

const devRendererOrigins = new Set([
  "http://localhost:5173",
  "http://127.0.0.1:5173"
]);

function resolvePackagedRendererEntryPath(appDirectory) {
  return path.resolve(appDirectory, "../dist/index.html");
}

function normalizeFilePath(value) {
  return path.normalize(value).toLowerCase();
}

function isTrustedDesktopFrameUrl(frameUrl, context) {
  if (!frameUrl) {
    return false;
  }

  let parsed;
  try {
    parsed = new URL(frameUrl);
  } catch {
    return false;
  }

  if (context.isDev) {
    return devRendererOrigins.has(parsed.origin);
  }

  if (parsed.protocol !== "file:" || !context.packagedIndexPath) {
    return false;
  }

  try {
    return normalizeFilePath(fileURLToPath(parsed))
      === normalizeFilePath(context.packagedIndexPath);
  } catch {
    return false;
  }
}

function isExternalUrl(targetUrl) {
  try {
    const parsed = new URL(targetUrl);
    return parsed.protocol === "https:" || parsed.protocol === "http:" || parsed.protocol === "mailto:";
  } catch {
    return false;
  }
}

function isTrustedNavigationUrl(targetUrl, context) {
  if (isTrustedDesktopFrameUrl(targetUrl, context)) {
    return true;
  }

  if (!context.isDev || !context.devOrigin) {
    return false;
  }

  try {
    return new URL(targetUrl).origin === context.devOrigin;
  } catch {
    return false;
  }
}

function openExternalIfSafe(shell, targetUrl) {
  if (!isExternalUrl(targetUrl)) {
    return;
  }

  void shell.openExternal(targetUrl);
}

function installNavigationGuards(mainWindow, shell, context) {
  mainWindow.webContents.on("will-navigate", (event, targetUrl) => {
    if (isTrustedNavigationUrl(targetUrl, context)) {
      return;
    }

    event.preventDefault();
    openExternalIfSafe(shell, targetUrl);
  });

  mainWindow.webContents.setWindowOpenHandler(({ url }) => {
    openExternalIfSafe(shell, url);
    return { action: "deny" };
  });
}

function toFileUrl(filePath) {
  return pathToFileURL(filePath).toString();
}

module.exports = {
  devRendererOrigins,
  installNavigationGuards,
  isTrustedDesktopFrameUrl,
  isTrustedNavigationUrl,
  resolvePackagedRendererEntryPath,
  toFileUrl
};
