using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainBattleTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void Expedition_validates_and_creates_real_battle_state()
    {
        var session = CreateSession();
        var before = session.World.Cities[5].Food;
        var result = IssueExpedition(session);
        Assert.True(result.Accepted, $"{result.Code}: {result.Message}");
        Assert.Equal(before - 100, session.World.Cities[5].Food);
        var battle = session.EnterBattle(result.OrderId!);
        Assert.Equal(7, battle.Units.Count);
        Assert.Equal(3, battle.Units.Count(unit => unit.IsAttacker));
        Assert.Equal(3, battle.Weather);
        Assert.All(session.World.Orders, order => Assert.NotEqual(27, order.OrderId));
        Assert.Empty(session.World.Cities[4].PersonIds);
        Assert.Empty(session.World.Orders);
        Assert.All(battle.Units, unit => Assert.True(unit.Hp > 0 && unit.Mp > 0 && unit.Move > 0));
        var invalid = session.IssueCommand(new MainCommandRequest(27, "city-005", "person-089", TargetCityId: "city-006", Food: 0));
        Assert.False(invalid.Accepted);
    }

    [Fact]
    public void Move_attack_and_skill_use_actual_units_and_resources()
    {
        var session = CreateBattle();
        var actor = session.World.ActiveBattle!.Units.First(unit => unit.PersonId == "person-089");
        Assert.False(MainBattleService.Move(session.World, session.Content, actor.PersonId, actor.X, actor.Y));
        var destination = FreeNeighbor(session, actor);
        Assert.True(MainBattleService.Move(session.World, session.Content, actor.PersonId, destination.X, destination.Y));
        Assert.True(actor.HasActed);

        var combat = CreateBattle();
        actor = combat.World.ActiveBattle!.Units.First(unit => unit.PersonId == "person-089");
        var target = combat.World.ActiveBattle.Units.First(unit => !unit.IsAttacker);
        actor.X = 5;
        actor.Y = 5;
        target.X = 6;
        target.Y = 5;
        var arms = combat.World.Persons.Single(person => person.Id == target.PersonId).Arms;
        Assert.True(MainBattleService.Attack(combat.World, combat.Content, actor.PersonId, target.PersonId));
        Assert.True(combat.World.Persons.Single(person => person.Id == target.PersonId).Arms < arms);
        Assert.True(actor.HasActed);

        var magical = CreateBattle();
        actor = magical.World.ActiveBattle!.Units.First(unit => unit.PersonId == "person-089");
        target = magical.World.ActiveBattle.Units.First(unit => !unit.IsAttacker);
        actor.X = 5;
        actor.Y = 5;
        target.X = 6;
        target.Y = 5;
        var mp = actor.Mp;
        arms = magical.World.Persons.Single(person => person.Id == target.PersonId).Arms;
        Assert.True(MainBattleService.UseSkill(magical.World, magical.Content, actor.PersonId, "skill-004", target.PersonId));
        Assert.Equal(mp - 15, actor.Mp);
        Assert.True(magical.World.Persons.Single(person => person.Id == target.PersonId).Arms < arms);
        Assert.True(actor.HasActed);
    }

    [Fact]
    public void End_turn_runs_enemy_ai_and_resets_actions()
    {
        var session = CreateBattle();
        var battle = session.World.ActiveBattle!;
        battle.AttackerFood = 100;
        battle.DefenderFood = 100;
        MainBattleService.EndTurn(session.World, session.Content);
        Assert.Equal(1, battle.Day);
        Assert.InRange(battle.Weather, 1, 5);
        Assert.All(battle.Units, unit => Assert.False(unit.HasActed));
    }

    [Fact]
    public void Empty_city_is_captured_and_survivors_return_once()
    {
        var session = CreateSession();
        session.World.Cities[4].Belong = "";
        session.World.Cities[4].PersonIds.Clear();
        var order = IssueExpedition(session);
        session.EnterBattle(order.OrderId!);
        Assert.Equal(MainBattleResult.AttackerWon, session.World.ActiveBattle!.Result);
        var message = session.ResolveBattle();
        Assert.Contains("攻下", message);
        Assert.Null(session.World.ActiveBattle);
        Assert.Equal("person-013", session.World.Cities[4].Belong);
        var participants = session.World.Persons.Where(person => person.Id is "person-013" or "person-089" or "person-090").ToList();
        Assert.All(participants, person =>
        {
            Assert.Equal("city-004", person.CityId);
            Assert.Null(person.OrderId);
            Assert.Equal(1, session.World.Cities[4].PersonIds.Count(id => id == person.Id));
        });
    }

    [Fact]
    public void Retreat_and_midbattle_save_resolve_identically()
    {
        var continuous = CreateBattle();
        var path = Path.Combine(Path.GetTempPath(), $"main-battle-{Guid.NewGuid()}.json");
        try
        {
            MainSaveService.Save(continuous, path);
            var restored = MainSaveService.Load(path, continuous.Content);
            Assert.Equal(continuous.World.ActiveBattle!.Units.Count, restored.World.ActiveBattle!.Units.Count);
            MainBattleService.Retreat(continuous.World);
            MainBattleService.Retreat(restored.World);
            continuous.ResolveBattle();
            restored.ResolveBattle();
            Assert.Equal(continuous.GetStateHash(), restored.GetStateHash());
            Assert.All(continuous.World.Persons.Where(person => person.Id is "person-013" or "person-089" or "person-090"), person => Assert.NotEqual("", person.CityId));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Battle_supports_all_twenty_slots()
    {
        var session = CreateSession();
        var source = session.World.Cities[5];
        var participants = session.World.Persons.Where(person => person.Name.Length > 0).Take(10).ToList();
        foreach (var person in participants)
        {
            person.Belong = "person-013";
            person.Status = MainPersonStatus.Active;
            person.CityId = source.Id;
            person.OrderId = null;
            if (!source.PersonIds.Contains(person.Id)) source.PersonIds.Add(person.Id);
        }
        var target = session.World.Cities[6];
        foreach (var extra in new[] { session.World.Persons[10], session.World.Persons[149] })
        {
            extra.Belong = target.Belong;
            extra.Status = MainPersonStatus.Active;
            extra.CityId = target.Id;
            target.PersonIds.Add(extra.Id);
        }
        var result = session.IssueCommand(new MainCommandRequest(27, source.Id, participants[0].Id,
            TargetCityId: target.Id, Food: 100, ParticipantIds: participants.Select(person => person.Id).ToList()));
        Assert.True(result.Accepted, result.Message);
        var battle = session.EnterBattle(result.OrderId!);
        Assert.True(battle.Units.Count == 20, string.Join(",", battle.Units.Select(unit => unit.PersonId)));
        Assert.Equal(10, battle.Units.Count(unit => unit.IsAttacker));
        Assert.Equal(10, battle.Units.Count(unit => !unit.IsAttacker));
    }

    private static MainGameSession CreateBattle()
    {
        var session = CreateSession();
        session.EnterBattle(IssueExpedition(session).OrderId!);
        return session;
    }

    private static MainCommandResult IssueExpedition(MainGameSession session) => session.IssueCommand(new MainCommandRequest(
        27, "city-005", "person-089", TargetCityId: "city-004", Food: 100,
        ParticipantIds: ["person-013", "person-089", "person-090"]));

    private static (int X, int Y) FreeNeighbor(MainGameSession session, MainBattleUnit actor)
    {
        var battle = session.World.ActiveBattle!;
        var map = session.Content.Battle.Maps.Single(map => map.CityId == battle.TargetCityId);
        for (var y = 0; y < map.Height; y++)
        for (var x = 0; x < map.Width; x++)
            if (Math.Abs(actor.X - x) + Math.Abs(actor.Y - y) <= actor.Move && map.Terrains[y * map.Width + x] != 7 &&
                battle.Units.All(unit => unit.State == 8 || unit.X != x || unit.Y != y)) return (x, y);
        throw new InvalidOperationException("没有可移动格。");
    }

    private static MainGameSession CreateSession() => MainContentLoader.CreateSession(MainContentLoader.Load(
        Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json")), 1, "person-013");
}
