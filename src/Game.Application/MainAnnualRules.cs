using Game.Domain;

namespace Game.Application;

public static class MainAnnualRules
{
    public static IReadOnlyList<string> Update(MainWorld world, MainContent content)
    {
        var period = content.Periods.Single(item => item.Period == world.Period);
        foreach (var condition in period.ToolConditions.Where(condition => condition.Birth == world.Year))
        {
            var city = world.Cities[condition.CityIndex];
            var tool = content.Tools[condition.SourceIndex];
            city.ToolEntries.Add(new MainCityToolEntry { ToolId = tool.Id, Discovered = false });
            city.ToolIds.Add(tool.Id);
        }
        var messages = new List<string>();
        foreach (var person in world.Persons) person.Age = (person.Age + 1) & 0xff;
        foreach (var condition in period.PersonConditions.Where(condition => condition.Birth + 16 == world.Year))
        {
            var cityIndex = condition.CityIndex == 0 ? MainRandom.Next(world, world.Cities.Count) : condition.CityIndex;
            if (cityIndex >= world.Cities.Count) continue;
            var city = world.Cities[cityIndex];
            var person = world.Persons[condition.SourceIndex];
            if (!city.PersonIds.Contains(person.Id, StringComparer.Ordinal)) city.PersonIds.Insert(0, person.Id);
            person.CityId = city.Id;
            person.Age = 16;
            messages.Add($"{city.Name}：{person.Name}已出场。");
        }
        return messages;
    }
}
