using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainBattlePolishTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Resolution_places_every_winner_including_routed_officers(bool attackerWins)
    {
        var session = CreateBattle();
        var battle = session.World.ActiveBattle!;
        var routed = battle.Units.Single(unit => unit.PersonId == (attackerWins ? "person-056" : "person-013"));
        routed.State = 8;
        var person = session.World.Persons.Single(person => person.Id == routed.PersonId);
        person.Arms = 0;
        person.OrderId = "removed-expedition";
        battle.Result = attackerWins ? MainBattleResult.AttackerWon : MainBattleResult.DefenderWon;
        session.ResolveBattle();
        Assert.Null(person.OrderId);
        Assert.Equal("city-005", person.CityId);
        Assert.Equal(MainPersonStatus.Active, person.Status);
        Assert.Single(session.World.Cities.SelectMany(city => city.PersonIds).Where(id => id == person.Id));
        Assert.Null(session.World.ActiveBattle);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Final_city_battle_updates_outcome_and_satrap_immediately(bool playerWins)
    {
        var session = CreateBattle();
        foreach (var city in session.World.Cities) city.Belong = "";
        session.World.Cities[5].Belong = "person-013";
        session.World.Cities[0].Belong = playerWins ? "" : "person-005";
        session.World.ActiveBattle!.Result = playerWins ? MainBattleResult.DefenderWon : MainBattleResult.AttackerWon;
        session.ResolveBattle();
        Assert.Equal(playerWins ? MainGameOutcome.Won : MainGameOutcome.Lost, session.World.Outcome);
        Assert.NotEmpty(session.World.Cities[5].SatrapId);
        Assert.Single(session.World.Reports.Where(report => report.Message.Contains("战局结束")));
        var hash = session.GetStateHash();
        session.EndMonth();
        Assert.Equal(hash, session.GetStateHash());
    }

    [Fact]
    public void Finished_battle_rejects_all_further_actions_without_mutation()
    {
        var session = CreateBattle();
        session.World.ActiveBattle!.Result = MainBattleResult.DefenderWon;
        var hash = session.GetStateHash();
        Assert.False(MainBattleService.Move(session.World, session.Content, "person-056", 2, 3));
        Assert.False(MainBattleService.Attack(session.World, session.Content, "person-056", "person-013"));
        Assert.False(MainBattleService.UseSkill(session.World, session.Content, "person-056", "skill-003", "person-013"));
        Assert.False(MainBattleService.Wait(session.World, "person-056"));
        MainBattleService.EndTurn(session.World, session.Content);
        MainBattleService.Retreat(session.World);
        session.EndMonth();
        Assert.Equal(hash, session.GetStateHash());
    }

    [Fact]
    public void Entering_city_tile_ends_battle_without_an_extra_attack()
    {
        var session = CreateBattle();
        var map = session.Content.Battle.Maps[5];
        map.CityX = 2; map.CityY = 3;
        Assert.True(MainBattleService.Move(session.World, session.Content, "person-056", 2, 3));
        Assert.Equal(MainBattleResult.AttackerWon, session.World.ActiveBattle!.Result);
    }

    [Fact]
    public void Ai_detours_around_obstacles_within_one_shared_movement_budget()
    {
        var session = CreateBattle();
        var battle = session.World.ActiveBattle!;
        // The defender must travel around a wall to reach the attacking unit.
        var defender = battle.Units.Single(unit => !unit.IsAttacker);
        defender.X = 1; defender.Y = 3;
        var attacker = battle.Units.Single(unit => unit.IsAttacker);
        attacker.X = 7; attacker.Y = 3;
        var map = session.Content.Battle.Maps[5];
        for (var y = 1; y <= 6; y++) map.Terrains[y * 9 + 3] = 2;
        session.Content.Battle.MovementResistance[0][2] = 0xff;
        var allowed = MainBattleService.GetReachableCells(session.World, session.Content, defender.PersonId).Select(cell => (cell.X, cell.Y)).ToHashSet();
        var rng = session.World.RandomState;
        MainBattleService.RunAiTurn(session.World, session.Content, false);
        Assert.Contains((defender.X, defender.Y), allowed);
        Assert.True(defender.Y < 3 || defender.Y > 3);
        Assert.True(defender.HasMoved);
        Assert.True(defender.HasActed);
        Assert.Equal(rng, session.World.RandomState); // Preview and path planning do not roll dice.
    }

    [Fact]
    public void Skill_previews_explain_failure_without_cost_or_rng_and_use_real_range()
    {
        var session = CreateBattle();
        session.Content.Battle.SpecialSkills[0][56] = "skill-003";
        var actor = session.World.ActiveBattle!.Units[0];
        actor.Mp = 0;
        var hash = session.GetStateHash();
        Assert.Contains("技力不足", MainBattleService.SkillFailure(session.World, session.Content, actor.PersonId, "skill-003", "person-013"));
        Assert.Empty(MainBattleService.GetActionRange(session.World, session.Content, actor.PersonId, "skill-003"));
        MainBattleService.GetReachableCells(session.World, session.Content, actor.PersonId);
        Assert.Equal(hash, session.GetStateHash());
    }

    [Fact]
    public void Ai_heals_wounded_self_instead_of_wasting_support_on_healthy_units()
    {
        var session = CreateBattle();
        var actor = session.World.ActiveBattle!.Units.Single(unit => !unit.IsAttacker);
        session.Content.Battle.SpecialSkills[0][13] = "skill-016";
        var person = session.World.Persons[13];
        person.Iq = 200; person.Arms = 10;
        actor.Mp = actor.MaxMp = 80;
        session.World.RandomState = 1;
        MainBattleService.RunAiTurn(session.World, session.Content, false);
        Assert.Equal(810, person.Arms);
        Assert.Equal(65, actor.Mp);
        Assert.True(actor.HasActed);
        Assert.False(actor.HasMoved);
    }

    [Fact]
    public void Ai_surround_uses_allies_around_target_and_awards_only_caster()
    {
        var session = CreateBattle();
        var battle = session.World.ActiveBattle!;
        var actor = battle.Units.Single(unit => !unit.IsAttacker);
        var target = battle.Units.Single(unit => unit.IsAttacker);
        actor.X = 2; target.X = 4;
        actor.Mp = 80;
        session.World.Persons[13].Iq = 200;
        session.World.Persons[56].Iq = 0;
        session.World.Persons[89].Arms = 500;
        battle.Units.Add(new MainBattleUnit { PersonId = "person-089", X = 3, Y = 3, IsAttacker = false, HasActed = true });
        session.Content.Battle.SpecialSkills[0][13] = "skill-026";
        session.World.RandomState = 1;
        MainBattleService.RunAiTurn(session.World, session.Content, false);
        Assert.Equal(490, session.World.Persons[56].Arms);
        Assert.Equal(50, actor.Mp);
        Assert.Equal(1, session.World.Persons[13].Experience);
        Assert.Equal(0, session.World.Persons[89].Experience);
        Assert.False(actor.HasMoved);
    }

    private static MainGameSession CreateBattle()
    {
        var content = MainContentLoader.Load(Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json"));
        var session = MainContentLoader.CreateSession(content, 1, "person-013");
        var map = content.Battle.Maps[5];
        map.Width = map.Height = 9; map.CityX = 8; map.CityY = 8;
        map.Terrains = Enumerable.Repeat(1, 81).ToList(); map.TerrainKinds = Enumerable.Repeat(0, 81).ToList();
        foreach (var row in content.Battle.MovementResistance) row[1] = 0x81;
        foreach (var skills in content.Battle.UnitSkills) skills.Clear();
        content.Battle.SpecialSkills[0][56] = content.Battle.SpecialSkills[0][13] = null;
        foreach (var id in new[] { "person-056", "person-013" })
        {
            var person = session.World.Persons.Single(person => person.Id == id);
            session.World.Cities.ForEach(city => city.PersonIds.Remove(id));
            person.CityId = ""; person.OrderId = null; person.Arms = 500; person.ArmsType = 0;
        }
        session.World.ActiveBattle = new MainBattleState
        {
            SourceCityId = "city-000", TargetCityId = "city-005", AttackerRulerId = "person-005", DefenderRulerId = "person-013",
            PlayerIsAttacker = false, AttackerFood = 5000, DefenderFood = 5000,
            Units = [new MainBattleUnit { PersonId = "person-056", IsAttacker = true, X = 1, Y = 3, Move = 4, BaseMove = 4 },
                     new MainBattleUnit { PersonId = "person-013", IsAttacker = false, X = 7, Y = 3, Move = 4, BaseMove = 4 }]
        };
        return session;
    }
}
