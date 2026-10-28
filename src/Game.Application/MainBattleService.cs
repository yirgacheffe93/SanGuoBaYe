using Game.Domain;

namespace Game.Application;

public static class MainBattleService
{
    private static readonly int[] Moves = [5, 4, 4, 5, 6, 3];
    private static readonly double[] AttackModulus = [1.0, 0.8, 0.9, 0.8, 1.3, 0.4];
    private static readonly double[] DefenceModulus = [0.7, 1.2, 1.0, 1.1, 1.2, 0.6];
    private static readonly double[] TerrainDefence = [1.0, 1.0, 1.3, 1.15, 1.1, 1.5, 1.2, 0.8];
    private static readonly double[,] Subdue = {
        {1.0,1.2,0.8,1.0,0.7,1.3}, {0.8,1.0,1.2,1.0,0.6,1.2}, {1.2,0.8,1.0,1.0,1.1,1.2},
        {1.0,1.0,1.0,1.0,1.0,1.0}, {1.1,1.3,0.9,1.0,1.0,1.5}, {0.6,0.6,0.6,0.6,0.6,0.6}
    };

    public static MainBattleState Begin(MainWorld world, MainContent content, string orderId)
    {
        if (world.ActiveBattle is not null) throw new InvalidOperationException("已有进行中的战斗。");
        var order = world.Orders.Single(item => item.Id == orderId && item.OrderId == MainCommandCatalog.BattleOrderId);
        var source = world.Cities.Single(city => city.Id == order.CityId);
        var target = world.Cities.Single(city => city.Id == order.ObjectId);
        var map = content.Battle.Maps.Single(item => item.CityId == target.Id);
        var defenders = target.PersonIds.Select(id => world.Persons.Single(person => person.Id == id))
            .Where(person => person.Belong == target.Belong && person.Status == MainPersonStatus.Active)
            .OrderByDescending(person => person.Arms).ThenBy(person => person.SourceIndex).Take(10).ToList();
        var way = Math.Max(0, target.Connections.IndexOf(source.Id));
        var state = new MainBattleState
        {
            SourceCityId = source.Id, TargetCityId = target.Id, AttackerRulerId = source.Belong,
            DefenderRulerId = target.Belong, AttackerFood = order.Food, DefenderFood = target.Food
        };
        var attackPositions = content.Battle.InitialPositions[way];
        var defencePositions = content.Battle.InitialPositions[8];
        var (ax, ay) = AttackBase(map, way);
        var dx = map.CityX - 2;
        var dy = map.CityY - 2;
        for (var i = 0; i < order.ParticipantIds.Count; i++)
        {
            var person = world.Persons.Single(person => person.Id == order.ParticipantIds[i]);
            state.Units.Add(CreateUnit(content, person, true, ax + attackPositions[i * 2], ay + attackPositions[i * 2 + 1]));
        }
        for (var i = 0; i < defenders.Count; i++)
        {
            var person = defenders[i];
            target.PersonIds.Remove(person.Id);
            person.CityId = "";
            state.Units.Add(CreateUnit(content, person, false, dx + defencePositions[i * 2], dy + defencePositions[i * 2 + 1]));
        }
        world.ActiveBattle = state;
        world.Orders.Remove(order);
        CheckEnd(world, content, state);
        return state;
    }

    public static IReadOnlyList<string> GetSkills(MainWorld world, MainContent content, string personId)
    {
        var person = world.Persons.Single(item => item.Id == personId);
        var result = new List<string>();
        var special = content.Battle.SpecialSkills[world.Period - 1][person.SourceIndex];
        if (special is not null) result.Add(special);
        if (person.Id == person.Belong) result.Add("skill-029");
        var count = content.Battle.UnitSkillCounts[person.ArmsType] * person.Level / 21 + 1;
        result.AddRange(content.Battle.UnitSkills[person.ArmsType].Take(count));
        return result.Distinct(StringComparer.Ordinal).ToList();
    }

    public static bool Move(MainWorld world, MainContent content, string personId, int x, int y)
    {
        var battle = Required(world);
        var unit = Unit(battle, personId);
        if (!CanAct(unit) || !CanMoveTo(world, content, battle, unit, x, y)) return false;
        unit.X = x;
        unit.Y = y;
        unit.HasActed = true;
        return true;
    }

    public static bool Attack(MainWorld world, MainContent content, string personId, string targetId)
    {
        var battle = Required(world);
        var actor = Unit(battle, personId);
        var target = Unit(battle, targetId);
        if (!CanAct(actor) || target.State == 8 || actor.IsAttacker == target.IsAttacker || !InAttackRange(world, actor, target)) return false;
        ApplyAttack(world, content, battle, actor, target);
        actor.HasActed = true;
        CheckEnd(world, content, battle);
        return true;
    }

    public static bool UseSkill(MainWorld world, MainContent content, string personId, string skillId, string targetId)
    {
        var battle = Required(world);
        var actor = Unit(battle, personId);
        var target = Unit(battle, targetId);
        var skill = content.Skills.SingleOrDefault(item => item.Id == skillId);
        if (!CanAct(actor) || target.State == 8 || skill is null || !GetSkills(world, content, personId).Contains(skillId) || actor.Mp < skill.UseMp) return false;
        if (skill.Aim == 0 && actor.IsAttacker == target.IsAttacker || skill.Aim == 1 && actor.IsAttacker != target.IsAttacker) return false;
        if (skill.Weather[battle.Weather - 1] <= 0) return false;
        actor.Mp -= skill.UseMp;
        var hurt = Scale(world, content, skill.Power, skill, battle, actor, target);
        var destroy = Scale(world, content, skill.Destroy, skill, battle, actor, target);
        var person = world.Persons.Single(person => person.Id == target.PersonId);
        person.Arms = Math.Max(0, person.Arms - hurt);
        if (target.IsAttacker) battle.AttackerFood = Math.Max(0, battle.AttackerFood - destroy);
        else battle.DefenderFood = Math.Max(0, battle.DefenderFood - destroy);
        if (skill.State != 0) target.State = skill.State;
        if (person.Arms == 0) target.State = 8;
        actor.HasActed = true;
        battle.Logs.Add($"{Name(world, actor)}使用{skill.Name}，兵力伤害 {hurt}，粮草伤害 {destroy}。");
        CheckEnd(world, content, battle);
        return true;
    }

    public static bool Wait(MainWorld world, string personId)
    {
        var unit = Unit(Required(world), personId);
        if (!CanAct(unit)) return false;
        unit.HasActed = true;
        return true;
    }

    public static void EndTurn(MainWorld world, MainContent content)
    {
        var battle = Required(world);
        foreach (var defender in battle.Units.Where(unit => !unit.IsAttacker && CanAct(unit)).ToList())
        {
            var targets = battle.Units.Where(unit => unit.IsAttacker && unit.State != 8)
                .OrderBy(target => Distance(defender, target)).ThenBy(target => target.PersonId, StringComparer.Ordinal).ToList();
            if (targets.Count == 0) break;
            var target = targets[0];
            if (!InAttackRange(world, defender, target))
            {
                var x = defender.X + Math.Sign(target.X - defender.X);
                var y = defender.Y + Math.Sign(target.Y - defender.Y);
                if (CanMoveTo(world, content, battle, defender, x, y))
                {
                    defender.X = x;
                    defender.Y = y;
                }
            }
            if (InAttackRange(world, defender, target)) ApplyAttack(world, content, battle, defender, target);
            defender.HasActed = true;
            CheckEnd(world, content, battle);
            if (battle.Result != MainBattleResult.Ongoing) break;
        }
        if (battle.Result != MainBattleResult.Ongoing) return;
        battle.AttackerFood = Math.Max(0, battle.AttackerFood - (int)Math.Sqrt(TotalArms(world, battle, true)) / 3);
        battle.DefenderFood = Math.Max(0, battle.DefenderFood - (int)Math.Sqrt(TotalArms(world, battle, false)) / 3);
        battle.Day++;
        battle.Weather = 1 + MainRandom.Next(world, 5);
        foreach (var unit in battle.Units) unit.HasActed = false;
        CheckEnd(world, content, battle);
    }

    public static void Retreat(MainWorld world)
    {
        var battle = Required(world);
        battle.Result = MainBattleResult.DefenderWon;
        battle.Logs.Add("进攻方全军撤退。");
    }

    public static string Resolve(MainWorld world)
    {
        var battle = Required(world);
        if (battle.Result == MainBattleResult.Ongoing) throw new InvalidOperationException("战斗尚未结束。");
        var city = world.Cities.Single(city => city.Id == battle.TargetCityId);
        var attackersWin = battle.Result == MainBattleResult.AttackerWon;
        var winnerRuler = attackersWin ? battle.AttackerRulerId : battle.DefenderRulerId;
        city.Belong = winnerRuler;
        foreach (var unit in battle.Units.Where(unit => unit.IsAttacker == attackersWin && unit.State != 8))
        {
            var person = world.Persons.Single(person => person.Id == unit.PersonId);
            person.Belong = winnerRuler;
            person.Status = MainPersonStatus.Active;
            person.OrderId = null;
            AddPerson(city, person);
        }
        foreach (var unit in battle.Units.Where(unit => unit.IsAttacker != attackersWin))
        {
            var person = world.Persons.Single(person => person.Id == unit.PersonId);
            person.OrderId = null;
            if (MainRandom.Next(world, 100) > person.Iq)
            {
                Capture(city, person);
                continue;
            }
            var ownCities = world.Cities.Where(item => item.Belong == person.Belong).OrderBy(item => item.SourceIndex).ToList();
            if (ownCities.Count == 0) Capture(city, person);
            else AddPerson(ownCities[MainRandom.Next(world, ownCities.Count)], person);
        }
        city.Farming -= city.Farming / 20;
        city.Commerce -= city.Commerce / 20;
        city.Money -= city.Money / 20;
        city.PeopleDevotion -= city.PeopleDevotion / 10;
        city.Food = battle.AttackerFood + battle.DefenderFood;
        var message = attackersWin ? $"攻下{city.Name}，战斗胜利。" : $"进攻{city.Name}失败，守方获胜。";
        world.Reports.Add(new ReportEntry { Turn = world.Year * 12 + world.Month, Message = message });
        world.ActiveBattle = null;
        return message;
    }

    private static MainBattleUnit CreateUnit(MainContent content, MainPerson person, bool attacker, int x, int y)
    {
        var move = Moves[person.ArmsType] + person.EquipIds.Where(id => id.Length > 0)
            .Select(id => content.Tools.Single(tool => tool.Id == id)).Sum(tool => tool.Move);
        return new MainBattleUnit
        {
            PersonId = person.Id, IsAttacker = attacker, X = x, Y = y, Move = Math.Min(8, move),
            Hp = (person.Force * 80 / 100 + person.Iq * 30 / 100 + person.Level) * person.Thew / 100,
            Mp = (person.Iq * 80 / 100 + (int)Math.Sqrt(person.Force) / 2 + person.Level) * person.Thew / 100
        };
    }

    private static void ApplyAttack(MainWorld world, MainContent content, MainBattleState battle, MainBattleUnit actor, MainBattleUnit target)
    {
        var attacker = world.Persons.Single(person => person.Id == actor.PersonId);
        var defender = world.Persons.Single(person => person.Id == target.PersonId);
        var map = content.Battle.Maps.Single(map => map.CityId == battle.TargetCityId);
        var terrain = map.Terrains[target.Y * map.Width + target.X];
        var at = (int)(attacker.Force * (attacker.Level + 10) * AttackModulus[attacker.ArmsType]);
        var df = (int)(defender.Iq * (defender.Level + 10) * DefenceModulus[defender.ArmsType] * TerrainDefence[terrain]);
        var hurt = (int)(at / (double)Math.Max(1, df) * (attacker.Arms >> 3) * Subdue[attacker.ArmsType, defender.ArmsType]) + 10;
        defender.Arms = Math.Max(0, defender.Arms - hurt);
        target.Hp = Math.Max(0, target.Hp - hurt / 10);
        attacker.Experience = Math.Min(100, attacker.Experience + 2);
        if (attacker.Experience >= 100)
        {
            attacker.Experience = 0;
            attacker.Level = Math.Min(20, attacker.Level + 1);
        }
        if (defender.Arms == 0 || target.Hp == 0) target.State = 8;
        battle.Logs.Add($"{Name(world, actor)}攻击{Name(world, target)}，造成 {hurt} 伤害。");
    }

    private static int Scale(MainWorld world, MainContent content, int value, MainSkill skill, MainBattleState battle, MainBattleUnit actor, MainBattleUnit target)
    {
        var map = content.Battle.Maps.Single(map => map.CityId == battle.TargetCityId);
        var actorTerrain = map.Terrains[actor.Y * map.Width + actor.X];
        var targetTerrain = map.Terrains[target.Y * map.Width + target.X];
        var targetType = world.Persons.Single(person => person.Id == target.PersonId).ArmsType;
        value = value * skill.Weather[battle.Weather - 1] / 100;
        value = value * skill.Earm[targetType] / 100;
        value = value * skill.Eland[targetTerrain] / 100;
        value = value * skill.Oland[actorTerrain] / 100;
        return value;
    }

    private static MainBattleState Required(MainWorld world) => world.ActiveBattle ?? throw new InvalidOperationException("没有进行中的战斗。");
    private static MainBattleUnit Unit(MainBattleState battle, string personId) => battle.Units.Single(unit => unit.PersonId == personId);
    private static bool CanAct(MainBattleUnit unit) => unit.State != 8 && !unit.HasActed;
    private static bool InAttackRange(MainWorld world, MainBattleUnit actor, MainBattleUnit target) =>
        Distance(actor, target) <= (world.Persons.Single(person => person.Id == actor.PersonId).ArmsType == 2 ? 2 : 1);
    private static int Distance(MainBattleUnit a, MainBattleUnit b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    private static string Name(MainWorld world, MainBattleUnit unit) => world.Persons.Single(person => person.Id == unit.PersonId).Name;

    private static bool CanMoveTo(MainWorld world, MainContent content, MainBattleState battle, MainBattleUnit unit, int x, int y)
    {
        if (x < 0 || y < 0 || Math.Abs(unit.X - x) + Math.Abs(unit.Y - y) > unit.Move) return false;
        var map = content.Battle.Maps.Single(map => map.CityId == battle.TargetCityId);
        if (x >= map.Width || y >= map.Height) return false;
        if (battle.Units.Any(other => other.State != 8 && other.X == x && other.Y == y)) return false;
        var terrain = map.Terrains[y * map.Width + x];
        var person = world.Persons.Single(person => person.Id == unit.PersonId);
        return terrain != 7 || person.ArmsType == 3;
    }

    private static void CheckEnd(MainWorld world, MainContent content, MainBattleState battle)
    {
        if (battle.Result != MainBattleResult.Ongoing) return;
        if (!battle.Units.Any(unit => unit.IsAttacker && unit.State != 8)) battle.Result = MainBattleResult.DefenderWon;
        else if (!battle.Units.Any(unit => !unit.IsAttacker && unit.State != 8)) battle.Result = MainBattleResult.AttackerWon;
        else
        {
            var map = content.Battle.Maps.Single(map => map.CityId == battle.TargetCityId);
            if (battle.Units.Any(unit => unit.IsAttacker && unit.State != 8 && unit.X == map.CityX && unit.Y == map.CityY))
                battle.Result = MainBattleResult.AttackerWon;
            else if (battle.AttackerFood <= 0) battle.Result = MainBattleResult.DefenderWon;
            else if (battle.DefenderFood <= 0) battle.Result = MainBattleResult.AttackerWon;
            else if (battle.Day > battle.MaxDays) battle.Result = MainBattleResult.DefenderWon;
        }
    }

    private static int TotalArms(MainWorld world, MainBattleState battle, bool attacker) => battle.Units
        .Where(unit => unit.IsAttacker == attacker && unit.State != 8)
        .Sum(unit => world.Persons.Single(person => person.Id == unit.PersonId).Arms);

    private static (int X, int Y) AttackBase(MainBattleMap map, int way) => way switch
    {
        1 => (map.Width / 2 - 2, 0), 2 => (map.Width - 5, 2), 3 => (map.Width - 5, map.Height / 2 - 2),
        4 => (map.Width / 2 - 2, map.Height - 5), 5 => (2, map.Height - 5), 6 => (2, map.Height / 2 - 2),
        7 => (0, 0), _ => (map.Width - 5, map.Height - 5)
    };

    private static void Capture(MainCity city, MainPerson person)
    {
        person.OldBelong = person.Belong;
        person.Belong = "";
        person.Status = MainPersonStatus.Captive;
        person.Arms = 0;
        AddPerson(city, person);
    }

    private static void AddPerson(MainCity city, MainPerson person)
    {
        person.CityId = city.Id;
        if (!city.PersonIds.Contains(person.Id, StringComparer.Ordinal)) city.PersonIds.Insert(0, person.Id);
    }
}
