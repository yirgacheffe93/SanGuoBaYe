using Game.Domain;

namespace Game.Infrastructure;

internal static class MainContentValidator
{
    public static void Validate(MainContent content)
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
            if (period.PersonConditions.Count != 200 || period.ToolConditions.Count != 33)
                throw new MainContentException($"时期 {period.Period} 搜寻条件数量非法。");
            if (period.PersonConditions.Any(condition => condition.SourceIndex is < 0 or > 199 || condition.Birth is < 0 or > 255 ||
                condition.Bole is < 0 or > 200 || condition.CityIndex is < 0 or > 255) ||
                period.ToolConditions.Any(condition => condition.SourceIndex is < 0 or > 32 || condition.Birth is < 0 or > 255 ||
                condition.Bole is < 0 or > 200 || condition.CityIndex is < 0 or > 255))
                throw new MainContentException($"时期 {period.Period} 搜寻条件数值非法。");
            EnsureUnique(period.Cities.Select(city => city.Id), $"时期 {period.Period} 城市");
            EnsureUnique(period.Persons.Select(person => person.Id), $"时期 {period.Period} 人物");
            var personIds = period.Persons.Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
            var toolIds = content.Tools.Select(tool => tool.Id).ToHashSet(StringComparer.Ordinal);
            if (period.RulerIds.Count == 0 || period.RulerIds.Any(id => !personIds.Contains(id)))
                throw new MainContentException($"时期 {period.Period} 君主引用非法。");
            var cityIds = period.Cities.Select(city => city.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var city in period.Cities)
            {
                if (city.Connections.Count == 0 || city.Connections.Count > 8 || city.Connections.Distinct(StringComparer.Ordinal).Count() != city.Connections.Count ||
                    city.Connections.Any(id => !cityIds.Contains(id) || id == city.Id))
                    throw new MainContentException($"城市 {city.Id} 连接表非法。");
                foreach (var targetId in city.Connections)
                    if (!period.Cities.Single(target => target.Id == targetId).Connections.Contains(city.Id, StringComparer.Ordinal))
                        throw new MainContentException($"城市 {city.Id} 与 {targetId} 的连接不对称。");
                if (city.Money < 0 || city.Food < 0 || city.Population < 0 || city.MothballArms < 0 ||
                    city.Farming < 0 || city.Farming > city.FarmingLimit || city.Commerce < 0 || city.Commerce > city.CommerceLimit ||
                    city.Population > city.PopulationLimit)
                    throw new MainContentException($"城市 {city.Id} 数值非法。");
                if (!string.IsNullOrEmpty(city.Belong) && !personIds.Contains(city.Belong))
                    throw new MainContentException($"城市 {city.Id} 归属引用非法。");
                if (city.PersonIds.Any(id => !personIds.Contains(id)) || city.ToolIds.Any(id => !toolIds.Contains(id)) ||
                    city.ToolEntries.Any(entry => !toolIds.Contains(entry.ToolId)))
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
        MainBattleValidator.Validate(content);
    }

    private static void EnsureUnique(IEnumerable<string> ids, string kind)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids) if (!seen.Add(id)) throw new MainContentException($"{kind} ID 重复：{id}");
    }
}
