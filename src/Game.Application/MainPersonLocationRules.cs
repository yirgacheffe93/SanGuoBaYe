using Game.Domain;

namespace Game.Application;

internal static class MainPersonLocationRules
{
    public static void LeaveCity(MainCity city, MainPerson person)
    {
        city.PersonIds.Remove(person.Id);
        person.CityId = "";
    }

    public static void EnterCity(MainCity city, MainPerson person)
    {
        if (!city.PersonIds.Contains(person.Id, StringComparer.Ordinal)) city.PersonIds.Insert(0, person.Id);
        person.CityId = city.Id;
    }
}
