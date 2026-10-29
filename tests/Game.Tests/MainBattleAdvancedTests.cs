using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainBattleAdvancedTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void Auto_battle_uses_one_probability_roll_and_shared_resolution()
    {
        var session = CreateSession();
        var source = session.World.Cities[0];
        var target = session.World.Cities[4];
        source.Food = 500;
        var order = new MainOrder
        {
            Id = "auto-1", OrderId = 27, CityId = source.Id, ObjectId = target.Id, PersonId = "person-056",
            ParticipantIds = ["person-056", "person-057"], Food = 100
        };
        session.World.Persons[56].Arms = 1000;
        session.World.Persons[57].Arms = 1000;
        foreach (var person in target.PersonIds.Select(id => session.World.Persons.Single(person => person.Id == id)))
        {
            person.Arms = 10;
            person.Iq = 0;
        }
        session.World.Orders.Add(order);
        var message = MainBattleService.ResolveAuto(session.World, session.Content, order);
        Assert.Contains("攻下", message);
        Assert.Equal(400, source.Food);
        Assert.Equal("person-005", target.Belong);
        Assert.Empty(session.World.Orders);
        Assert.All(target.PersonIds, id => Assert.NotEqual("", session.World.Persons.Single(person => person.Id == id).CityId));
    }

    [Fact]
    public void Ai_attack_on_player_enters_defence_then_continues_month()
    {
        var session = CreateSession();
        var source = session.World.Cities[0];
        source.Food = 500;
        session.World.Orders.Add(new MainOrder
        {
            Id = "defence-1", OrderId = 27, CityId = source.Id, ObjectId = "city-005", PersonId = "person-056",
            ParticipantIds = ["person-056"], Food = 50
        });
        session.EndMonth(false);
        var battle = session.World.ActiveBattle;
        Assert.NotNull(battle);
        Assert.False(battle.PlayerIsAttacker);
        Assert.Equal(450, source.Food);
        MainBattleService.Retreat(session.World);
        Assert.Equal(MainBattleResult.AttackerWon, battle.Result);
        session.ResolveBattle();
        Assert.Equal("person-005", session.World.Cities[5].Belong);
        session.EndMonth(false);
        Assert.Equal(2, session.World.Month);
        Assert.Equal(446, source.Food);
    }

    [Fact]
    public void Skill_hit_applies_state_and_consumes_mp()
    {
        var session = CreateBattle();
        session.Content.Battle.SpecialSkills[0][89] = "skill-013";
        var (actor, target) = PlaceAdjacent(session);
        session.World.Persons[89].Iq = 255;
        session.World.Persons.Single(person => person.Id == target.PersonId).Iq = 0;
        var mp = actor.Mp;
        Assert.True(MainBattleService.UseSkill(session.World, session.Content, actor.PersonId, "skill-013", target.PersonId));
        Assert.Equal(2, target.State);
        Assert.Equal(mp - 15, actor.Mp);
    }

    [Fact]
    public void Healing_is_capped_and_does_not_kill_friendly_unit()
    {
        var session = CreateBattle();
        session.Content.Battle.SpecialSkills[0][89] = "skill-016";
        var (actor, target) = PlaceAdjacent(session, "person-090");
        session.World.Persons[89].Iq = 255;
        session.World.Persons[90].Iq = 0;
        session.World.Persons[90].Arms = 10;
        Assert.True(MainBattleService.UseSkill(session.World, session.Content, actor.PersonId, "skill-016", target.PersonId));
        Assert.Equal(810, session.World.Persons[90].Arms);
        Assert.NotEqual(8, target.State);
    }

    [Fact]
    public void Weather_and_haste_special_skills_change_real_state()
    {
        var weather = CreateBattle();
        weather.Content.Battle.SpecialSkills[0][89] = "skill-021";
        var (actor, _) = PlaceAdjacent(weather, "person-090");
        Assert.True(MainBattleService.UseSkill(weather.World, weather.Content, actor.PersonId, "skill-021", actor.PersonId));
        Assert.InRange(weather.World.ActiveBattle!.Weather, 1, 5);

        var haste = CreateBattle();
        haste.Content.Battle.SpecialSkills[0][89] = "skill-027";
        (actor, _) = PlaceAdjacent(haste, "person-090");
        var move = actor.Move;
        Assert.True(MainBattleService.UseSkill(haste.World, haste.Content, actor.PersonId, "skill-027", actor.PersonId));
        Assert.Equal(Math.Min(8, move + 2), actor.Move);
    }

    [Fact]
    public void Stone_damage_recovery_and_mp_restore_happen_daily()
    {
        var session = CreateBattle();
        var (actor, _) = PlaceAdjacent(session);
        actor.State = 6;
        actor.Mp = 0;
        var person = session.World.Persons[89];
        person.Iq = 0;
        person.Arms = 80;
        foreach (var unit in session.World.ActiveBattle!.Units.Where(unit => unit.IsAttacker != actor.IsAttacker)) unit.HasActed = true;
        MainBattleService.EndTurn(session.World, session.Content);
        Assert.Equal(70, person.Arms);
        Assert.Equal(6, actor.State);
        Assert.Equal(0, actor.Mp);
        Assert.False(actor.HasActed);
    }

    [Fact]
    public void Attack_and_skill_reject_out_of_map_coordinates()
    {
        var session = CreateBattle();
        var (actor, target) = PlaceAdjacent(session);
        target.X = 32;
        Assert.False(MainBattleService.Attack(session.World, session.Content, actor.PersonId, target.PersonId));
        session.Content.Battle.SpecialSkills[0][89] = "skill-004";
        Assert.False(MainBattleService.UseSkill(session.World, session.Content, actor.PersonId, "skill-004", target.PersonId));
    }

    [Fact]
    public void Defensive_save_preserves_player_side_and_rng()
    {
        var session = CreateSession();
        session.World.Orders.Add(new MainOrder
        {
            Id = "defence-save", OrderId = 27, CityId = "city-000", ObjectId = "city-005", PersonId = "person-056",
            ParticipantIds = ["person-056"], Food = 50
        });
        session.EndMonth(false);
        var path = Path.Combine(Path.GetTempPath(), $"main-defence-{Guid.NewGuid()}.json");
        try
        {
            MainSaveService.Save(session, path);
            var restored = MainSaveService.Load(path, session.Content);
            Assert.False(restored.World.ActiveBattle!.PlayerIsAttacker);
            MainBattleService.Retreat(session.World);
            MainBattleService.Retreat(restored.World);
            session.ResolveBattle();
            restored.ResolveBattle();
            Assert.Equal(session.GetStateHash(), restored.GetStateHash());
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private static (MainBattleUnit Actor, MainBattleUnit Target) PlaceAdjacent(MainGameSession session, string? targetId = null)
    {
        var actor = session.World.ActiveBattle!.Units.First(unit => unit.PersonId == "person-089");
        var target = session.World.ActiveBattle.Units.First(unit => unit.PersonId == (targetId ?? session.World.ActiveBattle.Units.First(unit => !unit.IsAttacker).PersonId));
        actor.X = 5;
        actor.Y = 5;
        target.X = 6;
        target.Y = 5;
        return (actor, target);
    }

    private static MainGameSession CreateBattle()
    {
        var session = CreateSession();
        var result = session.IssueCommand(new MainCommandRequest(27, "city-005", "person-089", TargetCityId: "city-004", Food: 100,
            ParticipantIds: ["person-013", "person-089", "person-090"]));
        session.EnterBattle(result.OrderId!);
        return session;
    }

    private static MainGameSession CreateSession() => MainContentLoader.CreateSession(MainContentLoader.Load(
        Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json")), 1, "person-013");
}
