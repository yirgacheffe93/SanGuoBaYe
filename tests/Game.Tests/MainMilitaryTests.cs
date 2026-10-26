using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainMilitaryTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void Conscript_applies_cost_and_reserve_once()
    {
        var session = CreateSession();
        var city = City(session);
        var person = GuanYu(session);
        city.PeopleDevotion = 50;
        city.Money = 1000;
        city.Food = 234;
        city.Population = 12345;
        city.MothballArms = 100;
        person.Thew = 80;
        var rng = session.World.RandomState;
        var preview = session.PreviewCommand(new MainCommandRequest(24, city.Id, person.Id, Quantity: 1000));
        Assert.Equal(100, preview.MoneyCost);
        var result = session.IssueCommand(new MainCommandRequest(24, city.Id, person.Id, Quantity: 1000));
        Assert.True(result.Accepted);
        Assert.Equal(900, city.Money);
        Assert.Equal(234, city.Food);
        Assert.Equal(12345, city.Population);
        Assert.Equal(1100, city.MothballArms);
        Assert.Equal(68, person.Thew);
        Assert.Equal(rng, session.World.RandomState);
        Assert.Equal(24, Assert.Single(session.World.Orders).OrderId);
        session.ExecuteOrders();
        Assert.Equal(1100, city.MothballArms);
        Assert.Equal(city.Id, person.CityId);
    }

    [Fact]
    public void Distribute_adjusts_person_and_reserve_without_queue()
    {
        var session = CreateSession();
        var city = City(session);
        var person = GuanYu(session);
        person.Arms = 200;
        city.MothballArms = 500;
        var rng = session.World.RandomState;
        var result = session.IssueCommand(new MainCommandRequest(25, city.Id, person.Id, Arms: 700));
        Assert.True(result.Accepted);
        Assert.Equal(700, person.Arms);
        Assert.Equal(0, city.MothballArms);
        Assert.Empty(session.World.Orders);
        Assert.Equal(rng, session.World.RandomState);
    }

    [Fact]
    public void Plunder_applies_resources_and_city_damage()
    {
        var session = CreateSession();
        var city = City(session);
        var person = GuanYu(session);
        person.Iq = 80;
        person.Force = 70;
        city.Money = 2000;
        city.Food = 1000;
        city.PeopleDevotion = 81;
        city.Farming = 101;
        city.Commerce = 201;
        city.Population = 12345;
        city.MothballArms = 77;
        person.Thew = 80;
        session.IssueCommand(new MainCommandRequest(26, city.Id, person.Id));
        session.ExecuteOrders();
        Assert.Equal(2300, city.Money);
        Assert.Equal(1750, city.Food);
        Assert.Equal(40, city.PeopleDevotion);
        Assert.Equal(50, city.Farming);
        Assert.Equal(100, city.Commerce);
        Assert.Equal(12345, city.Population);
        Assert.Equal(77, city.MothballArms);
        Assert.Equal(68, person.Thew);
        Assert.Equal(city.Id, person.CityId);
    }

    private static MainGameSession CreateSession() => MainContentLoader.CreateSession(LoadContent(), 1, "person-013");
    private static MainContent LoadContent() => MainContentLoader.Load(
        Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json"));
    private static MainCity City(MainGameSession session) => session.World.Cities.Single(city => city.Id == "city-005");
    private static MainPerson GuanYu(MainGameSession session) => session.World.Persons.Single(person => person.Id == "person-089");
}
