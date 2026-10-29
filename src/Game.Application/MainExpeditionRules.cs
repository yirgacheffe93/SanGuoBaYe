using Game.Domain;

namespace Game.Application;

public static class MainExpeditionRules
{
    public static MainCommandResult Accept(MainWorld world, MainCommandRequest request)
    {
        var source = world.Cities.Single(city => city.Id == request.CityId);
        var order = new MainOrder
        {
            Id = $"main.order.{world.NextSequence++}", OrderId = MainCommandCatalog.BattleOrderId,
            CityId = source.Id, ObjectId = request.TargetCityId!, PersonId = request.PersonId,
            ParticipantIds = request.ParticipantIds ?? [request.PersonId], Food = request.Food, FoodPaid = true, TimeCount = 0
        };
        order.TimeCount = Math.Abs(source.NativeX - world.Cities.Single(city => city.Id == order.ObjectId).NativeX) +
            Math.Abs(source.NativeY - world.Cities.Single(city => city.Id == order.ObjectId).NativeY);
        source.Food -= request.Food;
        foreach (var person in order.ParticipantIds.Select(id => world.Persons.Single(person => person.Id == id)))
        {
            person.OrderId = order.Id;
            person.CityId = "";
            source.PersonIds.Remove(person.Id);
        }
        world.Orders.Add(order);
        return new MainCommandResult(true, "accepted", $"出征部队已编成，携带 {order.Food} 粮，可进入战场。", order.Id);
    }
}
