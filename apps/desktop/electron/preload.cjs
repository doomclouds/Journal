const { contextBridge, ipcRenderer } = require("electron");
const { createNativeMenuBridge } = require("./nativeMenuBridge.cjs");
const { nativeMenuChannel } = require("./menu.cjs");

contextBridge.exposeInMainWorld("journalDesktop", {
  platform: process.platform,
  getLocalServiceStatus: () => ipcRenderer.invoke("journal:get-local-service-status"),
  getApiBaseUrl: () => ipcRenderer.invoke("journal:get-api-base-url"),
  getDesktopAccessToken: () => ipcRenderer.invoke("journal:get-desktop-access-token"),
  selectImportPackage: () => ipcRenderer.invoke("journal:select-import-package"),
  openPath: targetPath => ipcRenderer.invoke("journal:open-path", targetPath),
  readLegalDocument: documentId => ipcRenderer.invoke("journal:read-legal-document", documentId),
  ...createNativeMenuBridge(ipcRenderer, nativeMenuChannel)
});
