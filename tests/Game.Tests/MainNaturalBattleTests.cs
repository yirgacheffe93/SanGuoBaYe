using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainNaturalBattleTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Full_battle_reaches_natural_result_and_continues_identically_after_load(int period)
    {
        var content = MainContentLoader.Load(Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json"));
        var definition = content.Periods[period - 1];
        var source = definition.Cities.First(city => city.Belong.Length > 0 && city.PersonIds.Any() &&
            city.Connections.Any(id => definition.Cities.Any(target => target.Id == id && target.Belong.Length > 0 && target.Belong != city.Belong && target.PersonIds.Any())));
        var target = definition.Cities.First(city => source.Connections.Contains(city.Id) && city.Belong.Length > 0 && city.Belong != source.Belong && city.PersonIds.Any());
        var session = MainContentLoader.CreateSession(content, period, source.Belong);
        var participants = session.World.Cities.Single(city => city.Id == source.Id).PersonIds
            .Where(id => session.World.Persons.Single(person => person.Id == id).Belong == source.Belong).Take(3).ToList();
        var origin = session.World.Cities.Single(city => city.Id == source.Id);
        origin.Food = 10000;
        foreach (var id in participants) session.World.Persons.Single(person => person.Id == id).Arms = 1000;
        var result = session.IssueCommand(new MainCommandRequest(27, source.Id, participants[0], TargetCityId: target.Id, Food: 3000, ParticipantIds: participants));
        Assert.True(result.Accepted, result.Message);
        session.EnterBattle(result.OrderId!);
        var path = Path.Combine(Path.GetTempPath(), $"main-natural-{period}-{Guid.NewGuid()}.json");
        try
        {
            MainSaveService.Save(session, path);
            var restored = MainSaveService.Load(path, content);
            for (var day = 0; day <= 31 && session.World.ActiveBattle!.Result == MainBattleResult.Ongoing; day++)
            {
                foreach (var game in new[] { session, restored })
                {
                    MainBattleService.RunAiTurn(game.World, content, true);
                    MainBattleService.EndTurn(game.World, content);
                }
                Assert.Equal(session.GetStateHash(), restored.GetStateHash());
                if (day == 1) { MainSaveService.Save(session, path); restored = MainSaveService.Load(path, content); }
            }
            Assert.NotEqual(MainBattleResult.Ongoing, session.World.ActiveBattle!.Result);
            session.ResolveBattle(); restored.ResolveBattle();
            Assert.Equal(session.GetStateHash(), restored.GetStateHash());
            foreach (var person in session.World.Persons.Where(person => participants.Contains(person.Id)))
            {
                Assert.NotEmpty(person.CityId);
                Assert.Null(person.OrderId);
                Assert.Single(session.World.Cities.Where(city => city.PersonIds.Contains(person.Id)));
            }
            session.EndMonth(false); restored.EndMonth(false);
            Assert.Equal(session.GetStateHash(), restored.GetStateHash());
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
