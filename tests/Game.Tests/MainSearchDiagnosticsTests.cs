using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainSearchDiagnosticsTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void Describes_pingyuan_people_and_beiping_tool_without_changing_rng()
    {
        var content = LoadContent();
        var liuBei = MainContentLoader.CreateSession(content, 1, "person-013");
        var guanYu = liuBei.World.Persons.Single(person => person.Id == "person-089");
        var plain = liuBei.World.Cities.Single(city => city.Id == "city-005");
        var before = liuBei.World.RandomState;
        var people = liuBei.GetSearchDiagnostics(plain.Id, guanYu.Id);
        Assert.Equal(2, people.PersonCandidates);
        Assert.Equal(0, people.ToolCandidates);
        Assert.Equal(before, liuBei.World.RandomState);
        var gongsun = MainContentLoader.CreateSession(content, 1, "person-016");
        var ruler = gongsun.World.Persons.Single(person => person.Id == "person-016");
        var beiping = gongsun.World.Cities.Single(city => city.Id == "city-001");
        var tool = gongsun.GetSearchDiagnostics(beiping.Id, ruler.Id);
        Assert.Equal(1, tool.ToolCandidates);
        Assert.Equal(before, gongsun.World.RandomState);
    }

    [Fact]
    public void Explains_bole_mismatch_and_empty_candidates()
    {
        var content = LoadContent();
        content.Periods[0].ToolConditions[31].Bole = 99;
        var session = MainContentLoader.CreateSession(content, 1, "person-016");
        var ruler = session.World.Persons.Single(person => person.Id == "person-016");
        var city = session.World.Cities.Single(city => city.Id == "city-001");
        var mismatch = session.GetSearchDiagnostics(city.Id, ruler.Id);
        Assert.Contains("伯乐", mismatch.Message);
        city.PersonIds.Clear();
        city.ToolEntries.Clear();
        var empty = session.GetSearchDiagnostics(city.Id, ruler.Id);
        Assert.Equal(0, empty.PersonCandidates);
        Assert.Equal(0, empty.ToolCandidates);
        Assert.Contains("没有可搜寻人物或道具", empty.Message);
    }

    private static MainContent LoadContent() => MainContentLoader.Load(
        Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json"));
}
