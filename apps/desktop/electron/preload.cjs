const { contextBridge } = require("electron");

contextBridge.exposeInMainWorld("journalDesktop", {
  platform: process.platform
});
