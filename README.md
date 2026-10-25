# Project Three Kingdoms（代号 `rewrite`）

一个从零开始的单机回合制三国策略游戏。灵感来自经典的三国策略玩法，但**代码、数据、美术、文本、音乐、名称全部原创**。

> 正式名称待定；在完成商标检索前，不沿用《三国霸业》的名字、Logo 或既有素材。

## 状态

**预制作 R0，技术栈 Spike 与低保真双风格界面已完成。** 当前有可点击战略地图、城池/道路/旗号、深色墨色与浅色纸本切换，以及真实开发/取消/回合/存读档流程和 16 项自动化测试；尚不是 R1 可玩原型，G0 仍未通过。技术实测见 `docs/09-r0-s1-spike.md`，界面样片见 `docs/10-r0-ui-samples.md`。

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
| [docs/09-r0-s1-spike.md](docs/09-r0-s1-spike.md) | R0-S1 工具版本、实测数据、验证结果与限制 |
| [docs/10-r0-ui-samples.md](docs/10-r0-ui-samples.md) | R0-S2 低保真地图、深色/浅色样片与可读性检查 |
| [docs/provenance.md](docs/provenance.md) | 内容与资产来源记录 |

## 构建与验证

本地工具链位于 `.tools/`（不入库），版本见 Spike 记录。macOS 上执行：

```bash
scripts/verify.sh        # 构建、16 项测试、Godot UI 冒烟
scripts/export-macos.sh # 导出并运行独立 macOS 应用冒烟
scripts/capture-ui.sh   # 重新生成深色、浅色和对比样片
```

Godot 编辑器打开根目录 `project.godot`；正常运行可点击地图或城池列表，切换主题，并试用开发、取消、回合和存读档界面。

## 下一步

R0 还没结束。正式 R1 前需要完成：

1. 原型具体内容清单、初始局面、费用收益参数和自动战斗公式。
2. 数据 schema 草案（角色/城市/剧本/事件），校验原型内容。
3. 纸面模拟至少 5 回合，验证扣费、取消、战斗与结局。
4. 名称/商标初查，以及正式字体与后续外部素材授权记录。

## 注意

本仓库是研究与开发用途。新游戏不包含原版素材；但“三国”题材、人物、历史事件属于公共领域，具体表达必须原创。正式发行前仍需做商标与法律审查。
