# 三国霸业 · Godot 重写

`godot` 分支从零开始：这里只保留新工程，不包含旧的 H5/Electron 代码。

| 分支 | 内容 | 用途 |
|---|---|---|
| `godot` | 原生 iBaye C 核心 + Godot 表现层（本分支） | 新架构，P0 已验证 |
| `main` | 旧的 H5 引擎 + Electron 桌面外壳 | 参考实现 / 当前可运行版本 |

旧代码需要参考时，不用切分支，直接读 `main` 的对象即可：

```bash
git show main:js/lcd.js
git show main:docs/desktop-macos.md

# 或者把旧版本检出到相邻目录，方便对照
git worktree add ../SanGuoBaYe-main main
```

## 当前状态（P0 技术验证通过）

2026-10-22 在 macOS 27 arm64 上验证：

- iBaye C 核心可以原生编译并运行，不再依赖 WebAssembly。
- 引擎跑在独立线程，通过消息队列接收按键；POSIX 定时器驱动游戏时钟。
- 从 `libs/SGBY.lib` 启动，按键进入「新君登基 → 时期 → 君主 → 地图」。
- 在引擎等待输入的安全点抓取完整帧，输出 480×288 画面（逻辑 240×144，游戏区 160×96）。

![原生引擎跑通到游戏地图](docs/images/native-map.png)

## 快速开始

前置：macOS、Xcode 命令行工具（`clang`）、Python 3。当前阶段不需要安装 Godot。

```bash
# 1. 编译原生引擎 + 无头冒烟程序（产物在 build/）
bash tools/build_native.sh

# 2. 启动冒烟测试，帧输出到 build/frames/
bash tools/run_smoke.sh

# 3. 把最后一帧转成 PNG
python3 tools/ppm2png.py build/frames/final.ppm build/frames/final.png
```

`run_smoke.sh` 默认使用 `libs/SGBY.lib`；可以换版本：

```bash
BAYE_LIB=libs/balance1.8G1.lib bash tools/run_smoke.sh
```

字体已从上游 Emscripten 字体数组提取，放在 `assets/fonts/`，不需要额外准备。

## 目录

```text
.
├── README.md
├── NOTICE.md               # 授权说明（务必先读）
├── assets/
│   ├── branding/           # 应用图标（从旧版本保留）
│   └── fonts/              # 从上游 Emscripten 数组提取的点阵字体
├── docs/
│   ├── architecture.md     # 线程模型、取帧、UI 桥接、分阶段路线
│   ├── p0-report.md        # P0 验证记录与踩坑
│   ├── plan_goddot.md      # 最初的规划（部分假设已被 P0 修正）
│   └── images/
├── godot/                  # Godot 工程（P1）
├── libs/                   # 游戏数据包（.lib，保留在根目录）
├── libs.json               # 版本列表
├── native/
│   ├── ibaye_core/         # 固定版本的 iBaye C 源码（MIT）
│   ├── patches/            # 对上游源码的最小补丁
│   └── platform/posix/     # macOS/POSIX 平台后端 + 冒烟程序
├── tools/
│   ├── build_native.sh
│   ├── run_smoke.sh
│   ├── extract_fonts.py
│   └── ppm2png.py
└── build/                  # 生成物（已 gitignore）
```

## 重要约束

- **授权**：iBaye 是 MIT，但源码里保留了 2002 年步步高的版权头；`libs/*.lib`、字体、美术、音乐的权利状态尚未确认。上游 `baye-alpha` 的 README 明确写了「仅供学习和交流使用，请勿用于商业用途」。在取得授权前，不要公开发行或商业上架。详见 [NOTICE.md](NOTICE.md)。
- **不要直接写游戏全局变量**：`g_Cities`、`g_Persons` 等可以读（反射层/调试），但写操作必须走 C 侧的命令函数，否则会绕过回合、资源、距离校验。
- **上游源码保持原样**：本地修改一律放 `native/patches/`，构建时复制到 `build/src` 再打补丁。
- **不要在引擎线程调用 Godot API**：帧和 UI 请求通过互斥队列交给主线程。

## 下一步

1. 写 GDExtension 包装层：把 `baye_smoke` 里的线程/消息/帧回调封装成 `BayeEngine` 节点。
2. 在 Godot 里显示 480×288 帧，并做整数倍缩放。
3. 把键鼠输入映射到 `GuiPushMsg` / `VM_TOUCH`。
4. 选一个界面做「控件层接管」（建议先做武将信息面板或主菜单），验证 Godot UI 驱动 C 流程。
5. 按 [docs/architecture.md](docs/architecture.md) 的 A/B/C 策略选择第一版范围。
