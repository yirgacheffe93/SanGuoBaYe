using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Application;
using Game.Domain;

namespace Game.Infrastructure;

public sealed class MainContentException(string message) : Exception(message);

public static class MainContentLoader
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static MainContent Load(string contentPath, string scenariosPath) =>
        LoadJson(File.ReadAllText(contentPath), File.ReadAllText(scenariosPath),
            File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(contentPath))!, "content.schema.json")),
            File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(scenariosPath))!, "scenarios.schema.json")),
            File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(contentPath))!, "battle.json")),
            File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(contentPath))!, "battle.schema.json")));

    public static MainContent LoadJson(string contentJson, string scenariosJson,
        string? contentSchemaJson = null, string? scenariosSchemaJson = null, string? battleJson = null, string? battleSchemaJson = null)
    {
        try
        {
            if (contentSchemaJson is not null) ScenarioSchemaValidator.ValidateWithSchema(contentJson, contentSchemaJson);
            if (scenariosSchemaJson is not null) ScenarioSchemaValidator.ValidateWithSchema(scenariosJson, scenariosSchemaJson);
            if (battleJson is not null && battleSchemaJson is not null) ScenarioSchemaValidator.ValidateWithSchema(battleJson, battleSchemaJson);
            var common = JsonSerializer.Deserialize<ContentDocument>(contentJson, Options)
                ?? throw new MainContentException("main 通用内容为空。");
            var scenarios = JsonSerializer.Deserialize<ScenarioDocument>(scenariosJson, Options)
                ?? throw new MainContentException("main 时期内容为空。");
            var battle = battleJson is null ? new MainBattleContent() : MainBattleMapper.Map(JsonSerializer.Deserialize<BattleDocument>(battleJson, Options)
                ?? throw new MainContentException("main 战斗内容为空。"));
            var contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contentJson + scenariosJson + battleJson))).ToLowerInvariant();
            var content = MainContentMapper.Map(common, scenarios, contentHash);
            content.Battle = battle;
            MainContentValidator.Validate(content);
            return content;
        }
        catch (JsonException error)
        {
            throw new MainContentException($"main 内容 JSON 无效：{error.Message}");
        }
    }

    public static MainGameSession CreateSession(MainContent content, int period, string rulerId)
    {
        var definition = content.Periods.FirstOrDefault(item => item.Period == period)
            ?? throw new MainContentException($"时期 {period} 不存在。");
        if (!definition.RulerIds.Contains(rulerId, StringComparer.Ordinal)) throw new MainContentException("该君主在此时期不可选。");
        var periodState = JsonSerializer.Deserialize<MainPeriod>(JsonSerializer.Serialize(definition), Options)!;
        return new MainGameSession(new MainWorld
        {
            ContentVersion = content.ContentVersion,
            ContentHash = content.ContentHash,
            Period = period,
            Year = definition.Year,
            Month = definition.Month,
            PlayerRulerId = rulerId,
            RandomState = 1,
            Cities = periodState.Cities,
            Persons = periodState.Persons
        }, content);
    }
}
