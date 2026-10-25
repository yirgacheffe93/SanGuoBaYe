using System.Text.Json;
using Game.Domain;

namespace Game.Infrastructure;

public sealed class ContentDefinition
{
    public string ContentVersion { get; set; } = "";
    public ulong Seed { get; set; }
    public string FactionId { get; set; } = "";
    public RuleParameters? Rules { get; set; }
    public List<City> Cities { get; set; } = [];
    public List<Character> Characters { get; set; } = [];
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
        if (string.IsNullOrWhiteSpace(content.FactionId)) throw new ContentException("缺少 factionId。");
        if (content.Seed == 0) throw new ContentException("seed 必须大于零。");
        if (content.Rules is null) throw new ContentException("缺少 rules。");
        if (content.Rules.DevelopmentCost < 0 || content.Rules.LevelCap < 1 ||
            content.Rules.AgricultureIncome < 0 || content.Rules.CommerceIncome < 0)
            throw new ContentException("规则参数超出范围。");
        if (content.Cities.Count == 0) throw new ContentException("至少需要一座城市。");
        EnsureUnique(content.Cities.Select(c => c.Id), "城市");
        EnsureUnique(content.Characters.Select(c => c.Id), "武将");
        var cityIds = content.Cities.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var city in content.Cities)
        {
            if (string.IsNullOrWhiteSpace(city.Id) || string.IsNullOrWhiteSpace(city.Name) || city.FactionId != content.FactionId)
                throw new ContentException("城市缺少 ID/名称或势力非法。");
            if (city.Gold < 0 || city.Food < 0 || city.Troops < 0 || city.Agriculture < 0 || city.Commerce < 0 ||
                city.Agriculture > content.Rules.LevelCap || city.Commerce > content.Rules.LevelCap)
                throw new ContentException($"城市 {city.Id} 数值非法。");
            if (city.Connections.Any(id => !cityIds.Contains(id))) throw new ContentException($"城市 {city.Id} 有悬空道路引用。");
        }
        foreach (var character in content.Characters)
        {
            if (string.IsNullOrWhiteSpace(character.Id) || string.IsNullOrWhiteSpace(character.Name) ||
                character.FactionId != content.FactionId || !cityIds.Contains(character.CityId))
                throw new ContentException($"武将 {character.Id} 缺少字段或有悬空城市引用。");
        }
        return new World
        {
            ContentVersion = content.ContentVersion, FactionId = content.FactionId, RandomState = content.Seed,
            Rules = content.Rules, Cities = content.Cities, Characters = content.Characters
        };
    }

    private static void EnsureUnique(IEnumerable<string> ids, string kind)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids) if (!seen.Add(id)) throw new ContentException($"{kind} ID 重复：{id}");
    }
}
