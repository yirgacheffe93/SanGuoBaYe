using System.Text;
using Game.Application;
using Game.Domain;
using Game.Infrastructure;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: Game.R0Simulation <scenario.json> <scenario.schema.json> <report.md>");
    return 2;
}

try
{
    var first = R0Simulation.Run(args[0], args[1]);
    var second = R0Simulation.Run(args[0], args[1]);
    if (!string.Equals(first, second, StringComparison.Ordinal))
        throw new InvalidOperationException("相同输入的两次模拟报告不一致。");
    var output = Path.GetFullPath(args[2]);
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    File.WriteAllText(output, first);
    Console.WriteLine($"R0_SIMULATION_OK turns=5 report={output}");
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine($"R0_SIMULATION_FAILED: {error}");
    return 1;
}

internal enum TaskKind { Development, Recruitment, Truce }
internal enum SimTaskStatus { Queued, Completed, Cancelled }

internal sealed class SimTask
{
    public required string Id { get; init; }
    public required TaskKind Kind { get; init; }
    public required string CityId { get; init; }
    public required string CharacterId { get; init; }
    public DevelopmentTrack Track { get; set; }
    public int Amount { get; set; }
    public int Gold { get; init; }
    public int Food { get; init; }
    public int Population { get; init; }
    public SimTaskStatus Status { get; set; } = SimTaskStatus.Queued;
}

internal sealed class SimArmy
{
    public required string Id { get; init; }
    public required string FactionId { get; init; }
    public required string GeneralId { get; init; }
    public required string SourceCityId { get; init; }
    public required string TargetCityId { get; init; }
    public int Troops { get; set; }
    public int Food { get; set; }
    public int RemainingTurns { get; set; } = 1;
}

internal static class R0Simulation
{
    public static string Run(string contentPath, string schemaPath)
    {
        var world = ScenarioSchemaValidator.LoadValidatedFile(contentPath, schemaPath);
        var tasks = new List<SimTask>();
        var armies = new List<SimArmy>();
        var report = new StringBuilder();
        var truceStart = -1;
        var sawBattle = false;
        var sawWithdrawal = false;
        var initialTroops = world.Cities.Sum(city => city.Troops);
        var battleLosses = 0;
        report.AppendLine("# 12 · R0 五回合规则模拟");
        report.AppendLine();
        report.AppendLine("> 由 `Game.R0Simulation` 使用真实 R0 JSON、Schema、参数和 `BattleResolver` 生成。相同输入执行两次，报告逐字一致。数值仍待 R1 试玩验证。");
        report.AppendLine();
        report.AppendLine("## 逐回合记录");
        report.AppendLine();

        for (var turn = 1; turn <= 5; turn++)
        {
            report.AppendLine($"### 第 {turn} 回合");
            report.AppendLine();
            if (truceStart > 0 && turn >= truceStart + 3)
            {
                report.AppendLine("- 停战在回合开始到期，双方恢复出征资格。");
                truceStart = -1;
            }

            switch (turn)
            {
                case 1:
                    var cancelled = AddDevelopment(world, tasks, "city.anling", "char.shenyan", DevelopmentTrack.Agriculture);
                    Cancel(world, cancelled);
                    report.AppendLine("- 安陵开发任务在排队中取消：100 金全额退回，沈砚释放；未消耗 RNG。");
                    AddDevelopment(world, tasks, "city.anling", "char.shenyan", DevelopmentTrack.Agriculture);
                    AddRecruitment(world, tasks, "city.beiping", "char.luqing", 200);
                    AddDevelopment(world, tasks, "city.hejin", "char.dufeng", DevelopmentTrack.Commerce);
                    report.AppendLine("- 安陵重新开发农业；北平征兵 200；河津开发商业。");
                    break;
                case 2:
                    AddTruce(world, tasks, "city.beiping", "char.hanqing");
                    AddDevelopment(world, tasks, "city.lingchuan", "char.suyan", DevelopmentTrack.Agriculture);
                    report.AppendLine("- 昭明支付 50 金提出停战；玄朔临川开发农业。");
                    break;
                case 3:
                    AddRecruitment(world, tasks, "city.anling", "char.zhouheng", 300);
                    AddRecruitment(world, tasks, "city.hejin", "char.weizhang", 200);
                    report.AppendLine("- 停战有效期内：安陵征兵 300，河津征兵 200；双方不得出征。");
                    break;
                case 4:
                    AddDevelopment(world, tasks, "city.beiping", "char.luqing", DevelopmentTrack.Commerce);
                    AddDevelopment(world, tasks, "city.lingchuan", "char.guxiao", DevelopmentTrack.Commerce);
                    report.AppendLine("- 北平与临川分别开发商业；停战继续有效。");
                    break;
                case 5:
                    AddArmy(world, armies, "city.anling", "city.hejin", "char.zhouheng", 800, 200);
                    report.AppendLine("- 停战已失效：周衡率 800 兵、携 200 粮从安陵出征河津，兵粮立即从来源城扣除。");
                    break;
            }

            ResolveTasks(world, tasks, report, ref truceStart);
            ResolveArmies(world, armies, report, ref sawBattle, ref sawWithdrawal, ref battleLosses);
            foreach (var reportEvent in world.Events.Where(e => e.TriggerTurn == turn).OrderBy(e => e.Id, StringComparer.Ordinal))
            {
                world.Reports.Add(new ReportEntry { Turn = turn, Message = reportEvent.Text });
                report.AppendLine($"- 报告事件 `{reportEvent.Id}`：{reportEvent.Text}（无资源或归属效果）。");
            }
            Advance(world, armies, report);
            AssertInvariants(world, tasks, armies);
            report.AppendLine($"- 回合末城市：{string.Join("；", world.Cities.OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => $"{c.Name}[金{c.Gold}/粮{c.Food}/兵{c.Troops}/人口{c.Population}]"))}。");
            report.AppendLine("- 不变量检查：资源、人口、兵力非负；武将无重复占用；任务未重复结算。通过。");
            report.AppendLine();
            world.Turn++;
        }

        if (!sawBattle || !sawWithdrawal) throw new InvalidOperationException("五回合模拟未覆盖战斗及轮限撤军。");
        var recruited = tasks.Where(task => task.Kind == TaskKind.Recruitment && task.Status == SimTaskStatus.Completed).Sum(task => task.Amount);
        var finalTroops = world.Cities.Sum(city => city.Troops) + armies.Sum(army => army.Troops);
        if (finalTroops != initialTroops + recruited - battleLosses)
            throw new InvalidOperationException("全过程兵力收支不一致。");
        var finalHash = new GameSession(world).GetStateHash();
        var outcomeState = Clone(world);
        foreach (var city in outcomeState.Cities.Where(c => c.FactionId == outcomeState.FactionId)) city.FactionId = "faction.xuanshuo";
        if (Outcome(outcomeState, []) != "失败") throw new InvalidOperationException("无城且无军队未判负。");
        var zeroState = 1UL;
        var zero = BattleResolver.Resolve(new BattleSetup(1, 1, 100000, 100000, 1), ref zeroState);
        if (zero.AttackerWon || zero.Attackers != 0 || zero.Defenders != 0)
            throw new InvalidOperationException("同时归零规则错误。");

        report.AppendLine("## 分支与结局检查");
        report.AppendLine();
        report.AppendLine("- 主线：停战在第 2–4 回合有效，第 5 回合到期；河津战斗达到 8 轮，攻方剩余部队撤军返航，不按败军消灭。");
        report.AppendLine("- 失败分支：玩家无城且无野外军队，判定为失败。");
        report.AppendLine("- 同时归零：攻守同时归零时守方保住城市，攻方不获胜。");
        report.AppendLine($"- 兵力总账：初始 {initialTroops} + 新征 {recruited} - 战斗伤亡 {battleLosses} = 期末 {finalTroops}，核对一致；出征携粮 200 在撤军后全部返回来源城。");
        report.AppendLine($"- 确定性：相同 JSON、Schema、种子与命令序列执行两次，完整报告一致；最终 World SHA-256 为 `{finalHash}`。");
        report.AppendLine("- 范围说明：这是规则与数值模拟，不包含正式 AI、UI 或完整 R1 命令处理。");
        return report.ToString();
    }

    private static SimTask AddDevelopment(World world, List<SimTask> tasks, string cityId, string characterId, DevelopmentTrack track)
    {
        var city = City(world, cityId);
        var character = Character(world, characterId);
        ValidateActor(city, character);
        if (character.OrderId is not null || city.Gold < world.Rules.DevelopmentCost) throw new InvalidOperationException("开发前置条件不足。");
        var task = NewTask(tasks, TaskKind.Development, cityId, characterId, world.Rules.DevelopmentCost, 0, 0);
        task.Track = track;
        city.Gold -= task.Gold;
        character.OrderId = task.Id;
        tasks.Add(task);
        return task;
    }

    private static void AddRecruitment(World world, List<SimTask> tasks, string cityId, string characterId, int amount)
    {
        if (amount <= 0 || amount % 100 != 0) throw new InvalidOperationException("征兵数量必须为 100 的倍数。");
        var city = City(world, cityId);
        var character = Character(world, characterId);
        ValidateActor(city, character);
        var gold = amount / 100 * world.Rules.RecruitGoldPer100Troops;
        var food = amount / 100 * world.Rules.RecruitFoodPer100Troops;
        if (character.OrderId is not null || city.Gold < gold || city.Food < food || city.Population < amount)
            throw new InvalidOperationException("征兵资源或执行者不足。");
        var task = NewTask(tasks, TaskKind.Recruitment, cityId, characterId, gold, food, amount);
        task.Amount = amount;
        city.Gold -= gold;
        city.Food -= food;
        city.Population -= amount;
        character.OrderId = task.Id;
        tasks.Add(task);
    }

    private static void AddTruce(World world, List<SimTask> tasks, string cityId, string characterId)
    {
        var city = City(world, cityId);
        var character = Character(world, characterId);
        ValidateActor(city, character);
        if (character.OrderId is not null || city.Gold < world.Rules.EnvoyCost) throw new InvalidOperationException("停战前置条件不足。");
        var task = NewTask(tasks, TaskKind.Truce, cityId, characterId, world.Rules.EnvoyCost, 0, 0);
        city.Gold -= task.Gold;
        character.OrderId = task.Id;
        tasks.Add(task);
    }

    private static SimTask NewTask(List<SimTask> tasks, TaskKind kind, string cityId, string characterId, int gold, int food, int population) =>
        new() { Id = $"sim.task.{tasks.Count + 1}", Kind = kind, CityId = cityId, CharacterId = characterId,
            Gold = gold, Food = food, Population = population };

    private static void Cancel(World world, SimTask task)
    {
        if (task.Status != SimTaskStatus.Queued) throw new InvalidOperationException("只有排队任务可取消。");
        var city = City(world, task.CityId);
        city.Gold += task.Gold;
        city.Food += task.Food;
        city.Population += task.Population;
        Character(world, task.CharacterId).OrderId = null;
        task.Status = SimTaskStatus.Cancelled;
    }

    private static void ResolveTasks(World world, List<SimTask> tasks, StringBuilder report, ref int truceStart)
    {
        foreach (var task in tasks.Where(t => t.Status == SimTaskStatus.Queued).OrderBy(t => t.Id, StringComparer.Ordinal))
        {
            var city = City(world, task.CityId);
            switch (task.Kind)
            {
                case TaskKind.Development:
                    if (task.Track == DevelopmentTrack.Agriculture) city.Agriculture++;
                    else city.Commerce++;
                    break;
                case TaskKind.Recruitment:
                    city.Troops += task.Amount;
                    break;
                case TaskKind.Truce:
                    var proposer = Strength(world, world.FactionId);
                    var opponentId = world.Factions.Select(f => f.Id).First(id => id != world.FactionId);
                    var opponent = Strength(world, opponentId);
                    var score = world.Rules.TruceBaseScore + world.Rules.TruceStrengthWeight * opponent / (proposer + opponent);
                    if (score < world.Rules.TruceAcceptScore) throw new InvalidOperationException($"停战评分 {score} 未达预期。");
                    truceStart = world.Turn;
                    report.AppendLine($"- 停战评分 {score} = 基础 {world.Rules.TruceBaseScore} + 实力权重项 {score - world.Rules.TruceBaseScore}，达到 {world.Rules.TruceAcceptScore}，提议接受；本回合起效。");
                    break;
            }
            Character(world, task.CharacterId).OrderId = null;
            task.Status = SimTaskStatus.Completed;
        }
    }

    private static void AddArmy(World world, List<SimArmy> armies, string sourceId, string targetId, string generalId, int troops, int food)
    {
        var source = City(world, sourceId);
        var target = City(world, targetId);
        var general = Character(world, generalId);
        ValidateActor(source, general);
        if (source.FactionId == target.FactionId || !source.Connections.Contains(targetId, StringComparer.Ordinal) ||
            general.OrderId is not null || source.Troops < troops || source.Food < food || troops <= 0)
            throw new InvalidOperationException("出征前置条件不足。");
        source.Troops -= troops;
        source.Food -= food;
        general.OrderId = "sim.army.1";
        armies.Add(new SimArmy { Id = "sim.army.1", FactionId = source.FactionId, GeneralId = generalId,
            SourceCityId = sourceId, TargetCityId = targetId, Troops = troops, Food = food });
    }

    private static void ResolveArmies(World world, List<SimArmy> armies, StringBuilder report, ref bool sawBattle, ref bool sawWithdrawal, ref int battleLosses)
    {
        foreach (var army in armies.ToList())
        {
            if (--army.RemainingTurns > 0) continue;
            var target = City(world, army.TargetCityId);
            var attacker = Character(world, army.GeneralId);
            var defender = world.Characters.Where(c => c.CityId == target.Id && c.FactionId == target.FactionId)
                .OrderBy(c => c.Id, StringComparer.Ordinal).First();
            var setup = new BattleSetup(army.Troops, target.Troops, (attacker.Leadership + attacker.War) / 2,
                (defender.Leadership + defender.War) / 2 + target.Defense, world.Rules.BattleMaxRounds,
                world.Rules.BattleDamageDivisor, world.Rules.BattleMinRoll, world.Rules.BattleMaxRoll, world.Rules.BattleMinDamage);
            var randomState = world.RandomState;
            var result = BattleResolver.Resolve(setup, ref randomState);
            world.RandomState = randomState;
            battleLosses += army.Troops + target.Troops - result.Attackers - result.Defenders;
            sawBattle = true;
            report.AppendLine($"- 河津战斗：攻方 {army.Troops} / 攻值 {setup.Attack}，守方 {target.Troops} / 守值 {setup.Defense}（含城防 {target.Defense}）。");
            foreach (var round in result.Rounds)
                report.AppendLine($"  - 第 {round.Round} 轮：攻损 {round.AttackerLosses}、守损 {round.DefenderLosses}，余攻 {round.Attackers}、余守 {round.Defenders}。");
            target.Troops = result.Defenders;
            if (result.AttackerWon)
            {
                target.FactionId = army.FactionId;
                target.Troops = result.Attackers;
                target.Food += army.Food;
                attacker.CityId = target.Id;
            }
            else
            {
                sawWithdrawal = true;
                var source = City(world, army.SourceCityId);
                if (source.FactionId == army.FactionId)
                {
                    var foodBeforeReturn = source.Food;
                    source.Troops += result.Attackers;
                    source.Food += army.Food;
                    if (source.Food != foodBeforeReturn + army.Food) throw new InvalidOperationException("撤军携粮去向不一致。");
                    attacker.CityId = source.Id;
                    report.AppendLine($"- 达到轮限或进攻失败：{result.Attackers} 名剩余攻军与 {army.Food} 粮返航来源城；守方保留河津。");
                }
            }
            attacker.OrderId = null;
            armies.Remove(army);
        }
    }

    private static void Advance(World world, List<SimArmy> armies, StringBuilder report)
    {
        foreach (var city in world.Cities.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            city.Gold += city.Agriculture * world.Rules.AgricultureIncome + city.Commerce * world.Rules.CommerceIncome;
            var upkeep = city.Troops * world.Rules.UpkeepFoodPer1000Troops / 1000;
            ApplyUpkeep(city.Food, upkeep, food => city.Food = food, city.Troops,
                world.Rules.StarvationLossPercent, losses => city.Troops -= losses);
        }
        foreach (var army in armies)
            ApplyUpkeep(army.Food, army.Troops * world.Rules.UpkeepFoodPer1000Troops / 1000,
                food => army.Food = food, army.Troops, world.Rules.StarvationLossPercent, losses => army.Troops -= losses);
        report.AppendLine("- 结算收入与唯一一次维护；各城维护按 `floor(兵力 × 每千人粮耗 / 1000)` 计算。");
    }

    private static void ApplyUpkeep(int available, int upkeep, Action<int> setFood, int troops, int lossPercent, Action<int> applyLosses)
    {
        var paid = Math.Min(available, upkeep);
        setFood(available - paid);
        if (paid < upkeep && troops > 0) applyLosses(Math.Max(1, troops * lossPercent / 100));
    }

    private static void AssertInvariants(World world, List<SimTask> tasks, List<SimArmy> armies)
    {
        if (world.Cities.Any(c => c.Gold < 0 || c.Food < 0 || c.Population < 0 || c.Troops < 0) ||
            armies.Any(a => a.Troops < 0 || a.Food < 0)) throw new InvalidOperationException("出现负资源或兵力。");
        var busy = world.Characters.Where(c => c.OrderId is not null).Select(c => c.OrderId!).ToList();
        if (busy.Count != busy.Distinct(StringComparer.Ordinal).Count()) throw new InvalidOperationException("任务或武将重复占用。");
        var active = tasks.Where(t => t.Status == SimTaskStatus.Queued).Select(t => t.CharacterId).ToList();
        if (active.Count != active.Distinct(StringComparer.Ordinal).Count()) throw new InvalidOperationException("排队任务重复占用武将。");
    }

    private static int Strength(World world, string factionId) => world.Cities.Where(c => c.FactionId == factionId).Sum(c => c.Troops);
    private static City City(World world, string id) => world.Cities.Single(c => c.Id == id);
    private static Character Character(World world, string id) => world.Characters.Single(c => c.Id == id);
    private static void ValidateActor(City city, Character character)
    {
        if (city.Id != character.CityId || city.FactionId != character.FactionId)
            throw new InvalidOperationException("执行者必须位于同城并属于该城势力。");
    }
    private static World Clone(World world) => System.Text.Json.JsonSerializer.Deserialize<World>(
        System.Text.Json.JsonSerializer.Serialize(world))!;
    private static string Outcome(World world, IReadOnlyCollection<SimArmy> armies)
    {
        var hasCity = world.Cities.Any(c => c.FactionId == world.FactionId);
        var hasArmy = armies.Any(a => a.FactionId == world.FactionId && a.Troops > 0);
        if (!hasCity && !hasArmy) return "失败";
        return world.Cities.All(c => c.FactionId == world.FactionId) ? "胜利" : "继续";
    }
}
