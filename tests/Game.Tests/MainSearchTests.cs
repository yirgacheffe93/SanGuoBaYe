using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainSearchTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void Search_no_result_matches_golden()
    {
        var session = CreateSession();
        session.IssueCommand(3, "city-005", "person-089");
        session.World.RandomState = 5;
        session.ExecuteOrders();
        Assert.Equal(1222621274UL, session.World.RandomState);
        Assert.Equal(93, City(session).Money);
        Assert.Equal(234, City(session).Food);
        AssertReturned(session);
    }

    [Fact]
    public void Special_search_failure_matches_golden()
    {
        var session = CreateSession();
        session.IssueCommand(3, "city-005", "person-089");
        session.World.RandomState = 10;
        session.ExecuteOrders();
        Assert.Equal(1849040536UL, session.World.RandomState);
        Assert.Equal(93, City(session).Money);
        Assert.Equal(234, City(session).Food);
        AssertReturned(session);
    }

    [Fact]
    public void Food_search_matches_golden()
    {
        var session = CreateSession();
        session.IssueCommand(3, "city-005", "person-089");
        session.World.RandomState = 2;
        session.ExecuteOrders();
        Assert.Equal(353, City(session).Food);
        Assert.Equal(1495354192UL, session.World.RandomState);
        AssertReturned(session);
    }

    [Fact]
    public void Person_join_matches_golden()
    {
        var session = CreateSession();
        session.IssueCommand(3, "city-005", "person-089");
        session.World.RandomState = 1;
        session.ExecuteOrders();
        var person = session.World.Persons.Single(person => person.Id == "person-172");
        Assert.Equal("person-013", person.Belong);
        Assert.Equal(91, person.Devotion);
        Assert.Equal(2516284547UL, session.World.RandomState);
        Assert.Equal(93, City(session).Money);
        Assert.Equal(234, City(session).Food);
        AssertReturned(session);
    }

    [Fact]
    public void Alternate_person_join_matches_golden()
    {
        var session = CreateSession();
        session.IssueCommand(3, "city-005", "person-089");
        session.World.RandomState = 38;
        session.ExecuteOrders();
        var person = session.World.Persons.Single(person => person.Id == "person-143");
        Assert.Equal("person-013", person.Belong);
        Assert.Equal(81, person.Devotion);
        Assert.Equal(886320000UL, session.World.RandomState);
        AssertReturned(session);
    }

    [Fact]
    public void Person_reject_matches_golden()
    {
        var session = CreateSession();
        session.IssueCommand(3, "city-005", "person-089");
        session.World.RandomState = 34;
        session.ExecuteOrders();
        var person = session.World.Persons.Single(person => person.Id == "person-172");
        Assert.Equal("", person.Belong);
        Assert.Equal(0, person.Devotion);
        Assert.Equal(1494626063UL, session.World.RandomState);
        AssertReturned(session);
    }

    [Fact]
    public void Tool_search_marks_production_discovery()
    {
        var content = LoadContent();
        var session = MainContentLoader.CreateSession(content, 1, "person-016");
        session.IssueCommand(3, "city-001", "person-016");
        session.World.RandomState = 43;
        session.ExecuteOrders();
        Assert.Equal(1008917062UL, session.World.RandomState);
        var city = session.World.Cities.Single(city => city.Id == "city-001");
        var entry = Assert.Single(city.ToolEntries);
        Assert.Equal("tool-031", entry.ToolId);
        Assert.True(entry.Discovered);
        Assert.Equal(341, city.Money);
        Assert.Equal(355, city.Food);
        Assert.Equal("city-001", session.World.Persons.Single(person => person.Id == "person-016").CityId);
        Assert.Empty(session.World.Orders);
    }

    private static void AssertReturned(MainGameSession session)
    {
        var city = City(session);
        Assert.Equal("person-089", city.PersonIds[0]);
        Assert.Equal(5, city.PersonIds.Count);
        Assert.Equal("city-005", session.World.Persons.Single(person => person.Id == "person-089").CityId);
        Assert.Empty(session.World.Orders);
    }

    private static MainGameSession CreateSession() => MainContentLoader.CreateSession(LoadContent(), 1, "person-013");

    private static MainContent LoadContent() =>
        MainContentLoader.Load(Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json"));

    private static MainCity City(MainGameSession session) => session.World.Cities.Single(city => city.Id == "city-005");
}
