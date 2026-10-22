/*
 * 由 desktop.html 在 js/lcd.js 之后、js/baye.js 之前加载。
 * lcd.js 末尾会重置全局 Module，所以必须在这里挂接运行时回调；
 * wasm 实例化是异步的，真实启动逻辑放在 shell/shell.js 里。
 */
Module.onRuntimeInitialized = function () {
  if (typeof window.__bayeBoot === 'function') {
    window.__bayeBoot();
  } else {
    window.__bayeBootPending = true;
  }
};
