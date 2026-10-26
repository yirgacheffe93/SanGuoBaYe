using Game.Domain;

namespace Game.Application;

public static class MainOrderExecutor
{
    public static IReadOnlyList<string> Execute(MainWorld world)
    {
        var messages = new List<string>();
        foreach (var order in world.Orders.OrderBy(item => item.Id, StringComparer.Ordinal).ToList())
        {
            if (order.OrderId == MainCommandCatalog.SearchOrderId)
            {
                var message = ExecuteSearch(world, order);
                if (message.Length > 0) messages.Add(message);
            }
            ReturnPerson(world, order);
            world.Orders.Remove(order);
        }
        var turn = world.Year * 12 + world.Month;
        foreach (var message in messages) world.Reports.Add(new ReportEntry { Turn = turn, Message = message });
        return messages;
    }

    private static string ExecuteSearch(MainWorld world, MainOrder order)
    {
        var person = world.Persons.Single(item => item.Id == order.PersonId);
        var city = world.Cities.Single(item => item.Id == order.CityId);
        var branch = MainRandom.Next(world, 4);
        switch (branch)
        {
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
            case 0:
                return $"{city.Name}：{person.Name}搜寻无收获。";
            default:
                return $"{city.Name}：{person.Name}搜寻的人物/道具分支将在后续里程碑开放。";
        }
    }

    private static void ReturnPerson(MainWorld world, MainOrder order)
    {
        var city = world.Cities.Single(item => item.Id == order.CityId);
        var person = world.Persons.Single(item => item.Id == order.PersonId);
        person.CityId = order.CityId;
        person.OrderId = null;
        if (!city.PersonIds.Contains(person.Id, StringComparer.Ordinal)) city.PersonIds.Insert(0, person.Id);
    }
}
