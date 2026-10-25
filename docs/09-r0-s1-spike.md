# 09 · R0-S1 技术栈 Spike 结果

> 状态：已完成，结论见 ADR-0008。本工程是技术验证，不是 R1 可玩原型，也不代表 G0 已通过。

## 环境与版本

- macOS 27.0.1，arm64。
- Godot `4.7.2.stable.mono.official.ed1daf0bf`，使用官方 .NET macOS universal 构建。
- .NET SDK `8.0.426`，目标框架 `net8.0`。
- Godot .NET 导出模板 `4.7.2-stable`；导出为 universal 应用，实际包含 arm64 与 x86_64。
- 工具来自 [Godot 官方发布](https://github.com/godotengine/godot/releases/tag/4.7.2-stable) 与 [.NET 官方安装脚本](https://dot.net/v1/dotnet-install.sh)。本地工具位于忽略目录 `.tools/`，导出模板使用 Godot 的 macOS 用户模板目录。

## 实测范围

工程分为纯 .NET 的 Domain/Application、JSON/文件 Infrastructure、Godot UI 和独立测试工程。真实 JSON 内容包含 2 城、1 势力、2 武将。

已跑通：

1. 加载内容并校验必填字段、重复 ID、悬空引用与非法数值。
2. 开发农业/商业、原子扣费、武将占用、一回合任务和下一回合增收。
3. 资源不足拒绝、本回合取消与全额退款。
4. 固定算法 RNG、固定种子战斗公式和逐轮结果。
5. 玩家阶段快照存读档、版本与校验和检查、RNG/任务/序号恢复。
6. Godot 原生控件 UI：城池、详情、武将、开发、任务、报告、存读档。
7. 编辑器与独立 macOS 导出应用中的同一套 UI 冒烟流程。

## 结果数据

| 项目 | 结果 |
|---|---|
| 非测试 C# 代码 | 630 行 |
| 测试代码 | 212 行 |
| 自动化测试 | 16/16 通过 |
| 测试执行时间 | 约 30 ms（不含构建） |
| 解决方案构建 | 约 1.9 s（本机热缓存） |
| 完整 `scripts/verify.sh` | 约 4.4 s |
| macOS 导出及独立应用冒烟 | 约 8.3 s（本机热缓存） |
| 导出应用大小 | 约 320 MB |
| 导出冒烟状态哈希 | `a6ecce6eb2111b27474cbe7943c23a365f6c5991da905f0247dd5bc493a1c7df` |

UI 使用 C# 原生容器和控件，城市/任务刷新可直接绑定应用层查询，未需要双语言桥接。当前程序式布局适合验证，但后续复杂地图和视觉样片需要改用场景化布局并评估设计器迭代；本次没有验证正式美术、复杂 UI 或 Windows 导出。

## 验证命令

```bash
scripts/verify.sh
scripts/export-macos.sh
```

`verify.sh` 执行完整解决方案构建、无 Godot 引用的规则测试和 Godot UI 冒烟。导出脚本还会运行独立应用，验证资源不足拒绝、取消、开发、结束回合、存档、读档继续和状态一致性。成功标志为 `SPIKE_SMOKE_OK`。

## 已解决的问题

- macOS arm64/universal 导出要求启用 ETC2/ASTC 纹理导入，已在项目设置中明确启用。
- 官方模板提供 universal macOS 二进制，因此预设使用 universal，并确认产物包含 arm64。
- JSON 内容通过 Godot `FileAccess` 从 PCK 读取，不依赖编辑器文件系统路径。
- 根目录 Godot 项目显式限定 C# 编译范围，避免把其他项目及测试源文件重复编译。
- 导出排除规则源码、测试、工具与构建目录；规则程序集由 .NET 发布流程提供。

## 结论与限制

C# 方案满足本次全部硬约束，无需为了比较而重复实现 GDScript 版本。选定 Godot 4.7.2 + C#/.NET 8，具体理由与后果见 ADR-0008。

本 Spike 尚无 AI、出征、外交、正式战斗流程、完整数据 schema、胜负结局和外部试玩；R0 的其他交付仍需完成。
