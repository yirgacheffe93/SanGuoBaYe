# P0 验证报告：原生 iBaye + macOS

日期：2026-10-22
环境：macOS 27.0.1 (arm64)、Apple clang 21.0.0、Python 3

## 1. 结论

**P0 通过。** iBaye C 核心可以脱离 WebAssembly，在 Apple Silicon 上原生编译、启动、接收按键并进入游戏地图。这是 Godot 方案成立的前提。

验证证据：

- `tools/build_native.sh` 无错误完成。
- `tools/run_smoke.sh` 启动 SGBY 版，输出 480×288 帧，最终画面为 190 年 1 月、北平的城池地图。
- 截图：`docs/images/native-map.png`。

![进入游戏地图](images/native-map.png)

## 2. 上游版本

| 项 | 值 |
|---|---|
| 仓库 | https://gitee.com/bgwp/iBaye |
| commit | `62241e294f3ba3d4589d6e1205e6deb486a6c2d9` |
| 日期 | 2026-03-17 |
| 许可 | MIT（仓库根目录 `LICENSE`） |
| 注意 | 源码中保留 2002 年步步高版权头；游戏数据/美术/音乐权利未确认 |

本地 vendored 在 `native/ibaye_core/src`，只保留可移植部分：去掉 `platform/js`（Emscripten）、`platform/win`、`platform/ios`，保留 `platform/common` 和全部游戏逻辑。

## 3. 验证过的流程

启动时的实际调用链（日志）：

```text
PlcMovie(spe=3)                     开局动画
GamMainChose()                      主菜单
  GamPicMenu(MAIN_PIC=44)           新君登基 / 重返沙场 / 制作群组 / 卸甲归田
  GamPicMenu(YEAR_PIC=45)           时期选择
  GamGetKing()                      君主选择
GameDevDrv()                        进入地图
```

冒烟程序按键序列：

```text
t=3.0s  VK_ENTER     跳过开场动画 / 选「新君登基」
t=3.8s  VK_ENTER     选第一个时期
t=4.6s  VK_DOWN
t=4.8s  VK_ENTER     选君主
t=5.6s  VK_ENTER     进入地图
```

## 4. 得到的运行时参数

| 项 | 值 |
|---|---|
| 逻辑屏幕 | 240×144 |
| `g_scale` | 2 |
| 帧缓冲 | 480×288，每像素 4 字节，`0xAARRGGBB` |
| 游戏内容区 | 左上角 160×96（逻辑），其余为画布留白，与 H5 版一致 |
| 安全取帧 | 引擎阻塞在 `GuiGetMsg()` 前 |
| 引擎线程 | 独立线程，`GamBaYeEng()` 不返回 |
| 定时器 | 5ms POSIX 心跳；timer0 滚动、timer1 延时、timer2 刷新 |

## 5. 发现并修复的四个问题

### 5.1 `has_hook` 不能返回 1

上游 `platform/ios/dummy-script.c`：

```c
int has_hook(const char* name) { return 1; }
```

`GamMainChose()` 里：

```c
IF_HAS_HOOK("chooseGameEntry") {
    choice = CALL_HOOK_A();   // dummy 返回 -1
}
switch (choice) { ... }       // -1 不匹配任何 case，主菜单被跳过
```

原生后端的正确行为是 `has_hook` 返回 0，让所有 hook 走默认 C 逻辑。

### 5.2 字体路径缓冲区溢出

`comIn.c`：

```c
U8 path[64];
sprintf(path, "%s/font24.en.%d", g_fontDir, i);
```

`/tmp/baye_clean/build/fonts/...` 这种短路径不会触发；换成项目里的
`/Users/.../SanGuoBaYe/assets/fonts/...` 后直接 `SIGTRAP`。补丁改为 `path[512]`。

这个坑曾经被误判为「字体文件多一个字节」问题，实际是路径长度导致的栈溢出。

### 5.3 取帧时机：不能在定时器里直接取

`timer2` 每 20ms 调 `timed_flush_lcd()`，可能打断引擎绘制，产出半帧。

解决方案：

1. 补丁 `0001`：`GuiGetMsg()` 阻塞前调用宿主回调。
2. 补丁 `0002`：新增 `SysFlushLcdNow()`，强制刷新完整帧。
3. 冒烟程序只在宿主回调里保存帧。

这个机制会原样搬到 GDExtension：**帧在引擎等待输入时提交**。

### 5.4 屏幕尺寸必须在初始化前设置

SGBY 的 `.lib` 里有一段脚本：

```js
baye.resizeScreen(240, 144);
```

H5 版在 `GamVarInit` 里执行它；原生没有 JS 脚本，所以必须在
`GamConInit()` 之前设置 `g_screenWidth = 240; g_screenHeight = 144;`。

## 6. 字体

Emscripten 版把字体作为 C 数组编译进 `fsys.c`，原生需要文件。
`tools/extract_fonts.py` 从上游数组还原：

| 文件 | 大小 | 说明 |
|---|---|---|
| `font.bin` | 163,840 | 默认 12px 字体 |
| `font24.cn.1..4` | 600,048 ×4 | 24px 中文 |
| `font24.en.1..2` | 6,144 ×2 | 24px 英文（可选，加载已验证，渲染未逐字验证） |

## 7. 尚未完成

- Godot 工程 / GDExtension 包装（P1）。
- 鼠标点击 C 界面：通用菜单、主地图、武将选择已有触摸路径，专用界面仍需补或走宿主 UI 协议。
- 存档路径目前是普通目录；Godot 里应指向 `user://`。
- 战斗流程尚未跑通（P0 只验证到地图）。
- `.lib` 内的 `ENGINE_SCRIPT`（JS）在原生后端被禁用；mod 支持待决策。
- 音频、动画性能、长时间运行稳定性未测。

## 8. 复现命令

```bash
bash tools/build_native.sh
bash tools/run_smoke.sh
python3 tools/ppm2png.py build/frames/final.ppm build/frames/final.png
```

可用版本：

```bash
BAYE_LIB=libs/balance1.8G1.lib bash tools/run_smoke.sh
```
