/*
 * 桌面外壳逻辑：启动引擎、画面缩放、键盘/鼠标输入、版本选择、设置。
 * 依赖 js/lcd.js（渲染与按键）、js/bridge.js（脚本桥）、shell/storage.js（存档）。
 */
(function () {
  'use strict';

  var VK = {
    up: 0x22,
    down: 0x23,
    left: 0x24,
    right: 0x25,
    help: 0x26,
    enter: 0x27,
    exit: 0x28,
    search: 0x33,
  };

  var LS = {
    libpath: 'baye/libpath',
    libname: 'baye/libname',
    integer: 'baye.desktop.integerScale',
  };

  var canvas, stage, modalLayer, modal, toastEl, libLabel, loading, loadingText, bootError, bootErrorText;

  var state = {
    frames: 0,
    startedAt: 0,
    ready: false,
    lib: '',
    libName: '',
    modalOpen: false,
    modalDismissible: true,
    integerScale: true,
    watchTimer: null,
  };

  /* ------------------------------------------------------------------ */
  /* 小工具                                                              */
  /* ------------------------------------------------------------------ */

  function byId(id) { return document.getElementById(id); }

  function make(tag, className, text) {
    var node = document.createElement(tag);
    if (className) node.className = className;
    if (text != null) node.textContent = text;
    return node;
  }

  var toastTimer = null;
  function toast(message, duration) {
    if (!toastEl) return;
    toastEl.textContent = message;
    toastEl.classList.remove('hidden');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { toastEl.classList.add('hidden'); }, duration || 2600);
  }

  function basename(p) {
    var parts = String(p || '').split('/');
    return parts[parts.length - 1];
  }

  function hasDesktop() {
    return !!(window.bayeDesktop && window.bayeDesktop.desktop);
  }

  /* ------------------------------------------------------------------ */
  /* 画面缩放                                                            */
  /* ------------------------------------------------------------------ */

  function fit() {
    if (!canvas || !canvas.width || !canvas.height || !stage) return;

    var rect = stage.getBoundingClientRect();
    // 32px：stage 左右内边距；24px：bezel 内边距；再加一点余量
    var availW = Math.max(48, rect.width - 72);
    var availH = Math.max(48, rect.height - 72);

    var scale = Math.min(availW / canvas.width, availH / canvas.height);
    if (!isFinite(scale) || scale <= 0) scale = 1;

    if (state.integerScale && scale >= 1) {
      var whole = Math.floor(scale);
      // 只有整数倍不会浪费太多空间时才用，否则宁可铺满
      if (whole >= 1 && whole / scale >= 0.85) scale = whole;
    }

    canvas.style.width = Math.max(1, Math.round(canvas.width * scale)) + 'px';
    canvas.style.height = Math.max(1, Math.round(canvas.height * scale)) + 'px';
  }

  /* ------------------------------------------------------------------ */
  /* 提示 / 错误                                                         */
  /* ------------------------------------------------------------------ */

  function hideLoading() {
    if (loading) loading.classList.add('hidden');
  }

  function showBootError(error) {
    hideLoading();
    if (!bootError) return;
    bootErrorText.textContent = (error && error.stack) || String(error);
    bootError.classList.remove('hidden');
    console.error('[shell] 启动失败:', error);
  }

  function watchFirstFrame() {
    clearTimeout(state.watchTimer);
    state.watchTimer = setTimeout(function () {
      if (state.frames > 0) return;
      if (loadingText) loadingText.textContent = '引擎没有输出画面，可以尝试重新启动或更换版本';
    }, 9000);
  }

  /* ------------------------------------------------------------------ */
  /* 引擎钩子                                                            */
  /* ------------------------------------------------------------------ */

  function installHooks() {
    // 引擎通过 eval 执行 .lib 里的脚本；记录下来便于排查（也用于冒烟测试）
    var rawEval = window.eval;
    window.__bayeScripts = [];
    window.eval = function (code) {
      try { window.__bayeScripts.push(String(code)); } catch (err) { /* 忽略 */ }
      return rawEval.call(window, code);
    };

    var originalResize = window.bayeResizeScreen;
    window.bayeResizeScreen = function () {
      var result = originalResize.apply(this, arguments);
      fit();
      return result;
    };

    var originalFlush = window.bayeFlushLcdBuffer;
    window.bayeFlushLcdBuffer = function () {
      state.frames += 1;
      if (state.frames === 1) {
        state.ready = true;
        window.__bayeReady = true;
        hideLoading();
        fit();
      }
      return originalFlush.apply(this, arguments);
    };

    // 引擎“退出游戏”会调用 goHome()，桌面版改为回到版本选择
    window.goHome = function () {
      showLauncher({ dismissible: true });
    };

    // 原引擎用 alert 报错，桌面版改成不阻塞的提示
    window.alert = function (message) {
      console.warn('[engine alert]', message);
      toast(String(message));
    };

    window.onerror = function (message, source, line) {
      console.error('[window.onerror]', message, source, line);
      toast('运行错误：' + message);
      return false;
    };
  }

  /* ------------------------------------------------------------------ */
  /* 输入                                                                */
  /* ------------------------------------------------------------------ */

  function sendVK(name) {
    if (typeof window.sendKey !== 'function') return;
    var code = VK[name];
    if (code == null) return;
    window.sendKey(code);
  }

  var KEYMAP = {
    ArrowUp: 'up',
    ArrowDown: 'down',
    ArrowLeft: 'left',
    ArrowRight: 'right',
    Enter: 'enter',
    ' ': 'exit',
    Escape: 'exit',
    h: 'help',
    H: 'help',
    f: 'search',
    F: 'search',
    s: 'search',
    S: 'search',
  };

  function onKeyDown(event) {
    if (event.metaKey || event.ctrlKey || event.altKey) return;

    if (state.modalOpen) {
      if (event.key === 'Escape' && state.modalDismissible) {
        event.preventDefault();
        closeModal();
      }
      return;
    }

    var target = event.target;
    if (target && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA')) return;

    var action = KEYMAP[event.key];
    if (!action) return;
    event.preventDefault();
    sendVK(action);
  }

  function bindKeypad() {
    var buttons = document.querySelectorAll('.key[data-vk]');
    Array.prototype.forEach.call(buttons, function (button) {
      var action = button.getAttribute('data-vk');
      button.addEventListener('pointerdown', function (event) {
        event.preventDefault();
        button.classList.add('active');
        sendVK(action);
      });
      var release = function () { button.classList.remove('active'); };
      button.addEventListener('pointerup', release);
      button.addEventListener('pointerleave', release);
      button.addEventListener('pointercancel', release);
    });
  }

  /* ------------------------------------------------------------------ */
  /* 模态框                                                              */
  /* ------------------------------------------------------------------ */

  function openModal(builder, options) {
    options = options || {};
    modal.innerHTML = '';
    builder(modal);
    modalLayer.classList.remove('hidden');
    state.modalOpen = true;
    state.modalDismissible = options.dismissible !== false;
  }

  function closeModal() {
    modalLayer.classList.add('hidden');
    modal.innerHTML = '';
    state.modalOpen = false;
  }

  /* ------------------------------------------------------------------ */
  /* 版本选择                                                            */
  /* ------------------------------------------------------------------ */

  function currentLibPath() {
    return localStorage.getItem(LS.libpath) || '';
  }

  function chooseLib(item) {
    localStorage.setItem(LS.libpath, item.path);
    localStorage.setItem(LS.libname, item.title);
    window.location.reload();
  }

  function showLauncher(options) {
    options = options || {};
    var dismissible = options.dismissible !== false;

    openModal(function (root) {
      var head = make('div', 'modal-head');
      head.appendChild(make('h2', null, '三国霸业'));
      head.appendChild(make('p', 'sub', dismissible ? '当前版本：' + (state.libName || basename(currentLibPath()) || '未选择') : '选择一个版本开始游戏'));
      root.appendChild(head);

      var list = make('div', 'lib-list');
      list.appendChild(make('p', 'empty', '正在读取版本列表…'));
      root.appendChild(list);

      var actions = make('div', 'modal-actions');
      if (hasDesktop()) {
        var openFolder = make('button', 'btn', '打开存档文件夹');
        openFolder.type = 'button';
        openFolder.addEventListener('click', function () { window.bayeDesktop.openSaveFolder(); });
        actions.appendChild(openFolder);
      }
      if (dismissible) {
        var resume = make('button', 'btn', '返回游戏');
        resume.type = 'button';
        resume.addEventListener('click', closeModal);
        actions.appendChild(resume);
      }
      if (actions.childElementCount > 0) root.appendChild(actions);

      fetch('libs.json', { cache: 'no-cache' })
        .then(function (response) { return response.json(); })
        .then(function (libs) {
          list.innerHTML = '';
          if (!Array.isArray(libs) || libs.length === 0) {
            list.appendChild(make('p', 'empty', '没有找到 libs.json 中的版本'));
            return;
          }
          var current = currentLibPath();
          libs.forEach(function (item) {
            var row = make('button', 'lib-row');
            row.type = 'button';
            if (item.path === current) row.classList.add('selected');

            row.appendChild(make('span', 'title', item.title));
            if (item.path === 'libs/SGBY.lib') row.appendChild(make('span', 'badge', '推荐'));
            row.appendChild(make('span', 'path', basename(item.path)));

            row.addEventListener('click', function () { chooseLib(item); });
            list.appendChild(row);

            if (item.path === current) {
              var selectedRow = list.lastChild;
              var desc = make('pre', 'lib-desc hidden');
              fetch(item.path + '.about', { cache: 'no-cache' })
                .then(function (r) { return r.ok ? r.text() : ''; })
                .then(function (text) {
                  if (!text) return;
                  desc.textContent = text;
                  selectedRow.insertAdjacentElement('afterend', desc);
                })
                .catch(function () {});
            }
          });
        })
        .catch(function (err) {
          list.innerHTML = '';
          list.appendChild(make('p', 'empty', '读取版本列表失败：' + err));
        });
    }, { dismissible: dismissible });
  }

  /* ------------------------------------------------------------------ */
  /* 设置                                                                */
  /* ------------------------------------------------------------------ */

  function toggleIntegerScale() {
    state.integerScale = !state.integerScale;
    localStorage.setItem(LS.integer, state.integerScale ? '1' : '0');
    fit();
    toast(state.integerScale ? '已切换为整数倍缩放（更清晰）' : '已切换为铺满缩放（更大画面）');
  }

  function toggleScanlines() {
    var on = !document.body.classList.contains('scanlines');
    document.body.classList.toggle('scanlines', on);
    localStorage.setItem('baye.desktop.scanlines', on ? '1' : '0');
    toast(on ? '扫描线已开启' : '扫描线已关闭');
  }

  function showSettings() {
    openModal(function (root) {
      var head = make('div', 'modal-head');
      head.appendChild(make('h2', null, '设置'));
      head.appendChild(make('p', 'sub', '显示与存档'));
      root.appendChild(head);

      var rows = make('div', 'settings');

      rows.appendChild(toggleRow('整数倍缩放', '像素更锐利，画面略小', state.integerScale, function () {
        toggleIntegerScale();
      }, function () { return state.integerScale; }));

      rows.appendChild(toggleRow('扫描线效果', '模拟老式点阵屏质感',
        document.body.classList.contains('scanlines'),
        toggleScanlines,
        function () { return document.body.classList.contains('scanlines'); }));

      rows.appendChild(actionRow('全屏显示', 'Ctrl + ⌘ + F', '切换', function () {
        if (hasDesktop()) window.bayeDesktop.toggleFullscreen();
      }));

      if (hasDesktop()) {
        var info = window.bayeDesktop.info();
        rows.appendChild(actionRow('存档文件夹', info.saveDir, '打开', function () {
          window.bayeDesktop.openSaveFolder();
        }));
        rows.appendChild(infoRow('版本', info.version + ' · Electron ' + info.electron));
        rows.appendChild(infoRow('平台', info.platform + ' / ' + info.arch));
      }

      root.appendChild(rows);

      var actions = make('div', 'modal-actions');
      var reload = make('button', 'btn ghost', '重新启动');
      reload.type = 'button';
      reload.addEventListener('click', function () { window.location.reload(); });
      actions.appendChild(reload);

      var close = make('button', 'btn primary', '完成');
      close.type = 'button';
      close.addEventListener('click', closeModal);
      actions.appendChild(close);
      root.appendChild(actions);
    }, { dismissible: true });
  }

  function toggleRow(label, hint, on, onClick, read) {
    var row = make('div', 'row');
    var text = make('div', 'row-text');
    text.appendChild(make('span', 'label', label));
    if (hint) text.appendChild(make('span', 'hint', hint));
    row.appendChild(text);

    var button = make('button', 'switch' + (on ? ' on' : ''));
    button.type = 'button';
    button.setAttribute('aria-label', label);
    button.addEventListener('click', function () {
      onClick();
      button.classList.toggle('on', read());
    });
    row.appendChild(button);
    return row;
  }

  function actionRow(label, hint, actionText, onClick) {
    var row = make('div', 'row');
    var text = make('div', 'row-text');
    text.appendChild(make('span', 'label', label));
    if (hint) text.appendChild(make('span', 'hint', hint));
    row.appendChild(text);

    var button = make('button', 'btn small', actionText);
    button.type = 'button';
    button.addEventListener('click', onClick);
    row.appendChild(button);
    return row;
  }

  function infoRow(label, value) {
    var row = make('div', 'row');
    row.appendChild(make('span', 'label', label));
    row.appendChild(make('span', 'value', value));
    return row;
  }

  /* ------------------------------------------------------------------ */
  /* 启动                                                                */
  /* ------------------------------------------------------------------ */

  function boot() {
    if (window.__bayeBooted) return;
    window.__bayeBooted = true;
    state.startedAt = Date.now();
    state.lib = currentLibPath();
    state.libName = localStorage.getItem(LS.libname) || '';

    if (!state.lib) {
      document.body.classList.add('no-lib');
      hideLoading();
      showLauncher({ dismissible: false });
      return;
    }
    document.body.classList.remove('no-lib');

    if (libLabel) libLabel.textContent = state.libName || basename(state.lib);

    try {
      installHooks();
      lcdSetDotSize(1);
      lcdInit();
      fit();
      watchFirstFrame();
      bayeMain();
    } catch (err) {
      showBootError(err);
    }
  }

  /* ------------------------------------------------------------------ */
  /* 初始化                                                              */
  /* ------------------------------------------------------------------ */

  function init() {
    canvas = byId('lcd');
    stage = byId('stage');
    modalLayer = byId('modal-layer');
    modal = byId('modal');
    toastEl = byId('toast');
    libLabel = byId('lib-name');
    loading = byId('loading');
    loadingText = byId('loading-text');
    bootError = byId('boot-error');
    bootErrorText = byId('boot-error-text');

    // 开发/测试用：desktop.html?lib=libs/SGBY.lib 可直接指定版本
    //             desktop.html?ui=settings 可直接打开设置面板
    try {
      var params = new URLSearchParams(window.location.search);
      if (params.get('lib')) {
        localStorage.setItem(LS.libpath, params.get('lib'));
        localStorage.setItem(LS.libname, params.get('name') || basename(params.get('lib')));
      }
      window.__bayeUiParam = params.get('ui') || '';
    } catch (err) { /* 忽略 */ }

    if (!hasDesktop()) document.body.classList.add('web');
    if (!currentLibPath()) document.body.classList.add('no-lib');

    state.integerScale = localStorage.getItem(LS.integer) !== '0';
    if (localStorage.getItem('baye.desktop.scanlines') === '1') {
      document.body.classList.add('scanlines');
    }

    byId('btn-retry').addEventListener('click', function () { window.location.reload(); });
    byId('btn-choose').addEventListener('click', function () { showLauncher({ dismissible: true }); });
    byId('btn-settings').addEventListener('click', showSettings);
    byId('btn-fullscreen').addEventListener('click', function () {
      if (hasDesktop()) {
        window.bayeDesktop.toggleFullscreen();
      } else if (document.fullscreenElement) {
        document.exitFullscreen();
      } else {
        document.documentElement.requestFullscreen().catch(function () {});
      }
    });

    modalLayer.addEventListener('click', function (event) {
      if (event.target === modalLayer && state.modalDismissible) closeModal();
    });

    canvas.addEventListener('dblclick', function () {
      if (hasDesktop()) window.bayeDesktop.toggleFullscreen();
    });

    document.addEventListener('keydown', onKeyDown, true);
    window.addEventListener('resize', fit);
    if (window.ResizeObserver) new ResizeObserver(fit).observe(stage);

    bindKeypad();

    if (hasDesktop()) {
      window.bayeDesktop.onCommand(function (action) {
        if (action === 'settings') showSettings();
        else if (action === 'choose-lib') showLauncher({ dismissible: true });
        else if (action === 'toggle-integer-scale') toggleIntegerScale();
        else if (action === 'toggle-scanlines') toggleScanlines();
      });
    }

    window.__bayeBoot = boot;
    if (window.__bayeBootPending) boot();

    // 便于调试与自动化截图
    window.bayeShell = {
      fit: fit,
      showLauncher: showLauncher,
      showSettings: showSettings,
      closeModal: closeModal,
      toggleIntegerScale: toggleIntegerScale,
      toggleScanlines: toggleScanlines,
      state: state,
    };

    if (window.__bayeUiParam === 'settings') {
      setTimeout(showSettings, 400);
    } else if (window.__bayeUiParam === 'launcher') {
      setTimeout(function () { showLauncher({ dismissible: true }); }, 400);
    }
  }

  // 冒烟测试（BAYE_SMOKE=1）用的状态与存档回归检查
  window.__bayeSmokeStatus = function () {
    var info = null;
    try { if (hasDesktop()) info = window.bayeDesktop.info(); } catch (err) { info = String(err); }

    var storage = null;
    if (hasDesktop()) {
      try {
        window.bayeDesktop.writeSave('baye//data/__smoke.sav', 'SMOKE-123');
        var back = window.bayeDesktop.readSave('baye//data/__smoke.sav');
        var list = window.bayeDesktop.listSaves();
        window.bayeDesktop.removeSave('baye//data/__smoke.sav');
        storage = { ok: back === 'SMOKE-123', readBack: back, count: list.length };
      } catch (err) {
        storage = { error: String(err) };
      }
    }

    return {
      ready: !!window.__bayeReady,
      frames: state.frames,
      lib: state.libName || basename(state.lib),
      canvas: { w: canvas.width, h: canvas.height },
      css: { w: canvas.style.width, h: canvas.style.height },
      integerScale: state.integerScale,
      storage: storage,
      info: info,
    };
  };

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
