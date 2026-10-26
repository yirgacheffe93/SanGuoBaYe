using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainContentTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void Imports_all_main_periods_without_truncating_attributes()
    {
        var content = Load();
        Assert.Equal(4, content.Periods.Count);
        Assert.Equal(33, content.Tools.Count);
        Assert.Equal(30, content.Skills.Count);
        Assert.All(content.Periods, period =>
        {
            Assert.Equal(38, period.Cities.Count);
            Assert.Equal(200, period.Persons.Count);
        });
        Assert.Equal(18, content.Periods[0].RulerIds.Count);
        Assert.Equal(150, content.Periods.SelectMany(period => period.Persons).Max(person => person.Force));
        Assert.Equal("main-sgby-eba4a77", content.ContentVersion);
    }

    [Fact]
    public void LiuBei_190_matches_main_golden()
    {
        var content = Load();
        var session = MainContentLoader.CreateSession(content, 1, "person-013");
        Assert.Equal(190, session.World.Year);
        Assert.Equal(1, session.World.Month);
        var city = session.World.Cities.Single(city => city.Id == "city-005");
        Assert.Equal("平原", city.Name);
        Assert.Equal("person-013", city.Belong);
        Assert.Equal(938, city.Farming);
        Assert.Equal(2470, city.FarmingLimit);
        Assert.Equal(845, city.Commerce);
        Assert.Equal(2486, city.CommerceLimit);
        Assert.Equal(68, city.PeopleDevotion);
        Assert.Equal(32225, city.Population);
        Assert.Equal(87096, city.PopulationLimit);
        Assert.Equal(93, city.Money);
        Assert.Equal(234, city.Food);
        Assert.Equal(["person-013", "person-089", "person-090", "person-143", "person-172"], city.PersonIds);
        Assert.Equal(85, Person(session, "person-013").Force);
        Assert.Equal(106, Person(session, "person-089").Force);
        Assert.Equal(107, Person(session, "person-090").Force);
        Assert.Equal("青龙刀", content.Tools.Single(tool => tool.Id == "tool-002").Name);
        Assert.Equal("丈八矛", content.Tools.Single(tool => tool.Id == "tool-003").Name);
        Assert.Equal("双股剑", content.Tools.Single(tool => tool.Id == "tool-004").Name);
    }

    private static MainContent Load() => MainContentLoader.Load(
        Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json"));
    private static MainPerson Person(MainGameSession session, string id) => session.World.Persons.Single(person => person.Id == id);
}
