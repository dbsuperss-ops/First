'use strict'
const { contextBridge, ipcRenderer } = require('electron')

contextBridge.exposeInMainWorld('electronAPI', {
  readStore:  (key)       => ipcRenderer.invoke('fs:readStore', key),
  writeStore: (key, data) => ipcRenderer.invoke('fs:writeStore', key, data),
  callAI:     (opts)      => ipcRenderer.invoke('ai:call', opts),
})
