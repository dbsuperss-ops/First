'use strict'
const { contextBridge, ipcRenderer } = require('electron')

contextBridge.exposeInMainWorld('electronAPI', {
  callClaude: (apiKey, messages) =>
    ipcRenderer.invoke('call-claude', { apiKey, messages }),
})
