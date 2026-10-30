# 32 · 当前代码架构审查

审查基线：`rewrite@47f4790`。范围包含四个生产工程、测试工程、导入与 golden 工具、构建与导出脚本。此次只新增审查报告，不修改业务实现。

## 总体判断

现有工程分层值得保留：Domain 无外部工程依赖，Application 只引用 Domain，Infrastructure 引用 Application/Domain，Godot 作为组合入口引用三者。规则没有 Godot 依赖，可以无界面运行。内容导入 DTO/Mapper/Validator 已拆开，战略界面也有独立面板，RNG 只有一个兼容实现。

但代码尚未满足严格的解耦与模块化要求。主要问题是状态写入权限和流程所有权没有收口：UI 可以绕过会话直接运行规则，可变对象跨输入/输出边界共享，战斗、战略和存档数据形状相互牵连。部分问题已有可复现的错误，不只是命名或文件大小问题。

## 优先修复的边界问题

### A1 · P1：月度流程没有可恢复阶段，守城后重新运行战略 AI

位置：`src/Game.Application/MainGameSession.cs:48`。

`EndMonth` 每次都先调用 `MainAiPlanner.Plan`，遇到守城订单则进入战斗并返回；`ResolveBattle` 仅结算战斗，没有记录应该恢复到订单执行阶段。战后玩家再次结束策略会再次运行当月 AI。

独立复现：同一月第一次 AI 阶段把某 AI 城防灾从 0 加到 1；守城结算后恢复月份推进，防灾再次变为 2，月份才由 1 变为 2。该分支可能重复施加 AI 补粮、内政增益或生成订单。现有守城续月测试调用 `EndMonth(false)`，没有覆盖真实默认入口。

建议：建立明确的月度阶段及继续位置，由应用层 `TurnCoordinator` 独占推进权限，支持 `Player -> Ai -> Orders -> AwaitBattle -> Orders -> MonthUpdate -> Complete`。阶段随快照保存，恢复时不重跑已完成阶段。相应限制 `ExecuteOrders`、`ConditionUpdate`、`EnterBattle` 等绕过流程的公开入口。

验收：同月多个守城战、中途存读档和默认开启 AI 的继续操作中，AI、收入及粮耗各只执行一次，RNG 与连续运行一致。

### A2 · P1：存档格式直接绑定运行时模型，旧版本迁移检查并未验证真正旧结构

位置：`src/Game.Infrastructure/MainSaveService.cs:44`、`:52`；`tests/Game.Tests/MainFactionTests.cs:61`。

`Envelope.World` 直接使用最新 `MainWorld`；加载时先转成最新类型，再序列化验证旧校验和。新增属性的默认值会参与序列化，导致旧版本原始结构的正确校验和失效，迁移代码尚未执行就已拒绝存档。

独立复现：在保持 ContentHash 一致的条件下，生成不含 `ActiveBattle` 的 schema 5 原始世界，按其自身结构计算校验和，加载返回“存档校验和失败”。现有迁移测试先用最新 `MainWorld` 重新计算校验和，把缺失字段又补回去了，掩盖了这个边界问题。此复现隔离了 schema 问题，不表示内容哈希不匹配的历史存档应无条件开放。

建议：将运行时状态与版本化 Save DTO 分离。先依据原 schema 的序列化合同验证 checksum，再迁移 Save DTO，最后转换为运行时 World；内容兼容校验保持独立。用真实历史格式 fixture 验证迁移，避免只给新结构改版本号。

验收：原生 schema 4/5 fixture 的缺失字段、原始校验和、损坏检测和当前 schema 回写均有回归。

### A3 · P1：命令授权、目标和状态迁移没有统一边界

位置：`src/Game.Application/MainCommandService.cs:31`、`:57`、`:152`；`src/Game.Application/MainDispositionRules.cs:39`。

处斩/流放跳过了整段城池归属、人物归属与位置校验；流放只排除已处斩者，没有检查是否在所选我方城市。`IssueInternal` 还是公开入口，可以绕过 Preview。

独立复现：通过正常 `IssueCommand`，向我方平原提交另一座敌城人物的流放请求，返回 Accepted。人物被放进新的城市，但原敌城队列仍保留该人物，产生双重位置引用。当前 UI 的筛选能限制常见点击路径，不能替代应用层合同。

建议：区分操作方、执行者与处置目标，统一验证调用者身份、来源城与目标位置；公开命令只有校验后的入口。人员移动、离队、归队和占用释放集中为少数状态操作，供招揽、流放、出征、归队及战斗结算共用；保留 AI 与玩家确有差异的数值公式，不强行把它们等同。

验收：不合法或跨城目标不改变任何状态；每名人物在城市、订单、战场之间保持合法唯一位置。

### A4 · P2：只读视图和命令输入仍然共享可变集合

位置：`src/Game.Application/MainViewFactory.cs:16`；`MainBattleViewModels.cs:33`、`:39`；`MainExpeditionRules.cs:14`。

城市视图直接引用 `city.Connections`；战斗视图直接引用地图地形和战斗日志。`IReadOnlyList` 只限制接口，不会复制底层 List。出征订单也直接保存请求的 `ParticipantIds`。

独立复现：把城市视图的 Connections 转成 IList 后清空，真实世界的连接立即变为 0；出征接受后清空原请求列表，订单编队变为 0，但两名人物仍然离城并被占用。第二种情况无需直接访问 World。

建议：应用入口复制并校验命令集合，输出使用与世界独立的不可变快照；不能只将类型名改成 IReadOnlyList。外部仅获得查询/命令合同，不获得可变 World。

验收：修改原输入、持有旧视图或推进会话均不能反向污染另一侧对象。

## 需要拆分的职责

### A5 · P2：战斗服务聚合了多个独立模块，并直接接管战略结算

位置：`src/Game.Application/MainBattleService.cs`，638 行；AI 从第 229 行开始，战略结果应用从第 333 行开始，寻路从第 553 行开始。

同一类型处理战斗初始化、概率自动结算、普通攻击、技能、逐日状态、AI 评分与寻路、范围查询、俘虏、城池账本、太守和全局胜负。调整 AI、战术规则和战略战果都会改动同一实现，测试也必须构造完整 World/Content。

建议分成：

- `BattleFactory`：由战略出征创建战场。
- `BattleRules`：行动、伤害、技能和状态，只依赖战斗需要的状态/内容。
- `BattleNavigation`：可达格和射程，共享给规则、预览和 AI。
- `TacticalAi`：根据状态规划合法动作，通过同一行动入口执行。
- `BattleOutcomeApplier`：将战斗结果写回人物、城市与势力。
- `BattleCoordinator`：协调上述模块并提供统一外观。

自动战斗保留 main 的独立概率规则，不因结构重构而替换成另一种模拟。

### A6 · P2：Godot 主入口混入用例控制与大量自动验收逻辑

位置：`src/Game.Godot/Main.cs:143`、`:295`、`:359`。

Main.cs 共 771 行，其中第 359 行起为冒烟、构造世界、修改内容、断言和截图。正常战斗输入直接把 `session.World` 交给底层规则，内容查询、存档选择、界面导航、错误处理也由同一节点负责。

建议：Main 仅作为组合入口；拆出战略 Presenter、战斗 Presenter、存档 Controller 与屏幕导航。冒烟 fixture、断言和截图放入独立验证宿主或明确隔离的验证模块，保留真正的 Godot UI 交互冒烟。Godot 普通面板只接收 ViewModel 并发出意图事件。

验证代码不应为了便于设置 fixture 而迫使生产 API 永久公开 World。将文件存储实现注入应用边界即可，不必引入大型 DI 框架。

### A7 · P2：命令能力描述散落在规则、会话、执行器与界面

位置：`MainCommandCatalog.cs`、`MainCommandService.cs`、`MainOrderExecutor.cs`、`MainGameSession.cs:84`、`MainCityDetailPanel.cs:29`、`:65`、`:124`、`:196`。

新增一个命令，需要分别更新名称/成本、通用大参数对象、校验、接受、月末执行、目标查询、UI 下拉、控件显隐和字段读取。侦察/出征目标的业务筛选由 UI 再实现；人员操作还存在执行者与目标角色复用。

建议按内政、人事、后勤、外交、出征组织小型 Handler；集中命令描述和目标查询，使用能表达必要参数的请求类型。UI 保留具体控件布局，但不重复判断城市是否具备业务资格。未知命令应明确拒绝，不能默默落入默认归队分支。无需为此引入通用消息总线或反射插件系统。

### A8 · P2：静态内容、运行初值和领域状态尚未形成清楚的所有权

位置：`src/Game.Domain/MainWorld.cs:3`、`:14`；`src/Game.Infrastructure/MainContentLoader.cs:48`；`MainContentValidator.cs:49`。

MainContent 的 get-only 引用不代表内容不可变，Tools/Skills/Periods 及其内部对象均可写，内容 Hash 却只在读取 JSON 时计算。MainPeriod 用运行时 MainCity/MainPerson 存储初值，Validator 还会回填 CityId；Loader 创建会话时通过 JSON 序列化完成深拷贝。

这些设计让内容校验承担规范化，让序列化承担复制，让加载器承担新游戏初始化；以后改存档字段或领域对象就会牵动内容与建局。

建议：分别定义不可变 Content/ScenarioDefinition、可变 WorldState 和持久化 Save DTO；由应用层 NewGameFactory 显式创建状态，由 Mapper 完成数据规范化，Validator 尽量只检查。先处理集合与对象的所有权，不要求把全部数据类改成复杂的领域实体。

### A9 · P3：历史验证代码与工具公共设施应显式隔离

R0 的 World/GameSession、ContentLoader/SaveService、StrategicMap 与生产 Main 类型并列编译，当前靠 Main 前缀区分两套系统。工具中 capture-skill-natural/reward-ui 从 capture-m3-c 可执行脚本导入 run/scheduler，而 import.mjs 同时承担引擎加载、导入、校验和命令行。

建议将 R0 收进独立 Legacy/R0 命名空间或验证工程，继续保留回归；把 EngineHarness、VirtualScheduler、ScenarioRunner 放进工具 lib 目录，使各 CLI 只负责参数和场景。相对五级目录查找的测试根路径集中到测试 fixture 工厂。验证入口可分成规则、内容对照、Godot UI 三组，最终总验证仍聚合运行。

## 建议的模块结构

不需要新增一批工程；保留当前四工程，先在工程内部按功能模块组织，并用可见性和少量合同约束依赖：

| 工程 | 建议内部模块 | 对外边界 |
|---|---|---|
| Game.Domain | State、Content、Shared/Invariants | 纯 .NET 状态与不可变定义，无文件或 Godot 类型 |
| Game.Application | Session、Turns、Strategy/Commands、Battle、Queries、Persistence/Contracts | 命令、查询、建局/恢复与存储端口 |
| Game.Infrastructure | ContentImport、Persistence/DTO、Persistence/Migrations、FileStorage | 实现内容读取和存储端口，不负责 UI 格式化 |
| Game.Godot | Composition、Navigation、Presenters、Views | 依赖应用合同，具体基础设施只在组合入口连接 |
| 验证工具 | R0、ProductionGolden、GodotSmoke、TestFixtures | 可以构造测试状态，不进入正常游戏调用链 |

规则模块保持单向依赖：战略层可以创建战斗并应用战果，战术规则本身不反向修改战略城市与势力。规则间确需共享的数据通过明确上下文传入，避免任何模块都获得完整 World 的无限写权限。

## 重构顺序与门禁

1. 先加入 A1–A4 的失败回归，再修边界：可恢复阶段、命令授权、集合所有权和历史存档合同。
2. 建立应用门面与真正不可变的查询快照，移除普通 Godot 控制器对可变 World 的依赖。
3. 拆战斗规则/导航/AI/战果应用，保持所有既有 golden 的行为与 RNG 顺序。
4. 拆命令 Handler、内容初值与存档 DTO，最后隔离 R0 与 CLI 公共设施。

每一步应独立可验收，不将架构重构与玩法改动合成一个大提交。除明确修复已证实的问题外，规则数值、RNG 调用顺序、生产 golden 和现有存档兼容政策保持不变。

建议增加少量架构守卫：Godot 普通 Views/Presenters 不调用底层 Rules 或访问 World；Domain/Application 不引用 Godot、具体文件存储或基础设施类型；生产程序集不包含测试 fixture。原 221 项回归需要继续通过，新边界问题应增加能先失败再修复的测试。

## 复现与验证说明

独立复现程序位于 `/tmp/sgby-architecture-audit/Audit.cs`，直接引用当前已编译程序集；没有改写项目源代码或玩家存档。输出：

```text
VIEW_ALIAS worldConnectionsAfterViewEdit=0
COMMAND_ALIAS accepted=True queuedParticipants=0 busyOfficers=2
OLD_SCHEMA 存档校验和失败。
MONTH_RESUME aiAvoidFirst=1 aiAvoidAfterResume=2 monthBefore=1 monthAfter=2
EXILE_BOUNDARY accepted=True target=person-000 inOriginalQueue=True newCity=city-025 originalCity=city-015
```

上述五个复现对应四类边界缺陷。其余结论来自项目依赖、源码调用、对象所有权和职责审查，不冒充额外性能测试或原版行为等价证明。

审查时重新执行当前测试基线：221/221 通过。现有测试没有覆盖上述失败路径；测试通过不能替代输入/输出所有权、流程恢复与版本合同的专项验证。
