using Game.Domain;

namespace Game.Application;

public static class MainAiPlanner
{
    public static void Plan(MainWorld world)
    {
        foreach (var city in world.Cities.Where(city => city.Belong.Length > 0 && city.Belong != world.PlayerRulerId)
                     .OrderBy(city => city.SourceIndex))
        {
            if (world.Orders.Any(order => order.CityId == city.Id)) continue;
            var person = city.PersonIds.Select(id => world.Persons.Single(person => person.Id == id))
                .Where(person => person.Belong == city.Belong && person.OrderId is null && person.Thew >= MainCommandCatalog.ReclaimThewCost)
                .OrderBy(person => person.SourceIndex).FirstOrDefault();
            if (person is null || city.Money < MainCommandCatalog.ReclaimCost) continue;
            var commandId = city.Farming < city.FarmingLimit ? MainCommandCatalog.ReclaimOrderId :
                city.Commerce < city.CommerceLimit ? MainCommandCatalog.CommerceOrderId : 0;
            if (commandId != 0) MainCommandService.IssueInternal(world, commandId, city.Id, person.Id);
        }
    }
}
