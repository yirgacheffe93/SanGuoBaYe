using Game.Domain;

namespace Game.Infrastructure;

internal static class MainContentMapper
{
    public static MainContent Map(ContentDocument common, ScenarioDocument scenarios, string contentHash) => new()
    {
        ContentVersion = $"main-sgby-{common.Source.Commit[..7]}",
        ContentHash = contentHash,
        Source = $"main@{common.Source.Commit}/libs/SGBY.lib",
        Tools = common.Tools.Select(item => new MainTool
        {
            Id = item.Id, SourceIndex = item.Index, Name = item.Name, UseFlag = item.Raw.useflag,
            ChangeAttackRange = item.Raw.changeAttackRange, Attack = item.Raw.at,
            Iq = item.Raw.iq, Move = item.Raw.move, Arms = item.Raw.arm
        }).ToList(),
        Skills = common.Skills.Select(item => new MainSkill
        {
            Id = item.Id, SourceIndex = item.Index, Name = item.Name, Aim = item.Raw.aim,
            State = item.Raw.state, Power = item.Raw.power, Destroy = item.Raw.destroy, UseMp = item.Raw.useMp,
            Weather = item.Raw.weather, Eland = item.Raw.eland, Oland = item.Raw.oland, Earm = item.Raw.earm
        }).ToList(),
        Periods = scenarios.Periods.Select(ConvertPeriod).ToList()
    };

    private static MainPeriod ConvertPeriod(PeriodDocument document)
    {
        var cities = document.Cities.Select(item => new MainCity
        {
            Id = item.Id, SourceIndex = item.Index, Name = item.Name, State = item.Raw.State,
            Belong = item.OwnerPersonId ?? "", SatrapId = item.SatrapPersonId ?? "",
            FarmingLimit = item.Raw.FarmingLimit, Farming = item.Raw.Farming,
            CommerceLimit = item.Raw.CommerceLimit, Commerce = item.Raw.Commerce,
            PeopleDevotion = item.Raw.PeopleDevotion, AvoidCalamity = item.Raw.AvoidCalamity,
            PopulationLimit = item.Raw.PopulationLimit, Population = item.Raw.Population,
            Money = item.Raw.Money, Food = item.Raw.Food, MothballArms = item.Raw.MothballArms,
            PersonIds = item.PersonIds, ToolIds = item.ToolIds,
            ToolEntries = item.ToolEntries.Select(entry => new MainCityToolEntry { ToolId = entry.ToolId, Discovered = entry.Discovered }).ToList()
        }).ToList();
        var persons = document.Persons.Select(item => new MainPerson
        {
            Id = item.Id, SourceIndex = item.Index, Name = item.Name,
            Status = item.Raw.Belong == 0xffff ? MainPersonStatus.Captive : item.Raw.Belong == 0 ? MainPersonStatus.Free : MainPersonStatus.Active,
            OldBelong = item.OldOwnerPersonId ?? "", Belong = item.OwnerPersonId ?? "",
            Level = item.Raw.Level, Force = item.Raw.Force, Iq = item.Raw.IQ, Devotion = item.Raw.Devotion,
            Character = item.Raw.Character, Experience = item.Raw.Experience, Thew = item.Raw.Thew,
            ArmsType = item.Raw.ArmsType, Arms = item.Raw.Arms, Tool1Id = item.Tool1Id ?? "", Tool2Id = item.Tool2Id ?? "",
            EquipIds = item.Raw.Equip.Select(ToolId).ToList(), Age = item.Raw.Age
        }).ToList();
        return new MainPeriod
        {
            Period = document.Period, Year = document.Date.Year, Month = document.Date.Month,
            RulerIds = cities.OrderBy(city => city.SourceIndex).Select(city => city.Belong)
                .Where(id => id.Length > 0).Distinct(StringComparer.Ordinal).ToList(),
            Cities = cities, Persons = persons,
            PersonConditions = document.PersonSearchConditions.Select(ConvertCondition).ToList(),
            ToolConditions = document.ToolSearchConditions.Select(ConvertCondition).ToList()
        };
    }

    private static MainSearchCondition ConvertCondition(SearchConditionDocument document) => new()
    {
        SourceIndex = document.Index, Birth = document.Birth, Bole = document.Bole, CityIndex = document.City
    };

    private static string ToolId(int rawId) => rawId == 0 || rawId == 255 ? "" : $"tool-{rawId - 1:000}";
}
