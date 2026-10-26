using System.Text.Json;
using Game.Domain;

namespace Game.Infrastructure;

public sealed class ContentDefinition
{
    public string ContentVersion { get; set; } = "";
    public string RulesVersion { get; set; } = "spike-1";
    public ulong Seed { get; set; }
    public string FactionId { get; set; } = "";
    public RuleParameters? Rules { get; set; }
    public List<Faction> Factions { get; set; } = [];
    public List<City> Cities { get; set; } = [];
    public List<Character> Characters { get; set; } = [];
    public List<RoadDefinition> Roads { get; set; } = [];
    public List<ReportEventDefinition> Events { get; set; } = [];
}

public sealed class ContentException(string message) : Exception(message);

public static class ContentLoader
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static World LoadFile(string path) => Load(File.ReadAllText(path));

    public static World Load(string json)
    {
        ContentDefinition content;
        try { content = JsonSerializer.Deserialize<ContentDefinition>(json, Options) ?? throw new ContentException("内容为空。"); }
        catch (JsonException error) { throw new ContentException($"内容 JSON 无效：{error.Message}"); }
        return CreateWorld(content);
    }

    public static World CreateWorld(ContentDefinition content)
    {
        if (string.IsNullOrWhiteSpace(content.ContentVersion)) throw new ContentException("缺少 contentVersion。");
        if (string.IsNullOrWhiteSpace(content.RulesVersion)) throw new ContentException("缺少 rulesVersion。");
        if (string.IsNullOrWhiteSpace(content.FactionId)) throw new ContentException("缺少 factionId。");
        if (content.Seed == 0) throw new ContentException("seed 必须大于零。");
        if (content.Rules is null) throw new ContentException("缺少 rules。");
        ValidateRules(content.Rules);
        if (content.Cities.Count == 0) throw new ContentException("至少需要一座城市。");
        var factions = content.Factions.Count == 0
            ? [new Faction { Id = content.FactionId, Name = "我方" }]
            : content.Factions;
        EnsureUnique(factions.Select(f => f.Id), "势力");
        EnsureUnique(content.Cities.Select(c => c.Id), "城市");
        EnsureUnique(content.Characters.Select(c => c.Id), "武将");
        EnsureUnique(content.Events.Select(e => e.Id), "事件");
        var factionIds = factions.Select(f => f.Id).ToHashSet(StringComparer.Ordinal);
        if (!factionIds.Contains(content.FactionId)) throw new ContentException("玩家势力未定义。");
        var cityIds = content.Cities.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var city in content.Cities)
        {
            if (string.IsNullOrWhiteSpace(city.Id) || string.IsNullOrWhiteSpace(city.Name) || !factionIds.Contains(city.FactionId))
                throw new ContentException("城市缺少 ID/名称或势力非法。");
            if (city.Gold < 0 || city.Food < 0 || city.Population < 0 || city.Troops < 0 ||
                city.Agriculture < 0 || city.Commerce < 0 || city.Defense < 0 || city.Defense > 100 ||
                city.Agriculture > content.Rules.LevelCap || city.Commerce > content.Rules.LevelCap)
                throw new ContentException($"城市 {city.Id} 数值非法。");
            if (city.Connections.Any(id => !cityIds.Contains(id))) throw new ContentException($"城市 {city.Id} 有悬空道路引用。");
            foreach (var targetId in city.Connections)
            {
                var target = content.Cities.Single(c => c.Id == targetId);
                if (!target.Connections.Contains(city.Id, StringComparer.Ordinal))
                    throw new ContentException($"道路 {city.Id} 与 {targetId} 不是双向连接。");
            }
        }
        foreach (var character in content.Characters)
        {
            if (string.IsNullOrWhiteSpace(character.Id) || string.IsNullOrWhiteSpace(character.Name) ||
                !factionIds.Contains(character.FactionId) || !cityIds.Contains(character.CityId))
                throw new ContentException($"武将 {character.Id} 缺少字段或有悬空引用。");
            if (character.Leadership is < 1 or > 100 || character.War is < 1 or > 100 ||
                character.Intellect is < 1 or > 100 || character.Politics is < 1 or > 100 || character.Charm is < 1 or > 100)
                throw new ContentException($"武将 {character.Id} 五维超出 1–100。");
            var city = content.Cities.Single(c => c.Id == character.CityId);
            if (city.FactionId != character.FactionId) throw new ContentException($"武将 {character.Id} 与所在城市势力不一致。");
        }
        var roads = content.Roads.Count == 0 ? DeriveRoads(content.Cities) : content.Roads;
        ValidateRoads(roads, content.Cities);
        foreach (var reportEvent in content.Events)
        {
            if (string.IsNullOrWhiteSpace(reportEvent.Id) || string.IsNullOrWhiteSpace(reportEvent.TextKey) ||
                string.IsNullOrWhiteSpace(reportEvent.Text) || reportEvent.TriggerTurn < 1 || reportEvent.TriggerTurn > content.Rules.MaxTurns)
                throw new ContentException($"事件 {reportEvent.Id} 的回合或文本非法。");
        }
        return new World
        {
            ContentVersion = content.ContentVersion, RulesVersion = content.RulesVersion,
            FactionId = content.FactionId, RandomState = content.Seed, Rules = content.Rules,
            Factions = factions, Cities = content.Cities, Characters = content.Characters,
            Roads = roads, Events = content.Events
        };
    }

    private static void ValidateRules(RuleParameters rules)
    {
        if (rules.MaxTurns < 1 || rules.DevelopmentCost < 0 || rules.LevelCap < 1 ||
            rules.AgricultureIncome < 0 || rules.CommerceIncome < 0 || rules.UpkeepFoodPer1000Troops < 0 ||
            rules.RecruitGoldPer100Troops < 0 || rules.RecruitFoodPer100Troops < 0 || rules.EnvoyCost < 0 ||
            rules.StarvationLossPercent is < 0 or > 100 || rules.TruceBaseScore < 0 ||
            rules.TruceStrengthWeight < 0 || rules.TruceRelationWeight < 0 || rules.TruceAcceptScore < 0 ||
            rules.BattleDamageDivisor < 1 || rules.BattleMinRoll < 0 || rules.BattleMaxRoll < rules.BattleMinRoll ||
            rules.BattleMaxRounds < 1 || rules.BattleMinDamage < 1)
            throw new ContentException("规则参数超出范围。");
    }

    private static List<RoadDefinition> DeriveRoads(IEnumerable<City> cities)
    {
        var roads = new List<RoadDefinition>();
        foreach (var city in cities)
        foreach (var target in city.Connections.Where(id => StringComparer.Ordinal.Compare(city.Id, id) < 0))
            roads.Add(new RoadDefinition { FromCityId = city.Id, ToCityId = target, Distance = 1 });
        return roads;
    }

    private static void ValidateRoads(IReadOnlyCollection<RoadDefinition> roads, IReadOnlyCollection<City> cities)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var road in roads)
        {
            var from = cities.FirstOrDefault(c => c.Id == road.FromCityId);
            var to = cities.FirstOrDefault(c => c.Id == road.ToCityId);
            if (from is null || to is null || road.FromCityId == road.ToCityId || road.Distance < 1)
                throw new ContentException("道路端点或距离非法。");
            if (!from.Connections.Contains(to.Id, StringComparer.Ordinal) || !to.Connections.Contains(from.Id, StringComparer.Ordinal))
                throw new ContentException("道路与城市连接不一致。");
            var key = string.CompareOrdinal(road.FromCityId, road.ToCityId) < 0
                ? road.FromCityId + "|" + road.ToCityId
                : road.ToCityId + "|" + road.FromCityId;
            if (!keys.Add(key)) throw new ContentException("道路重复定义。");
        }
        var expected = cities.Sum(city => city.Connections.Count) / 2;
        if (roads.Count != expected) throw new ContentException("道路数量与城市连接不一致。");
    }

    private static void EnsureUnique(IEnumerable<string> ids, string kind)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids) if (!seen.Add(id)) throw new ContentException($"{kind} ID 重复：{id}");
    }
}
