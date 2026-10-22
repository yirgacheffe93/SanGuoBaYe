'use strict';

const { contextBridge, ipcRenderer } = require('electron');

/**
 * 渲染进程可用的最小 API。
 * 引擎（以及 .lib 里的脚本）运行在同一个页面里，所以这里只暴露存档目录内的能力，
 * 不暴露任意文件系统访问。
 */
contextBridge.exposeInMainWorld('bayeDesktop', {
  desktop: true,

  readSave: (name) => ipcRenderer.sendSync('save:read', String(name)),
  writeSave: (name, content) =>
    ipcRenderer.sendSync('save:write', String(name), content == null ? '' : String(content)),
  listSaves: () => ipcRenderer.sendSync('save:list'),
  removeSave: (name) => ipcRenderer.sendSync('save:remove', String(name)),

  openSaveFolder: () => ipcRenderer.invoke('save:open-folder'),
  openExternal: (url) => ipcRenderer.invoke('app:open-external', String(url)),

  info: () => ipcRenderer.sendSync('app:info'),
  quit: () => ipcRenderer.send('app:quit'),

  toggleFullscreen: () => ipcRenderer.invoke('win:toggle-fullscreen'),
  isFullscreen: () => ipcRenderer.sendSync('win:is-fullscreen'),

  onCommand: (callback) => {
    const handler = (_event, action) => callback(action);
    ipcRenderer.on('ui:action', handler);
    return () => ipcRenderer.removeListener('ui:action', handler);
  },
});
