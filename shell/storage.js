/*
 * 存档桥：把引擎的 localStorage 键值读写改接到真实文件。
 *
 * 引擎（baye.js 里的 emscripten 回调）只通过两个全局函数做持久化：
 *   bayeLoadFileContent(name) -> string
 *   bayeSaveFileContent(name, content)
 * 其中 name 形如 "baye//data/sango0.sav"；dat.lib 是只读资源，仍然走原逻辑。
 */
(function () {
  'use strict';

  var api = window.bayeDesktop;
  if (!api || !api.desktop) {
    console.log('[storage] 未检测到桌面 API，退回 localStorage');
    return;
  }

  var originalLoad = window.bayeLoadFileContent;
  var originalSave = window.bayeSaveFileContent;
  var isLib = function (name) { return String(name).indexOf('dat.lib') !== -1; };

  window.bayeLoadFileContent = function (name) {
    try {
      if (isLib(name)) {
        return originalLoad ? originalLoad(name) : null;
      }
      var value = api.readSave(name);
      return value == null ? '' : value;
    } catch (err) {
      console.error('[storage] 读取失败:', name, err);
      return '';
    }
  };

  window.bayeSaveFileContent = function (name, content) {
    try {
      if (isLib(name)) return;
      api.writeSave(name, content == null ? '' : String(content));
    } catch (err) {
      console.error('[storage] 写入失败:', name, err);
    }
  };

  console.log('[storage] 存档已改为文件持久化');
})();
