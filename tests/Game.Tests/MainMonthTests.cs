using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainMonthTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void March_revenue_matches_golden()
    {
        var session = CreateSession();
        session.World.Month = 2;
        session.World.RandomState = 4122348881;
        var city = City(session);
        city.Food = 228;
        city.Population = 32275;
        session.ConditionUpdate();
        Assert.Equal(515, city.Money);
        Assert.Equal(222, city.Food);
        Assert.Equal(32325, city.Population);
        Assert.Equal(61, city.AvoidCalamity);
        Assert.Equal(2604806732UL, session.World.RandomState);
    }

    [Fact]
    public void June_harvest_revenue_then_upkeep_matches_golden()
    {
        var session = CreateSession();
        session.World.Month = 5;
        session.World.RandomState = 2848809862;
        var city = City(session);
        city.Money = 515;
        city.Food = 210;
        city.Population = 32425;
        city.AvoidCalamity = 61;
        session.ConditionUpdate();
        Assert.Equal(937, city.Money);
        Assert.Equal(438, city.Food);
        Assert.Equal(32475, city.Population);
        Assert.Equal(58, city.AvoidCalamity);
    }

    [Fact]
    public void October_harvest_matches_golden()
    {
        var session = CreateSession();
        session.World.Month = 9;
        session.World.RandomState = 91356638;
        var city = City(session);
        city.Money = 1359;
        city.Food = 420;
        city.Population = 32625;
        city.AvoidCalamity = 54;
        session.ConditionUpdate();
        Assert.Equal(1359, city.Money);
        Assert.Equal(648, city.Food);
        Assert.Equal(32675, city.Population);
        Assert.Equal(54, city.AvoidCalamity);
        Assert.Equal(3741420731UL, session.World.RandomState);
    }

    [Fact]
    public void Revenue_and_harvest_respect_30000_precondition()
    {
        var session = CreateSession();
        var city = City(session);
        city.Money = 30000;
        city.Food = 30000;
        session.World.Month = 5;
        session.ConditionUpdate();
        Assert.Equal(30000, city.Money);
        Assert.Equal(29994, city.Food);
    }

    [Fact]
    public void New_year_increases_year_and_all_ages()
    {
        var session = CreateSession();
        session.World.Month = 12;
        var ages = session.World.Persons.ToDictionary(person => person.Id, person => person.Age);
        session.ConditionUpdate();
        Assert.Equal(191, session.World.Year);
        Assert.Equal(1, session.World.Month);
        Assert.All(session.World.Persons, person => Assert.Equal(Math.Min(255, ages[person.Id] + 1), person.Age));
    }

    private static MainGameSession CreateSession()
    {
        var content = MainContentLoader.Load(Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json"));
        return MainContentLoader.CreateSession(content, 1, "person-013");
    }
    private static MainCity City(MainGameSession session) => session.World.Cities.Single(city => city.Id == "city-005");
}
