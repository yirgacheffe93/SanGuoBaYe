using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainSaveContractTests : IDisposable
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"main-save-contract-{Guid.NewGuid()}");
    private readonly MainContent content = LoadContent();

    [Fact]
    public void Current_schema_round_trips_special_text()
    {
        var session = MainContentLoader.CreateSession(content, 1, "person-013");
        session.World.Persons[0].Name = "刘\n\"备\"\\测试";
        session.World.Reports.Add(new ReportEntry { Turn = 2281, Message = "中文\n\"引号\"\\反斜杠" });
        var path = Path.Combine(directory, "round-trip.json");
        MainSaveService.Save(session, path);
        var restored = MainSaveService.Load(path, content);
        Assert.Equal(7, restored.World.SchemaVersion);
        Assert.Equal(session.GetStateHash(), restored.GetStateHash());
        AssertSpecialText(restored.World);
    }

    [Theory]
    [InlineData("9e0053e")]
    [InlineData("47f4790")]
    public void Checksum_uses_logical_json_instead_of_file_layout(string sourceCommit)
    {
        var path = CopyFixture(sourceCommit);
        var envelope = JsonNode.Parse(File.ReadAllText(path))!;
        File.WriteAllText(path, envelope.ToJsonString());
        if (sourceCommit == "47f4790")
        {
            var error = Assert.Throws<MainSaveException>(() => MainSaveService.Load(path, content));
            Assert.Contains("无法可靠推断", error.Message);
            return;
        }
        var restored = MainSaveService.Load(path, content);
        Assert.Equal(7, restored.World.SchemaVersion);
        AssertSpecialText(restored.World);
    }

    [Theory]
    [InlineData("62f4b7b")]
    [InlineData("9e0053e")]
    [InlineData("47f4790")]
    public void Tampering_without_checksum_update_is_rejected(string sourceCommit)
    {
        var path = CopyFixture(sourceCommit);
        var envelope = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        envelope["World"]!["Year"] = 191;
        File.WriteAllText(path, envelope.ToJsonString());
        var error = Assert.Throws<MainSaveException>(() => MainSaveService.Load(path, content));
        Assert.Contains("校验和", error.Message);
    }

    [Fact]
    public void Envelope_and_world_versions_must_match()
    {
        var path = CopyFixture("47f4790");
        var envelope = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        envelope["World"]!["SchemaVersion"] = 5;
        File.WriteAllText(path, envelope.ToJsonString());
        var error = Assert.Throws<MainSaveException>(() => MainSaveService.Load(path, content));
        Assert.Contains("版本不一致", error.Message);
    }

    [Fact]
    public void Envelope_and_world_content_hashes_must_match()
    {
        var path = CopyFixture("47f4790");
        var envelope = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        envelope["World"]!["ContentHash"] = "different";
        File.WriteAllText(path, envelope.ToJsonString());
        Assert.Throws<MainSaveException>(() => MainSaveService.Load(path, content));
    }

    [Theory]
    [InlineData("envelope")]
    [InlineData("world")]
    public void Duplicate_fields_are_rejected(string location)
    {
        var path = CopyFixture("47f4790");
        var json = File.ReadAllText(path);
        var indent = location == "envelope" ? "  " : "    ";
        var property = $"{indent}\"SchemaVersion\": 6,";
        json = json.Replace(property, $"{property}\n{property}");
        File.WriteAllText(path, json);
        var error = Assert.Throws<MainSaveException>(() => MainSaveService.Load(path, content));
        Assert.Contains("重复", error.Message);
    }

    [Fact]
    public void Missing_world_version_is_rejected_before_checksum()
    {
        var path = CopyFixture("47f4790");
        var envelope = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        envelope["World"]!.AsObject().Remove("SchemaVersion");
        File.WriteAllText(path, envelope.ToJsonString());
        var error = Assert.Throws<MainSaveException>(() => MainSaveService.Load(path, content));
        Assert.Contains("缺少", error.Message);
    }

    [Theory]
    [InlineData("SchemaVersion")]
    [InlineData("ContentHash")]
    [InlineData("Checksum")]
    [InlineData("World")]
    public void Missing_required_envelope_fields_are_rejected(string field)
    {
        var path = CopyFixture("47f4790");
        var envelope = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        envelope.Remove(field);
        File.WriteAllText(path, envelope.ToJsonString());
        Assert.Throws<MainSaveException>(() => MainSaveService.Load(path, content));
    }

    [Fact]
    public void Unknown_schema_is_rejected()
    {
        var path = CopyFixture("47f4790");
        var envelope = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        envelope["SchemaVersion"] = 8;
        File.WriteAllText(path, envelope.ToJsonString());
        var error = Assert.Throws<MainSaveException>(() => MainSaveService.Load(path, content));
        Assert.Contains("不支持", error.Message);
    }

    [Fact]
    public void Invalid_reference_is_rejected_after_valid_checksum()
    {
        var session = MainContentLoader.CreateSession(content, 1, "person-013");
        var path = Path.Combine(directory, "invalid-reference.json");
        MainSaveService.Save(session, path);
        var envelope = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var world = envelope["World"]!.AsObject();
        world["Cities"]![0]!["PersonIds"]!.AsArray().Add("missing-person");
        envelope["Checksum"] = Hash(world.ToJsonString());
        File.WriteAllText(path, envelope.ToJsonString());
        Assert.Throws<MainSaveException>(() => MainSaveService.Load(path, content));
    }

    [Fact]
    public void Failed_load_leaves_existing_session_unchanged()
    {
        var session = MainContentLoader.CreateSession(content, 1, "person-013");
        var before = session.GetStateHash();
        var path = CopyFixture("47f4790");
        var envelope = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        envelope["World"]!["Year"] = 191;
        File.WriteAllText(path, envelope.ToJsonString());
        Assert.Throws<MainSaveException>(() => MainSaveService.Load(path, content));
        Assert.Equal(before, session.GetStateHash());
    }

    [Theory]
    [InlineData("expedition", MainTurnPhase.Player)]
    [InlineData("defence", MainTurnPhase.Orders)]
    public void Schema6_battle_restores_deterministic_return_phase(string kind, MainTurnPhase returnPhase)
    {
        var restored = MainSaveService.Load(Fixture($"47f4790-{kind}"), content);
        Assert.Equal(7, restored.World.SchemaVersion);
        Assert.Equal(MainTurnPhase.AwaitBattle, restored.World.TurnPhase);
        Assert.Equal(returnPhase, restored.World.BattleReturnPhase);
        Assert.NotEqual("", restored.World.ActiveBattle!.OrderId);
    }

    [Fact]
    public void Schema6_inprogress_strategic_save_is_explicitly_incompatible()
    {
        var path = Fixture("47f4790");
        var before = File.ReadAllBytes(path);
        var error = Assert.Throws<MainSaveException>(() => MainSaveService.Load(path, content));
        Assert.Contains("无法可靠推断", error.Message);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void Schema6_finished_strategic_save_migrates_without_resume_guessing()
    {
        var path = CopyFixture("47f4790");
        var envelope = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var world = envelope["World"]!.AsObject();
        world["Outcome"] = (int)MainGameOutcome.Won;
        envelope["Checksum"] = Hash(world.ToJsonString());
        File.WriteAllText(path, envelope.ToJsonString());
        var restored = MainSaveService.Load(path, content);
        Assert.Equal(MainGameOutcome.Won, restored.World.Outcome);
        Assert.Equal(MainTurnPhase.Player, restored.World.TurnPhase);
    }

    private string CopyFixture(string sourceCommit)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{sourceCommit}.json");
        File.Copy(Fixture(sourceCommit), path, true);
        return path;
    }

    private static string Fixture(string sourceCommit) => Path.Combine(Root, $"tests/Game.Tests/Fixtures/Saves/schema-{sourceCommit}.json");
    private static MainContent LoadContent() => MainContentLoader.Load(
        Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json"));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static void AssertSpecialText(MainWorld world)
    {
        Assert.Equal("刘\n\"备\"\\测试", world.Persons[0].Name);
        Assert.Equal("中文\n\"引号\"\\反斜杠", Assert.Single(world.Reports).Message);
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
