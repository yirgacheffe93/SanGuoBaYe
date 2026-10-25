using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class GameSessionTests
{
    [Fact]
    public void Development_charges_once_and_income_starts_next_turn()
    {
        var session = CreateSession();
        var result = session.IssueDevelopment("city.anling", "char.shenyan", DevelopmentTrack.Agriculture);
        Assert.True(result.Accepted);
        Assert.Equal(200, City(session, "city.anling").Gold);
        Assert.Equal(1, City(session, "city.anling").Agriculture);
        session.EndTurn();
        Assert.Equal(2, City(session, "city.anling").Agriculture);
        Assert.Equal(255, City(session, "city.anling").Gold);
        Assert.Null(Character(session, "char.shenyan").OrderId);
        session.EndTurn();
        Assert.Equal(310, City(session, "city.anling").Gold);
    }

    [Fact]
    public void Insufficient_gold_leaves_state_unchanged()
    {
        var session = CreateSession();
        var before = session.GetStateHash();
        var result = session.IssueDevelopment("city.beiping", "char.luqing", DevelopmentTrack.Commerce);
        Assert.False(result.Accepted);
        Assert.Equal("insufficient_gold", result.Code);
        Assert.Equal(before, session.GetStateHash());
    }

    [Fact]
    public void Character_cannot_receive_two_orders()
    {
        var session = CreateSession();
        session.IssueDevelopment("city.anling", "char.shenyan", DevelopmentTrack.Agriculture);
        var before = session.GetStateHash();
        var result = session.IssueDevelopment("city.anling", "char.shenyan", DevelopmentTrack.Commerce);
        Assert.False(result.Accepted);
        Assert.Equal("character_busy", result.Code);
        Assert.Equal(before, session.GetStateHash());
    }

    [Fact]
    public void Cancel_refunds_and_releases_character()
    {
        var session = CreateSession();
        var result = session.IssueDevelopment("city.anling", "char.shenyan", DevelopmentTrack.Agriculture);
        var cancelled = session.CancelOrder(result.OrderId!);
        Assert.True(cancelled.Accepted);
        Assert.Equal(300, City(session, "city.anling").Gold);
        Assert.Null(Character(session, "char.shenyan").OrderId);
        session.EndTurn();
        Assert.Equal(1, City(session, "city.anling").Agriculture);
    }

    [Fact]
    public void Preview_does_not_change_rng_or_world()
    {
        var session = CreateSession();
        var before = session.GetStateHash();
        var rng = session.World.RandomState;
        session.PreviewDevelopment("city.anling", "char.shenyan", DevelopmentTrack.Agriculture);
        Assert.Equal(before, session.GetStateHash());
        Assert.Equal(rng, session.World.RandomState);
    }

    [Fact]
    public void Same_commands_produce_same_hash()
    {
        var first = CreateSession();
        var second = CreateSession();
        first.IssueDevelopment("city.anling", "char.shenyan", DevelopmentTrack.Agriculture);
        second.IssueDevelopment("city.anling", "char.shenyan", DevelopmentTrack.Agriculture);
        first.EndTurn();
        second.EndTurn();
        Assert.Equal(first.GetStateHash(), second.GetStateHash());
    }

    private static GameSession CreateSession() => new(ContentLoader.CreateWorld(TestContent.Create()));
    private static City City(GameSession session, string id) => session.World.Cities.Single(c => c.Id == id);
    private static Character Character(GameSession session, string id) => session.World.Characters.Single(c => c.Id == id);
}

public sealed class ContentAndSaveTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "sanguobaye-tests-" + Guid.NewGuid());

    [Fact]
    public void Loads_real_spike_content()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../content/spike/scenario.json"));
        var world = ContentLoader.LoadFile(path);
        Assert.Equal(2, world.Cities.Count);
        Assert.Equal(2, world.Characters.Count);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("dangling")]
    [InlineData("negative")]
    public void Invalid_content_is_rejected(string mutation)
    {
        var content = TestContent.Create();
        switch (mutation)
        {
            case "missing": content.ContentVersion = ""; break;
            case "duplicate": content.Cities[1].Id = content.Cities[0].Id; break;
            case "dangling": content.Characters[0].CityId = "city.missing"; break;
            case "negative": content.Cities[0].Gold = -1; break;
        }
        Assert.Throws<ContentException>(() => ContentLoader.CreateWorld(content));
    }

    [Fact]
    public void Save_load_continue_matches_continuous_play()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "save.json");
        var continuous = new GameSession(ContentLoader.CreateWorld(TestContent.Create()));
        continuous.IssueDevelopment("city.anling", "char.shenyan", DevelopmentTrack.Agriculture);
        SaveService.Save(continuous, path);
        var restored = SaveService.Load(path);
        continuous.EndTurn();
        restored.EndTurn();
        continuous.IssueDevelopment("city.anling", "char.shenyan", DevelopmentTrack.Commerce);
        restored.IssueDevelopment("city.anling", "char.shenyan", DevelopmentTrack.Commerce);
        continuous.EndTurn();
        restored.EndTurn();
        Assert.Equal(continuous.GetStateHash(), restored.GetStateHash());
    }

    [Fact]
    public void Corrupt_save_is_rejected_without_touching_existing_file()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "save.json");
        SaveService.Save(new GameSession(ContentLoader.CreateWorld(TestContent.Create())), path);
        var original = File.ReadAllBytes(path);
        File.WriteAllText(path, "{broken");
        Assert.Throws<SaveException>(() => SaveService.Load(path));
        File.WriteAllBytes(path, original);
        Assert.Equal(1, SaveService.Load(path).World.Turn);
    }

    [Fact]
    public void Tampered_save_checksum_is_rejected()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "save.json");
        SaveService.Save(new GameSession(ContentLoader.CreateWorld(TestContent.Create())), path);
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"Gold\": 300", "\"Gold\": 999999"));
        Assert.Throws<SaveException>(() => SaveService.Load(path));
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}

public sealed class BattleTests
{
    [Fact]
    public void Same_seed_reproduces_battle()
    {
        var firstState = 42UL;
        var secondState = 42UL;
        var setup = new BattleSetup(1000, 800, 55, 45, 8);
        var first = BattleResolver.Resolve(setup, ref firstState);
        var second = BattleResolver.Resolve(setup, ref secondState);
        Assert.Equal(first.AttackerWon, second.AttackerWon);
        Assert.Equal(first.Attackers, second.Attackers);
        Assert.Equal(first.Defenders, second.Defenders);
        Assert.Equal(first.Rounds, second.Rounds);
        Assert.Equal(firstState, secondState);
        Assert.NotEmpty(first.Rounds);
    }

    [Fact]
    public void Round_limit_preserves_remaining_attackers()
    {
        var state = 42UL;
        var result = BattleResolver.Resolve(new BattleSetup(1000, 1000, 1, 1, 1), ref state);
        Assert.False(result.AttackerWon);
        Assert.True(result.Attackers > 0);
        Assert.Single(result.Rounds);
    }
}

internal static class TestContent
{
    public static ContentDefinition Create() => new()
    {
        ContentVersion = "test-1", Seed = 123456789, FactionId = "faction.player",
        Rules = new RuleParameters(),
        Cities =
        [
            new City { Id = "city.anling", Name = "安陵", FactionId = "faction.player", Gold = 300, Food = 500,
                Agriculture = 1, Commerce = 1, Troops = 1000, Connections = ["city.beiping"] },
            new City { Id = "city.beiping", Name = "北平", FactionId = "faction.player", Gold = 80, Food = 400,
                Agriculture = 1, Commerce = 1, Troops = 800, Connections = ["city.anling"] }
        ],
        Characters =
        [
            new Character { Id = "char.shenyan", Name = "沈砚", FactionId = "faction.player", CityId = "city.anling" },
            new Character { Id = "char.luqing", Name = "陆青", FactionId = "faction.player", CityId = "city.beiping" }
        ]
    };
}
