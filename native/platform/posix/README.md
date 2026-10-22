# POSIX（macOS）平台后端

这是 iBaye 在 macOS 上运行所需的最小平台层，替代上游的 `platform/js`（Emscripten）、`platform/win`（Win32）和 `platform/ios`（UIKit）。

| 文件 | 作用 | 上游参考 |
|---|---|---|
| `fsys.c` | `gam_fopen/fread/fwrite/...` 的 stdio 实现 | `platform/ios/fsys.c` |
| `sem.c` | 用 `pthread_mutex` 模拟信号量/锁 | `platform/win/sem.c` |
| `timer.c` | 3 个定时器（滚动、延时、LCD 刷新），5ms 心跳 | `platform/js/timer.c` 语义 |
| `sys.c` | `SysGetSecond()` 等少量系统函数 | `platform/ios/sys-ios.c` |
| `script.c` | 禁用 JS hook：`has_hook()` 返回 0，`call_hook_*()` 返回 -1 | `platform/ios/dummy-script.c`（已修正） |
| `smoke.c` | 无头冒烟程序：启动引擎、注入按键、安全取帧 | 新增 |

## 关键约定

- **`has_hook()` 必须返回 0。** 上游 iOS 的 dummy 返回 1，会让 `GamMainChose()` 误以为 `chooseGameEntry` 存在，拿到 -1 后跳过主菜单。
- **定时器回调只投递消息**，不要在定时器线程里直接绘制或取帧。`SysMemInit()` 会注册 `_timer0cb`、`_timer1cb` 和 `timed_flush_lcd`。
- **取帧只在 `GuiGetMsg()` 阻塞前**（补丁 0001 + 0002）。定时器刷新可能落在绘制中途。
- **屏幕尺寸在 `GamConInit()` 之前设置**，否则 `WK_*` 和缓冲布局会按默认 208×128 初始化。
- `smoke.c` 里的 `g_screenWidth = 240; g_screenHeight = 144;` 对应 SGBY 的 `.lib` 脚本；其他版本可能不同，后续应改为从 `.lib` 或配置读取。

## 与 Godot 的关系

`smoke.c` 是 GDExtension 的参考实现：

- `GamConInit()` 后启动引擎线程；
- `GuiPushMsg()` 注入输入；
- `GuiSetMsgWaitCallback()` + `SysFlushLcdNow()` 提交完整帧；
- 主线程持有最近一帧并消费。

GDExtension 里不要直接在引擎线程调用 Godot API；把帧拷贝到互斥缓冲，让主线程在 `_process()` 上传纹理。
