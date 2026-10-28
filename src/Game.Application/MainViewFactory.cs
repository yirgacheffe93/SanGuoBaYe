using Game.Domain;

namespace Game.Application;

public static class MainViewFactory
{
    public static MainGameView Create(MainWorld world) => new(
        world.Period,
        world.Year,
        world.Month,
        world.PlayerRulerId,
        world.Cities.OrderBy(city => city.SourceIndex).Select(city => new MainCityView(
            city.Id, city.Name, city.Belong, city.X, city.Y, city.Farming, city.FarmingLimit,
            city.Commerce, city.CommerceLimit, city.PeopleDevotion, city.State, city.AvoidCalamity,
            city.Population, city.PopulationLimit, city.Money, city.Food, city.MothballArms,
            city.ToolEntries.Where(entry => entry.Discovered).Select(entry => entry.ToolId).ToList(), city.Connections)).ToList(),
        world.Persons.Where(person => person.Name.Length > 0).OrderBy(person => person.SourceIndex).Select(person => new MainPersonView(
            person.Id, person.Name, person.CityId, person.Belong, person.Status, person.Level, person.Force, person.Iq, person.Devotion,
            person.Thew, person.Arms, person.Tool1Id, person.Tool2Id, person.OrderId is not null)).ToList(),
        world.Orders.OrderBy(order => order.Id, StringComparer.Ordinal).Select(order => new MainOrderView(
            order.Id, order.OrderId, order.CityId, world.Cities.Single(city => city.Id == order.CityId).Name,
            world.Persons.Single(person => person.Id == order.PersonId).Name, order.Money, order.TimeCount)).ToList(),
        world.Reports.OrderBy(report => report.Turn).Select(report => new ReportEntryView(report.Turn, report.Message)).ToList());
}
