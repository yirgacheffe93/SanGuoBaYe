using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainCommandTests : IDisposable
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    private readonly string directory = Path.Combine(Path.GetTempPath(), "main-command-tests-" + Guid.NewGuid());

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Internal_commands_match_acceptance_golden(int commandId)
    {
        var session = CreateSession();
        var result = session.IssueCommand(commandId, "city-005", "person-089");
        Assert.True(result.Accepted);
        var city = City(session);
        var person = Person(session);
        Assert.Equal(commandId == 4 ? 93 : commandId == 3 ? 93 : 43, city.Money);
        Assert.Equal(commandId == 4 ? 100 : 92, person.Thew);
        Assert.Equal("", person.CityId);
        Assert.DoesNotContain(person.Id, city.PersonIds);
        Assert.Equal(4, city.PersonIds.Count);
        var order = Assert.Single(session.World.Orders);
        Assert.Equal(commandId, order.OrderId);
        Assert.Equal(0, order.TimeCount);
        Assert.Equal(1103527590UL, session.World.RandomState);
        switch (commandId)
        {
            case 1: Assert.Equal(1002, city.Farming); break;
            case 2: Assert.Equal(909, city.Commerce); break;
            case 3: Assert.Equal(938, city.Farming); break;
            case 4: Assert.Equal(64, city.AvoidCalamity); break;
            case 5:
                Assert.Equal(70, city.PeopleDevotion);
                Assert.Equal(32325, city.Population);
                break;
        }
    }

    [Fact]
    public void Reclaim_month_end_matches_production_golden()
    {
        var session = CreateSession();
        session.IssueCommand(1, "city-005", "person-089");
        session.ExecuteOrders();
        Assert.Equal(1002, City(session).Farming);
        session.World.RandomState = 648760710;
        session.ConditionUpdate();
        var city = City(session);
        Assert.Equal(190, session.World.Year);
        Assert.Equal(2, session.World.Month);
        Assert.Equal(43, city.Money);
        Assert.Equal(228, city.Food);
        Assert.Equal(1002, city.Farming);
        Assert.Equal(32275, city.Population);
        Assert.Equal(5, city.PersonIds.Count);
        Assert.Equal(96, Person(session).Thew);
        Assert.Empty(session.World.Orders);
        Assert.Equal(1406511986UL, session.World.RandomState);
    }

    [Fact]
    public void Search_month_end_matches_production_golden()
    {
        var session = CreateSession();
        session.IssueCommand(3, "city-005", "person-089");
        session.World.RandomState = 3105560752;
        session.ExecuteOrders();
        Assert.Equal(206, City(session).Money);
        Assert.Equal(204674734UL, session.World.RandomState);
        session.World.RandomState = 3102103705;
        session.ConditionUpdate();
        var city = City(session);
        Assert.Equal(206, city.Money);
        Assert.Equal(228, city.Food);
        Assert.Equal(938, city.Farming);
        Assert.Equal(32275, city.Population);
        Assert.Equal(5, city.PersonIds.Count);
        Assert.Equal(96, Person(session).Thew);
        Assert.Empty(session.World.Orders);
        Assert.Equal(3949672138UL, session.World.RandomState);
    }

    [Fact]
    public void Duplicate_and_invalid_commands_do_not_change_state()
    {
        var session = CreateSession();
        session.IssueCommand(1, "city-005", "person-089");
        var before = session.GetStateHash();
        var duplicate = session.IssueCommand(1, "city-005", "person-089");
        Assert.False(duplicate.Accepted);
        Assert.Equal("invalid_target", duplicate.Code);
        Assert.Equal(before, session.GetStateHash());
        var fresh = CreateSession();
        fresh.World.Cities.Single(city => city.Id == "city-005").AvoidCalamity = 100;
        before = fresh.GetStateHash();
        var govern = fresh.IssueCommand(4, "city-005", "person-089");
        Assert.False(govern.Accepted);
        Assert.Equal("govern_unneeded", govern.Code);
        Assert.Equal(before, fresh.GetStateHash());
    }

    [Fact]
    public void Cancellation_is_explicitly_not_main_compatible()
    {
        var session = CreateSession();
        var order = session.IssueCommand(1, "city-005", "person-089");
        var before = session.GetStateHash();
        var result = session.CancelOrder(order.OrderId!);
        Assert.False(result.Accepted);
        Assert.Equal("not_supported", result.Code);
        Assert.Equal(before, session.GetStateHash());
    }

    [Fact]
    public void Save_before_and_after_month_preserves_execution()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "save.json");
        var continuous = CreateSession();
        continuous.IssueCommand(3, "city-005", "person-089");
        MainSaveService.Save(continuous, path);
        var restored = MainSaveService.Load(path, continuous.Content);
        continuous.World.RandomState = 3105560752;
        restored.World.RandomState = 3105560752;
        continuous.EndMonth(false);
        restored.EndMonth(false);
        Assert.Equal(continuous.GetStateHash(), restored.GetStateHash());
        MainSaveService.Save(restored, path);
        var completed = MainSaveService.Load(path, restored.Content);
        Assert.Empty(completed.World.Orders);
        Assert.Equal(96, completed.World.Persons.Single(person => person.Id == "person-089").Thew);
    }

    private static MainGameSession CreateSession()
    {
        var content = MainContentLoader.Load(Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json"));
        return MainContentLoader.CreateSession(content, 1, "person-013");
    }
    private static MainCity City(MainGameSession session) => session.World.Cities.Single(city => city.Id == "city-005");
    private static MainPerson Person(MainGameSession session) => session.World.Persons.Single(person => person.Id == "person-089");
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
