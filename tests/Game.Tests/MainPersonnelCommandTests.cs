using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainPersonnelCommandTests : IDisposable
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    private readonly string directory = Path.Combine(Path.GetTempPath(), "main-personnel-tests-" + Guid.NewGuid());

    [Fact]
    public void Banquet_restores_thew_and_devotion_without_queue()
    {
        var session = CreateLiuBei();
        var city = PingYuan(session);
        var person = GuanYu(session);
        city.Money = 1000;
        person.Thew = 20;
        person.Devotion = 97;
        var result = session.IssueCommand(new MainCommandRequest(12, city.Id, person.Id));
        Assert.True(result.Accepted);
        Assert.Equal(900, city.Money);
        Assert.Equal(70, person.Thew);
        Assert.Equal(98, person.Devotion);
        Assert.Equal(city.Id, person.CityId);
        Assert.Empty(session.World.Orders);
    }

    [Fact]
    public void Reward_and_confiscate_preserve_tools_and_attributes()
    {
        var session = CreateLiuBei();
        var city = PingYuan(session);
        var person = GuanYu(session);
        person.Devotion = 95;
        city.ToolEntries.Add(new MainCityToolEntry { ToolId = "tool-000", Discovered = true });
        city.ToolEntries.Add(new MainCityToolEntry { ToolId = "tool-000", Discovered = true });
        var reward = session.IssueCommand(new MainCommandRequest(9, city.Id, person.Id, ToolId: "tool-000"));
        Assert.True(reward.Accepted);
        Assert.Equal(116, person.Force);
        Assert.Equal(100, person.Devotion);
        Assert.Equal("tool-000", person.Tool2Id);
        Assert.Single(city.ToolEntries);
        person.Devotion = 15;
        person.Tool1Id = "";
        var confiscate = session.IssueCommand(new MainCommandRequest(10, city.Id, person.Id));
        Assert.True(confiscate.Accepted);
        Assert.Equal(106, person.Force);
        Assert.Equal(0, person.Devotion);
        Assert.Equal("", person.Tool1Id);
        Assert.Equal("", person.Tool2Id);
        Assert.Equal(2, city.ToolEntries.Count);
        Assert.All(city.ToolEntries, entry => Assert.True(entry.Discovered));
    }

    [Fact]
    public void Confiscate_qinglong_matches_golden()
    {
        var session = CreateLiuBei();
        var person = GuanYu(session);
        person.Devotion = 15;
        var result = session.IssueCommand(new MainCommandRequest(10, PingYuan(session).Id, person.Id));
        Assert.True(result.Accepted);
        Assert.Equal(98, person.Force);
        Assert.Equal(0, person.Devotion);
        Assert.Equal("", person.Tool1Id);
        Assert.Equal("", person.Tool2Id);
        var entry = Assert.Single(PingYuan(session).ToolEntries);
        Assert.Equal("tool-002", entry.ToolId);
        Assert.True(entry.Discovered);
    }

    [Fact]
    public void Trade_buy_and_sell_follow_main_ratios()
    {
        var session = CreateLiuBei();
        var city = PingYuan(session);
        var person = GuanYu(session);
        city.Money = 1000;
        city.Food = 234;
        person.Thew = 80;
        session.IssueCommand(new MainCommandRequest(11, city.Id, person.Id, Quantity: 200));
        Assert.Equal(0, city.Money);
        Assert.Equal(434, city.Food);
        Assert.Equal(68, person.Thew);
        Assert.Equal(11, Assert.Single(session.World.Orders).OrderId);
        session.ExecuteOrders();
        Assert.Equal(city.Id, person.CityId);
        city.Money = 29000;
        city.Food = 1000;
        session.IssueCommand(new MainCommandRequest(11, city.Id, person.Id, TradeDirection: MainTradeDirection.Sell, Quantity: 1000));
        Assert.Equal(30000, city.Money);
        Assert.Equal(0, city.Food);
    }

    [Fact]
    public void Move_arrives_at_target_without_resource_changes()
    {
        var session = CreateLuBu();
        var source = session.World.Cities.Single(city => city.Id == "city-015");
        var target = session.World.Cities.Single(city => city.Id == "city-010");
        var before = (source.Money, source.Food, target.Money, target.Food);
        session.IssueCommand(new MainCommandRequest(14, source.Id, "person-020", TargetCityId: target.Id));
        Assert.Equal(0, Assert.Single(session.World.Orders).TimeCount);
        session.ExecuteOrders();
        Assert.Equal(target.Id, session.World.Persons.Single(person => person.Id == "person-020").CityId);
        Assert.Equal("person-020", target.PersonIds[0]);
        Assert.DoesNotContain("person-020", source.PersonIds);
        Assert.Equal(before, (source.Money, source.Food, target.Money, target.Food));
    }

    [Fact]
    public void Transport_success_delivers_and_actor_returns()
    {
        var session = CreateLuBu();
        var (source, target) = PrepareTransport(session);
        session.IssueCommand(new MainCommandRequest(13, source.Id, "person-020", TargetCityId: target.Id, Food: 123, Money: 45, Arms: 67));
        Assert.Equal(877, source.Food);
        Assert.Equal(455, source.Money);
        Assert.Equal(133, source.MothballArms);
        session.World.RandomState = 2588401304;
        session.ExecuteOrders();
        Assert.Equal(1220, target.Food);
        Assert.Equal(621, target.Money);
        Assert.Equal(67, target.MothballArms);
        Assert.Equal(source.Id, session.World.Persons.Single(person => person.Id == "person-020").CityId);
    }

    [Fact]
    public void Transport_failure_loses_cargo_without_returning_it()
    {
        var session = CreateLuBu();
        var (source, target) = PrepareTransport(session);
        session.IssueCommand(new MainCommandRequest(13, source.Id, "person-020", TargetCityId: target.Id, Food: 123, Money: 45, Arms: 67));
        session.World.RandomState = 3;
        session.ExecuteOrders();
        Assert.Equal(1097, target.Food);
        Assert.Equal(576, target.Money);
        Assert.Equal(0, target.MothballArms);
        Assert.Equal(877, source.Food);
        Assert.Equal(455, source.Money);
        Assert.Equal(133, source.MothballArms);
        Assert.Equal(source.Id, session.World.Persons.Single(person => person.Id == "person-020").CityId);
    }

    [Fact]
    public void Transport_save_does_not_duplicate_delivery()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "transport.json");
        var session = CreateLuBu();
        var (source, target) = PrepareTransport(session);
        session.IssueCommand(new MainCommandRequest(13, source.Id, "person-020", TargetCityId: target.Id, Food: 123, Money: 45, Arms: 67));
        MainSaveService.Save(session, path);
        var restored = MainSaveService.Load(path, session.Content);
        session.World.RandomState = 2588401304;
        restored.World.RandomState = 2588401304;
        session.ExecuteOrders();
        restored.ExecuteOrders();
        Assert.Equal(session.GetStateHash(), restored.GetStateHash());
        Assert.Equal(1220, target.Food);
    }

    private static (MainCity Source, MainCity Target) PrepareTransport(MainGameSession session)
    {
        var source = session.World.Cities.Single(city => city.Id == "city-015");
        var target = session.World.Cities.Single(city => city.Id == "city-010");
        source.Food = 1000;
        source.Money = 500;
        source.MothballArms = 200;
        target.Food = 1097;
        target.Money = 576;
        target.MothballArms = 0;
        return (source, target);
    }

    private static MainGameSession CreateLiuBei() => MainContentLoader.CreateSession(LoadContent(), 1, "person-013");
    private static MainGameSession CreateLuBu()
    {
        var content = LoadContent();
        content.Periods[0].RulerIds.Add("person-020");
        var session = MainContentLoader.CreateSession(content, 1, "person-020");
        foreach (var city in session.World.Cities.Where(city => city.Id is "city-015" or "city-010") )
            city.Belong = "person-020";
        var source = session.World.Cities.Single(city => city.Id == "city-015");
        if (!source.PersonIds.Contains("person-020")) source.PersonIds.Add("person-020");
        var person = session.World.Persons.Single(person => person.Id == "person-020");
        person.CityId = source.Id;
        person.Belong = "person-020";
        return session;
    }
    private static MainContent LoadContent() => MainContentLoader.Load(
        Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json"));
    private static MainCity PingYuan(MainGameSession session) => session.World.Cities.Single(city => city.Id == "city-005");
    private static MainPerson GuanYu(MainGameSession session) => session.World.Persons.Single(person => person.Id == "person-089");
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
