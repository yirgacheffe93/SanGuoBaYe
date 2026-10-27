using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainAiPlannerTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void Interior_and_harmonize_match_production_golden()
    {
        var session = Prepare(3);
        MainAiPlanner.Plan(session.World);
        var city = session.World.Cities[0];
        Assert.Equal(0, city.State);
        Assert.Equal(100, city.AvoidCalamity);
        Assert.Equal(14, city.PeopleDevotion);
        Assert.Equal(200, city.Population);
        Assert.Equal(10000, city.Money);
        Assert.Equal(500, city.Food);
        Assert.Equal([161], city.PersonIds.Select(id => session.World.Persons.Single(person => person.Id == id).SourceIndex));
        Assert.Equal([(3, 56), (5, 57)], session.World.Orders.Select(order => (order.OrderId, session.World.Persons.Single(person => person.Id == order.PersonId).SourceIndex)));
        Assert.Equal(679304702UL, session.World.RandomState);
    }

    [Fact]
    public void Diplomacy_branches_match_production_golden()
    {
        var skipped = Prepare(29);
        MainAiPlanner.Plan(skipped.World);
        Assert.Empty(skipped.World.Orders);
        Assert.Equal(893552736UL, skipped.World.RandomState);

        var ordered = Prepare(80);
        MainAiPlanner.Plan(ordered.World);
        var order = Assert.Single(ordered.World.Orders);
        Assert.Equal(15, order.OrderId);
        Assert.Equal("person-057", order.PersonId);
        Assert.Equal("person-090", order.ObjectId);
        Assert.Equal(946655612UL, ordered.World.RandomState);
        Assert.Equal(["person-056", "person-161"], ordered.World.Cities[0].PersonIds);
    }

    [Fact]
    public void Armament_direct_effect_matches_production_golden()
    {
        var session = Prepare(7);
        MainAiPlanner.Plan(session.World);
        Assert.Empty(session.World.Orders);
        Assert.Equal(1610, session.World.Persons[57].Arms);
        Assert.Equal(100, session.World.Persons[56].Arms);
        Assert.Equal(1486001571UL, session.World.RandomState);
    }

    [Fact]
    public void Command_validation_uses_the_supplied_ruler()
    {
        var session = CreateSession();
        var request = new MainCommandRequest(1, "city-000", "person-056");
        Assert.False(session.IssueCommand(request).Accepted);
        var result = session.IssueCommandForRuler(request, "person-005");
        Assert.True(result.Accepted);
        Assert.Equal(1795, session.World.Cities[0].Farming);
        Assert.Equal(92, session.World.Persons[56].Thew);
        Assert.Single(session.World.Orders);
    }

    private static MainGameSession Prepare(ulong seed)
    {
        var session = CreateSession();
        for (var i = 0; i < session.World.Cities.Count; i++)
        {
            session.World.Cities[i].Belong = i == 0 ? "person-005" : i == 5 ? "person-013" : "";
            session.World.Cities[i].SatrapId = "";
        }
        var city = session.World.Cities[0];
        city.State = 2;
        city.Farming = 100;
        city.FarmingLimit = 1000;
        city.Commerce = 100;
        city.CommerceLimit = 1000;
        city.PeopleDevotion = 10;
        city.AvoidCalamity = 99;
        city.Population = 100;
        city.PopulationLimit = 1000;
        city.Money = 20000;
        city.Food = 50;
        city.MothballArms = 100;
        session.World.RandomState = seed;
        return session;
    }

    private static MainGameSession CreateSession() => MainContentLoader.CreateSession(MainContentLoader.Load(
        Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json")), 1, "person-013");
}
