# 16 · Main-R2 加载层与战略 UI 结构收尾

> 状态：已完成。本轮没有新增玩法或改变 UI 外观，M2A golden、四时期、五项内政、存档和双主题行为保持一致。

## 1. 加载层职责

`MainContentLoader` 已从 269 行降为 61 行，只负责：

- 文件/PCK 文本读取和 JSON 反序列化。
- Schema 调用和 ContentHash。
- 调用 Mapper 与 Validator。
- 创建包含静态 Content 和所选时期 World 的会话。

独立组件：

| 文件 | 职责 |
|---|---|
| `MainRawDocuments.cs` | main 导入 JSON 对应的纯 DTO，不包含业务校验 |
| `MainContentMapper.cs` | Raw DTO 转 MainContent/MainPeriod/城市/人物/道具/技能，保留 ID、索引和君主顺序 |
| `MainContentValidator.cs` | 数量、重复 ID、归属、队列、引用和数值校验，并回填人物 CityId |

公开 `Load`、`LoadJson`、`CreateSession` 签名未改变，UI 和测试不需要了解 Raw DTO。

## 2. 战略 UI 组件

| 组件 | 职责 |
|---|---|
| `MainStartScreen` | 四时期与真实君主选择 |
| `MainStrategicMap` | 城市节点、选中、悬停和地图点击 |
| `MainCityListPanel` | 38 城列表、我方/命令标记和 CitySelected |
| `MainCityDetailPanel` | 城市状态、人物、道具、命令预览和确认事件 |
| `MainOrderReportPanel` | 命令队列与月度报告 |
| `Main.cs` | 内容加载、组合、选中状态、主题、事件连接、应用层调用、存档和自动验收 |

组件通过事件与 `MainGameView` 协作，不直接访问 World、文件或公式。每次 `Refresh()` 仍只创建一份 MainGameView。

## 3. 保持不变的边界

- main JSON 格式、ContentHash 和 schema 3 存档不变。
- 命令成本、即时效果、RNG 调用顺序和月度顺序不变。
- 四时期君主、38 城、五项命令和月度归队不变。
- 1280×720 布局、墨色/纸本和按钮行为不变。
- 没有引入 DI 框架、脚本公式、额外 UI 库或大规模重命名。

## 4. 验证

- 44 项测试全部通过，main 导入器 33 项检查通过。
- M2A 接受时和月末 golden 保持通过。
- Godot M2A 冒烟与独立 macOS 应用通过。
- 深色/浅色截图已重新生成并实际检查，外观与重构前一致。
- 构建 0 警告、0 错误，`git diff --check` 通过。

## 5. 下一步

结构收尾到此结束。Main-M2B 已在该边界上完成月度与搜寻主要分支，见 `17-main-m2b.md`，未再增加新的架构专项。
