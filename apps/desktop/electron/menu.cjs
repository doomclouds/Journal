const nativeMenuChannel = "journal:native-menu";

function sendCommand(mainWindow, command) {
  if (!mainWindow || mainWindow.isDestroyed?.()) {
    return;
  }

  mainWindow.webContents.send(nativeMenuChannel, command);
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
      label: "视图",
      submenu: [
        { label: "重新加载", role: "reload" },
        { label: "切换开发者工具", role: "toggleDevTools" },
        { type: "separator" },
        { label: "放大", role: "zoomIn" },
        { label: "缩小", role: "zoomOut" },
        { label: "重置缩放", role: "resetZoom" },
        { type: "separator" },
        { label: "全屏", role: "togglefullscreen" }
      ]
    },
    {
      label: "窗口",
      submenu: [
        { label: "最小化", role: "minimize" },
        { label: "关闭窗口", role: "close" }
      ]
    },
    {
      label: "帮助",
      submenu: [
        {
          label: "关于 Journal",
          click: () => showAbout?.()
        }
      ]
    }
  ];
}

module.exports = {
  createApplicationMenuTemplate,
  nativeMenuChannel
};
