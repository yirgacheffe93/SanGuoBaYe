'use strict';

/**
 * 三国霸业 桌面版 —— Electron 主进程
 *
 * 设计要点：
 * 1. 用自定义协议 app:// 提供静态资源（file:// 下 wasm 无法加载，已验证）。
 * 2. 引擎只通过两个回调（bayeLoadFileContent / bayeSaveFileContent）读写存档，
 *    这里把它们接到 userData/saves 下的真实文件，方便备份与将来接 Steam Cloud。
 * 3. 渲染进程禁用 Node、开启 contextIsolation + sandbox，只用 preload 暴露最小 API。
 */

const { app, BrowserWindow, Menu, ipcMain, protocol, shell, session } = require('electron');
const path = require('node:path');
const fs = require('node:fs');

const ROOT = path.join(__dirname, '..');
const SCHEME = 'app';
const HOST = 'baye';
const SMOKE = process.env.BAYE_SMOKE === '1';
const DEV = !app.isPackaged || process.env.BAYE_DEV === '1';

// 固定用户数据目录（ASCII 路径，避免中文路径带来的兼容问题，也方便 Steam Cloud 映射）
// 便携版 / 测试时可用 BAYE_USER_DATA 覆盖。
const USER_DATA = process.env.BAYE_USER_DATA
  ? path.resolve(process.env.BAYE_USER_DATA)
  : path.join(app.getPath('appData'), 'SanGuoBaYe');
app.setPath('userData', USER_DATA);
app.setName('三国霸业');

const SAVE_DIR = path.join(USER_DATA, 'saves');
const SETTINGS_FILE = path.join(USER_DATA, 'settings.json');
try {
  fs.mkdirSync(SAVE_DIR, { recursive: true });
} catch (err) {
  console.error('[main] 无法创建存档目录:', SAVE_DIR, err.message);
}

/* ------------------------------------------------------------------ */
/* 自定义协议                                                          */
/* ------------------------------------------------------------------ */

protocol.registerSchemesAsPrivileged([
  {
    scheme: SCHEME,
    privileges: {
      standard: true,
      secure: true,
      supportFetchAPI: true,
      corsEnabled: true,
      stream: true,
    },
  },
]);

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.map': 'application/json; charset=utf-8',
  '.wasm': 'application/wasm',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.gif': 'image/gif',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon',
  '.woff2': 'font/woff2',
  '.about': 'text/plain; charset=utf-8',
  '.text': 'text/plain; charset=utf-8',
};

function registerAppProtocol() {
  protocol.handle(SCHEME, async (request) => {
    let target;
    try {
      const url = new URL(request.url);
      let rel = decodeURIComponent(url.pathname);
      if (!rel || rel === '/') rel = '/desktop.html';
      // 规范化并阻止目录穿越
      target = path.normalize(path.join(ROOT, rel));
      if (target !== ROOT && !target.startsWith(ROOT + path.sep)) {
        return new Response('Forbidden', { status: 403 });
      }
      const data = await fs.promises.readFile(target);
      const type = MIME[path.extname(target).toLowerCase()] || 'application/octet-stream';
      return new Response(data, {
        status: 200,
        headers: { 'content-type': type, 'cache-control': 'no-cache' },
      });
    } catch (err) {
      if (err && err.code === 'ENOENT') {
        return new Response('Not Found: ' + (target || request.url), { status: 404 });
      }
      return new Response('Bad Request', { status: 400 });
    }
  });
}

/* ------------------------------------------------------------------ */
/* 存档读写（同步 IPC：引擎要求同步返回）                                */
/* ------------------------------------------------------------------ */

const SAVE_NAME_RE = /^[A-Za-z0-9._-]{1,128}$/;

function savePathFor(name) {
  const base = String(name || '').split('/').pop();
  if (!SAVE_NAME_RE.test(base)) throw new Error('非法的存档名: ' + name);
  const full = path.join(SAVE_DIR, base);
  if (path.dirname(full) !== SAVE_DIR) throw new Error('非法路径');
  return full;
}

function readSave(name) {
  try {
    return fs.readFileSync(savePathFor(name), 'utf8');
  } catch (err) {
    if (err && err.code === 'ENOENT') return null;
    console.error('[save] read failed:', name, err.message);
    return null;
  }
}

function writeSave(name, content) {
  // dat.lib 是只读资源，永不回写
  if (String(name).includes('dat.lib')) return true;
  if (typeof content !== 'string') content = content == null ? '' : String(content);
  if (content.length > 16 * 1024 * 1024) throw new Error('存档过大');
  const target = savePathFor(name);
  const tmp = target + '.tmp';
  fs.writeFileSync(tmp, content, 'utf8');
  fs.renameSync(tmp, target);
  return true;
}

function listSaves() {
  try {
    return fs
      .readdirSync(SAVE_DIR)
      .filter((f) => !f.startsWith('.') && !f.endsWith('.tmp'))
      .map((f) => {
        const st = fs.statSync(path.join(SAVE_DIR, f));
        return { name: f, size: st.size, mtime: st.mtimeMs };
      })
      .sort((a, b) => b.mtime - a.mtime);
  } catch (err) {
    console.error('[save] list failed:', err.message);
    return [];
  }
}

/* ------------------------------------------------------------------ */
/* 窗口                                                               */
/* ------------------------------------------------------------------ */

function loadSettings() {
  try {
    return JSON.parse(fs.readFileSync(SETTINGS_FILE, 'utf8'));
  } catch {
    return {};
  }
}

function storeSettings(patch) {
  const next = Object.assign(loadSettings(), patch);
  try {
    fs.writeFileSync(SETTINGS_FILE, JSON.stringify(next, null, 2), 'utf8');
  } catch (err) {
    console.error('[settings] write failed:', err.message);
  }
  return next;
}

let mainWindow = null;
let stateTimer = null;

function persistWindowState(win) {
  clearTimeout(stateTimer);
  stateTimer = setTimeout(() => {
    if (!win || win.isDestroyed() || win.isFullScreen()) return;
    const b = win.getNormalBounds();
    storeSettings({ window: { width: b.width, height: b.height, x: b.x, y: b.y } });
  }, 500);
}

function createWindow() {
  const saved = loadSettings().window || {};
  mainWindow = new BrowserWindow({
    width: saved.width || 1280,
    height: saved.height || 840,
    x: saved.x,
    y: saved.y,
    minWidth: 860,
    minHeight: 560,
    show: false,
    backgroundColor: '#0b0d11',
    title: '三国霸业',
    titleBarStyle: 'hiddenInset',
    trafficLightPosition: { x: 16, y: 16 },
    paintWhenInitiallyHidden: true,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
      spellcheck: false,
      backgroundThrottling: false,
      devTools: DEV,
    },
  });

  const smokeLib = process.env.BAYE_SMOKE_LIB || 'libs/SGBY.lib';
  const query = SMOKE && smokeLib ? '?lib=' + encodeURIComponent(smokeLib) : '';
  mainWindow.loadURL(`${SCHEME}://${HOST}/desktop.html${query}`);

  mainWindow.once('ready-to-show', () => {
    if (!SMOKE || process.env.BAYE_SMOKE_VISIBLE === '1') mainWindow.show();
  });

  // 页面内导航只允许本协议，外链交给系统浏览器
  mainWindow.webContents.on('will-navigate', (event, url) => {
    if (!url.startsWith(`${SCHEME}://${HOST}/`)) {
      event.preventDefault();
      shell.openExternal(url).catch(() => {});
    }
  });
  mainWindow.webContents.setWindowOpenHandler(({ url }) => {
    if (/^https?:/.test(url)) shell.openExternal(url).catch(() => {});
    return { action: 'deny' };
  });
  mainWindow.webContents.on('will-attach-webview', (event) => event.preventDefault());
  mainWindow.webContents.setVisualZoomLevelLimits(1, 1);
  mainWindow.webContents.on('before-input-event', (event, input) => {
    if (input.type === 'keyDown' && (input.meta || input.control) && input.key.toLowerCase() === '=') {
      event.preventDefault();
    }
  });

  mainWindow.on('resize', () => persistWindowState(mainWindow));
  mainWindow.on('move', () => persistWindowState(mainWindow));
  mainWindow.on('closed', () => {
    mainWindow = null;
  });
  mainWindow.on('enter-full-screen', () => mainWindow.webContents.send('ui:action', 'fullscreen-changed'));
  mainWindow.on('leave-full-screen', () => mainWindow.webContents.send('ui:action', 'fullscreen-changed'));

  if (SMOKE) runSmoke(mainWindow);
  return mainWindow;
}

/* ------------------------------------------------------------------ */
/* 菜单                                                               */
/* ------------------------------------------------------------------ */

function buildMenu() {
  const send = (action) => {
    if (mainWindow && !mainWindow.isDestroyed()) mainWindow.webContents.send('ui:action', action);
  };
  const isMac = process.platform === 'darwin';

  const template = [
    ...(isMac
      ? [
          {
            label: app.name,
            submenu: [
              { role: 'about', label: '关于 三国霸业' },
              { type: 'separator' },
              { label: '设置…', accelerator: 'Cmd+,', click: () => send('settings') },
              { type: 'separator' },
              { role: 'hide', label: '隐藏 三国霸业' },
              { role: 'hideOthers', label: '隐藏其他' },
              { role: 'unhide', label: '全部显示' },
              { type: 'separator' },
              { role: 'quit', label: '退出 三国霸业' },
            ],
          },
        ]
      : []),
    {
      label: '游戏',
      submenu: [
        { label: '选择版本…', accelerator: 'CmdOrCtrl+O', click: () => send('choose-lib') },
        { label: '打开存档文件夹', accelerator: 'CmdOrCtrl+Shift+S', click: () => shell.openPath(SAVE_DIR) },
        { type: 'separator' },
        { role: isMac ? 'close' : 'quit', label: isMac ? '关闭窗口' : '退出' },
      ],
    },
    {
      label: '编辑',
      submenu: [
        { role: 'undo', label: '撤销' },
        { role: 'redo', label: '重做' },
        { type: 'separator' },
        { role: 'cut', label: '剪切' },
        { role: 'copy', label: '复制' },
        { role: 'paste', label: '粘贴' },
        { role: 'selectAll', label: '全选' },
      ],
    },
    {
      label: '显示',
      submenu: [
        { role: 'togglefullscreen', label: '切换全屏' },
        { type: 'separator' },
        { label: '整数倍缩放', click: () => send('toggle-integer-scale') },
        { label: '扫描线效果', click: () => send('toggle-scanlines') },
        { type: 'separator' },
        ...(DEV ? [{ role: 'toggleDevTools', label: '开发者工具' }, { role: 'reload', label: '重新载入' }] : []),
      ],
    },
    {
      label: '窗口',
      submenu: [{ role: 'minimize', label: '最小化' }, { role: 'zoom', label: '缩放' }],
    },
  ];

  Menu.setApplicationMenu(Menu.buildFromTemplate(template));
}

/* ------------------------------------------------------------------ */
/* IPC                                                                */
/* ------------------------------------------------------------------ */

function registerIpc() {
  ipcMain.on('save:read', (event, name) => {
    event.returnValue = readSave(name);
  });
  ipcMain.on('save:write', (event, name, content) => {
    try {
      event.returnValue = writeSave(name, content);
    } catch (err) {
      console.error('[save] write failed:', name, err.message);
      event.returnValue = false;
    }
  });
  ipcMain.on('save:list', (event) => {
    event.returnValue = listSaves();
  });
  ipcMain.on('save:remove', (event, name) => {
    try {
      fs.unlinkSync(savePathFor(name));
      event.returnValue = true;
    } catch (err) {
      event.returnValue = false;
    }
  });

  ipcMain.handle('save:open-folder', () => shell.openPath(SAVE_DIR));

  ipcMain.on('app:info', (event) => {
    event.returnValue = {
      desktop: true,
      version: app.getVersion(),
      electron: process.versions.electron,
      chrome: process.versions.chrome,
      platform: process.platform,
      arch: process.arch,
      saveDir: SAVE_DIR,
    };
  });

  ipcMain.handle('win:toggle-fullscreen', (event) => {
    const win = BrowserWindow.fromWebContents(event.sender);
    if (!win) return false;
    win.setFullScreen(!win.isFullScreen());
    return win.isFullScreen();
  });

  ipcMain.on('win:is-fullscreen', (event) => {
    const win = BrowserWindow.fromWebContents(event.sender);
    event.returnValue = win ? win.isFullScreen() : false;
  });

  ipcMain.handle('app:open-external', (event, url) => {
    if (/^https?:/.test(String(url))) return shell.openExternal(String(url));
    return false;
  });

  ipcMain.on('app:quit', () => app.quit());
}

/* ------------------------------------------------------------------ */
/* 冒烟测试（CI / 本地验证用，不影响正常启动）                            */
/* ------------------------------------------------------------------ */

async function runSmoke(win) {
  const logs = [];
  // Electron 35+ 把 console-message 改成事件对象，这里两种签名都兼容
  win.webContents.on('console-message', (event, level, message) => {
    const text = typeof message === 'string' ? message : (event && event.message) || '';
    const lvl = typeof level === 'number' ? level : (event && event.level) || 0;
    logs.push(lvl + ':' + text);
    process.stdout.write('[renderer] ' + text + '\n');
  });

  await new Promise((resolve) => win.webContents.once('did-finish-load', resolve));
  let status = null;
  try {
    status = await win.webContents.executeJavaScript(
      `(async () => {
         const t0 = Date.now();
         while (!window.__bayeReady && Date.now() - t0 < 20000) {
           await new Promise(r => setTimeout(r, 100));
         }
         if (!window.__bayeSmokeStatus) return { ready: !!window.__bayeReady, error: 'no smoke hook' };
         return await window.__bayeSmokeStatus();
       })()`,
      true
    );
  } catch (err) {
    status = { error: String(err) };
  }
  console.log('SMOKE_STATUS ' + JSON.stringify(status));

  // 可选：向渲染进程注入按键，验证输入链路
  if (process.env.BAYE_SMOKE_KEYS) {
    let step = 0;
    for (const key of process.env.BAYE_SMOKE_KEYS.split(',')) {
      const keyCode = key.trim();
      if (!keyCode) continue;
      win.webContents.sendInputEvent({ type: 'keyDown', keyCode });
      win.webContents.sendInputEvent({ type: 'keyUp', keyCode });
      await new Promise((resolve) => setTimeout(resolve, 900));
      step += 1;
      if (process.env.BAYE_SMOKE_TRACE_DIR) {
        const image = await win.webContents.capturePage();
        const file = path.join(process.env.BAYE_SMOKE_TRACE_DIR, `step-${step}-${keyCode}.png`);
        fs.writeFileSync(file, image.toPNG());
        console.log('SMOKE_TRACE ' + file);
      }
    }
  }

  // 可见模式下等引擎多跑几帧，确认动画与循环正常
  if (process.env.BAYE_SMOKE_VISIBLE === '1') {
    await new Promise((resolve) => setTimeout(resolve, 3000));
    try {
      status = await win.webContents.executeJavaScript('window.__bayeSmokeStatus()', true);
      console.log('SMOKE_STATUS2 ' + JSON.stringify(status));
    } catch (err) {
      console.log('SMOKE_STATUS2_FAILED ' + err.message);
    }
  }

  if (process.env.BAYE_SMOKE_SCRIPTS) {
    try {
      const matches = await win.webContents.executeJavaScript(
        `(function(needle){
           var list = window.__bayeScripts || [];
           var hits = list.filter(function(s){ return s.indexOf(needle) >= 0; });
           return { total: list.length, hits: hits.length, sizes: list.map(function(s){ return s.length; }), samples: hits.slice(0, 6).map(function(s){ return s.slice(0, 600); }) };
         })(${JSON.stringify(process.env.BAYE_SMOKE_SCRIPTS)})`,
        true
      );
      console.log('SMOKE_SCRIPTS ' + JSON.stringify(matches).slice(0, 4000));
    } catch (err) {
      console.log('SMOKE_SCRIPTS_FAILED ' + err.message);
    }
  }

  console.log('SMOKE_WINDOW ' + JSON.stringify({
    bounds: win.getContentBounds(),
    fullscreen: win.isFullScreen(),
    zoom: win.webContents.getZoomFactor(),
  }));

  if (process.env.BAYE_SMOKE_SHOT) {
    try {
      const image = await win.webContents.capturePage();
      fs.writeFileSync(process.env.BAYE_SMOKE_SHOT, image.toPNG());
      console.log('SMOKE_SHOT ' + process.env.BAYE_SMOKE_SHOT);
    } catch (err) {
      console.log('SMOKE_SHOT_FAILED ' + err.message);
    }
  }
  console.log('SMOKE_LOGS ' + JSON.stringify(logs.slice(-40)));
  app.exit(0);
}

/* ------------------------------------------------------------------ */
/* 生命周期                                                            */
/* ------------------------------------------------------------------ */

if (!app.requestSingleInstanceLock()) {
  app.quit();
} else {
  app.on('second-instance', () => {
    if (mainWindow) {
      if (mainWindow.isMinimized()) mainWindow.restore();
      mainWindow.focus();
    }
  });

  app.whenReady().then(() => {
    registerAppProtocol();
    registerIpc();
    buildMenu();

    // 统一拒绝网页权限申请（麦克风、地理位置等一概不需要）
    session.defaultSession.setPermissionRequestHandler((_wc, _permission, callback) => callback(false));

    app.setAboutPanelOptions({
      applicationName: '三国霸业',
      applicationVersion: app.getVersion(),
      copyright: '基于 baye-alpha 引擎',
    });

    createWindow();

    app.on('activate', () => {
      if (BrowserWindow.getAllWindows().length === 0) createWindow();
    });
  });

  app.on('window-all-closed', () => {
    if (process.platform !== 'darwin' || SMOKE) app.quit();
  });
}

process.on('uncaughtException', (err) => {
  console.error('[main] uncaught:', err);
  if (SMOKE) app.exit(1);
});

process.on('unhandledRejection', (err) => {
  console.error('[main] unhandled rejection:', err);
});

if (SMOKE) {
  // 冒烟测试兜底：任何一步卡住都不要把进程留在后台
  setTimeout(() => {
    console.log('SMOKE_TIMEOUT');
    app.exit(2);
  }, 45000).unref();
}
