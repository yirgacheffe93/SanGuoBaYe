using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainSaveTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "main-save-tests-" + Guid.NewGuid());

    [Fact]
    public void Main_snapshot_preserves_attributes_above_one_hundred()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "main.json");
        var session = new MainGameSession(CreateWorld(), CreateContent());
        MainSaveService.Save(session, path);
        var restored = MainSaveService.Load(path, session.Content);
        Assert.Equal(106, restored.World.Persons.Single(person => person.Id == "person.guanyu").Force);
        Assert.Equal(session.GetStateHash(), restored.GetStateHash());
        Assert.Same(session.Content, restored.Content);
    }

    [Fact]
    public void Main_snapshot_rejects_tampering()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "main.json");
        var session = new MainGameSession(CreateWorld(), CreateContent());
        MainSaveService.Save(session, path);
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"Money\": 93", "\"Money\": 9999"));
        Assert.Throws<MainSaveException>(() => MainSaveService.Load(path, session.Content));
    }

    [Fact]
    public void Main_snapshot_excludes_static_definitions_and_checks_content_hash()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "main.json");
        var session = new MainGameSession(CreateWorld(), CreateContent());
        MainSaveService.Save(session, path);
        var json = File.ReadAllText(path);
        Assert.DoesNotContain("\"Tools\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Skills\"", json, StringComparison.Ordinal);
        var other = CreateContent();
        other.ContentHash = "different";
        Assert.Throws<MainSaveException>(() => MainSaveService.Load(path, other));
    }

    private static MainContent CreateContent() => new()
    {
        ContentVersion = "test-main",
        ContentHash = "test-hash",
        Tools = [new MainTool { Id = "tool.test", Name = "测试道具" }],
        Skills = [new MainSkill { Id = "skill.test", Name = "测试技能" }]
    };

    private static MainWorld CreateWorld()
    {
        var ruler = new MainPerson { Id = "person.liubei", SourceIndex = 1, Name = "刘备", CityId = "city.pingyuan", Force = 85, Thew = 100 };
        var guanyu = new MainPerson { Id = "person.guanyu", SourceIndex = 2, Name = "关羽", CityId = "city.pingyuan", Force = 106, Thew = 100 };
        ruler.Belong = ruler.Id;
        guanyu.Belong = ruler.Id;
        return new MainWorld
        {
            ContentHash = "test-hash",
            Period = 1, Year = 190, Month = 1, PlayerRulerId = ruler.Id, RandomState = 123456789,
            Cities = [new MainCity { Id = "city.pingyuan", Name = "平原", Belong = ruler.Id, Money = 93, Food = 234,
                Farming = 938, FarmingLimit = 2470, Commerce = 845, CommerceLimit = 2486, Population = 32225,
                PopulationLimit = 87096, PersonIds = [ruler.Id, guanyu.Id] }],
            Persons = [ruler, guanyu]
        };
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
