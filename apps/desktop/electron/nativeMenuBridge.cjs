function createNativeMenuBridge(ipcRenderer, channel) {
  const handlers = new Set();
  const pendingCommands = [];

  ipcRenderer.on(channel, (_event, command) => {
    if (handlers.size === 0) {
      pendingCommands.push(command);
      return;
    }

    handlers.forEach(handler => handler(command));
  });

  return {
    onNativeMenuCommand: callback => {
      handlers.add(callback);

      while (pendingCommands.length > 0) {
        callback(pendingCommands.shift());
      }

      return () => {
        handlers.delete(callback);
      };
    }
  };
}

module.exports = {
  createNativeMenuBridge
};
