using Game.Domain;

namespace Game.Application;

public static class MainOrderExecutor
{
    public static IReadOnlyList<string> Execute(MainWorld world, MainContent content)
    {
        var messages = new List<string>();
        foreach (var order in world.Orders.OrderBy(item => item.Id, StringComparer.Ordinal).ToList())
        {
            if (order.OrderId == MainCommandCatalog.SearchOrderId)
            {
                var message = MainSearchRules.Execute(world, content, order);
                if (message.Length > 0) messages.Add(message);
            }
            ReturnPerson(world, order);
            world.Orders.Remove(order);
        }
        var turn = world.Year * 12 + world.Month;
        foreach (var message in messages) world.Reports.Add(new ReportEntry { Turn = turn, Message = message });
        return messages;
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
