# macOS 桌面版（第一阶段）

把 `baye-alpha` 的网页版改造成可在 macOS 上双击运行、完全离线的单机应用。

![桌面版主界面](images/desktop-title.png)

*主界面：外壳为 Electron 重写，画面仍是引擎绘制的 480×288 单色点阵。*

## 现在能做什么

- 双击启动，离线运行（不依赖浏览器、不访问任何网络）。
- 版本选择、整数倍锐利缩放、全屏、扫描线开关。
- 键盘 + 鼠标（虚拟按键）操作。
- 存档读写改为真实文件，可直接备份、替换、将来接入 Steam Cloud。
- 打包成 `.app` / `.dmg`（ad-hoc 签名，本机可运行）。

## 环境要求

- macOS 12+（Apple Silicon；Intel 需要额外指定 `--x64`，见下文）
- Node.js 20+

## 安装与运行

```bash
npm install
# 首次需要下载 Electron 运行时（一次性）
ELECTRON_CACHE="$PWD/.electron-cache" node node_modules/electron/install.js

npm start
```

如果 Electron 已经下载过，`npm start` 会直接启动。

## 打包

```bash
npm run pack   # 只生成 .app，适合本机试跑
npm run dist   # 生成 .app + .dmg + .zip
```

产物在 `dist/` 下。脚本会做 ad-hoc 签名（`codesign --force --deep --sign -`），
所以本机能直接打开；**分发到别的机器会被 Gatekeeper 拦截**，需要 Apple Developer
证书 + 公证：

```bash
export CSC_LINK=/path/to/developer-id.p12
export CSC_KEY_PASSWORD=...
export APPLE_ID=...
export APPLE_APP_SPECIFIC_PASSWORD=...
export APPLE_TEAM_ID=...
# 然后把 package.json 里 mac.identity 改成 null 之外的值，或删掉该字段
npx electron-builder --mac --arm64
```

Intel 版本：需要下载 x64 的 Electron，并把 `--arm64` 换成 `--x64`（或同时出通用包）。

## 存档位置

```
~/Library/Application Support/SanGuoBaYe/saves/
```

- 每个存档位是一个文本文件（`sango0.sav` 等），内容是引擎自己的十六进制文本格式。
- `dat.lib` 只读，不会被写回。
- 菜单「游戏 → 打开存档文件夹」可以直接打开该目录。
- 备份时整目录复制即可；换机器时把目录拷回去即可继续。

## 快捷键

| 按键 | 作用 |
|---|---|
| ↑ ↓ ← → | 移动 |
| Enter | 确认 |
| 空格 / Esc | 返回 |
| H | 帮助 |
| F / S | 查找 |
| ⌘O | 选择版本 |
| ⌘, | 设置 |
| Ctrl+⌘F | 全屏 |
| 双击画面 | 全屏 |

## 与网页版的差异

| 项目 | 网页版 | 桌面版 |
|---|---|---|
| 启动方式 | 浏览器打开 | 双击 App |
| 资源加载 | HTTP | 自定义协议 `app://`（`file://` 下 wasm 无法加载） |
| 存档 | localStorage | `saves/` 目录下的文件 |
| 版本选择 | `choose.html`（已失效） | 内置启动器，读取 `libs.json` |
| 联网 | 云存档、百度统计、CDN | 全部移除 |
| 窗口 | 浏览器标签页 | 原生窗口 / 全屏 |

## 已知限制

1. **游戏内画面仍是引擎绘制的 480×288 单色点阵**，桌面版只优化了外壳、缩放与操作方式；
   第二阶段的界面改造需要引擎源码，另作评估。
2. `.lib` 里的脚本由引擎用 `eval` 执行，因此 CSP 必须允许 `unsafe-eval`。
   安装包只加载本地 `libs/`，但**不要加载来源不明的 .lib**。
3. 未公证。正式发行前需要完成签名、公证，以及第一节提到的授权确认。

## 存档验证（建议手动过一次）

自动化测试只能验证「存档文件读写链路」，无法替你走完游戏内的存档菜单
（菜单文字是点阵字形，不是可搜索的文本）。请手动确认一次：

1. 新建一局，进入地图后按 Enter 打开菜单；
2. 找到「存档」（不同版本位置可能不同，常见于内政/系统菜单）；
3. 存到第 1 个位置后，检查
   `~/Library/Application Support/SanGuoBaYe/saves/` 下是否出现 `sango0.sav`、`sango1.sav`；
4. 退回标题，选择「恢复存档」，确认能读回刚才的进度。

如果第 3 步没有生成文件，说明该版本的存档入口不在预期位置，请把菜单截图发我，
我再补映射（存档回调本身已经验证可用）。

## 自检

```bash
npm run smoke
```

会以隐藏窗口启动，检查引擎是否出画面、`app://` 协议是否正常、存档读写是否成功，
输出形如：

```
SMOKE_STATUS {"ready":true,"frames":..., "canvas":{"w":480,"h":288},"storage":{"ok":true,...}}
```

可用的环境变量：

| 变量 | 作用 |
|---|---|
| `BAYE_SMOKE=1` | 打开自检模式 |
| `BAYE_SMOKE_VISIBLE=1` | 显示窗口（否则后台运行，帧数会很少） |
| `BAYE_SMOKE_SHOT=/tmp/shot.png` | 结束后截图 |
| `BAYE_SMOKE_KEYS=Enter,Enter,Down` | 注入按键，验证输入链路 |
| `BAYE_SMOKE_TRACE_DIR=/tmp/trace` | 每个按键后各截一张图 |
| `BAYE_USER_DATA=/tmp/xxx` | 指定用户数据目录（便携模式/测试用） |

## 常见问题

**双击 .app 没反应 / 提示已损坏**
本地打包是 ad-hoc 签名，本机可运行；从网络下载到别的机器会被 Gatekeeper 拦。
自己构建后若仍打不开，可执行 `xattr -dr com.apple.quarantine "三国霸业.app"`。

**`npm start` 只打印一行版本号就退出**
环境里存在 `ELECTRON_RUN_AS_NODE=1` 时，Electron 会退化成 Node。
用 `env -u ELECTRON_RUN_AS_NODE npm start` 启动。

**在受限环境（容器/沙箱）里启动报 sandbox 错误**
Chromium 的沙箱需要系统权限；这种环境里可临时加 `--no-sandbox --disable-gpu`，
正常桌面环境不需要。
