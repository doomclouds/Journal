const { contextBridge, ipcRenderer } = require("electron");
const { createNativeMenuBridge } = require("./nativeMenuBridge.cjs");
const { nativeMenuChannel } = require("./menu.cjs");

contextBridge.exposeInMainWorld("journalDesktop", {
  platform: process.platform,
  ...createNativeMenuBridge(ipcRenderer, nativeMenuChannel)
});
