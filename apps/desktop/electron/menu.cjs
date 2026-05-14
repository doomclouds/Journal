const nativeMenuChannel = "journal:native-menu";

function sendCommand(mainWindow, command) {
  if (!mainWindow || mainWindow.isDestroyed?.()) {
    return;
  }

  mainWindow.webContents.send(nativeMenuChannel, command);
  mainWindow.webContents.executeJavaScript?.(
    `window.dispatchEvent(new CustomEvent("journal:native-menu-command", { detail: ${JSON.stringify(command)} }));`
  )?.catch?.(() => undefined);
}

function createApplicationMenuTemplate(options = {}) {
  const { app, mainWindow, showAbout } = options;

  return [
    {
      label: "文件",
      submenu: [
        {
          label: "LLM 配置",
          click: () => sendCommand(mainWindow, "open-llm-settings")
        },
        { type: "separator" },
        {
          label: "退出",
          role: "quit",
          click: () => app?.quit()
        }
      ]
    },
    {
      label: "编辑",
      submenu: [
        { label: "撤销", role: "undo" },
        { label: "重做", role: "redo" },
        { type: "separator" },
        { label: "剪切", role: "cut" },
        { label: "复制", role: "copy" },
        { label: "粘贴", role: "paste" },
        { label: "全选", role: "selectAll" }
      ]
    },
    {
      label: "帮助",
      submenu: [
        {
          label: "关于 Journal",
          click: () => sendCommand(mainWindow, "open-about")
        }
      ]
    }
  ];
}

module.exports = {
  createApplicationMenuTemplate,
  nativeMenuChannel
};
