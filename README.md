# Project Three Kingdoms（代号 `rewrite`）

一个从零开始的单机回合制三国策略游戏。灵感来自经典的三国策略玩法，但**代码、数据、美术、文本、音乐、名称全部原创**。

> 正式名称待定；在完成商标检索前，不沿用《三国霸业》的名字、Logo 或既有素材。

## 状态

**预制作 / 架构设计阶段。** 本分支目前只有设计文档，没有游戏代码或新 UI 实现。技术栈待定，优先 spike 验证 Godot + C# 与独立 .NET 规则库（见 `docs/04-tech-selection.md`）。

先交付 3–5 城、2 势力、8–12 武将、15–30 分钟的小原型（R1），包含真实可操作的最小 UI；试玩通过后再扩为 20 城/60 武将、2–3 小时的 MVP（R2）。

## 三个分支的关系

| 分支 | 内容 | 角色 |
|---|---|---|
| `main` | 旧的 H5/Electron 版本 | 历史版本，不再开发 |
| `godot` | iBaye C 核心的原生 P0 验证 | **行为参考**，不进入新工程 |
| `rewrite` | 本分支，干净历史，只有新设计 | 新游戏从零开始 |

`rewrite` 是 orphan 分支：没有旧代码、没有旧历史。旧的 iBaye UI 计划已归档到 `godot` 分支 `docs/reference/`，仅作历史参考，不属于本工程。需要看旧实现时：

```bash
git show godot:README.md
git show godot:docs/p0-report.md
git worktree add ../SanGuoBaYe-godot godot
```

## 已确定的决策

1. **放弃 iBaye 作为生产代码**：不看它的实现来写新代码，不做逐行移植。它只用于对照高层玩法行为。
2. **精神续作，而不是复刻**：三国历史属于公共领域，但原游戏的代码、数据、地图、美术、文本、音乐、名称都不能复用。
3. **规则先明确，最小 UI 同步**：先写清扣费、行动占用、冲突和胜负规则；用 spike 选栈，R1 即交付可操作界面，尽早试玩。
4. **美术方向**：R1/R2 采用 `08-ui-design.md` 的简洁国风战略桌面；写实半身立绘后置（ADR-0007）。
5. **不着急**：按阶段交付，每个阶段都有可验收的产物，不搞一次性大爆炸。

## 文档

| 文档 | 内容 |
|---|---|
| [docs/01-charter.md](docs/01-charter.md) | 愿景、范围、原则、成功标准 |
| [docs/02-architecture.md](docs/02-architecture.md) | 领域模型、回合流程、战斗、AI、存档、测试 |
| [docs/03-content.md](docs/03-content.md) | 精神续作的内容策略、clean-room 规则、MVP 内容量 |
| [docs/04-tech-selection.md](docs/04-tech-selection.md) | 技术栈候选、评估矩阵、推荐与 spike 计划 |
| [docs/05-roadmap.md](docs/05-roadmap.md) | R0–R5 阶段、门禁、风险 |
| [docs/06-decisions.md](docs/06-decisions.md) | 架构决策记录（ADR） |
| [docs/07-turn-rules.md](docs/07-turn-rules.md) | 原型回合合同、资源/占用/退款、冲突、胜负与验收 |
| [docs/08-ui-design.md](docs/08-ui-design.md) | 地图布局、下令反馈、任务/回合报告、视觉建议与 UI 验收 |
| [docs/provenance.md](docs/provenance.md) | 内容与资产来源记录模板 |

## 下一步

R0 还没结束。正式工程开始前需要完成（允许有限 spike 和线框验证）：

1. 优先 C# 技术栈 spike，记录具体工具版本、纯 .NET 测试、macOS 导出与 UI 迭代结果；必要时才对照 GDScript。
2. 原型具体内容清单、初始局面、费用收益参数和自动战斗公式。
3. 数据 schema 草案（角色/城市/剧本/事件），校验原型内容。
4. 纸面模拟至少 5 回合，验证扣费、取消、战斗与结局。
5. UI 低保真流程和深色/浅色两份风格样片，比较可读性；来源模板已建，实际素材授权逐项补齐。

## 注意

本仓库是研究与开发用途。新游戏不包含原版素材；但“三国”题材、人物、历史事件属于公共领域，具体表达必须原创。正式发行前仍需做商标与法律审查。
