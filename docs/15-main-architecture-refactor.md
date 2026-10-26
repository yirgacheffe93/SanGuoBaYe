# 15 · Main 内容/状态与规则流程重构

> 状态：已完成。此次为行为不变的结构重构，没有新增玩法。M2A golden、四时期、五项内政和独立 macOS 流程均保持可运行。

## 1. 重构目标

解决两个主要问题：

- `MainWorld` 同时保存可变局面和静态道具/技能，导致存档重复内容定义。
- `MainGameSession` 同时负责命令、公式、AI、队列、月度和 ViewModel，后续系统难以独立扩展。

同时把 UI 刷新改为每次只创建一份 `MainGameView`。

## 2. 当前内容、状态与公式

| 类型 | 当前位置 | 职责 |
|---|---|---|
| 静态内容 | `MainContent`、`content/main/` | Tools、Skills、Periods、版本和 ContentHash |
| 可变状态 | `MainWorld` | 年月、玩家、城市、人物、命令、报告、RNG 和内容 hash |
| 命令目录 | `MainCommandCatalog` | 五项 ID、名称、成本与常量 |
| 即时公式 | `MainInternalRules` | 开垦/招商/搜寻接受/治理/出巡 |
| 随机算法 | `MainRng` | main 的 uint32 状态更新与 temper |
| 随机状态管理 | `MainRandom` | 每次调用后写回 `World.RandomState` |
| 命令接受 | `MainCommandService` | 校验、扣费、离城、入队，玩家/AI 共用 |
| 队列执行 | `MainOrderExecutor` | 搜寻、归队、清槽和报告 |
| AI | `MainAiPlanner` | 当前其他势力的命令选择 |
| 月度 | `MainMonthService` | 日期、体力、人口、粮耗和灾害 |
| 查询 | `MainViewFactory` | 从一份 World 创建不可变 MainGameView |
| 会话 | `MainGameSession` | 62 行薄外观，持有 Content/World 并转发流程 |
| 存档 | `MainSaveService` | schema、ContentHash、checksum、原子写入与恢复 |

公式仍在 C# 中，不改成任意 JSON 脚本。命令成本和类型集中在目录中；是否正确继续由生产 WASM golden 验证。

## 3. 会话和存档边界

```text
MainContent（静态、当前进程共享）
  ├─ Tools / Skills / Periods
  └─ ContentHash
          │
MainGameSession
  ├─ Content（只读引用）
  └─ World（可变快照）
          │
MainSaveEnvelope
  ├─ SchemaVersion = 3
  ├─ ContentHash
  ├─ Checksum
  └─ MainWorld（不包含 Tools/Skills）
```

`MainSaveService.Load(path, content)` 必须提供当前内容定义。schema、envelope hash、World hash 或 checksum 不匹配都会拒绝加载。M1/schema 2 档不会被静默当成新格式。

## 4. 月度调用顺序

```text
MainGameSession.EndMonth
  ├─ MainAiPlanner.Plan（可选）
  ├─ MainOrderExecutor.Execute
  │   ├─ 搜寻等月末效果
  │   └─ 归队、清槽、报告
  └─ MainMonthService.Update
      ├─ 日期与体力
      ├─ 人口与粮耗
      ├─ 季度防灾
      └─ 随机事件
```

接受命令时的即时公式只由 `MainInternalRules` 执行，随机算法与状态写回分别由 `MainRng` 和 `MainRandom` 负责，避免重复实现和调用顺序漂移。

## 5. UI 刷新

`Main.cs` 在每次 `Refresh()` 开头只调用一次 `session.GetView()`，保存到 `currentView`。地图、城市列表、人物列表、详情和报告使用同一份不可变快照。

选中城市、武将和主题仍是 UI 状态，不写入 World。后续 Main-R2 已完成原始 DTO/Mapper/Validator 和战略面板拆分，见 `16-main-r2-structure.md`。

## 6. 验证

- 44 项测试通过，新增静态定义排除、ContentHash 不匹配拒绝和 Content 引用保持检查。
- 五项接受时 golden、开垦/搜寻月末 golden、main RNG、四时期君主与存档连续性保持通过。
- main 导入器 33 项检查通过。
- Godot M2A 冒烟及独立 macOS 应用通过，输出 `MAIN_M2A_SMOKE_OK`。
- 构建 0 警告、0 错误，`git diff --check` 通过。

## 7. 后续结构工作

1. 新系统优先放入对应命令/月度/AI 组件，不重新集中到 Session。
2. 只有确有 MOD 需求时才外置更多规则参数，不提前引入脚本公式系统。
3. Raw DTO 与战略面板的后续拆分已在 Main-R2 完成，下一阶段直接进入 M2B 功能开发。
