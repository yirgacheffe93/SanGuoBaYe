# 新架构：Godot 表现层 + 原生 iBaye 核心

本文描述 `godot` 分支的目标架构、已经验证的运行时模型，以及 UI 渐进迁移的边界。它取代 `docs/plan_goddot.md` 中「iBaye 提供现成规则 API」的假设。

## 1. 结论先行

- **技术上可行**：iBaye C 核心已经能在 arm64 macOS 原生运行，并驱动到游戏地图。
- **但不能把它当成无界面规则库**：游戏规则和 1bpp 界面、阻塞式按键循环写在一起。现代化 UI 的关键是增加一层「宿主 UI 协议」，逐个接管控件，而不是重写规则。
- **推荐策略**：先做「控件层接管」（策略 B），而不是「重写所有界面」（策略 C）。

## 2. 运行时模型

```text
┌──────────────────────────── Godot 主线程 ────────────────────────────┐
│  _process()                                                        │
│    ├─ 读取 engine 的帧快照 → ImageTexture → TextureRect              │
│    ├─ 处理鼠标/键盘 → 写入输入队列                                   │
│    └─ 消费 UI 事件队列（宿主 UI 协议）→ 显示 Godot 面板 → 回传结果    │
└───────────────▲──────────────────────────────┬─────────────────────┘
                │ 帧回调（线程安全）             │ 输入 / UI 响应
┌───────────────┴──────────────────────────────▼─────────────────────┐
│                        iBaye 引擎线程                                 │
│  GamBaYeEng()                                                        │
│    ├─ GamConInit / GamVarInit                                        │
│    ├─ 游戏主循环（地图、城市、战斗、剧情…）                            │
│    ├─ 每个界面：画 1bpp → GuiGetMsg() 阻塞等待消息                    │
│    └─ GuiGetMsg() 返回按键 / 触摸 / 定时器                            │
└──────────────────────────────────────────────────────────────────────┘
```

已验证的实现细节：

- **引擎线程**：`GamBaYeEng()` 永不返回，阻塞在 `GuiGetMsg()`。Godot 侧必须把它放到独立线程，否则主线程会被卡死。
- **消息队列**：`platform/common/gui.c` 用互斥量 + 信号量实现阻塞队列；按键通过 `GuiPushMsg()` 注入，触摸通过 `touchSendTouchEvent()` 注入。
- **定时器**：`platform/common/sys.c` 注册 timer 0（滚动/动画）、timer 1（延时/回合）、timer 2（LCD 刷新）。POSIX 后端用 5ms 心跳模拟，回调只负责往消息队列投递 `VM_TIMER`。
- **单实例**：引擎使用大量全局状态（`g_Cities`、`g_Persons`、`g_FightMap`…），不可重入；一个进程只能有一个引擎实例。

## 3. 帧路径：只在安全点取帧

这是 P0 发现的最重要问题。

原始 H5 是单线程 + `setTimeout` 刷新，不会在绘制中途取帧。原生引擎是独立线程，`timer2` 每 20ms 调一次 `timed_flush_lcd()`，可能在引擎画到一半时把缓冲区交出去，产生撕裂或半帧。

现在的做法：

1. 在 `GuiGetMsg()` 阻塞前调用一个宿主回调（补丁 `0001-gui-host-wait-hook.patch`）。
2. 回调里调用 `SysFlushLcdNow()`（补丁 `0002-sys-flush-now.patch`），强制刷新完整帧。
3. 宿主持有最近一帧的 32 位缓冲；Godot 主线程在 `_process()` 里上传为纹理。

这样每帧都是完整的，而且帧率自然跟随游戏逻辑（菜单/地图不需要 60fps 重绘）。

帧格式：

- 逻辑屏幕：SGBY 为 240×144，`g_scale = 2`。
- 输出缓冲：`240*2 × 144*2 = 480×288`，每像素 4 字节，调色板 `0xAARRGGBB`（小端字节序为 B,G,R,A）。
- 游戏实际内容区是左上角的 160×96（逻辑），在 240×144 画布中留白。这一点和现有 H5 版本一致。

Godot 侧建议：

- 用 `Image.create_from_data(480, 288, false, Image.FORMAT_RGBA8, ...)`，或者让 GDExtension 直接填 `Image`。
- `TextureRect` + `texture_filter = TEXTURE_FILTER_NEAREST`，整数倍缩放。
- 在引擎线程里**不要**碰 Godot API；帧拷贝到互斥保护的缓冲，主线程再消费。

## 4. 输入路径

- 键盘：`MsgType { type=VM_CHAR_FUN, param=VK_* }` → `GuiPushMsg()`。
- 触摸/鼠标：`touchSendTouchEvent(VT_TOUCH_DOWN/MOVE/UP, x, y)` → `VM_TOUCH`。
- 坐标是**逻辑坐标**（0..239, 0..143），不是 480×288 的物理像素；Godot 点击后要除以 `g_scale`。

已有的输入支持并不完整：

- 主地图、通用菜单 `PlcSplMenu`、武将选择、物品选择已经有触摸/点击路径。
- 大量专用界面仍然只处理 `VM_CHAR_FUN`（方向键/确认）。
- 因此「鼠标点按」需要逐屏补触摸分支，或者在宿主 UI 协议里直接返回选项索引，不再走坐标。

## 5. UI 桥接：接管控件，而不是重写规则

iBaye 的界面不是一层独立的 View，而是「画 1bpp → 阻塞取键 → 内嵌规则」的循环。直接重写每个界面等于重写游戏流程。

更可行的做法是在少数**通用控件**上开一个宿主 UI 协议：

| 控件 | 位置 | 覆盖范围 |
|---|---|---|
| `PlcSplMenu` | `PublicFun.c` | 大部分列表/菜单（约 14 处调用） |
| `ShowPersonControl` | `showface.c` | 选武将（约 36 处调用） |
| `ShowGoodsControl` | `showface.c` | 选物品 |
| `NumOperate` | `PublicFun.c` | 数字输入 |
| `GamMsgBox` / `GamChoose` / `GamSay` | `comOut.c` | 消息框、对话框 |
| 主地图 `GetCitySet` | `cityedit.c` | 点击城池、拖动地图 |
| 战斗循环 | `Fight.c` / `FightSub.c` | 移动、攻击、计谋、回合结束 |

每个控件增加一个可选的「宿主」回调：

```c
/* 伪代码 */
if (host_ui_attached()) {
    host_ui_request(id, items, selected, &result);   /* 阻塞，等 Godot 返回 */
    return result;
}
/* 否则走原来的 C 绘制 + 按键循环（回退路径） */
```

好处：

- 规则、流程、数值全部留在 C，不需要逐条命令重写。
- 未接管的界面继续用 C 画面，游戏始终可玩。
- 可以逐屏迁移、逐屏验收，随时回退。
- 与现有 64 个 hook（`mainSystemMenu`、`getPersonProperty*`、`willShowCityInfo`、`getCityPropertyDisplay`、`drawMapUnit`、`fightChooseAction`…）互补：能用 hook 的先用 hook，不能用 hook 的再在控件层加协议。

代价：

- 需要在 C 里增加一套「请求/响应」机制，并处理引擎线程与 Godot 主线程的同步。
- 每个界面都要在 Godot 里重画，工作量仍然不小；但不需要重写规则，风险显著低于策略 C。

## 6. 状态读取与写入

- **读**：`data-bind.c` / `bind-objects.c` 已经用 `Value` / `ObjectDef` 反射了 `g_Cities`、`g_Persons`、`g_Tools`、`g_Skills`、`g_FgtParam`、`g_GenPos` 等。GDExtension 可以把 `_bayeGetGlobal()` 暴露成字典/JSON，或者为常用字段写专门的 getter。
- **写**：只允许通过 C 的命令函数（如 `CityCommon(city, cmd)`、`OrderExec`、`AddOrder`），不要直接修改全局字段。直接写会绕过金钱、体力、距离、回合等校验。
- **事件**：hook 机制已经存在；宿主 UI 可以复用 `call_hook_*` 的同步调用模型，但要保证只在引擎线程调用。

## 7. 存档、资源与字体

- 游戏数据：`.lib` 包（`libs/SGBY.lib` 等），由 `GamSetResourcePath()` 指定。
- 字体：`assets/fonts/`，从上游 Emscripten 字体数组提取。`font.bin` 是默认 12px 字体；`font24.cn.*` 是 24px 中文字体；`font24.en.*` 是可选 24px 英文字体。
- 存档：`sango?.sav`，由 C 侧 `GamSaveRcd/GamLoadRcd` 读写；`GamSetDataDir()` 指向 `user://saves` 即可。复用 C 核心 = 存档格式兼容。
- 文本编码：`.lib` 内是 GBK；如果 Godot 要自己渲染文本，需要 GBK→UTF-8 转换和中文字体。若继续用 C 的 1bpp 文本，则不需要。
- Mod：`.lib` 可以携带 `ENGINE_SCRIPT`（JavaScript），原生构建目前用 `dummy-script.c` 等价物禁用脚本（`has_hook` 返回 0）。是否支持 mod 是产品决策；要支持需嵌入 QuickJS 之类的 JS 引擎。

## 8. 已发现的移植问题（P0）

1. **`has_hook` 必须返回 0**：上游 iOS 的 `dummy-script.c` 返回 1，导致 `GamMainChose` 认为 `chooseGameEntry` hook 存在，取到 `-1`，主菜单被跳过。原生后端必须返回 0。
2. **字体路径缓冲区只有 64 字节**：`comIn.c` 的 `U8 path[64]` 在项目绝对路径下溢出，直接崩溃。补丁改为 512。
3. **取帧时机**：见第 3 节；不能在定时器里直接取帧。
4. **屏幕尺寸要在初始化前设置**：SGBY 的 `.lib` 脚本调用 `baye.resizeScreen(240,144)`，原生没有脚本，必须在 `GamConInit()` 之前设置 `g_screenWidth/Height`。
5. **字体在 Emscripten 数组里**：原生需要先提取成文件（`tools/extract_fonts.py`）。

## 9. 分阶段路线

| 阶段 | 内容 | 验收 |
|---|---|---|
| P0（已完成） | 原生编译、线程/消息/定时器、安全取帧、按键到地图 | `tools/run_smoke.sh` 输出地图帧 |
| P1 | GDExtension 包装 `BayeEngine`；Godot 显示纹理；键鼠输入 | Godot 里能启动、显示、操作 |
| P2 | 控件层协议 + 第一个 Godot 面板（武将信息或主菜单） | Godot 面板驱动 C 流程 |
| P3 | 逐屏接管：城市/内政、武将/物品、事件/剧情；战斗单列 | 每个界面可回退、可对照 |
| P4 | macOS 打包、签名、公证；性能与内存 | 可独立运行的 `.app` |
| P5 | 跨平台、Steam（授权通过后） | Windows/Deck 构建 |

UI 策略三选一（建议先 A 验证链路，再 B 迭代）：

- **A. 像素重制**：保留 240×144，整数倍放大 + CRT shader + 现代外壳。1–3 个月可玩 v1，风险低。
- **B. 渐进接管（推荐）**：控件层协议，逐屏替换。6–12 个月做到大部分界面现代化。
- **C. 全面重写**：参数化命令 API + 全部界面 + 战斗重做。10–18 个月量级，风险最高。

## 10. 工程约定

- 上游源码保持原样，本地修改一律放 `native/patches/`；构建时复制到 `build/src` 再打补丁。
- `build/` 不入库；`assets/fonts/` 入库（游戏运行必需）。
- 不在引擎线程调用任何 Godot API。
- 所有对游戏状态的写操作必须经过 C 侧函数。
- 每加一个界面/补丁，跑一次 `tools/run_smoke.sh` 做回归对照。
