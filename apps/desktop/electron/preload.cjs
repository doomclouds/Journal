const { contextBridge, ipcRenderer } = require("electron");
const { nativeMenuChannel } = require("./menu.cjs");

contextBridge.exposeInMainWorld("journalDesktop", {
  platform: process.platform,
  onNativeMenuCommand: callback => {
    const listener = (_event, command) => callback(command);
    ipcRenderer.on(nativeMenuChannel, listener);

    return () => {
      ipcRenderer.removeListener(nativeMenuChannel, listener);
    };
  }
});
