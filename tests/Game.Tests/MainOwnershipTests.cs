using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainOwnershipTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void Expedition_copies_participant_ids_before_storing_order()
    {
        var session = CreateSession(LoadContent());
        var participantIds = new List<string> { "person-013", "person-089" };
        var result = session.IssueCommand(new MainCommandRequest(
            MainCommandCatalog.BattleOrderId, "city-005", "person-013", TargetCityId: "city-004", Food: 100,
            ParticipantIds: participantIds));
        Assert.True(result.Accepted);
        participantIds.Clear();
        var order = Assert.Single(session.World.Orders);
        Assert.Equal(["person-013", "person-089"], order.ParticipantIds);
        Assert.Equal(2, session.World.Persons.Count(person => person.OrderId == order.Id));
        Assert.All(session.World.Persons.Where(person => person.OrderId == order.Id), person => Assert.Equal("", person.CityId));
    }

    [Fact]
    public void City_view_connections_do_not_alias_world()
    {
        var session = CreateSession(LoadContent());
        var view = session.GetView();
        var cityView = view.Cities.Single(city => city.Id == "city-005");
        var worldCity = session.World.Cities.Single(city => city.Id == "city-005");
        var expected = worldCity.Connections.ToList();
        ((IList<string>)cityView.Connections).Clear();
        Assert.Equal(expected, worldCity.Connections);
    }

    [Fact]
    public void Battle_view_does_not_alias_content_or_battle_state()
    {
        var session = CreateSession(LoadContent());
        var result = session.IssueCommand(new MainCommandRequest(
            MainCommandCatalog.BattleOrderId, "city-005", "person-013", TargetCityId: "city-004", Food: 100,
            ParticipantIds: ["person-013"]));
        Assert.True(result.Accepted);
        session.EnterBattle(result.OrderId!);
        var battle = session.World.ActiveBattle!;
        var map = session.Content.Battle.Maps.Single(map => map.CityId == battle.TargetCityId);
        var terrain = map.Terrains[0];
        var terrainKind = map.TerrainKinds[0];
        var logCount = battle.Logs.Count;
        var unitCount = battle.Units.Count;
        var view = session.GetBattleView();
        ((IList<int>)view.Terrains)[0]++;
        ((IList<int>)view.TerrainKinds)[0]++;
        ((IList<string>)view.Logs).Clear();
        ((IList<MainBattleUnitView>)view.Units).Clear();
        Assert.Equal(terrain, map.Terrains[0]);
        Assert.Equal(terrainKind, map.TerrainKinds[0]);
        Assert.Equal(logCount, battle.Logs.Count);
        Assert.Equal(unitCount, battle.Units.Count);
    }

    [Fact]
    public void Old_views_cannot_mutate_session_after_month_advances()
    {
        var session = CreateSession(LoadContent());
        var oldView = session.GetView();
        var oldCity = oldView.Cities.Single(city => city.Id == "city-005");
        var worldCity = session.World.Cities.Single(city => city.Id == "city-005");
        var connections = worldCity.Connections.ToList();
        var toolEntryCount = worldCity.ToolEntries.Count;
        session.EndMonth(false);
        ((IList<string>)oldCity.Connections).Clear();
        ((IList<string>)oldCity.DiscoveredToolIds).Clear();
        ((IList<MainCityView>)oldView.Cities).Clear();
        Assert.Equal(connections, worldCity.Connections);
        Assert.Equal(toolEntryCount, worldCity.ToolEntries.Count);
        Assert.Equal(38, session.GetView().Cities.Count);
    }

    [Fact]
    public void Sessions_from_same_content_do_not_share_initial_mutable_state()
    {
        var content = LoadContent();
        var first = CreateSession(content);
        var second = CreateSession(content);
        var secondHash = second.GetStateHash();
        var definitionCity = content.Periods[0].Cities.Single(city => city.Id == "city-005");
        var definitionPerson = content.Periods[0].Persons.Single(person => person.Id == "person-089");
        var money = definitionCity.Money;
        var farming = definitionCity.Farming;
        var thew = definitionPerson.Thew;
        var cityId = definitionPerson.CityId;
        Assert.NotSame(definitionCity, first.World.Cities.Single(city => city.Id == definitionCity.Id));
        Assert.NotSame(definitionCity.Connections, first.World.Cities.Single(city => city.Id == definitionCity.Id).Connections);
        Assert.NotSame(definitionPerson, first.World.Persons.Single(person => person.Id == definitionPerson.Id));
        Assert.True(first.IssueCommand(MainCommandCatalog.ReclaimOrderId, "city-005", "person-089").Accepted);
        first.GetView();
        first.World.Cities.Single(city => city.Id == "city-005").Connections.Clear();
        Assert.Equal(secondHash, second.GetStateHash());
        Assert.Equal(money, definitionCity.Money);
        Assert.Equal(farming, definitionCity.Farming);
        Assert.Equal(thew, definitionPerson.Thew);
        Assert.Equal(cityId, definitionPerson.CityId);
    }

    private static MainGameSession CreateSession(MainContent content) => MainContentLoader.CreateSession(content, 1, "person-013");
    private static MainContent LoadContent() => MainContentLoader.Load(
        Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json"));
}
