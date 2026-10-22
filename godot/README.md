# Godot 工程（下一步）

这个目录先放占位说明。P0 阶段验证的是原生引擎和线程/取帧/输入模型；Godot 工程会在 P1 创建。

## P1 目标

1. 创建 Godot 4.x 工程（`project.godot`、主场景）。
2. 写 `native/godot_extension/`：用 GDExtension 把 `baye_smoke` 里的逻辑封装成 `BayeEngine` 节点：
   - `start(lib_path, fonts_path, save_dir)`
   - `send_key(code)` / `send_touch(event, x, y)`
   - `get_frame() -> Image` 或信号 `frame_ready`
   - `set_ui_host(callable)`（宿主 UI 协议，P2）
3. 主场景：`TextureRect` 显示 480×288 帧，`TEXTURE_FILTER_NEAREST`，整数倍缩放。
4. 输入：键盘 → `GuiPushMsg`；鼠标 → `touchSendTouchEvent`（逻辑坐标 = 物理坐标 / `g_scale`）。
5. 线程：引擎线程只跑 C；Godot 主线程只碰 Godot API；帧和 UI 请求通过互斥队列传递。

## 注意

- 不要用 Godot 的 `Timer` 驱动游戏时钟；游戏时钟是 C 侧的 timer0/1/2，必须由 POSIX 后端持续投递 `VM_TIMER`。
- 不要在引擎线程里调用 `Node`、`ImageTexture`、`Input` 等 Godot API。
- 一个进程只能有一个引擎实例（全局状态不可重入）。
- 分辨率：SGBY 为 480×288；不同 `.lib` 可能不同，启动后读取 `g_screenWidth/Height/scale`。

## 参考

- `native/platform/posix/smoke.c`：已经验证过的启动/按键/取帧顺序。
- `../docs/architecture.md`：线程模型、UI 桥接、分阶段路线。
