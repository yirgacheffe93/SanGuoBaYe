using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainFactionTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void Satrap_prefers_ruler_then_highest_iq_and_clears_empty_city()
    {
        var session = CreateSession();
        var city = session.World.Cities[0];
        city.PersonIds.Insert(0, "person-005");
        MainFactionRules.UpdateSatraps(session.World);
        Assert.Equal("person-005", city.SatrapId);
        city.PersonIds.Remove("person-005");
        MainFactionRules.UpdateSatraps(session.World);
        Assert.Equal("person-056", city.SatrapId);
        city.Belong = "";
        MainFactionRules.UpdateSatraps(session.World);
        Assert.Equal("", city.SatrapId);
    }

    [Fact]
    public void Victory_loss_and_finished_month_are_explicit_and_idempotent()
    {
        var session = CreateSession();
        foreach (var city in session.World.Cities.Where(city => city.Id != "city-005")) city.Belong = "";
        Assert.Equal(MainGameOutcome.Won, MainFactionRules.EvaluateOutcome(session.World));
        foreach (var city in session.World.Cities) city.Belong = "person-005";
        Assert.Equal(MainGameOutcome.Lost, MainFactionRules.EvaluateOutcome(session.World));
        session.World.Outcome = MainGameOutcome.Lost;
        var before = session.GetStateHash();
        Assert.Empty(session.EndMonth());
        Assert.Equal(before, session.GetStateHash());
        Assert.Equal("game_over", session.PreviewCommand(1, "city-005", "person-089").Code);
    }

    [Theory]
    [InlineData("62f4b7b")]
    [InlineData("9e0053e")]
    public void Previous_save_schemas_migrate_after_checksum_validation(string sourceCommit)
    {
        var session = CreateSession();
        var path = Path.Combine(Root, $"tests/Game.Tests/Fixtures/Saves/schema-{sourceCommit}.json");
        var restored = MainSaveService.Load(path, session.Content);
        Assert.Equal(7, restored.World.SchemaVersion);
        Assert.Equal(MainGameOutcome.InProgress, restored.World.Outcome);
        Assert.Equal(session.World.RandomState, restored.World.RandomState);
        Assert.Equal("刘\n\"备\"\\测试", restored.World.Persons[0].Name);
        Assert.Equal("中文\n\"引号\"\\反斜杠", Assert.Single(restored.World.Reports).Message);
    }

    private static MainGameSession CreateSession() => MainContentLoader.CreateSession(MainContentLoader.Load(
        Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json")), 1, "person-013");
}
