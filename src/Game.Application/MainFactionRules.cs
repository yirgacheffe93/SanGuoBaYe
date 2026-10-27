using Game.Domain;

namespace Game.Application;

public static class MainFactionRules
{
    public static void UpdateSatraps(MainWorld world)
    {
        foreach (var city in world.Cities)
        {
            city.SatrapId = "";
            if (city.Belong.Length == 0) continue;
            MainPerson? selected = null;
            foreach (var person in city.PersonIds.Select(id => world.Persons.Single(item => item.Id == id))
                         .Where(person => person.Status == MainPersonStatus.Active && person.Belong == city.Belong))
            {
                if (person.Id == city.Belong)
                {
                    selected = person;
                    break;
                }
                if (selected is null || selected.Iq < person.Iq) selected = person;
            }
            city.SatrapId = selected?.Id ?? "";
        }
    }

    public static MainGameOutcome EvaluateOutcome(MainWorld world)
    {
        if (!world.Cities.Any(city => city.Belong == world.PlayerRulerId)) return MainGameOutcome.Lost;
        return world.Cities.Any(city => city.Belong.Length > 0 && city.Belong != world.PlayerRulerId)
            ? MainGameOutcome.InProgress : MainGameOutcome.Won;
    }

    public static string Name(MainGameOutcome outcome) => outcome switch
    {
        MainGameOutcome.Won => "胜利",
        MainGameOutcome.Lost => "失败",
        _ => "进行中"
    };
}
