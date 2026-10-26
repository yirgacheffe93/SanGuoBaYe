using Game.Domain;

namespace Game.Application;

public static class MainMilitaryRules
{
    public static string Conscript(MainCity city, MainPerson person, int amount)
    {
        city.MothballArms = Math.Min(65535, city.MothballArms + amount);
        return $"{person.Name}征兵 {amount}，后备兵增加，月末归队。";
    }

    public static string Distribute(MainCity city, MainPerson person, int newArms)
    {
        city.MothballArms += person.Arms - newArms;
        person.Arms = newArms;
        return $"{person.Name}兵力调整为 {newArms}。";
    }

    public static string Plunder(MainCity city, MainPerson person)
    {
        var value = person.Iq + person.Force;
        city.Food = (city.Food + value * 5) & 0xffff;
        city.Money = (city.Money + value * 2) & 0xffff;
        city.PeopleDevotion /= 2;
        city.Farming /= 2;
        city.Commerce /= 2;
        return $"{person.Name}掠夺完成，城市资源与稳定受损。";
    }
}
