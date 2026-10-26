using Game.Domain;

namespace Game.Application;

public static class MainMonthService
{
    public static IReadOnlyList<string> Update(MainWorld world)
    {
        var messages = new List<string>();
        world.Month++;
        if (world.Month > 12)
        {
            world.Month = 1;
            world.Year++;
            foreach (var person in world.Persons) person.Age = Math.Min(255, person.Age + 1);
            messages.Add($"进入 {world.Year} 年，人物年龄增长。");
        }
        if (world.Month is 6 or 10)
            foreach (var city in world.Cities)
                if (city.Food < 30000)
                {
                    var gain = city.Farming / 4;
                    city.Food += gain;
                    if (gain > 0) messages.Add($"{city.Name}收获粮 +{gain}。");
                }
        if (world.Month % 3 == 0)
            foreach (var city in world.Cities)
                if (city.Money < 30000)
                {
                    var gain = city.Commerce / 2;
                    city.Money += gain;
                    if (gain > 0) messages.Add($"{city.Name}税收金 +{gain}。");
                }
        foreach (var person in world.Persons) person.Thew = Math.Min(100, person.Thew + 4);
        foreach (var city in world.Cities.OrderBy(city => city.SourceIndex))
        {
            if (city.Belong.Length == 0) continue;
            if (world.Month % 3 == 0)
            {
                var reduction = 1 + MainRandom.Next(world, 4);
                if (city.AvoidCalamity > reduction) city.AvoidCalamity -= reduction;
            }
            city.Money = Math.Min(30000, city.Money);
            city.Population = Math.Min(city.PopulationLimit, city.Population + 50);
            var upkeep = city.MothballArms;
            var persons = city.PersonIds.Select(id => world.Persons.Single(person => person.Id == id))
                .Where(person => person.Belong == city.Belong).ToList();
            foreach (var person in persons)
            {
                var arms = person.Arms;
                if (city.State is 2 or 3) arms -= arms / 4;
                else if (city.State == 4) arms /= 2;
                upkeep += arms;
                person.Arms = arms;
            }
            upkeep /= 50;
            if (city.Food > upkeep) city.Food -= upkeep;
            else
            {
                foreach (var person in persons) person.Arms /= 2;
                city.Food = 0;
                city.State = 1;
            }
        }
        ApplyRandomEvents(world);
        var turn = world.Year * 12 + world.Month;
        foreach (var message in messages) world.Reports.Add(new ReportEntry { Turn = turn, Message = message });
        return messages;
    }

    private static void ApplyRandomEvents(MainWorld world)
    {
        foreach (var city in world.Cities.OrderBy(city => city.SourceIndex))
        {
            if (city.Belong.Length == 0) continue;
            var roll = MainRandom.Next(world, 100);
            switch (city.State)
            {
                case 0 when roll > city.AvoidCalamity:
                    var disaster = MainRandom.Next(world, 5);
                    if (disaster == 0) city.State = 2;
                    else if (disaster == 1) city.State = 3;
                    else if (disaster == 2 && MainRandom.Next(world, 100) > city.PeopleDevotion)
                        city.State = 4;
                    break;
                case 1 when city.Food > 0:
                case 2 when roll < city.AvoidCalamity:
                case 3 when roll < city.AvoidCalamity:
                    city.State = 0;
                    break;
                case 4 when roll < city.PeopleDevotion:
                    city.State = 0;
                    break;
            }
        }
    }
}
