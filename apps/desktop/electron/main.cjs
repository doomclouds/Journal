const { app, BrowserWindow, ipcMain, Menu } = require("electron");
const path = require("node:path");
const { createBackendRuntime } = require("./backendRuntime.cjs");
const { createApplicationMenuTemplate } = require("./menu.cjs");

const isDev = !app.isPackaged;
const devApiBaseUrl = "http://localhost:5057";
let backendRuntime = null;
let isStoppingBackend = false;
let mainWindowRef = null;
let localServiceState = {
  status: isDev ? "connected" : "starting",
  apiBaseUrl: devApiBaseUrl,
  port: 5057,
  pid: null,
  dataRoot: null,
  logDirectory: null,
  reason: null
};

function resolveLocalJournalDataRoot() {
  const localAppData = process.env.LOCALAPPDATA;
  if (localAppData) {
    return path.join(localAppData, "Journal");
  }

  return path.join(app.getPath("userData"), "JournalData");
}

function resolvePackagedBackendExePath() {
  // The Windows installer staging places the published API beside app resources.
  return path.join(process.resourcesPath, "backend", "Journal.Api.exe");
}

function setLocalServiceState(nextState) {
  localServiceState = {
    ...localServiceState,
    ...nextState
  };
}

function getTrustedApiBaseUrl() {
  if ((localServiceState.status === "connected" || localServiceState.status === "reused") && localServiceState.apiBaseUrl) {
    return localServiceState.apiBaseUrl;
  }

  return isDev ? devApiBaseUrl : null;
}

function installLocalServiceIpcHandlers() {
  ipcMain.handle("journal:get-local-service-status", () => localServiceState);
  ipcMain.handle("journal:get-api-base-url", () => getTrustedApiBaseUrl());
}

async function startPackagedBackendRuntime() {
  if (backendRuntime) {
    const currentState = backendRuntime.getState();
    if (currentState.status === "starting" || currentState.status === "connected" || currentState.status === "reused") {
      return currentState;
    }
  }

  const dataRoot = resolveLocalJournalDataRoot();
  const runtimeDirectory = path.join(dataRoot, ".journal", "runtime");
  const logDirectory = path.join(dataRoot, ".journal", "logs");
  backendRuntime = createBackendRuntime({
    backendExePath: resolvePackagedBackendExePath(),
    dataRoot,
    runtimeDirectory,
    logDirectory,
    releaseVersion: app.getVersion(),
    onStateChange: setLocalServiceState
  });

  return await backendRuntime.start();
}

function installApplicationMenu(mainWindow) {
  const template = createApplicationMenuTemplate({
    app,
    mainWindow
  });

  Menu.setApplicationMenu(Menu.buildFromTemplate(template));
}

function focusExistingWindow() {
  const existingWindow = mainWindowRef;
  if (!existingWindow || existingWindow.isDestroyed()) {
    return;
  }

  if (existingWindow.isMinimized()) {
    existingWindow.restore();
  }
  existingWindow.focus();
}

async function createWindow() {
  const mainWindow = new BrowserWindow({
    width: 1180,
    height: 780,
    minWidth: 960,
    minHeight: 640,
    title: "Journal",
    backgroundColor: "#f6efe4",
    webPreferences: {
      preload: path.join(__dirname, "preload.cjs"),
      contextIsolation: true,
      nodeIntegration: false
    }
  });
  mainWindowRef = mainWindow;
  mainWindow.on("closed", () => {
    if (mainWindowRef === mainWindow) {
      mainWindowRef = null;
    }
  });

  if (isDev) {
    mainWindow.loadURL("http://localhost:5173");
  } else {
    await startPackagedBackendRuntime();
    mainWindow.loadFile(path.join(__dirname, "../dist/index.html"));
  }

  installApplicationMenu(mainWindow);
  return mainWindow;
}

const hasSingleInstanceLock = app.requestSingleInstanceLock();

if (!hasSingleInstanceLock) {
  app.quit();
} else {
  app.on("second-instance", focusExistingWindow);

  app.whenReady().then(() => {
    installLocalServiceIpcHandlers();
    void createWindow();

    app.on("activate", () => {
      if (BrowserWindow.getAllWindows().length === 0) {
        void createWindow();
      } else {
        focusExistingWindow();
      }
    });
  });

  app.on("before-quit", event => {
    if (!backendRuntime || isStoppingBackend) {
      return;
    }

    event.preventDefault();
    isStoppingBackend = true;
    backendRuntime.stop()
      .catch(() => undefined)
      .finally(() => app.quit());
  });

  app.on("window-all-closed", () => {
    if (process.platform !== "darwin") {
      app.quit();
    }
  });
}
