# 24 · Main-M2C-C 寻路、特殊装备与年度出现

> 状态：规则、UI、89 项 C# 测试、双主题与独立 macOS 应用验收已完成。原生赏赐 UI golden 尚待补录；完整 M2/M3 尚未完成。

## 1. 数据与寻路

导入器直接读取 `SGBY.lib`：资源 59/60 为每城 16 字节的右/左连接表，前 8 字节是一基城市编号；资源 2 第 14 项 `C_MAP` 是 12×9 城市矩阵。每城同时保留重写显示坐标和原始规则坐标，距离计算只使用原始坐标。

```text
可达 = 起点经双向同势力城市连接可到达目标
距离 = abs(起点原图X-目标原图X) + abs(起点原图Y-目标原图Y)
```

`MainTravelRules` 使用稳定 BFS 和 visited 集合，不经过敌城；`MainCommandService` 在接受前校验路径，失败不扣体力、不扣物资、不建队列。`MainGameSession.GetTravelRoute` 给 UI 提供只读路径，目标下拉只显示可达同归属城市及距离。

生产 `PolicyExec` 会立即执行所有订单并清槽，没有启用参考 C 中已注释的 `TimeCount` 递减。生产注入 `TimeCount=5` 的移动订单仍在当月完成，因此本阶段只记录距离，不实现跨月延迟。洛阳→长安的实际距离为 3，仍在当月到达。

## 2. 特殊装备

只有 `tool-030` 铁骑兵符、`tool-031` 太玄兵符、`tool-032` 水战兵符的 `UseFlag=1`。

- 普通装备 `UseFlag=0`：需要空装备槽，应用属性与兵种后入槽。
- 使用型兵符 `UseFlag=1`：即使装备槽已满也可使用，应用效果后从城市消耗，不占槽。
- 太玄兵符要求 `IQ>105`，铁骑兵符要求 `Force>105`；水战兵符无阈值。
- 没收只卸下真实装备、撤销其属性，不重复撤销已消耗兵符。武力和智力按 U8 绕回。
- `Move` 和 `ChangeAttackRange` 是战术字段，本阶段不擅自转成战略加成。

`MainTravelAndEquipmentTests` 已覆盖满装备槽使用、属性/忠诚/城市队列守恒、阈值失败原子性和没收后的属性。原生赏赐 UI 的自动键盘驱动尚未可靠完成，没有伪造生产 golden；`content/main/golden-m2c-c.json` 明确记录 `specialTool=not-captured`，UseFlag 分支依据原生 `AddGoodsPerson` 与 `LargessMake`。

## 3. 年度人物与道具

跨年时先更新道具，再统一增长年龄并处理人物出场，然后继续城市月度与随机事件：

```text
人物出场年 = birth + 16
道具出现年 = birth
人物 city=0：按 main RNG 随机 38 城；其他值：直接作为零基城市索引
```

出场人物加入目标城市队首，年龄重置为 16。新增道具为未发现状态。四时期 birth/city 数据已随内容导入；当前正常游戏年份内没有道具 birth 命中，年度道具另有受控规则测试。

生产 191 年 golden：刘晔→13、杨修→25、魏延与孙策→34，随机城市共两次 RNG，最终 RNG `3025925713`。192 年侯选→4、雷薄→22、陈武→24，最终 RNG `456633834`。法正条件的 `city=38` 超出 38 城有效索引，生产对照中未进入城市；重写显式跳过无效目的地，不崩溃、不伪造第 39 城，年龄仍正常增长。

`content/main/golden-m2c-c.json` 通过 `tools/main-import/capture-m2c-c.mjs` 可重复生成。该工具在生产阶段 3 设置年末、清空 AI 订单并固定 RNG，阶段 5 读取真实 `ConditionUpdate`，不是 UI 接受测试。生产引擎不进入 Godot 运行时。

## 4. 当前验证与边界

- `dotnet test SanGuoBaYe.sln --nologo`：89/89 通过。
- 可重复导入及城市引用、双向连接和 schema 校验通过。
- 年度固定/随机城市、年龄、RNG、月度顺序和存档连续性通过。
- UI 使用单快照，只展示实际可达目标与距离；兵符和年度冒烟已加入 Godot 驱动。
- 双主题 1280×720 截图已检查；Godot 与独立 macOS 应用完成 `MAIN_M2C_C_SMOKE_OK native-route use-item annual=191`。
- 后续另补原生赏赐 UI golden，不把当前 C# 规则验收冒充该项生产对照。

下一阶段为 M2 剩余对敌策略与完整 AI，再进入 M3 出征和独立战术战斗。
