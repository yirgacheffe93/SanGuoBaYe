# Vendored upstream: iBaye

| 项 | 值 |
|---|---|
| 仓库 | https://gitee.com/bgwp/iBaye |
| commit | `62241e294f3ba3d4589d6e1205e6deb486a6c2d9` |
| commit date | 2026-03-17 |
| 上游许可 | MIT（见 `LICENSE`） |

## 为什么 vendored

- 原生构建需要可复现的固定版本，不能依赖 Gitee 的浮动 master。
- 需要在 `native/patches/` 里维护少量本地补丁，同时保持上游源码可 diff。
- 社区 mod / `.lib` 数据和引擎版本强相关，升级时应当显式切换 commit 并重跑 P0 冒烟。

## 包含/排除

包含：

- `src/*.c`、`src/*.h`：游戏逻辑与引擎。
- `src/platform/common/`：LCD、消息队列、BIOS/内存等公共平台层。
- `src/inc/`、`src/baye/`、`src/data/`：头文件与数据。

排除：

- `src/platform/js/`：Emscripten 平台层（`baye.js` 胶水、JS 脚本桥、内嵌字体数组）。原生构建不使用；字体已提取到 `assets/fonts/`。
- `src/platform/win/`、`src/platform/ios/`：其他平台后端。需要参考时请看上游原仓库。
- `baye-ios/`、`submodules/`、`images/`：Xcode 工程、msgpack 子模块、截图。

被排除的平台实现可以在上游仓库查看；本工程用 `native/platform/posix/` 替代。

## 本地补丁

补丁在 `native/patches/`，构建时应用到 `build/src`，不修改 vendored 源码：

| 补丁 | 内容 | 原因 |
|---|---|---|
| `0001-gui-host-wait-hook.patch` | `GuiGetMsg()` 阻塞前调用宿主回调 | 在完整帧边界取帧 |
| `0002-sys-flush-now.patch` | 新增 `SysFlushLcdNow()` | 宿主主动刷新当前完整帧 |
| `0003-comIn-font-path-buffer.patch` | `path[64]` → `path[512]` | 绝对路径过长时栈溢出 |

新增补丁时请保持「最小、可解释、可回归」；每个补丁都要在 `docs/p0-report.md` 或本文件里写明原因。

## 字体

`assets/fonts/` 由 `tools/extract_fonts.py` 从上游
`src/platform/js/font.bin.c`、`font24.*.bin.c`、`font24_ascii.*.c` 提取。
上游 C 数组仍保留在 Gitee 仓库；本工程不重复提交十几 MB 的数组源码。

## 许可提醒

MIT 覆盖的是 loongw 的移植工作。`src/inc/dictsys.h` 等文件保留
「Copyright (c) 2002, 广东步步高教育电子分公司」的原始版权头；
游戏数据包（`.lib`）、美术、字体、音乐的权利状态未确认。
商业发行前必须单独解决授权问题，详见根目录 README 与
`docs/p0-report.md`。
