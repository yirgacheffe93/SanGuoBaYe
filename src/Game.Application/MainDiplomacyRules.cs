using Game.Domain;

namespace Game.Application;

public static class MainDiplomacyRules
{
    public static bool IsEnemyOfficer(MainWorld world, MainPerson target, string? rulerId = null)
    {
        var actingRulerId = rulerId ?? world.PlayerRulerId;
        return target.Status == MainPersonStatus.Active && target.Id != actingRulerId && target.Id != target.Belong &&
            world.Cities.Any(city => city.Id == target.CityId && city.Belong.Length > 0 && city.Belong == target.Belong && city.Belong != actingRulerId);
    }

    public static bool IsEnemySatrap(MainWorld world, MainPerson target, string? rulerId = null) =>
        IsEnemyOfficer(world, target, rulerId) && world.Cities.Any(city => city.SatrapId == target.Id);

    public static bool IsEnemyRuler(MainWorld world, MainPerson target, string? rulerId = null) =>
        target.Id != (rulerId ?? world.PlayerRulerId) && world.Cities.Any(city => city.Belong == target.Id);

    public static string Execute(MainWorld world, MainOrder order) => order.OrderId switch
    {
        MainCommandCatalog.AlienateOrderId => Alienate(world, order),
        MainCommandCatalog.CanvassOrderId => Canvass(world, order),
        MainCommandCatalog.CounterespionageOrderId => Counterespionage(world, order),
        MainCommandCatalog.InduceOrderId => "劝降未产生效果（与当前 main 生产行为一致）。",
        _ => throw new ArgumentOutOfRangeException(nameof(order))
    };

    public static string DescribeCity(MainCity city) =>
        $"侦察{city.Name}：金 {city.Money}，粮 {city.Food}，后备兵 {city.MothballArms}，农业 {city.Farming}/{city.FarmingLimit}，商业 {city.Commerce}/{city.CommerceLimit}，民忠 {city.PeopleDevotion}，人口 {city.Population}/{city.PopulationLimit}。";

    private static string Alienate(MainWorld world, MainOrder order)
    {
        var (actor, target) = Persons(world, order);
        var gate = (actor.Iq - target.Iq + 50) & 0xff;
        if (MainRandom.Next(world, 100) > gate || MainRandom.Next(world, 100) < target.Devotion ||
            MainRandom.Next(world, 100) > (target.Character switch { 0 => 50, 1 => 30, 2 => 40, 3 => 30, 4 => 5, _ => 0 }))
            return $"{target.Name}未被离间。";
        target.Devotion = Math.Max(0, target.Devotion - 4);
        return $"{target.Name}被离间，忠诚降至 {target.Devotion}。";
    }

    private static string Canvass(MainWorld world, MainOrder order)
    {
        var (actor, target) = Persons(world, order);
        var gate = (actor.Iq - target.Iq) & 0xff;
        var success = MainRandom.Next(world, 100) <= gate && MainRandom.Next(world, 100) >= target.Devotion &&
            MainRandom.Next(world, 100) <= (target.Character switch { 0 => 15, 1 => 40, 2 => 30, 3 => 20, 4 => 5, _ => 0 });
        if (!success)
        {
            if (actor.Belong == world.PlayerRulerId) MainRandom.Next(world, 3);
            return $"{target.Name}拒绝招揽。";
        }
        if (target.CityId.Length == 0)
        {
            if (actor.Belong == world.PlayerRulerId) MainRandom.Next(world, 3);
            return $"{target.Name}已不在城中，招揽失败。";
        }
        var source = world.Cities.Single(city => city.Id == target.CityId);
        var destination = world.Cities.Single(city => city.Id == order.CityId);
        source.PersonIds.Remove(target.Id);
        if (!destination.PersonIds.Contains(target.Id, StringComparer.Ordinal)) destination.PersonIds.Insert(0, target.Id);
        target.CityId = destination.Id;
        target.Belong = actor.Belong;
        target.Devotion = 40 + MainRandom.Next(world, 20);
        if (actor.Belong == world.PlayerRulerId) MainRandom.Next(world, 5);
        return $"{target.Name}已加入我方，忠诚 {target.Devotion}。";
    }

    private static string Counterespionage(MainWorld world, MainOrder order)
    {
        var (actor, target) = Persons(world, order);
        var gate = (uint)(actor.Iq - target.Iq + 50);
        var success = MainRandom.Next(world, 100) <= gate && MainRandom.Next(world, 100) >= target.Devotion &&
            MainRandom.Next(world, 100) <= (target.Character switch { 0 => 30, 1 => 10, 2 => 20, 3 => 60, 4 => 5, _ => 0 });
        if (!success)
        {
            if (actor.Belong == world.PlayerRulerId) MainRandom.Next(world, 2);
            return $"{target.Name}拒绝独立。";
        }
        if (target.CityId.Length == 0)
        {
            if (actor.Belong == world.PlayerRulerId) MainRandom.Next(world, 2);
            return $"{target.Name}已不在城中，策反失败。";
        }
        var city = world.Cities.Single(city => city.Id == target.CityId);
        city.Belong = target.Id;
        foreach (var personId in city.PersonIds)
        {
            var person = world.Persons.Single(item => item.Id == personId);
            if (person.Belong.Length > 0 && person.Status == MainPersonStatus.Active) person.Belong = target.Id;
        }
        if (actor.Belong == world.PlayerRulerId) MainRandom.Next(world, 3);
        MainRandom.Next(world, 100);
        return $"{target.Name}已在{city.Name}独立。";
    }

    private static (MainPerson Actor, MainPerson Target) Persons(MainWorld world, MainOrder order) =>
        (world.Persons.Single(person => person.Id == order.PersonId), world.Persons.Single(person => person.Id == order.ObjectId));
}
