using Game.Domain;

namespace Game.Application;

public static class MainInternalRules
{
    public static string ApplyImmediateEffect(MainWorld world, int commandId, MainCity city, MainPerson person)
    {
        switch (commandId)
        {
            case MainCommandCatalog.ReclaimOrderId:
            {
                var gain = Math.Min(city.FarmingLimit - city.Farming,
                    person.Iq / 10 * (2 + MainRandom.Next(world, 4)) + person.Iq / 2);
                city.Farming += gain;
                return $"开垦完成，农业 +{gain}，月末归队。";
            }
            case MainCommandCatalog.CommerceOrderId:
            {
                var gain = Math.Min(city.CommerceLimit - city.Commerce,
                    person.Iq / 10 * (2 + MainRandom.Next(world, 4)) + person.Iq / 2);
                city.Commerce += gain;
                return $"招商完成，商业 +{gain}，月末归队。";
            }
            case MainCommandCatalog.SearchOrderId:
                MainRandom.Next(world, 3);
                return "开始搜寻，结果将在月末揭晓。";
            case MainCommandCatalog.GovernOrderId:
            {
                var gain = 1 + MainRandom.Next(world, 4);
                city.AvoidCalamity = Math.Min(100, city.AvoidCalamity + gain);
                city.State = 0;
                return $"治理完成，防灾 +{gain}，月末归队。";
            }
            case MainCommandCatalog.InspectOrderId:
            {
                var gain = 1 + MainRandom.Next(world, 4);
                city.PeopleDevotion = Math.Min(100, city.PeopleDevotion + gain);
                city.Population = Math.Min(city.PopulationLimit, city.Population + 100);
                return $"出巡完成，民忠 +{gain}、人口 +100，月末归队。";
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(commandId));
        }
    }
}
