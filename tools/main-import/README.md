# Main 内容导入与 golden 导出

本工具只用于离线内容导入。WASM 仅在运行导入器时由 Node.js 加载；生成的 JSON 不依赖 WASM，也不要求生产 Godot 引入 JavaScript 或 WASM 运行时。

## 环境与命令

需要 Node.js 26 或更新版本，使用原生 `TextDecoder('GBK')` 解码名称。工具没有第三方 npm 依赖。

在仓库根目录准备只读参考 worktree，并执行真实导出：

```bash
git worktree add .reference/main main
node tools/main-import/import.mjs
```

重复导出到内存、执行全部校验并逐字节比对现有文件：

```bash
node tools/main-import/import.mjs --check
```

可显式指定参考目录和输出目录：

```bash
node tools/main-import/import.mjs \
  --reference .reference/main \
  --output content/main
```

导入源固定为参考目录中的 `libs/SGBY.lib`。加载顺序为 `_main()` 载入库、`_bayeGameEnvInit()` 初始化反射环境、`_bayeLoadPeriod(1..4)` 逐期加载。输出记录 main commit、库/引擎/bridge/WASM 的 SHA-256；不写入时间戳，因此相同输入可重复得到相同文件。

## 输出文件

- `content/main/content.json`：稳定 ID 映射、33 个道具和 30 个技能的完整反射字段。
- `content/main/scenarios.json`：4 个时期，每期 38 城、200 条人物记录、33 个道具状态、30 个技能及 200 个 `g_OrderQueue` 原始槽位。
- `content/main/golden-liubei-190.json`：190 年 1 月平原、刘备及城内 5 人的完整初始记录，以及 3 人实际装备的道具定义。
- `content/main/import-summary.json`：数量、时期日期、城内引用数量、超过 100 的属性统计、golden 基准、校验结果、文件哈希和限制。

`scenarios.json` 每条城市、人物、道具和技能记录都包含：

- `index`：main 数组中的原始零基索引。
- `id`：由类型和索引确定性生成的稳定 ID，例如 `city-005`、`person-013`、`tool-002`。
- `name`：从引擎名称接口实际解码的 GBK 名称。
- `raw`：未经截断或规则改写的反射字段。

城市的 `raw` 包含 `State/Belong/SatrapId/FarmingLimit/Farming/CommerceLimit/Commerce/PeopleDevotion/AvoidCalamity/PopulationLimit/Population/Money/Food/MothballArms/PersonQueue/Persons/ToolQueue/Tools`，另有规范化 `ownerPersonId/satrapPersonId/personIds/toolIds`。

人物的 `raw` 包含 `OldBelong/Belong/Level/Force/IQ/Devotion/Character/Experience/Thew/ArmsType/Arms/Tool1/Tool2/Equip/Age`，另有规范化 `oldOwnerPersonId/ownerPersonId/tool1Id/tool2Id`。`Equip` 保留原始二元数组，不额外猜测其语义。原始一基引用仍保留在 `raw` 中；`0` 和 `0xff` 在规范化引用中表示为 `null`。

`g_OrderQueue` 全量保留 `OrderId/Person/City/Object/Arms/Food/Money/Consume/TimeCount`，不读取 `demos.js` 推断空槽位或指令规则。人物末尾的未使用空槽也保留，以确保每期确有 200 条原始记录；有效人物必须有名称。

## Golden 与校验

190 年 1 月平原的实际导出基准为：

- 农业 `938/2470`，商业 `845/2486`，民忠 `68`。
- 人口 `32225/87096`，金 `93`，粮 `234`。
- 城内顺序为刘备、关羽、张飞、吴懿、丁奉。
- 刘备武力 `85`，关羽 `106`，张飞 `107`；道具分别为双股剑、青龙刀、丈八矛。

导入时检查时期/城市/人物/道具/技能数量、ID 与索引映射、人物和道具引用范围、队列边界、有效记录名称、U8 属性范围、城市身份稳定性，以及上述 golden 数值和道具。摘要另列每期 `Force/IQ/Devotion/Thew/Arms` 大于 100 的数量，避免把高属性静默截为 100。

## 未导出的内容

- 单独调用时期加载不会触发地图绘制，因此当前没有可靠的地图矩阵或逐城坐标可导出。`g_CityPos` 只是当前坐标，不冒充 38 城坐标表。
- 本工具不驱动 UI。关羽开垦（ID 1、费用 50）的接受、取消和月末状态未可靠捕获，golden 中明确标为 `not-captured`，没有手工构造这些状态。
