using System.Diagnostics;
using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainLongRunTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Four_periods_remain_consistent_across_long_ai_runs_and_loads(int period)
    {
        var content = LoadContent();
        var rulerId = content.Periods.Single(item => item.Period == period).RulerIds[0];
        var continuous = MainContentLoader.CreateSession(content, period, rulerId);
        var restored = MainContentLoader.CreateSession(content, period, rulerId);
        var path = Path.Combine(Path.GetTempPath(), $"main-long-{period}-{Guid.NewGuid()}.json");
        var watch = Stopwatch.StartNew();
        try
        {
            for (var month = 0; month < 120; month++)
            {
                if (month == 60)
                {
                    MainSaveService.Save(continuous, path);
                    restored = MainSaveService.Load(path, content);
                }
                continuous.EndMonth();
                restored.EndMonth();
                AssertInvariants(continuous.World);
                AssertInvariants(restored.World);
                Assert.Equal(continuous.GetStateHash(), restored.GetStateHash());
            }
            AssertInvariants(continuous.World);
            var ended = continuous.GetStateHash();
            continuous.EndMonth();
            if (continuous.World.Outcome != MainGameOutcome.InProgress) Assert.Equal(ended, continuous.GetStateHash());
        }
        finally { if (File.Exists(path)) File.Delete(path); }
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(60), $"时期 {period} 长局超时：{watch.Elapsed}");
    }

    private static void AssertInvariants(MainWorld world)
    {
        Assert.All(world.Cities, city =>
        {
            Assert.True(city.Money >= 0 && city.Food >= 0 && city.MothballArms >= 0 && city.Population >= 0);
            Assert.Equal(city.PersonIds.Count, city.PersonIds.Distinct(StringComparer.Ordinal).Count());
            Assert.All(city.PersonIds, id =>
            {
                var person = world.Persons.Single(person => person.Id == id);
                Assert.Equal(city.Id, person.CityId);
                Assert.Null(person.OrderId);
            });
        });
        var locations = world.Persons.Where(person => person.CityId.Length > 0).Select(person => person.Id).ToList();
        Assert.Equal(locations.Count, locations.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(world.Orders.Count, world.Orders.Select(order => order.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(world.Orders.Count, world.Orders.Select(order => order.PersonId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(world.Orders, order =>
        {
            var person = world.Persons.Single(person => person.Id == order.PersonId);
            Assert.Equal(order.Id, person.OrderId);
            Assert.Equal("", person.CityId);
            Assert.Contains(world.Cities, city => city.Id == order.CityId);
        });
        Assert.All(world.Persons, person =>
        {
            if (person.OrderId is not null) Assert.Contains(world.Orders, order => order.Id == person.OrderId);
        });
    }

    private static MainContent LoadContent() => MainContentLoader.Load(
        Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json"));
}
