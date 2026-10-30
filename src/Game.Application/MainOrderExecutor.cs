using Game.Domain;

namespace Game.Application;

public static class MainOrderExecutor
{
    public static IReadOnlyList<string> Execute(MainWorld world, MainContent content)
    {
        var messages = new List<string>();
        foreach (var order in world.Orders.OrderBy(item => item.Id, StringComparer.Ordinal).ToList())
        {
            switch (order.OrderId)
            {
                case MainCommandCatalog.SearchOrderId:
                    messages.Add(MainSearchRules.Execute(world, content, order));
                    ReturnPerson(world, order, order.CityId);
                    break;
                case MainCommandCatalog.TransportOrderId:
                    messages.Add(ResolveTransport(world, order));
                    ReturnPerson(world, order, order.CityId);
                    break;
                case MainCommandCatalog.MoveOrderId:
                    messages.Add(ResolveMove(world, order));
                    break;
                case MainCommandCatalog.AlienateOrderId:
                case MainCommandCatalog.CanvassOrderId:
                case MainCommandCatalog.CounterespionageOrderId:
                case MainCommandCatalog.InduceOrderId:
                    messages.Add(MainDiplomacyRules.Execute(world, order));
                    ReturnPerson(world, order, order.CityId);
                    break;
                case MainCommandCatalog.BattleOrderId:
                    messages.Add(MainBattleService.ResolveAuto(world, content, order));
                    break;
                case MainCommandCatalog.PlunderOrderId:
                    var city = world.Cities.Single(city => city.Id == order.CityId);
                    messages.Add(MainMilitaryRules.Plunder(city, world.Persons.Single(person => person.Id == order.PersonId)));
                    ReturnPerson(world, order, order.CityId);
                    break;
                case MainCommandCatalog.RecruitOrderId:
                    messages.Add(MainDispositionRules.Recruit(world, order));
                    ReturnPerson(world, order, order.CityId);
                    break;
                default:
                    ReturnPerson(world, order, order.CityId);
                    break;
            }
            world.Orders.Remove(order);
        }
        var turn = world.Year * 12 + world.Month;
        foreach (var message in messages) world.Reports.Add(new ReportEntry { Turn = turn, Message = message });
        return messages;
    }

    private static string ResolveTransport(MainWorld world, MainOrder order)
    {
        var person = world.Persons.Single(item => item.Id == order.PersonId);
        var target = world.Cities.Single(item => item.Id == order.ObjectId);
        if (MainRandom.Next(world, 100) > 20)
        {
            target.Food = (target.Food + order.Food) & 0xffff;
            target.Money = (target.Money + order.Money) & 0xffff;
            target.MothballArms = (target.MothballArms + order.Arms) & 0xffff;
            return $"{person.Name}输送的物资已到达{target.Name}。";
        }
        return $"{person.Name}输送的物资全部损失。";
    }

    private static string ResolveMove(MainWorld world, MainOrder order)
    {
        var person = world.Persons.Single(item => item.Id == order.PersonId);
        var target = world.Cities.Single(item => item.Id == order.ObjectId);
        if (target.Belong.Length == 0) target.Belong = person.Belong;
        if (target.Belong == person.Belong)
        {
            ReturnPerson(world, order, target.Id);
            return $"{person.Name}已到达{target.Name}。";
        }
        ReturnPerson(world, order, order.CityId);
        return $"{person.Name}未能进入{target.Name}，已返回原城。";
    }

    private static void ReturnPerson(MainWorld world, MainOrder order, string cityId)
    {
        var city = world.Cities.Single(item => item.Id == cityId);
        var person = world.Persons.Single(item => item.Id == order.PersonId);
        person.OrderId = null;
        MainPersonLocationRules.EnterCity(city, person);
    }
}
