using System.Text.Json;
using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainRewardUiGoldenTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(10)]
    public void Reward_matches_real_native_menu_equipment_and_threshold_rules(int index)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "content/main/golden-reward-ui.json")));
        var fixture = document.RootElement.GetProperty("scenarios")[index];
        var content = MainContentLoader.Load(Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json"));
        var session = MainContentLoader.CreateSession(content, 1, "person-013");
        var person = session.World.Persons[13]; var city = session.World.Cities[5];
        var before = fixture.GetProperty("before"); var after = fixture.GetProperty("after");
        person.Force = before.GetProperty("force").GetInt32(); person.Iq = before.GetProperty("iq").GetInt32();
        person.ArmsType = before.GetProperty("armsType").GetInt32(); person.Devotion = before.GetProperty("devotion").GetInt32();
        var equipment = before.GetProperty("equipment").EnumerateArray().Select(value => value.GetInt32()).ToArray();
        person.Tool1Id = equipment[0] > 0 ? $"tool-{equipment[0] - 1:000}" : "";
        person.Tool2Id = equipment[1] > 0 ? $"tool-{equipment[1] - 1:000}" : "";
        person.EquipIds = [person.Tool1Id, person.Tool2Id];
        var tool = fixture.GetProperty("scenario").GetProperty("tool").GetInt32();
        city.ToolEntries = [new MainCityToolEntry { ToolId = $"tool-{tool:000}", Discovered = true }];
        var hash = session.GetStateHash();
        var result = session.IssueCommand(new MainCommandRequest(9, city.Id, person.Id, ToolId: $"tool-{tool:000}"));
        Assert.Equal(fixture.GetProperty("accepted").GetBoolean(), result.Accepted);
        if (!result.Accepted) Assert.Equal(hash, session.GetStateHash());
        Assert.Equal(after.GetProperty("force").GetInt32(), person.Force);
        Assert.Equal(after.GetProperty("iq").GetInt32(), person.Iq);
        Assert.Equal(after.GetProperty("armsType").GetInt32(), person.ArmsType);
        Assert.Equal(after.GetProperty("devotion").GetInt32(), person.Devotion);
        Assert.Equal(after.GetProperty("equipment").EnumerateArray().Select(value => value.GetInt32()), person.EquipIds.Select(id => id.Length > 0 ? int.Parse(id[5..]) + 1 : 0));
        Assert.Equal(after.GetProperty("tools").GetArrayLength(), city.ToolEntries.Count);
    }
}
