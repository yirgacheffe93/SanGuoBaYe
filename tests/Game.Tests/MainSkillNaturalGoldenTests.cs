using System.Text.Json;
using Game.Application;
using Game.Domain;
using Game.Infrastructure;

namespace Game.Tests;

public sealed class MainSkillNaturalGoldenTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    public static IEnumerable<object[]> Cases() => Enumerable.Range(0, 62).Select(index => new object[] { index });

    [Theory]
    [MemberData(nameof(Cases))]
    public void Skill_matches_natural_production_branch_and_effects(int fixtureIndex)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "content/main/golden-skill-natural.json")));
        var fixture = document.RootElement.GetProperty("scenarios")[fixtureIndex];
        var scenario = fixture.GetProperty("scenario");
        var index = scenario.GetProperty("skillId").GetInt32() - 1;
        var before = fixture.GetProperty("before");
        var after = fixture.GetProperty("after");
        var content = MainContentLoader.Load(Path.Combine(Root, "content/main/content.json"), Path.Combine(Root, "content/main/scenarios.json"));
        var session = MainContentLoader.CreateSession(content, 1, $"person-{scenario.GetProperty("player").GetInt32():000}");
        foreach (var raw in before.GetProperty("persons").EnumerateArray())
        {
            var person = session.World.Persons[raw.GetProperty("index").GetInt32()];
            person.ArmsType = raw.GetProperty("ArmsType").GetInt32();
            person.Arms = raw.GetProperty("Arms").GetInt32(); person.Iq = raw.GetProperty("IQ").GetInt32();
            person.Level = raw.GetProperty("Level").GetInt32(); person.Experience = raw.GetProperty("Experience").GetInt32();
        }
        var units = before.GetProperty("units").EnumerateArray().Select(raw => new MainBattleUnit
        {
            PersonId = $"person-{raw.GetProperty("person").GetInt32():000}",
            IsAttacker = before.GetProperty("genArray").EnumerateArray().Skip(10).Any(id => id.GetInt32() == raw.GetProperty("person").GetInt32() + 1),
            X = raw.GetProperty("x").GetInt32(), Y = raw.GetProperty("y").GetInt32(),
            Hp = raw.GetProperty("hp").GetInt32(), Mp = raw.GetProperty("mp").GetInt32(), MaxMp = 255,
            Move = raw.GetProperty("move").GetInt32(), State = raw.GetProperty("state").GetInt32()
        }).ToList();
        session.World.ActiveBattle = new MainBattleState
        {
            TargetCityId = $"city-{scenario.GetProperty("target").GetInt32():000}",
            PlayerIsAttacker = false, Weather = before.GetProperty("weather").GetInt32(),
            AttackerFood = before.GetProperty("params").GetProperty("EProvender").GetInt32(),
            DefenderFood = before.GetProperty("params").GetProperty("MProvender").GetInt32(), Units = units
        };
        var actorIndex = before.GetProperty("genArray")[10].GetInt32() - 1;
        var targetIndex = before.GetProperty("genArray")[scenario.GetProperty("targetUnit").GetInt32()].GetInt32() - 1;
        var actorId = $"person-{actorIndex:000}"; var targetId = $"person-{targetIndex:000}"; var skillId = $"skill-{index:000}";
        content.Battle.SpecialSkills[0][actorIndex] = skillId;
        session.World.RandomState = fixture.GetProperty("skillStart").GetProperty("rng").GetUInt32();
        var failure = MainBattleService.SkillFailure(session.World, content, actorId, skillId, targetId);
        Assert.True(failure is null, $"{skillId}: {failure}");
        Assert.True(MainBattleService.UseSkill(session.World, content, actorId, skillId, targetId));
        foreach (var raw in after.GetProperty("persons").EnumerateArray())
        {
            var person = session.World.Persons[raw.GetProperty("index").GetInt32()];
            Assert.True(raw.GetProperty("Arms").GetInt32() == person.Arms, $"{skillId} {person.Id} arms expected={raw.GetProperty("Arms")} actual={person.Arms}");
            Assert.Equal(raw.GetProperty("Experience").GetInt32(), person.Experience);
        }
        foreach (var raw in after.GetProperty("units").EnumerateArray())
        {
            var unit = units.Single(unit => unit.PersonId == $"person-{raw.GetProperty("person").GetInt32():000}");
            Assert.Equal(raw.GetProperty("state").GetInt32(), unit.State);
            // The remaining enemy AI units wait after the captured command, recovering 1 MP.
            if (unit.PersonId == actorId) Assert.Equal(raw.GetProperty("mp").GetInt32(), unit.Mp);
            Assert.Equal(raw.GetProperty("move").GetInt32(), unit.Move);
        }
        Assert.Equal(after.GetProperty("weather").GetInt32(), session.World.ActiveBattle.Weather);
        Assert.Equal(after.GetProperty("params").GetProperty("EProvender").GetInt32(), session.World.ActiveBattle.AttackerFood);
        Assert.Equal(after.GetProperty("params").GetProperty("MProvender").GetInt32(), session.World.ActiveBattle.DefenderFood);
    }
}
