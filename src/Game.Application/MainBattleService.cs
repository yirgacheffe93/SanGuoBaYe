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
        var way = Math.Max(0, target.DirectionalConnectionIds.IndexOf(source.Id));
        var state = new MainBattleState
        {
            SourceCityId = source.Id, TargetCityId = target.Id, AttackerRulerId = source.Belong,
            DefenderRulerId = target.Belong, AttackerFood = order.Food, DefenderFood = target.Food,
            PlayerIsAttacker = source.Belong == world.PlayerRulerId
        };
        if (!order.FoodPaid) source.Food = Math.Max(0, source.Food - order.Food);
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

    public static string ResolveAuto(MainWorld world, MainContent content, MainOrder order)
    {
        var source = world.Cities.Single(city => city.Id == order.CityId);
        var target = world.Cities.Single(city => city.Id == order.ObjectId);
        if (!order.FoodPaid) source.Food = Math.Max(0, source.Food - order.Food);
        var defenders = target.PersonIds.Select(id => world.Persons.Single(person => person.Id == id))
            .Where(person => person.Belong == target.Belong && person.Status == MainPersonStatus.Active)
            .OrderByDescending(person => person.Arms).ThenBy(person => person.SourceIndex).Take(10).ToList();
        var state = new MainBattleState
        {
            SourceCityId = source.Id, TargetCityId = target.Id, AttackerRulerId = source.Belong,
            DefenderRulerId = target.Belong, AttackerFood = order.Food, DefenderFood = target.Food,
            PlayerIsAttacker = source.Belong == world.PlayerRulerId
        };
        foreach (var person in order.ParticipantIds.Select(id => world.Persons.Single(person => person.Id == id)))
            state.Units.Add(CreateUnit(content, person, true, 0, 0));
        foreach (var person in defenders)
        {
            target.PersonIds.Remove(person.Id);
            person.CityId = "";
            state.Units.Add(CreateUnit(content, person, false, 0, 0));
        }
        var attackArms = state.Units.Where(unit => unit.IsAttacker).Sum(unit => world.Persons.Single(person => person.Id == unit.PersonId).Arms);
        var defenceArms = state.Units.Where(unit => !unit.IsAttacker).Sum(unit => world.Persons.Single(person => person.Id == unit.PersonId).Arms);
        var roll = MainRandom.Next(world, 101);
        if (attackArms == 0) state.Result = MainBattleResult.DefenderWon;
        else if (defenceArms == 0) state.Result = MainBattleResult.AttackerWon;
        else if (attackArms > defenceArms)
        {
            var threshold = attackArms >> 1 > defenceArms ? 30 : state.AttackerFood > state.DefenderFood ? 40 : 60;
            state.Result = roll < threshold ? MainBattleResult.AttackerWon : MainBattleResult.DefenderWon;
        }
        else
        {
            var threshold = attackArms < defenceArms >> 1 ? 2 : state.AttackerFood > state.DefenderFood ? 30 : 10;
            state.Result = roll > threshold ? MainBattleResult.AttackerWon : MainBattleResult.DefenderWon;
        }
        world.ActiveBattle = state;
        world.Orders.Remove(order);
        return Resolve(world);
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
        if (!CanAct(unit) || unit.HasMoved || !CanMoveTo(world, content, battle, unit, x, y)) return false;
        unit.X = x;
        unit.Y = y;
        unit.HasMoved = true;
        return true;
    }

    public static bool Attack(MainWorld world, MainContent content, string personId, string targetId)
    {
        var battle = Required(world);
        var actor = Unit(battle, personId);
        var target = Unit(battle, targetId);
        if (!CanAct(actor) || target.State == 8 || actor.IsAttacker == target.IsAttacker || !InAttackRange(world, content, actor, target) ||
            !CoordinatesValid(content, battle, actor) || !CoordinatesValid(content, battle, target)) return false;
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
        if (!CanAct(actor) || actor.State == 2 || target.State == 8 || skill is null ||
            !GetSkills(world, content, personId).Contains(skillId) || actor.Mp < skill.UseMp) return false;
        if (skill.Aim == 0 && actor.IsAttacker == target.IsAttacker || skill.Aim == 1 && actor.IsAttacker != target.IsAttacker) return false;
        if (skill.Weather[battle.Weather - 1] <= 0 || !InSkillRange(content, skill, actor, target) ||
            !CoordinatesValid(content, battle, actor) || !CoordinatesValid(content, battle, target)) return false;
        var map = content.Battle.Maps.Single(map => map.CityId == battle.TargetCityId);
        var actorTerrain = map.TerrainKinds[actor.Y * map.Width + actor.X];
        var targetTerrain = map.TerrainKinds[target.Y * map.Width + target.X];
        var targetPerson = world.Persons.Single(person => person.Id == target.PersonId);
        if (skill.Oland[actorTerrain] <= 0 || skill.Eland[targetTerrain] <= 0 || skill.Earm[targetPerson.ArmsType] <= 0) return false;
        var actorPerson = world.Persons.Single(person => person.Id == actor.PersonId);
        actor.Mp -= skill.UseMp;
        var hit = MainRandom.Next(world, targetPerson.Iq + targetPerson.Level + 25) <= (actorPerson.Iq + actorPerson.Level + 5) / 2;
        actor.HasActed = true;
        if (!hit)
        {
            battle.Logs.Add($"{actorPerson.Name}的{skill.Name}未命中。");
            return true;
        }
        switch (skill.SourceIndex)
        {
            case 21:
                battle.Weather = 1 + MainRandom.Next(world, 5);
                battle.Logs.Add($"{actorPerson.Name}改变天气。");
                break;
            case 26:
                foreach (var enemy in battle.Units.Where(unit => unit.IsAttacker != actor.IsAttacker && unit.State != 8 && Distance(actor, unit) <= 1).Take(4))
                    ApplyAttack(world, content, battle, actor, enemy);
                break;
            case 27:
                actor.Move = Math.Min(8, actor.Move + 2);
                battle.Logs.Add($"{actorPerson.Name}移动力提升。");
                break;
            case 29:
                battle.Logs.Add($"谍报{targetPerson.Name}：兵力 {targetPerson.Arms}，HP {target.Hp}，MP {target.Mp}，状态 {target.State}。");
                break;
            default:
                ApplySkillEffect(world, content, battle, actor, target, skill);
                break;
        }
        CheckEnd(world, content, battle);
        return true;
    }

    public static bool Wait(MainWorld world, string personId)
    {
        var unit = Unit(Required(world), personId);
        if (!CanAct(unit)) return false;
        unit.Mp = Math.Min(unit.MaxMp, unit.Mp + 1);
        unit.HasActed = true;
        return true;
    }

    public static void EndTurn(MainWorld world, MainContent content)
    {
        var battle = Required(world);
        foreach (var unit in battle.Units.Where(unit => unit.IsAttacker != battle.PlayerIsAttacker && CanAct(unit)).ToList())
        {
            RunUnitAi(world, content, battle, unit);
            CheckEnd(world, content, battle);
            if (battle.Result != MainBattleResult.Ongoing) return;
        }
        battle.AttackerFood = Math.Max(0, battle.AttackerFood - (int)Math.Sqrt(TotalArms(world, battle, true)) / 3);
        battle.DefenderFood = Math.Max(0, battle.DefenderFood - (int)Math.Sqrt(TotalArms(world, battle, false)) / 3);
        battle.Day++;
        foreach (var unit in battle.Units.Where(unit => unit.State != 8))
        {
            var person = world.Persons.Single(person => person.Id == unit.PersonId);
            var recovered = MainRandom.Next(world, 60) < person.Iq / 2;
            if (unit.State == 6)
            {
                person.Arms = Math.Max(0, person.Arms - (person.Arms >> 3));
                if (recovered) unit.State = 0;
            }
            else if (unit.State is 1 or 2 or 3)
            {
                if (recovered) unit.State = 0;
            }
            else if (unit.State is 4 or 7 && !recovered) unit.State = 0;
            if (person.Arms == 0) unit.State = 8;
            if (unit.State != 3) unit.Move = Math.Min(8, unit.BaseMove == 0 ? unit.Move : unit.BaseMove);
            unit.HasMoved = false;
            unit.HasActed = false;
        }
        battle.Weather = 1 + MainRandom.Next(world, 5);
        CheckEnd(world, content, battle);
    }

    private static void RunUnitAi(MainWorld world, MainContent content, MainBattleState battle, MainBattleUnit actor)
    {
        var targets = battle.Units.Where(unit => unit.IsAttacker != actor.IsAttacker && unit.State != 8)
            .OrderBy(target => Distance(actor, target)).ThenBy(target => target.PersonId, StringComparer.Ordinal).ToList();
        if (targets.Count == 0) return;
        if (actor.State != 2)
        {
            foreach (var skillId in GetSkills(world, content, actor.PersonId))
            {
                var skill = content.Skills.Single(skill => skill.Id == skillId);
                var candidates = skill.Aim == 0 ? targets : battle.Units.Where(unit => unit.IsAttacker == actor.IsAttacker && unit.State != 8).ToList();
                var target = candidates.FirstOrDefault(target => Distance(actor, target) <= 3);
                if (target is not null && actor.Mp >= skill.UseMp && skill.Weather[battle.Weather - 1] > 0 &&
                    UseSkill(world, content, actor.PersonId, skillId, target.PersonId)) return;
            }
        }
        var nearest = targets[0];
        if (!InAttackRange(world, content, actor, nearest))
        {
            for (var step = 0; step < actor.Move; step++)
            {
                var candidates = new[]
                {
                    (X: actor.X + Math.Sign(nearest.X - actor.X), Y: actor.Y),
                    (X: actor.X, Y: actor.Y + Math.Sign(nearest.Y - actor.Y))
                };
                var moved = false;
                foreach (var candidate in candidates.Where(candidate => CanMoveTo(world, content, battle, actor, candidate.X, candidate.Y))
                             .OrderBy(candidate => Math.Abs(nearest.X - candidate.X) + Math.Abs(nearest.Y - candidate.Y)))
                {
                    actor.X = candidate.X;
                    actor.Y = candidate.Y;
                    actor.HasMoved = true;
                    moved = true;
                    break;
                }
                if (!moved) break;
                if (InAttackRange(world, content, actor, nearest)) break;
            }
        }
        if (InAttackRange(world, content, actor, nearest))
        {
            ApplyAttack(world, content, battle, actor, nearest);
            actor.HasActed = true;
        }
        else Wait(world, actor.PersonId);
    }

    public static void Retreat(MainWorld world)
    {
        var battle = Required(world);
        battle.Result = battle.PlayerIsAttacker ? MainBattleResult.DefenderWon : MainBattleResult.AttackerWon;
        battle.Logs.Add("玩家全军撤退。");
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
            PersonId = person.Id, IsAttacker = attacker, X = x, Y = y, Move = Math.Min(8, move), BaseMove = Math.Min(8, move),
            Hp = (person.Force * 80 / 100 + person.Iq * 30 / 100 + person.Level) * person.Thew / 100,
            Mp = (person.Iq * 80 / 100 + (int)Math.Sqrt(person.Force) / 2 + person.Level) * person.Thew / 100,
            MaxMp = (person.Iq * 80 / 100 + (int)Math.Sqrt(person.Force) / 2 + person.Level) * person.Thew / 100
        };
    }

    private static void ApplySkillEffect(MainWorld world, MainContent content, MainBattleState battle, MainBattleUnit actor, MainBattleUnit target, MainSkill skill)
    {
        var hurt = Scale(world, content, skill.Power, skill, battle, actor, target);
        var destroy = Scale(world, content, skill.Destroy, skill, battle, actor, target);
        var person = world.Persons.Single(person => person.Id == target.PersonId);
        if (skill.Aim == 1)
        {
            var maximum = person.Level * 100 + person.Force * 10 + person.Iq * 10;
            person.Arms = Math.Min(maximum, person.Arms + hurt);
        }
        else person.Arms = Math.Max(0, person.Arms - hurt);
        if (target.IsAttacker) battle.AttackerFood = Math.Max(0, battle.AttackerFood - destroy);
        else battle.DefenderFood = Math.Max(0, battle.DefenderFood - destroy);
        if (skill.SourceIndex == 24) target.State = 0;
        else if (skill.State != 0) target.State = skill.State;
        if (target.State == 3) target.Move = 1;
        if (person.Arms == 0 && skill.Aim == 0) target.State = 8;
        AddExperience(world.Persons.Single(person => person.Id == actor.PersonId), 2);
        battle.Logs.Add($"{Name(world, actor)}使用{skill.Name}，兵力变化 {hurt}，粮草变化 {destroy}。");
    }

    private static void ApplyAttack(MainWorld world, MainContent content, MainBattleState battle, MainBattleUnit actor, MainBattleUnit target)
    {
        var attacker = world.Persons.Single(person => person.Id == actor.PersonId);
        var defender = world.Persons.Single(person => person.Id == target.PersonId);
        var map = content.Battle.Maps.Single(map => map.CityId == battle.TargetCityId);
        var actorTerrain = map.TerrainKinds[actor.Y * map.Width + actor.X];
        var targetTerrain = map.TerrainKinds[target.Y * map.Width + target.X];
        var at = LandModified((int)(attacker.Force * (attacker.Level + 10) * AttackModulus[attacker.ArmsType]),
            content.Battle.LandFightModifiers[attacker.ArmsType][actorTerrain]);
        var df = (int)(LandModified((int)(defender.Iq * (defender.Level + 10) * DefenceModulus[defender.ArmsType]),
            content.Battle.LandFightModifiers[defender.ArmsType][targetTerrain]) * TerrainDefence[targetTerrain]);
        var hurt = df == 0 ? 10 : (int)(at / (double)df * (attacker.Arms >> 3) * Subdue[attacker.ArmsType, defender.ArmsType]) + 10;
        defender.Arms = Math.Max(0, defender.Arms - hurt);
        AddExperience(attacker, 2);
        if (defender.Arms == 0) target.State = 8;
        battle.Logs.Add($"{Name(world, actor)}攻击{Name(world, target)}，造成 {hurt} 伤害。");
    }

    private static int LandModified(int value, int modifier)
    {
        if (modifier is >= 0 and <= 3) return value >> modifier;
        modifier = Math.Clamp(modifier, -99, 99);
        return value - value * modifier / 100;
    }

    private static void AddExperience(MainPerson person, int amount)
    {
        person.Experience = Math.Min(100, person.Experience + amount);
        if (person.Experience < 100) return;
        person.Experience = 0;
        person.Level = Math.Min(20, person.Level + 1);
    }

    private static int Scale(MainWorld world, MainContent content, int value, MainSkill skill, MainBattleState battle, MainBattleUnit actor, MainBattleUnit target)
    {
        var map = content.Battle.Maps.Single(map => map.CityId == battle.TargetCityId);
        var actorTerrain = map.TerrainKinds[actor.Y * map.Width + actor.X];
        var targetTerrain = map.TerrainKinds[target.Y * map.Width + target.X];
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

    private static bool InAttackRange(MainWorld world, MainContent content, MainBattleUnit actor, MainBattleUnit target)
    {
        var person = world.Persons.Single(person => person.Id == actor.PersonId);
        return InRange(content.Battle.AttackRanges[person.ArmsType], 2, actor, target);
    }

    private static bool InSkillRange(MainContent content, MainSkill skill, MainBattleUnit actor, MainBattleUnit target) =>
        InRange(content.Battle.SkillRanges[skill.SourceIndex], 4, actor, target);

    private static bool InRange(IReadOnlyList<int> range, int center, MainBattleUnit actor, MainBattleUnit target)
    {
        var dx = target.X - actor.X;
        var dy = target.Y - actor.Y;
        var size = center * 2 + 1;
        if (dx < -center || dx > center || dy < -center || dy > center) return false;
        var value = range[(dy + center) * size + dx + center];
        return value == 1 || dx == 0 && dy == 0 && value == 2;
    }

    private static int Distance(MainBattleUnit a, MainBattleUnit b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    private static string Name(MainWorld world, MainBattleUnit unit) => world.Persons.Single(person => person.Id == unit.PersonId).Name;

    private static bool CoordinatesValid(MainContent content, MainBattleState battle, MainBattleUnit unit)
    {
        var map = content.Battle.Maps.Single(map => map.CityId == battle.TargetCityId);
        return unit.X >= 0 && unit.Y >= 0 && unit.X < map.Width && unit.Y < map.Height;
    }

    private static bool CanMoveTo(MainWorld world, MainContent content, MainBattleState battle, MainBattleUnit unit, int x, int y)
    {
        var map = content.Battle.Maps.Single(map => map.CityId == battle.TargetCityId);
        if (x < 0 || y < 0 || x >= map.Width || y >= map.Height || x == unit.X && y == unit.Y) return false;
        if (battle.Units.Any(other => other.State != 8 && other.X == x && other.Y == y)) return false;
        var person = world.Persons.Single(person => person.Id == unit.PersonId);
        var remaining = new Dictionary<(int X, int Y), int> { [(unit.X, unit.Y)] = unit.Move };
        var queue = new PriorityQueue<(int X, int Y), int>();
        queue.Enqueue((unit.X, unit.Y), -unit.Move);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var power = remaining[current];
            if (current == (x, y)) return true;
            foreach (var next in new[] { (X: current.X - 1, Y: current.Y), (X: current.X + 1, Y: current.Y),
                         (X: current.X, Y: current.Y - 1), (X: current.X, Y: current.Y + 1) })
            {
                if (next.X < 0 || next.Y < 0 || next.X >= map.Width || next.Y >= map.Height) continue;
                if (battle.Units.Any(other => other.State != 8 && other.X == next.X && other.Y == next.Y)) continue;
                var resistance = content.Battle.MovementResistance[person.ArmsType][map.Terrains[next.Y * map.Width + next.X]];
                if (unit.State != 4 && battle.Units.Any(other => other.State != 8 && other.IsAttacker != unit.IsAttacker &&
                    Math.Abs(other.X - next.X) + Math.Abs(other.Y - next.Y) == 1)) resistance |= 0xa0;
                if (resistance < 0x80 || resistance is 0xfe or 0xff) continue;
                int nextPower;
                if (resistance > 0xa0)
                {
                    var threshold = resistance - 0xa0;
                    if (power <= threshold) continue;
                    nextPower = 1;
                }
                else
                {
                    nextPower = power + 0x80 - resistance;
                    if (nextPower <= 0) continue;
                }
                if (remaining.TryGetValue(next, out var previous) && previous >= nextPower) continue;
                remaining[next] = nextPower;
                queue.Enqueue(next, -nextPower);
            }
        }
        return false;
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
        0 => (map.Width / 2 - 2, 0), 1 => (map.Width - 5, 2), 2 => (map.Width - 5, map.Height / 2 - 2),
        3 => (map.Width - 5, map.Height - 5),
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
