using Game.Domain;

namespace Game.Application;

public static class MainAiPlanner
{
    private static readonly int[] InteriorOdds = [50, 40, 30, 20, 10];
    private static readonly int[] DiplomacyOdds = [80, 70, 70, 40, 20];

    public static void Plan(MainWorld world)
    {
        foreach (var city in world.Cities.OrderBy(city => city.SourceIndex))
        {
            city.State = 0;
            if (city.Belong.Length == 0 || city.Belong == world.PlayerRulerId) continue;
            city.AvoidCalamity = Math.Min(100, city.AvoidCalamity + 1);
            if (city.Food < 100) city.Food = 500;
            if (city.Money > 10000) city.Money /= 2;
            var roll = MainRandom.Next(world, 100);
            var character = world.Persons.Single(person => person.Id == city.Belong).Character;
            if (InteriorOdds[character] > roll)
            {
                Interior(world, city);
                Harmonize(world, city);
            }
            else if (DiplomacyOdds[character] > roll) Diplomacy(world, city);
            else Armament(world, city);
        }
    }

    private static void Interior(MainWorld world, MainCity city)
    {
        foreach (var person in Officers(world, city))
        {
            var commandId = MainRandom.Next(world, 5) switch
            {
                0 => Develop(city, farming: true),
                1 => Develop(city, farming: false),
                2 => MainCommandCatalog.SearchOrderId,
                3 => Inspect(city),
                _ => 0
            };
            if (commandId != 0) Queue(world, city, person, commandId, city.Id);
        }
    }

    private static void Harmonize(MainWorld world, MainCity city)
    {
        var officers = Officers(world, city);
        for (var i = 0; i < officers.Count; i++)
        {
            var roll = MainRandom.Next(world, 7);
            if (roll == 0)
            {
                city.State = 0;
                city.AvoidCalamity = Math.Min(100, city.AvoidCalamity + 4);
                Queue(world, city, officers[i], MainCommandCatalog.GovernOrderId, city.Id);
            }
            else if (roll == 5 && i >= 3)
            {
                var ownCities = world.Cities.Where(item => item.Belong == city.Belong).OrderBy(item => item.SourceIndex).ToList();
                if (ownCities.Count < 2) continue;
                var target = ownCities[0];
                foreach (var candidate in ownCities)
                {
                    if (!candidate.Connections.Any(id => world.Cities.Single(item => item.Id == id).Belong is { Length: > 0 } other && other != candidate.Belong)) continue;
                    if (MainRandom.Next(world, 2) != 0)
                    {
                        target = candidate;
                        break;
                    }
                }
                Queue(world, city, officers[i], MainCommandCatalog.MoveOrderId, target.Id);
            }
        }
    }

    private static void Diplomacy(MainWorld world, MainCity city)
    {
        foreach (var person in Officers(world, city))
        {
            var roll = MainRandom.Next(world, 8);
            var captives = city.PersonIds.Select(id => world.Persons.Single(person => person.Id == id))
                .Where(person => person.Status == MainPersonStatus.Captive).ToList();
            if (captives.Count > 0) roll = 0;
            if (roll == 0)
            {
                if (captives.Count > 0)
                {
                    var target = captives[MainRandom.Next(world, captives.Count)];
                    target.Belong = city.Belong;
                    target.Status = MainPersonStatus.Active;
                }
                continue;
            }
            if (roll == 1)
            {
                if (captives.Count > 0)
                {
                    var target = captives[MainRandom.Next(world, captives.Count)];
                    city.PersonIds.Remove(target.Id);
                    target.CityId = "";
                    target.Status = MainPersonStatus.Executed;
                }
                continue;
            }
            if (roll is < 3 or 7) continue;
            var candidates = EnemyOfficers(world, city, satrapsOnly: roll == 5, rulersOnly: roll == 6);
            if (candidates.Count == 0) continue;
            var targetPerson = candidates[MainRandom.Next(world, candidates.Count)];
            var commandId = roll switch
            {
                3 => MainCommandCatalog.AlienateOrderId,
                4 => MainCommandCatalog.CanvassOrderId,
                5 => MainCommandCatalog.CounterespionageOrderId,
                _ => MainCommandCatalog.InduceOrderId
            };
            Queue(world, city, person, commandId, targetPerson.Id);
        }
    }

    private static void Armament(MainWorld world, MainCity city)
    {
        var officers = Officers(world, city);
        if (officers.Count == 0) return;
        var selected = officers[MainRandom.Next(world, officers.Count)];
        if (world.Month % 3 == 0) selected.Level = Math.Min(20, selected.Level + 1);
        for (var i = 0; i < officers.Count; i++)
        {
            var roll = MainRandom.Next(world, 9);
            if (roll is >= 1 and <= 5) selected.Arms = MaxArms(selected);
            else if (roll == 7 && i == 0)
            {
                var enemies = city.Connections.Select(id => world.Cities.Single(item => item.Id == id))
                    .Where(target => target.Belong.Length > 0 && target.Belong != city.Belong).ToList();
                if (enemies.Count == 0) continue;
                if (MainRandom.Next(world, enemies.Count * 2) >= enemies.Count) continue;
                var strongest = officers.OrderByDescending(person => person.Arms).ThenBy(person => person.SourceIndex).ToList();
                if (strongest[0].Arms < 1000 || strongest.Count < 4) continue;
            }
        }
    }

    private static int Develop(MainCity city, bool farming)
    {
        if (farming) city.Farming = Math.Min(city.FarmingLimit, city.Farming + 200);
        else city.Commerce = Math.Min(city.CommerceLimit, city.Commerce + 200);
        return farming ? MainCommandCatalog.ReclaimOrderId : MainCommandCatalog.CommerceOrderId;
    }

    private static int Inspect(MainCity city)
    {
        city.PeopleDevotion = Math.Min(100, city.PeopleDevotion + 4);
        city.Population = Math.Min(city.PopulationLimit, city.Population + 100);
        return MainCommandCatalog.InspectOrderId;
    }

    private static List<MainPerson> Officers(MainWorld world, MainCity city) => city.PersonIds
        .Select(id => world.Persons.Single(person => person.Id == id))
        .Where(person => person.Belong == city.Belong && person.Status == MainPersonStatus.Active).ToList();

    private static List<MainPerson> EnemyOfficers(MainWorld world, MainCity source, bool satrapsOnly, bool rulersOnly)
    {
        var ownerIndex = world.Persons.Single(person => person.Id == source.Belong).SourceIndex;
        var excludedRulerId = world.Persons.SingleOrDefault(person => person.SourceIndex == ownerIndex + 1)?.Id;
        return world.Cities.Where(city => city.Belong.Length > 0 && city.Belong != excludedRulerId)
            .SelectMany(city => city.PersonIds.Select(id => world.Persons.Single(person => person.Id == id))
                .Where(person => person.Belong == city.Belong && person.Status == MainPersonStatus.Active &&
                    (rulersOnly || person.Id != city.Belong) && (!satrapsOnly || city.SatrapId == person.Id)))
            .ToList();
    }

    private static int MaxArms(MainPerson person) => person.Level * 100 + person.Force * 10 + person.Iq * 10;

    private static void Queue(MainWorld world, MainCity city, MainPerson person, int commandId, string objectId)
    {
        var order = new MainOrder
        {
            Id = $"main.order.{world.NextSequence++}", OrderId = commandId, CityId = city.Id, PersonId = person.Id,
            ObjectId = objectId, TimeCount = 0
        };
        person.OrderId = order.Id;
        person.CityId = "";
        city.PersonIds.Remove(person.Id);
        world.Orders.Add(order);
    }
}
