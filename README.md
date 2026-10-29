# Project Three Kingdoms（代号 `rewrite`）

使用 Godot 4.7.2 + C#/.NET 8，按照 `main` 分支的 `SGBY.lib` 内容与功能重写单机三国策略游戏。当前优先技术落地，商标及其他合规工作按项目决定后置，见 ADR-0009。

## 状态

**Main-M3-D 已完成。**

Main-M3-A/B/C 已打通战略与战术主流程并建立生产 golden；Main-M3-D 进一步接入真实地形消耗、兵种/技能射程和地形修正，补齐移动后行动、友军/自身技能目标、待机恢复及搜寻在职状态。132 项测试、生产 golden、双主题及独立 macOS 应用验收已完成，见 `docs/30-main-m3-d.md`。

现有 Godot 分层工程、双主题地图、规则测试、快照存档和 macOS 导出链继续复用。R0 的虚构内容与自动战斗仅保留为历史技术验证，不再限制 main 的属性范围和战术战斗目标。

## 三个分支的关系

| 分支 | 内容 | 当前角色 |
|---|---|---|
| `main` | H5/Electron 版本与 `SGBY.lib` | 内容导入和 golden 行为基准 |
| `godot` | iBaye C 核心的原生 P0 验证 | 历史研究参考，不进入生产工程 |
| `rewrite` | Godot + C# 重写 | 当前生产开发分支 |

main 的 bridge/WASM 只用于只读导入和对照，不作为 Godot 生产运行时。Electron、联网页面、调试页和旧存档格式不按原样移植。

## 已确定的决策

1. **按 main/SGBY 重写**：首发只对齐原版精修，不同时支持 MOD。
2. **保留核心体验**：四时期、战略命令队列、月度 AI 和独立战术战斗进入路线图。
3. **原生分层**：Domain/Application 为纯 .NET，Godot 负责 UI；生产内容来自可重复导入的 JSON。
4. **垂直切片**：先完成 190 年刘备内政切片，再扩完整战略和战术战斗。
5. **技术优先**：商标及其他合规事项后置，不阻塞当前开发。

## 文档

| 文档 | 内容 |
|---|---|
| [docs/02-architecture.md](docs/02-architecture.md) | 分层、领域模型、存档与测试 |
| [docs/05-roadmap.md](docs/05-roadmap.md) | Main M1–M4 重写路线与阶段门禁 |
| [docs/06-decisions.md](docs/06-decisions.md) | 架构决策记录（ADR） |
| [docs/07-turn-rules.md](docs/07-turn-rules.md) | 历史 R0 回合合同 |
| [docs/08-ui-design.md](docs/08-ui-design.md) | 地图、下令反馈与 UI 验收 |
| [docs/09-r0-s1-spike.md](docs/09-r0-s1-spike.md) | Godot/.NET 工具链实测 |
| [docs/10-r0-ui-samples.md](docs/10-r0-ui-samples.md) | 双主题低保真样片 |
| [docs/11-r0-prototype.md](docs/11-r0-prototype.md) | 历史 R0 参数与模拟依据 |
| [docs/12-r0-five-turns.md](docs/12-r0-five-turns.md) | 历史 R0 五回合模拟 |
| [docs/13-main-m1.md](docs/13-main-m1.md) | Main-M1 内容导入与首个切片 |
| [docs/14-main-m2a.md](docs/14-main-m2a.md) | Main-M2A 四时期、五项内政 golden 与验收 |
| [docs/15-main-architecture-refactor.md](docs/15-main-architecture-refactor.md) | Main-R1 内容/状态、规则流程与存档结构 |
| [docs/16-main-r2-structure.md](docs/16-main-r2-structure.md) | Main-R2 加载层与战略 UI 组件 |
| [docs/17-main-m2b.md](docs/17-main-m2b.md) | Main-M2B 完整月度、搜寻与 SetGoods 修复 |
| [docs/20-setgoods-diagnosis-result.md](docs/20-setgoods-diagnosis-result.md) | U8/U16 诊断证据与修复验收 |
| [docs/21-main-m2c-a.md](docs/21-main-m2c-a.md) | Main-M2C-A 人员与物资六命令 |
| [docs/22-search-diagnostics.md](docs/22-search-diagnostics.md) | 搜寻候选、伯乐与失败原因诊断 |
| [docs/23-main-m2c-b.md](docs/23-main-m2c-b.md) | Main-M2C-B 军备与人员处置 |
| [docs/24-main-m2c-c.md](docs/24-main-m2c-c.md) | Main-M2C-C 寻路、特殊装备与年度出现 |
| [docs/25-main-m2d-a.md](docs/25-main-m2d-a.md) | Main-M2D-A 对敌策略与侦察 |
| [docs/26-main-m2d-b.md](docs/26-main-m2d-b.md) | Main-M2D-B 非战斗 AI 与势力闭环 |
| [docs/27-main-m3-a.md](docs/27-main-m3-a.md) | Main-M3-A 出征与首个战术切片 |
| [docs/28-main-m3-b.md](docs/28-main-m3-b.md) | Main-M3-B 非玩家战斗、守城与技能状态 |
| [docs/29-main-m3-c.md](docs/29-main-m3-c.md) | Main-M3-C 生产战斗 golden 与 MG3 验收 |
| [docs/30-main-m3-d.md](docs/30-main-m3-d.md) | Main-M3-D 战斗操作链与规则数据补齐 |
| [docs/provenance.md](docs/provenance.md) | 实际内容与资产来源记录 |

## 构建与验证

本地工具链位于 `.tools/`（不入库）。macOS 上执行：

```bash
scripts/verify.sh        # 导入校验、132 项测试、R0 回归、Godot 冒烟
scripts/export-macos.sh # 导出并运行独立 macOS 应用冒烟
scripts/capture-ui.sh   # 构建并重新生成深色、浅色和对比样片
```

Godot 编辑器打开根目录 `project.godot`。main 内容导入命令见 `tools/main-import/`；R0 模拟工具仅作历史回归。

## 下一步

优先 UI 与交互打磨，跨平台适配暂缓；完整战术 AI、更多技能的自然概率及原生赏赐 UI golden 可作为精修补充。

## 注意

本仓库包含 main 分支导入的内容，具体来源见 `docs/provenance.md`。当前工作以技术实现与可玩验收为准，后续发行前再单独处理合规检查。
