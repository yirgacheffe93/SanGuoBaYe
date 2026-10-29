using Game.Domain;

namespace Game.Application;

public sealed record MainSearchDiagnostics(int PersonCandidates, int ToolCandidates, string Message);

public static class MainSearchRules
{
    public static MainSearchDiagnostics Describe(MainWorld world, MainContent content, MainCity city, MainPerson person)
    {
        var personCandidates = city.PersonIds.Count(id => world.Persons.Single(candidate => candidate.Id == id).Status == MainPersonStatus.Free);
        var toolCandidates = city.ToolEntries.Count(entry => !entry.Discovered);
        var period = content.Periods.Single(period => period.Period == world.Period);
        var hasBoleMatch = city.ToolEntries.Where(entry => !entry.Discovered).Any(entry =>
            period.ToolConditions[content.Tools.Single(tool => tool.Id == entry.ToolId).SourceIndex].Bole is 0 || period.ToolConditions[content.Tools.Single(tool => tool.Id == entry.ToolId).SourceIndex].Bole - 1 == person.SourceIndex);
        var message = personCandidates == 0 && toolCandidates == 0 ? "本城没有可搜寻人物或道具"
            : !hasBoleMatch && toolCandidates > 0 ? "本城道具均不符合当前人物的伯乐条件"
            : $"在野人物 {personCandidates} 人，未发现道具 {toolCandidates} 件";
        return new MainSearchDiagnostics(personCandidates, toolCandidates, message);
    }

    public static string Execute(MainWorld world, MainContent content, MainOrder order)
    {
        var person = world.Persons.Single(item => item.Id == order.PersonId);
        var city = world.Cities.Single(item => item.Id == order.CityId);
        var branch = MainRandom.Next(world, 4);
        switch (branch)
        {
            case 0:
                return $"{city.Name}：{person.Name}搜寻无收获。";
            case 1:
                return ExecuteSpecial(world, content, city, person);
            case 2:
            {
                var gain = 10 + MainRandom.Next(world, person.Iq * 2);
                city.Money = Math.Min(65535, city.Money + gain);
                return $"{city.Name}：{person.Name}搜寻获得 {gain} 金。";
            }
            case 3:
            {
                var gain = 10 + MainRandom.Next(world, person.Iq * 2);
                city.Food = Math.Min(65535, city.Food + gain);
                return $"{city.Name}：{person.Name}搜寻获得 {gain} 粮。";
            }
            default:
                throw new InvalidOperationException("搜寻分支非法。");
        }
    }

    private static string ExecuteSpecial(MainWorld world, MainContent content, MainCity city, MainPerson person)
    {
        var roll = MainRandom.Next(world, 150);
        if (roll >= person.Iq) return $"{city.Name}：{person.Name}搜寻无收获。";
        if (roll % 2 == 0) return ExecutePersonSearch(world, content, city, person);
        return ExecuteToolSearch(world, content, city, person);
    }

    private static string ExecutePersonSearch(MainWorld world, MainContent content, MainCity city, MainPerson person)
    {
        var candidates = city.PersonIds.Select(id => world.Persons.Single(person => person.Id == id))
            .Where(candidate => candidate.Status == MainPersonStatus.Free).ToList();
        if (candidates.Count == 0) return $"{city.Name}：本城没有可招揽的在野人物。";
        var candidate = candidates[MainRandom.Next(world, candidates.Count)];
        var period = content.Periods.Single(period => period.Period == world.Period);
        var condition = period.PersonConditions[candidate.SourceIndex];
        var success = condition.Bole - 1 == person.SourceIndex;
        if (!success && condition.Bole == 0) success = MainRandom.Next(world, 110) < person.Iq;
        if (success)
        {
            candidate.Belong = person.Belong;
            candidate.Status = MainPersonStatus.Active;
            candidate.Devotion = 70 + MainRandom.Next(world, 30);
        }
        if (person.Belong == world.PlayerRulerId) MainRandom.Next(world, 4);
        return success
            ? $"{city.Name}：{person.Name}成功招揽{candidate.Name}，忠诚 {candidate.Devotion}。"
            : $"{city.Name}：{candidate.Name}拒绝了{person.Name}的招揽。";
    }

    private static string ExecuteToolSearch(MainWorld world, MainContent content, MainCity city, MainPerson person)
    {
        var candidates = city.ToolEntries.Where(entry => !entry.Discovered).ToList();
        if (candidates.Count == 0) return $"{city.Name}：本城没有未发现道具。";
        var entry = candidates[MainRandom.Next(world, candidates.Count)];
        var period = content.Periods.Single(period => period.Period == world.Period);
        var condition = period.ToolConditions[content.Tools.Single(tool => tool.Id == entry.ToolId).SourceIndex];
        if (condition.Bole != 0 && condition.Bole - 1 != person.SourceIndex)
            return $"{city.Name}：{person.Name}的伯乐条件不符合，未能发现道具。";
        var tool = content.Tools.Single(tool => tool.Id == entry.ToolId);
        if (!MarkFirstUndiscovered(city, tool.Id))
            throw new InvalidOperationException("发现道具后未找到可提交的队列项。");
        return $"{city.Name}：{person.Name}发现道具{tool.Name}。";
    }

    private static bool MarkFirstUndiscovered(MainCity city, string toolId)
    {
        var entry = city.ToolEntries.FirstOrDefault(item => item.ToolId == toolId && !item.Discovered);
        if (entry is null) return false;
        entry.Discovered = true;
        return true;
    }
}
