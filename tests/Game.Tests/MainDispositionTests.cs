using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainDispositionTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void Recruit_first_gate_failure_matches_golden()
    {
        var (session, target) = PrepareRecruit(70);
        session.World.RandomState = 4;
        session.IssueCommand(new MainCommandRequest(6, "city-005", "person-089", TargetPersonId: target.Id));
        session.ExecuteOrders();
        Assert.Equal(MainPersonStatus.Captive, target.Status);
        Assert.Equal(70, target.Devotion);
        Assert.Equal(3731259426UL, session.World.RandomState);
    }

    [Fact]
    public void Recruit_second_gate_failure_matches_golden()
    {
        var (session, target) = PrepareRecruit(50);
        session.World.RandomState = 20;
        session.IssueCommand(new MainCommandRequest(6, "city-005", "person-089", TargetPersonId: target.Id));
        session.ExecuteOrders();
        Assert.Equal(MainPersonStatus.Captive, target.Status);
        Assert.Equal(45, target.Devotion);
        Assert.Equal(2966873603UL, session.World.RandomState);
    }

    [Fact]
    public void Recruit_success_matches_golden()
    {
        var (session, target) = PrepareRecruit(50);
        session.World.RandomState = 3;
        session.IssueCommand(new MainCommandRequest(6, "city-005", "person-089", TargetPersonId: target.Id));
        session.ExecuteOrders();
        Assert.Equal(MainPersonStatus.Active, target.Status);
        Assert.Equal("person-013", target.Belong);
        Assert.Equal(48, target.Devotion);
        Assert.Equal(679304702UL, session.World.RandomState);
        Assert.Equal("city-005", target.CityId);
    }

    [Fact]
    public void Recruit_low_devotion_success_matches_golden()
    {
        var (session, target) = PrepareRecruit(20);
        session.World.RandomState = 5;
        session.IssueCommand(new MainCommandRequest(6, "city-005", "person-089", TargetPersonId: target.Id));
        session.ExecuteOrders();
        Assert.Equal(MainPersonStatus.Active, target.Status);
        Assert.Equal(51, target.Devotion);
        Assert.Equal(695785320UL, session.World.RandomState);
    }

    [Fact]
    public void Execute_preserves_equipment_and_marks_tools_discovered()
    {
        var session = CreateSession();
        var target = session.World.Persons.Single(person => person.Id == "person-172");
        var city = session.World.Cities.Single(city => city.Id == "city-005");
        target.Status = MainPersonStatus.Captive;
        target.Tool1Id = "tool-000";
        target.Tool2Id = "tool-001";
        session.World.RandomState = 11;
        session.IssueCommand(new MainCommandRequest(7, city.Id, target.Id));
        Assert.Equal(MainPersonStatus.Executed, target.Status);
        Assert.Equal("", target.CityId);
        Assert.Equal("tool-000", target.Tool1Id);
        Assert.Equal("tool-001", target.Tool2Id);
        Assert.Equal([true, true], city.ToolEntries.Select(entry => entry.Discovered).ToList());
        Assert.Equal(3548745448UL, session.World.RandomState);
    }

    [Fact]
    public void Exile_moves_person_without_changing_equipment()
    {
        var session = CreateSession();
        var target = session.World.Persons.Single(person => person.Id == "person-172");
        target.Status = MainPersonStatus.Captive;
        target.Tool1Id = "tool-000";
        target.Tool2Id = "tool-001";
        session.World.RandomState = 11;
        session.IssueCommand(new MainCommandRequest(8, "city-005", target.Id));
        Assert.Equal(MainPersonStatus.Free, target.Status);
        Assert.Equal("", target.Belong);
        Assert.Equal("city-002", target.CityId);
        Assert.Equal("tool-000", target.Tool1Id);
        Assert.Equal("tool-001", target.Tool2Id);
        Assert.Equal(3548745448UL, session.World.RandomState);
        Assert.Equal(target.Id, session.World.Cities.Single(city => city.Id == "city-002").PersonIds[0]);
    }

    [Theory]
    [InlineData(MainCommandCatalog.ExecuteOrderId)]
    [InlineData(MainCommandCatalog.ExileOrderId)]
    public void Disposition_rejects_cross_city_target_without_side_effects(int commandId)
    {
        var session = CreateSession();
        var target = session.World.Persons.Single(person => person.Id == "person-000");
        target.Status = MainPersonStatus.Captive;
        var before = session.GetStateHash();
        var randomState = session.World.RandomState;
        var result = session.IssueCommand(new MainCommandRequest(commandId, "city-005", target.Id));
        Assert.False(result.Accepted);
        Assert.Equal("invalid_target", result.Code);
        Assert.Equal(before, session.GetStateHash());
        Assert.Equal(randomState, session.World.RandomState);
    }

    [Theory]
    [InlineData(MainCommandCatalog.ExecuteOrderId)]
    [InlineData(MainCommandCatalog.ExileOrderId)]
    public void Disposition_rejects_city_outside_acting_rulers_control(int commandId)
    {
        var session = CreateSession();
        var city = session.World.Cities.Single(city => city.Id == "city-005");
        var target = session.World.Persons.Single(person => person.Id == "person-172");
        city.Belong = "person-005";
        target.Status = MainPersonStatus.Captive;
        var before = session.GetStateHash();
        var result = session.IssueCommand(new MainCommandRequest(commandId, city.Id, target.Id));
        Assert.False(result.Accepted);
        Assert.Equal("invalid_target", result.Code);
        Assert.Equal(before, session.GetStateHash());
    }

    [Theory]
    [InlineData(MainCommandCatalog.ExecuteOrderId)]
    [InlineData(MainCommandCatalog.ExileOrderId)]
    public void Disposition_rejects_target_missing_from_city_queue(int commandId)
    {
        var session = CreateSession();
        var city = session.World.Cities.Single(city => city.Id == "city-005");
        var target = session.World.Persons.Single(person => person.Id == "person-172");
        target.Status = MainPersonStatus.Captive;
        city.PersonIds.Remove(target.Id);
        var before = session.GetStateHash();
        var result = session.IssueCommand(new MainCommandRequest(commandId, city.Id, target.Id));
        Assert.False(result.Accepted);
        Assert.Equal("invalid_target", result.Code);
        Assert.Equal(before, session.GetStateHash());
    }

    [Theory]
    [InlineData(MainCommandCatalog.ExecuteOrderId)]
    [InlineData(MainCommandCatalog.ExileOrderId)]
    public void Disposition_rejects_target_referenced_by_multiple_cities(int commandId)
    {
        var session = CreateSession();
        var target = session.World.Persons.Single(person => person.Id == "person-172");
        target.Status = MainPersonStatus.Captive;
        session.World.Cities.Single(city => city.Id == "city-004").PersonIds.Add(target.Id);
        var before = session.GetStateHash();
        var result = session.IssueCommand(new MainCommandRequest(commandId, "city-005", target.Id));
        Assert.False(result.Accepted);
        Assert.Equal("invalid_target", result.Code);
        Assert.Equal(before, session.GetStateHash());
    }

    [Fact]
    public void Disposition_rejects_person_occupied_by_order_without_side_effects()
    {
        var session = CreateSession();
        var target = session.World.Persons.Single(person => person.Id == "person-089");
        Assert.True(session.IssueCommand(MainCommandCatalog.ReclaimOrderId, "city-005", target.Id).Accepted);
        var before = session.GetStateHash();
        var randomState = session.World.RandomState;
        var result = session.IssueCommand(new MainCommandRequest(MainCommandCatalog.ExileOrderId, "city-005", target.Id));
        Assert.False(result.Accepted);
        Assert.Equal(before, session.GetStateHash());
        Assert.Equal(randomState, session.World.RandomState);
        Assert.Equal(MainPersonStatus.Active, target.Status);
        Assert.Equal("person-013", target.Belong);
    }

    [Fact]
    public void Person_locations_stay_consistent_across_city_order_and_battle()
    {
        var session = CreateSession();
        Assert.True(session.IssueCommand(MainCommandCatalog.ReclaimOrderId, "city-005", "person-089").Accepted);
        var battleOrder = session.IssueCommand(new MainCommandRequest(
            MainCommandCatalog.BattleOrderId, "city-005", "person-013", TargetCityId: "city-004", Food: 100,
            ParticipantIds: ["person-013"]));
        Assert.True(battleOrder.Accepted);
        session.EnterBattle(battleOrder.OrderId!);
        AssertLocationConsistency(session.World);
        session.World.ActiveBattle!.Result = MainBattleResult.AttackerWon;
        session.ResolveBattle();
        AssertLocationConsistency(session.World);
    }

    private static void AssertLocationConsistency(MainWorld world)
    {
        foreach (var person in world.Persons)
        {
            var cities = world.Cities.Where(city => city.PersonIds.Contains(person.Id, StringComparer.Ordinal)).ToList();
            if (world.ActiveBattle?.Units.Any(unit => unit.PersonId == person.Id) == true)
            {
                Assert.Empty(cities);
                Assert.Equal("", person.CityId);
                continue;
            }
            if (person.OrderId is not null)
            {
                Assert.Contains(world.Orders, order => order.Id == person.OrderId);
                Assert.Empty(cities);
                Assert.Equal("", person.CityId);
                continue;
            }
            if (person.CityId.Length == 0)
            {
                Assert.Empty(cities);
                continue;
            }
            var city = Assert.Single(cities);
            Assert.Equal(person.CityId, city.Id);
        }
    }

    private static (MainGameSession Session, MainPerson Target) PrepareRecruit(int devotion)
    {
        var session = CreateSession();
        var actor = session.World.Persons.Single(person => person.Id == "person-089");
        actor.Iq = 80;
        var target = session.World.Persons.Single(person => person.Id == "person-090");
        target.Iq = 50;
        target.Character = 0;
        target.Status = MainPersonStatus.Captive;
        target.Devotion = devotion;
        return (session, target);
    }

    private static MainGameSession CreateSession() => MainContentLoader.CreateSession(LoadContent(), 1, "person-013");
    private static MainContent LoadContent() => MainContentLoader.Load(
        Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json"));
}
