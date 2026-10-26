using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainPeriodTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Theory]
    [InlineData(1, 18, 190)]
    [InlineData(2, 16, 198)]
    [InlineData(3, 11, 208)]
    [InlineData(4, 5, 225)]
    public void Every_period_and_ruler_can_start(int periodId, int rulerCount, int year)
    {
        var content = Load();
        var period = content.Periods.Single(period => period.Period == periodId);
        Assert.Equal(rulerCount, period.RulerIds.Count);
        foreach (var rulerId in period.RulerIds)
        {
            var session = MainContentLoader.CreateSession(content, periodId, rulerId);
            Assert.Equal(year, session.World.Year);
            Assert.Equal(rulerId, session.World.PlayerRulerId);
            Assert.Contains(session.World.Cities, city => city.Belong == rulerId);
        }
    }

    [Fact]
    public void Rulers_follow_city_order()
    {
        var period = Load().Periods[0];
        var names = period.RulerIds.Select(id => period.Persons.Single(person => person.Id == id).Name).ToList();
        Assert.Equal(
            ["马腾", "公孙瓒", "公孙度", "张杨", "刘备", "袁绍", "孔融", "王匡", "董卓", "韩馥", "曹操", "陶谦", "张鲁", "刘岱", "袁术", "刘焉", "刘表", "孙坚"],
            names);
    }

    [Fact]
    public void Main_rng_matches_production_calls()
    {
        var state = 3105560752UL;
        Assert.Equal(1956525574u, MainRng.Next(ref state));
        Assert.Equal(3660534569UL, state);
        Assert.Equal(1061053609u, MainRng.Next(ref state));
        Assert.Equal(204674734UL, state);
    }

    private static MainContent Load() => MainContentLoader.Load(
        Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json"));
}
