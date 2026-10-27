using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainAnnualTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void Year_191_matches_production_golden()
    {
        var session = CreateSession();
        session.World.Month = 12;
        session.ConditionUpdate();
        Assert.Equal(191, session.World.Year);
        Assert.Equal(1, session.World.Month);
        Assert.Equal(3025925713UL, session.World.RandomState);
        Assert.Equal("city-013", session.World.Persons[111].CityId);
        Assert.Equal("city-025", session.World.Persons[145].CityId);
        Assert.Equal("city-034", session.World.Persons[169].CityId);
        Assert.Equal("city-034", session.World.Persons[54].CityId);
        Assert.All(new[] { 54, 111, 145, 169 }, index => Assert.Equal(16, session.World.Persons[index].Age));
        Assert.Equal("person-111", session.World.Cities[13].PersonIds[0]);
        Assert.Contains(session.World.Reports, report => report.Message.Contains("出场"));
    }

    [Fact]
    public void Year_192_uses_fixed_city_without_random_draw()
    {
        var session = CreateSession();
        session.World.Year = 191;
        session.World.Month = 12;
        session.ConditionUpdate();
        Assert.Equal("city-004", session.World.Persons[59].CityId);
        Assert.Equal("city-022", session.World.Persons[48].CityId);
        Assert.Equal("city-024", session.World.Persons[114].CityId);
        Assert.Equal(456633834UL, session.World.RandomState);
    }

    [Fact]
    public void Annual_tools_enter_hidden_city_queues_by_birth_year()
    {
        var session = CreateSession();
        session.World.Year = 144;
        MainAnnualRules.Update(session.World, session.Content);
        Assert.Equal(2, session.World.Cities[17].ToolEntries.Count(entry => entry.ToolId is "tool-007" or "tool-008"));
        var tool = Assert.Single(session.World.Cities[28].ToolEntries, entry => entry.ToolId == "tool-023");
        Assert.Equal("tool-023", tool.ToolId);
        Assert.False(tool.Discovered);
        Assert.Equal(46, session.World.Persons[169].Age);
    }

    [Fact]
    public void Annual_save_continues_with_identical_rng_and_state()
    {
        var session = CreateSession();
        session.World.Month = 12;
        var path = Path.Combine(Path.GetTempPath(), $"main-annual-{Guid.NewGuid()}.json");
        try
        {
            MainSaveService.Save(session, path);
            var restored = MainSaveService.Load(path, session.Content);
            session.EndMonth(false);
            restored.EndMonth(false);
            Assert.Equal(session.GetStateHash(), restored.GetStateHash());
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private static MainGameSession CreateSession() => MainContentLoader.CreateSession(MainContentLoader.Load(
        Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json")), 1, "person-013");
}
