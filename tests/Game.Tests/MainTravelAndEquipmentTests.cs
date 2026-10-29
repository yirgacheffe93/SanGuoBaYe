using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainTravelAndEquipmentTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void Travel_uses_native_connections_and_coordinates()
    {
        var session = CreateLuBu();
        var route = MainTravelRules.Find(session.World, "city-015", "city-010");
        Assert.NotNull(route);
        Assert.Equal(3, route.Distance);
        Assert.Equal("city-015", route.CityIds[0]);
        Assert.Equal("city-010", route.CityIds[^1]);
        Assert.Equal(3, session.IssueCommand(new MainCommandRequest(14, "city-015", "person-020", TargetCityId: "city-010")).Accepted
            ? Assert.Single(session.World.Orders).TimeCount : -1);
    }

    [Fact]
    public void Disconnected_city_is_rejected_without_cost_or_order()
    {
        var session = CreateLuBu();
        foreach (var city in session.World.Cities.Where(city => city.Id is not "city-015" and not "city-005")) city.Belong = "person-001";
        session.World.Cities.Single(city => city.Id == "city-005").Belong = "person-020";
        var before = session.GetStateHash();
        var result = session.IssueCommand(new MainCommandRequest(14, "city-015", "person-020", TargetCityId: "city-005"));
        Assert.False(result.Accepted);
        Assert.Equal("no_route", result.Code);
        Assert.Equal(before, session.GetStateHash());
        Assert.Empty(session.World.Orders);
    }

    [Fact]
    public void Use_item_consumes_without_occupying_the_available_equipment_slot()
    {
        var session = CreateLiuBei();
        var city = session.World.Cities.Single(city => city.Id == "city-005");
        var person = session.World.Persons.Single(person => person.Id == "person-089");
        person.Tool1Id = "tool-000";
        person.Tool2Id = "";
        person.EquipIds = [person.Tool1Id, person.Tool2Id];
        person.Devotion = 90;
        city.ToolEntries.Add(new MainCityToolEntry { ToolId = "tool-032", Discovered = true });
        var result = session.IssueCommand(new MainCommandRequest(9, city.Id, person.Id, ToolId: "tool-032"));
        Assert.True(result.Accepted);
        Assert.Equal(3, person.ArmsType);
        Assert.Equal(106, person.Force);
        Assert.Equal(81, person.Iq);
        Assert.Equal(98, person.Devotion);
        Assert.Equal(["tool-000", ""], person.EquipIds);
        Assert.DoesNotContain(city.ToolEntries, entry => entry.ToolId == "tool-032");
        session.IssueCommand(new MainCommandRequest(10, city.Id, person.Id));
        Assert.Equal(96, person.Force);
        Assert.Equal(3, person.ArmsType);
    }

    [Fact]
    public void Special_item_threshold_failure_preserves_state()
    {
        var session = CreateLiuBei();
        var city = session.World.Cities.Single(city => city.Id == "city-005");
        city.ToolEntries.Add(new MainCityToolEntry { ToolId = "tool-031", Discovered = true });
        var before = session.GetStateHash();
        var result = session.IssueCommand(new MainCommandRequest(9, city.Id, "person-089", ToolId: "tool-031"));
        Assert.False(result.Accepted);
        Assert.Equal("tool_unusable", result.Code);
        Assert.Equal(before, session.GetStateHash());
    }

    private static MainGameSession CreateLiuBei() => MainContentLoader.CreateSession(LoadContent(), 1, "person-013");

    private static MainGameSession CreateLuBu()
    {
        var content = LoadContent();
        content.Periods[0].RulerIds.Add("person-020");
        var session = MainContentLoader.CreateSession(content, 1, "person-020");
        foreach (var city in session.World.Cities.Where(city => city.Id is "city-015" or "city-010")) city.Belong = "person-020";
        var source = session.World.Cities.Single(city => city.Id == "city-015");
        if (!source.PersonIds.Contains("person-020")) source.PersonIds.Add("person-020");
        var person = session.World.Persons.Single(person => person.Id == "person-020");
        person.CityId = source.Id;
        person.Belong = "person-020";
        return session;
    }

    private static MainContent LoadContent() => MainContentLoader.Load(
        Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json"));
}
