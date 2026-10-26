using Game.Domain;

namespace Game.Application;

public static class MainDispositionRules
{
    public static string Recruit(MainWorld world, MainOrder order)
    {
        var actor = world.Persons.Single(person => person.Id == order.PersonId);
        var target = world.Persons.Single(person => person.Id == order.ObjectId);
        var gate = (actor.Iq - target.Iq + 50) & 0xff;
        var first = MainRandom.Next(world, 100);
        if (first > gate) return Failure(world, actor, target, "拒绝投降", false);
        var devotion = target.Devotion - target.Devotion / 10;
        if (devotion > 60) return Failure(world, actor, target, "拒绝投降");
        var modifier = target.Character switch { 0 => 2, 1 => 5, 2 => 4, 3 => 3, 4 => 1, _ => 1 };
        var second = MainRandom.Next(world, 100);
        if (second < devotion / modifier) return Failure(world, actor, target, "拒绝投降");
        target.Status = MainPersonStatus.Active;
        target.Belong = actor.Belong;
        target.Devotion = 40 + MainRandom.Next(world, 40);
        return $"{target.Name}已归降，忠诚 {target.Devotion}。";
    }

    public static string Execute(MainWorld world, MainContent content, MainCity city, MainPerson target)
    {
        AddEquipment(city, content, target.Tool1Id);
        AddEquipment(city, content, target.Tool2Id);
        city.PersonIds.Remove(target.Id);
        target.CityId = "";
        target.Status = MainPersonStatus.Executed;
        MainRandom.Next(world, 3);
        return $"{target.Name}已被处斩，装备已入城。";
    }

    public static string Exile(MainWorld world, MainCity city, MainPerson target)
    {
        city.PersonIds.Remove(target.Id);
        target.Status = MainPersonStatus.Free;
        target.Belong = "";
        var destination = world.Cities[MainRandom.Next(world, world.Cities.Count)];
        destination.PersonIds.Insert(0, target.Id);
        target.CityId = destination.Id;
        return $"{target.Name}已流放至{destination.Name}。";
    }

    private static string Failure(MainWorld world, MainPerson actor, MainPerson target, string reason, bool reduceDevotion = true)
    {
        if (reduceDevotion) target.Devotion = Math.Max(0, target.Devotion - target.Devotion / 10);
        if (actor.Belong == world.PlayerRulerId) MainRandom.Next(world, 4);
        return $"{target.Name}{reason}。";
    }

    private static void AddEquipment(MainCity city, MainContent content, string toolId)
    {
        if (toolId.Length == 0) return;
        if (!content.Tools.Any(tool => tool.Id == toolId)) throw new InvalidOperationException($"未知装备：{toolId}");
        city.ToolEntries.Add(new MainCityToolEntry { ToolId = toolId, Discovered = true });
    }
}
