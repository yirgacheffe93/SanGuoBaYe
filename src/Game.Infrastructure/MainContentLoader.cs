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

    private sealed class SourceDocument { public string Commit { get; set; } = ""; }
    private sealed class ContentDocument
    {
        public SourceDocument Source { get; set; } = new();
        public List<ToolDocument> Tools { get; set; } = [];
        public List<SkillDocument> Skills { get; set; } = [];
    }
    private sealed class ScenarioDocument
    {
        public SourceDocument Source { get; set; } = new();
        public List<PeriodDocument> Periods { get; set; } = [];
    }
    private sealed class PeriodDocument
    {
        public int Period { get; set; }
        public DateDocument Date { get; set; } = new();
        public List<CityDocument> Cities { get; set; } = [];
        public List<PersonDocument> Persons { get; set; } = [];
    }
    private sealed class DateDocument { public int Year { get; set; } public int Month { get; set; } }
    private sealed class CityDocument
    {
        public string Id { get; set; } = "";
        public int Index { get; set; }
        public string Name { get; set; } = "";
        public RawCity Raw { get; set; } = new();
        public string? OwnerPersonId { get; set; }
        public string? SatrapPersonId { get; set; }
        public List<string> PersonIds { get; set; } = [];
        public List<string> ToolIds { get; set; } = [];
    }
    private sealed class RawCity
    {
        public int State { get; set; }
        public int FarmingLimit { get; set; }
        public int Farming { get; set; }
        public int CommerceLimit { get; set; }
        public int Commerce { get; set; }
        public int PeopleDevotion { get; set; }
        public int AvoidCalamity { get; set; }
        public int PopulationLimit { get; set; }
        public int Population { get; set; }
        public int Money { get; set; }
        public int Food { get; set; }
        public int MothballArms { get; set; }
    }
    private sealed class PersonDocument
    {
        public string Id { get; set; } = "";
        public int Index { get; set; }
        public string Name { get; set; } = "";
        public RawPerson Raw { get; set; } = new();
        public string? OldOwnerPersonId { get; set; }
        public string? OwnerPersonId { get; set; }
        public string? Tool1Id { get; set; }
        public string? Tool2Id { get; set; }
    }
    private sealed class RawPerson
    {
        public int Level { get; set; }
        public int Force { get; set; }
        public int IQ { get; set; }
        public int Devotion { get; set; }
        public int Character { get; set; }
        public int Experience { get; set; }
        public int Thew { get; set; }
        public int ArmsType { get; set; }
        public int Arms { get; set; }
        public List<int> Equip { get; set; } = [];
        public int Age { get; set; }
    }
    private sealed class ToolDocument
    {
        public string Id { get; set; } = "";
        public int Index { get; set; }
        public string Name { get; set; } = "";
        public RawTool Raw { get; set; } = new();
    }
    private sealed class RawTool
    {
        public int useflag { get; set; }
        public int changeAttackRange { get; set; }
        public int at { get; set; }
        public int iq { get; set; }
        public int move { get; set; }
        public int arm { get; set; }
    }
    private sealed class SkillDocument
    {
        public string Id { get; set; } = "";
        public int Index { get; set; }
        public string Name { get; set; } = "";
        public RawSkill Raw { get; set; } = new();
    }
    private sealed class RawSkill
    {
        public int aim { get; set; }
        public int state { get; set; }
        public int power { get; set; }
        public int destroy { get; set; }
        public int useMp { get; set; }
        public List<int> weather { get; set; } = [];
        public List<int> eland { get; set; } = [];
        public List<int> oland { get; set; } = [];
        public List<int> earm { get; set; } = [];
    }

    public static MainContent Load(string contentPath, string scenariosPath) =>
        LoadJson(File.ReadAllText(contentPath), File.ReadAllText(scenariosPath),
            File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(contentPath))!, "content.schema.json")),
            File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(scenariosPath))!, "scenarios.schema.json")));

    public static MainContent LoadJson(string contentJson, string scenariosJson,
        string? contentSchemaJson = null, string? scenariosSchemaJson = null)
    {
        try
        {
            if (contentSchemaJson is not null) ScenarioSchemaValidator.ValidateWithSchema(contentJson, contentSchemaJson);
            if (scenariosSchemaJson is not null) ScenarioSchemaValidator.ValidateWithSchema(scenariosJson, scenariosSchemaJson);
            var common = JsonSerializer.Deserialize<ContentDocument>(contentJson, Options)
                ?? throw new MainContentException("main 通用内容为空。");
            var scenarios = JsonSerializer.Deserialize<ScenarioDocument>(scenariosJson, Options)
                ?? throw new MainContentException("main 时期内容为空。");
            var content = new MainContent
            {
                ContentVersion = $"main-sgby-{common.Source.Commit[..7]}",
                ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contentJson + scenariosJson))).ToLowerInvariant(),
                Source = $"main@{common.Source.Commit}/libs/SGBY.lib",
                Tools = common.Tools.Select(item => new MainTool
                {
                    Id = item.Id, SourceIndex = item.Index, Name = item.Name, UseFlag = item.Raw.useflag,
                    ChangeAttackRange = item.Raw.changeAttackRange, Attack = item.Raw.at,
                    Iq = item.Raw.iq, Move = item.Raw.move, Arms = item.Raw.arm
                }).ToList(),
                Skills = common.Skills.Select(item => new MainSkill
                {
                    Id = item.Id, SourceIndex = item.Index, Name = item.Name, Aim = item.Raw.aim,
                    State = item.Raw.state, Power = item.Raw.power, Destroy = item.Raw.destroy, UseMp = item.Raw.useMp,
                    Weather = item.Raw.weather, Eland = item.Raw.eland, Oland = item.Raw.oland, Earm = item.Raw.earm
                }).ToList(),
                Periods = scenarios.Periods.Select(ConvertPeriod).ToList()
            };
            Validate(content);
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

    private static MainPeriod ConvertPeriod(PeriodDocument document)
    {
        var cities = document.Cities.Select(item => new MainCity
        {
            Id = item.Id, SourceIndex = item.Index, Name = item.Name, State = item.Raw.State,
            Belong = item.OwnerPersonId ?? "", SatrapId = item.SatrapPersonId ?? "",
            FarmingLimit = item.Raw.FarmingLimit, Farming = item.Raw.Farming,
            CommerceLimit = item.Raw.CommerceLimit, Commerce = item.Raw.Commerce,
            PeopleDevotion = item.Raw.PeopleDevotion, AvoidCalamity = item.Raw.AvoidCalamity,
            PopulationLimit = item.Raw.PopulationLimit, Population = item.Raw.Population,
            Money = item.Raw.Money, Food = item.Raw.Food, MothballArms = item.Raw.MothballArms,
            PersonIds = item.PersonIds, ToolIds = item.ToolIds
        }).ToList();
        var persons = document.Persons.Select(item => new MainPerson
        {
            Id = item.Id, SourceIndex = item.Index, Name = item.Name,
            OldBelong = item.OldOwnerPersonId ?? "", Belong = item.OwnerPersonId ?? "",
            Level = item.Raw.Level, Force = item.Raw.Force, Iq = item.Raw.IQ, Devotion = item.Raw.Devotion,
            Character = item.Raw.Character, Experience = item.Raw.Experience, Thew = item.Raw.Thew,
            ArmsType = item.Raw.ArmsType, Arms = item.Raw.Arms, Tool1Id = item.Tool1Id ?? "", Tool2Id = item.Tool2Id ?? "",
            EquipIds = item.Raw.Equip.Select(ToolId).ToList(), Age = item.Raw.Age
        }).ToList();
        return new MainPeriod
        {
            Period = document.Period, Year = document.Date.Year, Month = document.Date.Month,
            RulerIds = cities.OrderBy(city => city.SourceIndex).Select(city => city.Belong)
                .Where(id => id.Length > 0).Distinct(StringComparer.Ordinal).ToList(),
            Cities = cities, Persons = persons
        };
    }

    private static string ToolId(int rawId) => rawId == 0 || rawId == 255 ? "" : $"tool-{rawId - 1:000}";

    private static void Validate(MainContent content)
    {
        if (content.Tools.Count != 33) throw new MainContentException($"道具数量应为 33，实际 {content.Tools.Count}。");
        if (content.Skills.Count != 30) throw new MainContentException($"技能数量应为 30，实际 {content.Skills.Count}。");
        if (content.Periods.Count != 4) throw new MainContentException($"时期数量应为 4，实际 {content.Periods.Count}。");
        EnsureUnique(content.Tools.Select(tool => tool.Id), "道具");
        EnsureUnique(content.Skills.Select(skill => skill.Id), "技能");
        foreach (var period in content.Periods.OrderBy(item => item.Period))
        {
            if (period.Year < 1 || period.Month is < 1 or > 12) throw new MainContentException("时期日期非法。");
            if (period.Cities.Count != 38) throw new MainContentException($"时期 {period.Period} 城市数量不是 38。");
            if (period.Persons.Count != 200) throw new MainContentException($"时期 {period.Period} 人物记录数量不是 200。");
            EnsureUnique(period.Cities.Select(city => city.Id), $"时期 {period.Period} 城市");
            EnsureUnique(period.Persons.Select(person => person.Id), $"时期 {period.Period} 人物");
            var personIds = period.Persons.Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
            var toolIds = content.Tools.Select(tool => tool.Id).ToHashSet(StringComparer.Ordinal);
            if (period.RulerIds.Count == 0 || period.RulerIds.Any(id => !personIds.Contains(id)))
                throw new MainContentException($"时期 {period.Period} 君主引用非法。");
            foreach (var city in period.Cities)
            {
                if (city.Money < 0 || city.Food < 0 || city.Population < 0 || city.MothballArms < 0 ||
                    city.Farming < 0 || city.Farming > city.FarmingLimit || city.Commerce < 0 || city.Commerce > city.CommerceLimit ||
                    city.Population > city.PopulationLimit)
                    throw new MainContentException($"城市 {city.Id} 数值非法。");
                if (!string.IsNullOrEmpty(city.Belong) && !personIds.Contains(city.Belong))
                    throw new MainContentException($"城市 {city.Id} 归属引用非法。");
                if (city.PersonIds.Any(id => !personIds.Contains(id)) || city.ToolIds.Any(id => !toolIds.Contains(id)))
                    throw new MainContentException($"城市 {city.Id} 人物或道具队列引用非法。");
                foreach (var personId in city.PersonIds)
                {
                    var person = period.Persons.Single(item => item.Id == personId);
                    person.CityId = city.Id;
                }
            }
            foreach (var person in period.Persons)
            {
                if (person.Force is < 0 or > 255 || person.Iq is < 0 or > 255 || person.Thew is < 0 or > 100)
                    throw new MainContentException($"人物 {person.Id} 属性或体力非法。");
                if (!string.IsNullOrEmpty(person.Tool1Id) && !toolIds.Contains(person.Tool1Id) ||
                    !string.IsNullOrEmpty(person.Tool2Id) && !toolIds.Contains(person.Tool2Id))
                    throw new MainContentException($"人物 {person.Id} 道具引用非法。");
            }
        }
    }

    private static void EnsureUnique(IEnumerable<string> ids, string kind)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids) if (!seen.Add(id)) throw new MainContentException($"{kind} ID 重复：{id}");
    }
}
