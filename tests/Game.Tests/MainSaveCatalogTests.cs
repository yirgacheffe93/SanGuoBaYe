using Game.Application;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainSaveCatalogTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void Slots_keep_independent_games_and_bad_saves_do_not_block_the_list()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"main-slots-{Guid.NewGuid()}");
        try
        {
            var content = MainContentLoader.Load(Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json"));
            var catalog = new MainSaveCatalog(Path.Combine(directory, "saves"), content);
            Assert.Equal(5, catalog.List().Count);
            var first = MainContentLoader.CreateSession(content, 1, "person-013");
            var second = MainContentLoader.CreateSession(content, 2, content.Periods[1].RulerIds[0]);
            catalog.Save(first, 1); catalog.Save(second, 2);
            File.WriteAllText(catalog.GetPath(3), "invalid-json");
            MainSaveService.Save(first, Path.Combine(directory, "main-m2a-save.json"));
            var slots = catalog.List();
            Assert.Equal(6, slots.Count);
            Assert.True(slots.Single(slot => slot.Index == 0).CanLoad);
            Assert.False(slots.Single(slot => slot.Index == 3).CanLoad);
            Assert.False(slots.Single(slot => slot.Index == 4).Exists);
            Assert.Equal(first.GetStateHash(), catalog.Load(slots[0]).GetStateHash());
            Assert.Equal(second.GetStateHash(), catalog.Load(slots[1]).GetStateHash());
            Assert.Contains("190 年", slots[0].Summary);
            Assert.Throws<ArgumentOutOfRangeException>(() => catalog.GetPath(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => catalog.GetPath(6));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void Battle_slot_restores_moved_unit_and_deterministic_next_turn()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"main-battle-slot-{Guid.NewGuid()}");
        try
        {
            var content = MainContentLoader.Load(Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json"));
            var session = MainContentLoader.CreateSession(content, 1, "person-013");
            var order = session.IssueCommand(new MainCommandRequest(27, "city-005", "person-089", TargetCityId: "city-004", Food: 100,
                ParticipantIds: ["person-013", "person-089", "person-090"]));
            session.EnterBattle(order.OrderId!);
            session.World.ActiveBattle!.Units[0].HasMoved = true;
            var catalog = new MainSaveCatalog(directory, content);
            catalog.Save(session, 1);
            var slot = catalog.List()[0];
            Assert.Contains("战场", slot.Summary);
            var restored = catalog.Load(slot);
            Assert.True(restored.World.ActiveBattle!.Units[0].HasMoved);
            MainBattleService.EndTurn(session.World, content);
            MainBattleService.EndTurn(restored.World, content);
            Assert.Equal(session.GetStateHash(), restored.GetStateHash());
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
