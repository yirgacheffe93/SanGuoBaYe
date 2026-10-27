using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainDiplomacyTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void Alienate_success_and_failure_match_production_rng()
    {
        var success = CreateSession();
        Prepare(success, 200, 0, 3);
        success.World.RandomState = 7;
        Assert.True(Issue(success, 15).Accepted);
        success.ExecuteOrders();
        Assert.Equal(0, success.World.Persons[57].Devotion);
        Assert.Equal(712265938UL, success.World.RandomState);
        Assert.Equal("city-005", success.World.Persons[89].CityId);

        var failure = CreateSession();
        Prepare(failure, 0, 255, 100);
        Assert.True(Issue(failure, 15).Accepted);
        failure.ExecuteOrders();
        Assert.Equal(100, failure.World.Persons[57].Devotion);
        Assert.Equal(1103527590UL, failure.World.RandomState);
    }

    [Fact]
    public void Canvass_moves_person_and_matches_loyalty_and_report_rng()
    {
        var session = CreateSession();
        Prepare(session, 255, 0, 0);
        session.World.RandomState = 7;
        Assert.True(Issue(session, 16).Accepted);
        session.ExecuteOrders();
        Assert.Equal("person-013", session.World.Persons[57].Belong);
        Assert.Equal(43, session.World.Persons[57].Devotion);
        Assert.Equal("city-005", session.World.Persons[57].CityId);
        Assert.DoesNotContain("person-057", session.World.Cities[0].PersonIds);
        Assert.Equal(2131988640UL, session.World.RandomState);
        Assert.Equal("person-089", session.World.Cities[5].PersonIds[0]);
        Assert.Equal("person-057", session.World.Cities[5].PersonIds[1]);
    }

    [Fact]
    public void Canvass_rng_zero_and_failure_match_golden()
    {
        var success = CreateSession();
        Prepare(success, 255, 0, 0);
        success.World.RandomState = 63;
        Assert.True(Issue(success, 16).Accepted);
        success.ExecuteOrders();
        Assert.Equal(40, success.World.Persons[57].Devotion);
        Assert.Equal(173759992UL, success.World.RandomState);

        var failure = CreateSession();
        Prepare(failure, 0, 255, 100);
        Assert.True(Issue(failure, 16).Accepted);
        failure.ExecuteOrders();
        Assert.Equal("person-005", failure.World.Persons[57].Belong);
        Assert.Equal("city-000", failure.World.Persons[57].CityId);
        Assert.Equal(2524885223UL, failure.World.RandomState);
    }

    [Fact]
    public void Counterespionage_creates_independent_faction()
    {
        var session = CreateSession();
        Prepare(session, 255, 0, 0);
        session.World.Cities[0].SatrapId = "person-057";
        session.World.RandomState = 7;
        Assert.True(Issue(session, 17).Accepted);
        session.ExecuteOrders();
        Assert.Equal("person-057", session.World.Cities[0].Belong);
        Assert.All(session.World.Cities[0].PersonIds.Select(id => session.World.Persons.Single(person => person.Id == id)).Where(person => person.Belong.Length > 0), person => Assert.Equal("person-057", person.Belong));
        Assert.Equal(2131988640UL, session.World.RandomState);

        var failure = CreateSession();
        Prepare(failure, 0, 255, 100);
        failure.World.Cities[0].SatrapId = "person-057";
        Assert.True(Issue(failure, 17).Accepted);
        failure.ExecuteOrders();
        Assert.Equal("person-005", failure.World.Cities[0].Belong);
        Assert.Equal(662824084UL, failure.World.RandomState);
    }

    [Fact]
    public void Induce_preserves_production_no_effect_boundary()
    {
        var session = CreateSession();
        session.World.RandomState = 7;
        var before = session.World.Cities.Select(city => (city.Id, city.Belong)).ToList();
        var result = Issue(session, 19, "person-005");
        Assert.True(result.Accepted);
        Assert.Equal(43, session.World.Cities[5].Money);
        Assert.Equal(80, session.World.Persons[89].Thew);
        Assert.Equal(10, session.World.Orders.Single().TimeCount);
        session.ExecuteOrders();
        Assert.Equal(7UL, session.World.RandomState);
        Assert.Equal(before, session.World.Cities.Select(city => (city.Id, city.Belong)).ToList());
        Assert.Contains("未产生效果", session.World.Reports[^1].Message);
    }

    [Fact]
    public void Reconnoitre_reports_immediately_and_returns_actor()
    {
        var session = CreateSession();
        var result = session.IssueCommand(new MainCommandRequest(23, "city-005", "person-089", TargetCityId: "city-000"));
        Assert.True(result.Accepted);
        Assert.Contains("侦察西凉", result.Message);
        Assert.Contains("农业", session.World.Reports[^1].Message);
        Assert.Equal(73, session.World.Cities[5].Money);
        Assert.Equal(90, session.World.Persons[89].Thew);
        Assert.Equal(1, session.World.Orders.Single().TimeCount);
        session.ExecuteOrders();
        Assert.Equal("city-005", session.World.Persons[89].CityId);
        Assert.Empty(session.World.Orders);
    }

    [Fact]
    public void Invalid_targets_and_save_continuity_are_safe()
    {
        var session = CreateSession();
        var before = session.GetStateHash();
        var result = session.IssueCommand(new MainCommandRequest(16, "city-005", "person-089", TargetPersonId: "person-090"));
        Assert.False(result.Accepted);
        Assert.Equal("invalid_target", result.Code);
        Assert.Equal(before, session.GetStateHash());
        Assert.Contains("person-056", session.GetCommandTargetPersonIds(17, "city-005"));
        Assert.DoesNotContain("person-005", session.GetCommandTargetPersonIds(17, "city-005"));

        Prepare(session, 255, 0, 0);
        session.World.RandomState = 7;
        Issue(session, 16);
        var path = Path.Combine(Path.GetTempPath(), $"main-diplomacy-{Guid.NewGuid()}.json");
        try
        {
            MainSaveService.Save(session, path);
            var restored = MainSaveService.Load(path, session.Content);
            session.ExecuteOrders();
            restored.ExecuteOrders();
            Assert.Equal(session.GetStateHash(), restored.GetStateHash());
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private static MainCommandResult Issue(MainGameSession session, int command, string target = "person-057") =>
        session.IssueCommand(new MainCommandRequest(command, "city-005", "person-089", TargetPersonId: target));

    private static void Prepare(MainGameSession session, int actorIq, int targetIq, int devotion)
    {
        session.World.Persons[89].Iq = actorIq;
        session.World.Persons[57].Iq = targetIq;
        session.World.Persons[57].Devotion = devotion;
    }

    private static MainGameSession CreateSession() => MainContentLoader.CreateSession(MainContentLoader.Load(
        Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json")), 1, "person-013");
}
