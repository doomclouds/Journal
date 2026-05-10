const { app, BrowserWindow, dialog, Menu } = require("electron");
const path = require("node:path");
const { createApplicationMenuTemplate } = require("./menu.cjs");

const isDev = !app.isPackaged;

function installApplicationMenu(mainWindow) {
  const template = createApplicationMenuTemplate({
    app,
    mainWindow,
    showAbout: () => {
      dialog.showMessageBox(mainWindow, {
        type: "info",
        title: "关于 Journal",
        message: "Journal",
        detail: "本地优先的晨间日记应用。",
        buttons: ["确定"]
      });
    }
  });

  Menu.setApplicationMenu(Menu.buildFromTemplate(template));
}

function createWindow() {
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

  if (isDev) {
    mainWindow.loadURL("http://localhost:5173");
  } else {
    mainWindow.loadFile(path.join(__dirname, "../dist/index.html"));
  }

  installApplicationMenu(mainWindow);
  return mainWindow;
}

app.whenReady().then(() => {
  createWindow();

  app.on("activate", () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow();
    }
  });
});

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") {
    app.quit();
  }
});
