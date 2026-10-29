using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainBattleFlowTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void Move_once_then_attack_and_reset_next_turn()
    {
        var session = CreateBattle();
        var (actor, target) = Place(session, 5, 7);
        target.HasActed = true;
        Assert.True(MainBattleService.Move(session.World, session.Content, actor.PersonId, 6, 5));
        Assert.True(actor.HasMoved);
        Assert.False(actor.HasActed);
        Assert.False(MainBattleService.Move(session.World, session.Content, actor.PersonId, 5, 5));
        Assert.True(MainBattleService.Attack(session.World, session.Content, actor.PersonId, target.PersonId));
        Assert.True(actor.HasActed);

        foreach (var unit in session.World.ActiveBattle!.Units.Where(unit => unit.IsAttacker != actor.IsAttacker)) unit.HasActed = true;
        MainBattleService.EndTurn(session.World, session.Content);
        Assert.False(actor.HasMoved);
        Assert.False(actor.HasActed);
    }

    [Fact]
    public void Wait_restores_only_current_unit_mp()
    {
        var session = CreateBattle();
        var (actor, target) = Place(session, 5, 6);
        actor.Mp = 10;
        target.Mp = 10;
        Assert.True(MainBattleService.Wait(session.World, actor.PersonId));
        Assert.Equal(11, actor.Mp);
        Assert.Equal(10, target.Mp);

        foreach (var unit in session.World.ActiveBattle!.Units.Where(unit => unit.IsAttacker != actor.IsAttacker)) unit.HasActed = true;
        MainBattleService.EndTurn(session.World, session.Content);
        Assert.Equal(11, actor.Mp);
        Assert.Equal(10, target.Mp);
    }

    [Fact]
    public void Skill_uses_imported_range_and_rejects_invalid_target_without_cost()
    {
        var session = CreateBattle();
        var (actor, target) = Place(session, 5, 6);
        session.Content.Battle.SpecialSkills[0][89] = "skill-003";
        session.World.Persons[89].Iq = 255;
        session.World.Persons.Single(person => person.Id == target.PersonId).Iq = 0;
        var mp = actor.Mp;
        Assert.False(MainBattleService.UseSkill(session.World, session.Content, actor.PersonId, "skill-003", target.PersonId));
        Assert.Equal(mp, actor.Mp);
        Assert.False(actor.HasActed);

        target.X = 8;
        var skill = session.Content.Skills.Single(skill => skill.Id == "skill-003");
        var map = session.Content.Battle.Maps.Single(map => map.CityId == session.World.ActiveBattle!.TargetCityId);
        var actorTerrain = map.TerrainKinds[actor.Y * map.Width + actor.X];
        var targetTerrain = map.TerrainKinds[target.Y * map.Width + target.X];
        var accepted = MainBattleService.UseSkill(session.World, session.Content, actor.PersonId, "skill-003", target.PersonId);
        Assert.True(accepted, $"range={session.Content.Battle.SkillRanges[3][4 * 9 + 7]}, terrain={actorTerrain}/{targetTerrain}, modifiers={skill.Oland[actorTerrain]}/{skill.Eland[targetTerrain]}/{skill.Earm[session.World.Persons.Single(person => person.Id == target.PersonId).ArmsType]}, skills={string.Join(',', session.GetBattleSkills(actor.PersonId))}");
        Assert.Equal(mp - 30, actor.Mp);
    }

    [Fact]
    public void Healing_can_target_current_unit()
    {
        var session = CreateBattle();
        var (actor, _) = Place(session, 5, 6);
        session.Content.Battle.SpecialSkills[0][89] = "skill-016";
        session.World.Persons[89].Iq = 255;
        session.World.Persons[89].Arms = 10;
        session.World.RandomState = 1;
        Assert.True(MainBattleService.UseSkill(session.World, session.Content, actor.PersonId, "skill-016", actor.PersonId));
        Assert.Equal(810, session.World.Persons[89].Arms);
        Assert.True(actor.HasActed);
    }

    [Fact]
    public void Terrain_kinds_and_occupied_cells_follow_source_data()
    {
        var session = CreateBattle();
        var map = session.Content.Battle.Maps.Single(map => map.CityId == session.World.ActiveBattle!.TargetCityId);
        var cityIndex = map.Terrains.IndexOf(3);
        var hillIndex = map.Terrains.IndexOf(10);
        Assert.Equal(5, map.TerrainKinds[cityIndex]);
        Assert.True(hillIndex >= 0);
        Assert.Equal(2, map.TerrainKinds[hillIndex]);

        var (actor, target) = Place(session, 5, 6);
        Assert.False(MainBattleService.Move(session.World, session.Content, actor.PersonId, target.X, target.Y));
        Assert.False(actor.HasMoved);
    }

    private static (MainBattleUnit Actor, MainBattleUnit Target) Place(MainGameSession session, int actorX, int targetX)
    {
        var actor = session.World.ActiveBattle!.Units.First(unit => unit.PersonId == "person-089");
        var target = session.World.ActiveBattle.Units.First(unit => !unit.IsAttacker);
        actor.X = actorX;
        actor.Y = 5;
        target.X = targetX;
        target.Y = 5;
        return (actor, target);
    }

    private static MainGameSession CreateBattle()
    {
        var session = MainContentLoader.CreateSession(MainContentLoader.Load(
            Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json")), 1, "person-013");
        var result = session.IssueCommand(new MainCommandRequest(27, "city-005", "person-089", TargetCityId: "city-004", Food: 100,
            ParticipantIds: ["person-013", "person-089", "person-090"]));
        session.EnterBattle(result.OrderId!);
        return session;
    }
}
