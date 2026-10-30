using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainTurnCoordinatorTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void Defensive_resume_does_not_run_ai_twice()
    {
        var session = CreateSession();
        AddDefensiveOrder(session, "defence-1", "person-056");
        var aiCity = session.World.Cities[1];
        aiCity.AvoidCalamity = 0;
        aiCity.Food = 50;
        session.EndMonth();
        Assert.Equal(1, aiCity.AvoidCalamity);
        Assert.Equal(500, aiCity.Food);
        MainBattleService.Retreat(session.World);
        session.ResolveBattle();
        session.EndMonth();
        Assert.Equal(1, aiCity.AvoidCalamity);
        Assert.Equal(490, aiCity.Food);
        Assert.Equal(2, session.World.Month);
    }

    [Fact]
    public void Paused_month_rejects_new_strategic_commands_without_mutation()
    {
        var session = CreateSession();
        AddDefensiveOrder(session, "defence-1", "person-056");
        session.EndMonth(false);
        var before = session.GetStateHash();
        Assert.Empty(session.EndMonth());
        Assert.Equal(before, session.GetStateHash());
        var result = session.IssueCommand(MainCommandCatalog.ReclaimOrderId, "city-005", "person-089");
        Assert.False(result.Accepted);
        Assert.Equal("invalid_phase", result.Code);
        Assert.Equal(before, session.GetStateHash());
    }

    [Fact]
    public void Repeated_battle_resolution_has_no_effect()
    {
        var session = CreateSession();
        AddDefensiveOrder(session, "defence-1", "person-056");
        session.EndMonth(false);
        MainBattleService.Retreat(session.World);
        session.ResolveBattle();
        var before = session.GetStateHash();
        Assert.Equal("", session.ResolveBattle());
        Assert.Equal(before, session.GetStateHash());
    }

    [Fact]
    public void Multiple_defensive_battles_resume_remaining_orders_once()
    {
        var session = CreateSession();
        Assert.True(session.IssueCommand(MainCommandCatalog.ReclaimOrderId, "city-005", "person-089").Accepted);
        AddDefensiveOrder(session, "defence-1", "person-056");
        AddDefensiveOrder(session, "defence-2", "person-057", "city-006");
        for (var battle = 0; battle < 2; battle++)
        {
            session.EndMonth(false);
            Assert.NotNull(session.World.ActiveBattle);
            MainBattleService.Retreat(session.World);
            session.ResolveBattle();
        }
        session.EndMonth(false);
        Assert.Equal(2, session.World.Month);
        Assert.Null(session.World.ActiveBattle);
        Assert.Empty(session.World.Orders);
        Assert.Equal(96, session.World.Persons.Single(person => person.Id == "person-089").Thew);
    }

    [Fact]
    public void Game_ending_auto_battle_skips_remaining_month_update()
    {
        var session = CreateSession();
        var result = session.IssueCommand(new MainCommandRequest(
            MainCommandCatalog.BattleOrderId, "city-005", "person-013", TargetCityId: "city-004", Food: 100,
            ParticipantIds: ["person-013"]));
        Assert.True(result.Accepted);
        foreach (var city in session.World.Cities) city.Belong = "";
        session.World.Cities.Single(city => city.Id == "city-005").Belong = session.World.PlayerRulerId;
        var target = session.World.Cities.Single(city => city.Id == "city-004");
        target.Belong = "person-005";
        target.PersonIds.Clear();
        session.EndMonth(false);
        Assert.Equal(MainGameOutcome.Won, session.World.Outcome);
        Assert.Equal(1, session.World.Month);
    }

    [Fact]
    public void Active_expedition_returns_to_player_phase()
    {
        var session = CreateSession();
        var result = session.IssueCommand(new MainCommandRequest(
            MainCommandCatalog.BattleOrderId, "city-005", "person-013", TargetCityId: "city-004", Food: 100,
            ParticipantIds: ["person-013"]));
        Assert.True(result.Accepted);
        session.EnterBattle(result.OrderId!);
        Assert.Equal(MainTurnPhase.AwaitBattle, session.World.TurnPhase);
        Assert.Equal(MainTurnPhase.Player, session.World.BattleReturnPhase);
        MainBattleService.Retreat(session.World);
        session.ResolveBattle();
        Assert.Equal(MainTurnPhase.Player, session.World.TurnPhase);
        Assert.Null(session.World.ActiveBattle);
    }

    [Fact]
    public void Orders_checkpoint_save_resume_matches_continuous()
    {
        var continuous = CreateSession();
        Assert.True(continuous.IssueCommand(MainCommandCatalog.ReclaimOrderId, "city-005", "person-089").Accepted);
        AddDefensiveOrder(continuous, "defence-1", "person-056");
        AddDefensiveOrder(continuous, "defence-2", "person-057", "city-006");
        continuous.EndMonth(false);
        MainBattleService.Retreat(continuous.World);
        continuous.ResolveBattle();
        Assert.Equal(MainTurnPhase.Orders, continuous.World.TurnPhase);
        var path = Path.Combine(Path.GetTempPath(), $"main-orders-resume-{Guid.NewGuid()}.json");
        try
        {
            MainSaveService.Save(continuous, path);
            var restored = MainSaveService.Load(path, continuous.Content);
            Assert.Equal(continuous.GetStateHash(), restored.GetStateHash());
            foreach (var session in new[] { continuous, restored })
            {
                session.EndMonth(false);
                Assert.NotNull(session.World.ActiveBattle);
                MainBattleService.Retreat(session.World);
                session.ResolveBattle();
            }
            Assert.Equal(continuous.GetStateHash(), restored.GetStateHash());
            continuous.EndMonth(false);
            restored.EndMonth(false);
            Assert.Equal(2, continuous.World.Month);
            Assert.Equal(continuous.GetStateHash(), restored.GetStateHash());
            Assert.Empty(continuous.World.Orders);
            Assert.Equal(96, continuous.World.Persons.Single(person => person.Id == "person-089").Thew);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Unsafe_intermediate_phase_cannot_be_saved()
    {
        var session = CreateSession();
        session.World.TurnPhase = MainTurnPhase.Ai;
        var path = Path.Combine(Path.GetTempPath(), $"main-unsafe-{Guid.NewGuid()}.json");
        try
        {
            var error = Assert.Throws<MainSaveException>(() => MainSaveService.Save(session, path));
            Assert.Contains("安全阶段", error.Message);
            Assert.False(File.Exists(path));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private static void AddDefensiveOrder(MainGameSession session, string orderId, string personId, string targetCityId = "city-005")
    {
        var person = session.World.Persons.Single(person => person.Id == personId);
        var source = session.World.Cities.Single(city => city.Id == person.CityId);
        source.PersonIds.Remove(person.Id);
        person.CityId = "";
        person.OrderId = orderId;
        session.World.Orders.Add(new MainOrder
        {
            Id = orderId, OrderId = MainCommandCatalog.BattleOrderId, CityId = source.Id, ObjectId = targetCityId,
            PersonId = personId, ParticipantIds = [personId], Food = 50
        });
    }

    private static MainGameSession CreateSession()
    {
        var session = MainContentLoader.CreateSession(LoadContent(), 1, "person-013");
        session.World.Cities[6].Belong = session.World.PlayerRulerId;
        session.World.Cities[11].Belong = session.World.PlayerRulerId;
        return session;
    }
    private static MainContent LoadContent() => MainContentLoader.Load(
        Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json"));
}
