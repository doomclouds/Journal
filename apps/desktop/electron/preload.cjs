const { contextBridge, ipcRenderer } = require("electron");
const { createNativeMenuBridge } = require("./nativeMenuBridge.cjs");
const { nativeMenuChannel } = require("./menu.cjs");

contextBridge.exposeInMainWorld("journalDesktop", {
  platform: process.platform,
  getLocalServiceStatus: () => ipcRenderer.invoke("journal:get-local-service-status"),
  getApiBaseUrl: () => ipcRenderer.invoke("journal:get-api-base-url"),
  ...createNativeMenuBridge(ipcRenderer, nativeMenuChannel)
});
