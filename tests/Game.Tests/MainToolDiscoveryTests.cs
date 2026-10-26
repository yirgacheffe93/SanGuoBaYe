using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainToolDiscoveryTests : IDisposable
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    private readonly string directory = Path.Combine(Path.GetTempPath(), "tool-discovery-tests-" + Guid.NewGuid());

    [Fact]
    public void Discovered_tool_is_not_a_candidate_again()
    {
        var session = CreateSession();
        session.IssueCommand(3, "city-001", "person-016");
        session.World.RandomState = 43;
        session.ExecuteOrders();
        var city = City(session);
        Assert.True(city.ToolEntries[0].Discovered);
        session.IssueCommand(3, "city-001", "person-016");
        session.World.RandomState = 43;
        var before = session.World.RandomState;
        session.ExecuteOrders();
        Assert.Equal(ExpectedSpecialSearchState(before), session.World.RandomState);
        Assert.Contains(session.World.Reports, report => report.Message.Contains("没有未发现道具"));
        Assert.True(city.ToolEntries[0].Discovered);
    }

    [Fact]
    public void Duplicate_tools_mark_first_undiscovered_entry_in_queue_order()
    {
        var session = CreateSession();
        var city = City(session);
        city.ToolEntries =
        [
            new MainCityToolEntry { ToolId = "tool-031", Discovered = true },
            new MainCityToolEntry { ToolId = "tool-031", Discovered = false },
            new MainCityToolEntry { ToolId = "tool-031", Discovered = false }
        ];
        session.IssueCommand(3, "city-001", "person-016");
        session.World.RandomState = 43;
        session.ExecuteOrders();
        Assert.Equal([true, true, false], city.ToolEntries.Select(entry => entry.Discovered).ToList());
        session.IssueCommand(3, "city-001", "person-016");
        session.World.RandomState = 43;
        session.ExecuteOrders();
        Assert.Equal([true, true, true], city.ToolEntries.Select(entry => entry.Discovered).ToList());
    }

    [Fact]
    public void Discovery_isolated_to_target_city_and_session()
    {
        var content = LoadContent();
        var session = MainContentLoader.CreateSession(content, 1, "person-016");
        var otherCity = session.World.Cities.Single(city => city.Id == "city-002");
        otherCity.ToolEntries = [new MainCityToolEntry { ToolId = "tool-031" }];
        session.IssueCommand(3, "city-001", "person-016");
        session.World.RandomState = 43;
        session.ExecuteOrders();
        Assert.True(City(session).ToolEntries[0].Discovered);
        Assert.False(otherCity.ToolEntries[0].Discovered);
        Assert.False(content.Periods[0].Cities.Single(city => city.Id == "city-001").ToolEntries[0].Discovered);
        var newSession = MainContentLoader.CreateSession(content, 1, "person-016");
        Assert.False(newSession.World.Cities.Single(city => city.Id == "city-001").ToolEntries[0].Discovered);
    }

    [Fact]
    public void Bole_mismatch_does_not_mark_discovery()
    {
        var content = LoadContent();
        content.Periods[0].ToolConditions[31].Bole = 99;
        var session = MainContentLoader.CreateSession(content, 1, "person-016");
        session.IssueCommand(3, "city-001", "person-016");
        session.World.RandomState = 43;
        session.ExecuteOrders();
        Assert.False(City(session).ToolEntries[0].Discovered);
        Assert.Contains(session.World.Reports, report => report.Message.Contains("伯乐条件不符合"));
    }

    [Fact]
    public void Empty_candidates_do_not_consume_candidate_selection_rng()
    {
        var session = CreateSession();
        City(session).ToolEntries = [];
        session.IssueCommand(3, "city-001", "person-016");
        session.World.RandomState = 43;
        var before = session.World.RandomState;
        session.ExecuteOrders();
        Assert.Equal(ExpectedSpecialSearchState(before), session.World.RandomState);
        Assert.Empty(City(session).ToolEntries);
    }

    [Fact]
    public void Discovery_survives_save_and_continuous_execution()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "discovery.json");
        var session = CreateSession();
        session.IssueCommand(3, "city-001", "person-016");
        session.World.RandomState = 43;
        session.ExecuteOrders();
        MainSaveService.Save(session, path);
        var restored = MainSaveService.Load(path, session.Content);
        Assert.True(restored.World.Cities.Single(city => city.Id == "city-001").ToolEntries[0].Discovered);
        restored.IssueCommand(3, "city-001", "person-016");
        session.IssueCommand(3, "city-001", "person-016");
        restored.EndMonth(false);
        session.EndMonth(false);
        Assert.Equal(session.GetStateHash(), restored.GetStateHash());
    }

    private static MainGameSession CreateSession() => MainContentLoader.CreateSession(LoadContent(), 1, "person-016");
    private static MainContent LoadContent() => MainContentLoader.Load(
        Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json"));
    private static MainCity City(MainGameSession session) => session.World.Cities.Single(city => city.Id == "city-001");
    private static ulong ExpectedSpecialSearchState(ulong state)
    {
        MainRng.NextInt(ref state, 4);
        MainRng.NextInt(ref state, 150);
        return state;
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
