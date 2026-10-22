# Project Three Kingdoms（代号 `rewrite`）

一个从零开始的单机回合制三国策略游戏。灵感来自经典的三国策略玩法，但**代码、数据、美术、文本、音乐、名称全部原创**。

> 正式名称待定；在完成商标检索前，不沿用《三国霸业》的名字、Logo 或既有素材。

## 状态

**预制作 / 架构设计阶段。** 本分支目前只有设计文档，没有游戏代码。技术栈待定（见 `docs/04-tech-selection.md`）。

## 三个分支的关系

| 分支 | 内容 | 角色 |
|---|---|---|
| `main` | 旧的 H5/Electron 版本 | 历史版本，不再开发 |
| `godot` | iBaye C 核心的原生 P0 验证 | **行为参考**，不进入新工程 |
| `rewrite` | 本分支，干净历史，只有新设计 | 新游戏从零开始 |

`rewrite` 是 orphan 分支：没有旧代码、没有旧历史。需要看旧实现时：

```bash
git show godot:README.md
git show godot:docs/p0-report.md
git worktree add ../SanGuoBaYe-godot godot
```

## 已确定的决策

1. **放弃 iBaye 作为生产代码**：不看它的实现来写新代码，不做逐行移植。它只用于对照高层玩法行为。
2. **精神续作，而不是复刻**：三国历史属于公共领域，但原游戏的代码、数据、地图、美术、文本、音乐、名称都不能复用。
3. **架构优先**：先把领域模型、回合流程、数据格式、测试方案设计清楚，再选技术栈、再写代码。
4. **不着急**：按阶段交付，每个阶段都有可验收的产物，不搞一次性大爆炸。

## 文档

| 文档 | 内容 |
|---|---|
| [docs/01-charter.md](docs/01-charter.md) | 愿景、范围、原则、成功标准 |
| [docs/02-architecture.md](docs/02-architecture.md) | 领域模型、回合流程、战斗、AI、存档、测试 |
| [docs/03-content.md](docs/03-content.md) | 精神续作的内容策略、clean-room 规则、MVP 内容量 |
| [docs/04-tech-selection.md](docs/04-tech-selection.md) | 技术栈候选、评估矩阵、推荐与 spike 计划 |
| [docs/05-roadmap.md](docs/05-roadmap.md) | R0–R5 阶段、门禁、风险 |
| [docs/06-decisions.md](docs/06-decisions.md) | 架构决策记录（ADR） |

## 下一步

R0 还没结束。在写第一行游戏代码之前，需要完成：

1. 技术栈 spike（Godot 4 + GDScript vs C#）。
2. MVP 内容清单（哪些城市、人物、事件、战斗）。
3. 数据 schema 草案（角色/城市/剧本/事件）。
4. clean-room 来源记录模板。
5. 一个可玩的纸面原型（用表格/脚本模拟回合，不写引擎）。

## 注意

本仓库是研究与开发用途。新游戏不包含原版素材；但“三国”题材、人物、历史事件属于公共领域，具体表达必须原创。正式发行前仍需做商标与法律审查。
