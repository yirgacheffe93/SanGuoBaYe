using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainBattleGoldenTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void Initialization_matches_production_positions_and_attributes()
    {
        var session = CreateSession();
        var result = session.IssueCommand(new MainCommandRequest(27, "city-005", "person-013", TargetCityId: "city-004", Food: 100,
            ParticipantIds: ["person-013", "person-089", "person-090"]));
        var battle = session.EnterBattle(result.OrderId!);

        AssertUnit(battle, "person-013", 29, 16, 91, 65, 5);
        AssertUnit(battle, "person-089", 29, 17, 109, 70, 4);
        AssertUnit(battle, "person-090", 28, 16, 106, 59, 5);
        AssertUnit(battle, "person-009", 14, 17, 65, 48, 4);
    }

    [Fact]
    public void Normal_attack_matches_production_arms_hp_and_experience()
    {
        var session = CreateDefenceBattle();
        var (actor, target) = PlaceAdjacent(session, "person-056", "person-013");
        session.World.Persons[56].Iq = 255;
        session.World.Persons[13].Iq = 0;

        Assert.True(MainBattleService.Attack(session.World, session.Content, actor.PersonId, target.PersonId));
        Assert.Equal(90, session.World.Persons[13].Arms);
        Assert.Equal(91, target.Hp);
        Assert.Equal(0, target.State);
        Assert.Equal(2, session.World.Persons[56].Experience);
    }

    [Fact]
    public void Skill_hit_matches_production_damage_mp_state_and_experience()
    {
        var session = CreateDefenceBattle();
        var (actor, target) = PlaceAdjacent(session, "person-056", "person-013");
        session.Content.Battle.SpecialSkills[0][56] = "skill-003";
        session.World.Persons[56].Iq = 255;
        session.World.Persons[13].Iq = 0;
        session.World.RandomState = 3228380886;

        Assert.True(MainBattleService.UseSkill(session.World, session.Content, actor.PersonId, "skill-003", target.PersonId));
        Assert.Equal(0, session.World.Persons[13].Arms);
        Assert.Equal(8, target.State);
        Assert.Equal(91, target.Hp);
        Assert.Equal(23, actor.Mp);
        Assert.Equal(2, session.World.Persons[56].Experience);
    }

    [Fact]
    public void Skill_miss_only_consumes_mp_and_matches_production_rng()
    {
        var session = CreateDefenceBattle();
        var (actor, target) = PlaceAdjacent(session, "person-056", "person-013");
        session.Content.Battle.SpecialSkills[0][56] = "skill-003";
        session.World.Persons[56].Iq = 0;
        session.World.Persons[13].Iq = 255;
        session.World.RandomState = 3228380886;

        Assert.True(MainBattleService.UseSkill(session.World, session.Content, actor.PersonId, "skill-003", target.PersonId));
        Assert.Equal(100, session.World.Persons[13].Arms);
        Assert.Equal(0, target.State);
        Assert.Equal(23, actor.Mp);
        Assert.Equal(0, session.World.Persons[56].Experience);
        Assert.Equal(3259389271UL, session.World.RandomState);
    }

    [Fact]
    public void Healing_matches_production_arms_mp_and_experience()
    {
        var session = CreateDefenceBattle();
        var (actor, target) = PlaceAdjacent(session, "person-056", "person-057");
        session.Content.Battle.SpecialSkills[0][56] = "skill-016";
        session.World.Persons[56].Iq = 255;
        session.World.Persons[57].Iq = 0;
        session.World.Persons[57].Arms = 10;
        session.World.RandomState = 3228380886;

        Assert.True(MainBattleService.UseSkill(session.World, session.Content, actor.PersonId, "skill-016", target.PersonId));
        Assert.Equal(810, session.World.Persons[57].Arms);
        Assert.Equal(0, target.State);
        Assert.Equal(38, actor.Mp);
        Assert.Equal(2, session.World.Persons[56].Experience);
    }

    private static void AssertUnit(MainBattleState battle, string personId, int x, int y, int hp, int mp, int move)
    {
        var unit = battle.Units.Single(unit => unit.PersonId == personId);
        Assert.Equal((x, y, hp, mp, move), (unit.X, unit.Y, unit.Hp, unit.Mp, unit.Move));
    }

    private static (MainBattleUnit Actor, MainBattleUnit Target) PlaceAdjacent(MainGameSession session, string actorId, string targetId)
    {
        var actor = session.World.ActiveBattle!.Units.Single(unit => unit.PersonId == actorId);
        var target = session.World.ActiveBattle.Units.Single(unit => unit.PersonId == targetId);
        actor.X = 5;
        actor.Y = 5;
        target.X = 6;
        target.Y = 5;
        return (actor, target);
    }

    private static MainGameSession CreateDefenceBattle()
    {
        var session = CreateSession();
        var order = new MainOrder
        {
            Id = "golden-defence", OrderId = 27, CityId = "city-000", ObjectId = "city-005", PersonId = "person-056",
            ParticipantIds = ["person-056", "person-057"], Food = 100
        };
        session.World.Orders.Add(order);
        MainBattleService.Begin(session.World, session.Content, order.Id);
        return session;
    }

    private static MainGameSession CreateSession() => MainContentLoader.CreateSession(MainContentLoader.Load(
        Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json")), 1, "person-013");
}
