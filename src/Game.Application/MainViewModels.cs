namespace Game.Application;

public sealed record MainCityView(
    string Id,
    string Name,
    string Belong,
    int X,
    int Y,
    int Farming,
    int FarmingLimit,
    int Commerce,
    int CommerceLimit,
    int PeopleDevotion,
    int State,
    int AvoidCalamity,
    int Population,
    int PopulationLimit,
    int Money,
    int Food,
    int MothballArms);
public sealed record MainPersonView(
    string Id,
    string Name,
    string CityId,
    string Belong,
    int Level,
    int Force,
    int Iq,
    int Devotion,
    int Thew,
    int Arms,
    string Tool1Id,
    string Tool2Id,
    bool IsBusy);
public sealed record MainOrderView(string Id, int OrderId, string CityId, string CityName, string PersonName, int Money, int TimeCount);
public sealed record MainGameView(
    int Period,
    int Year,
    int Month,
    string PlayerRulerId,
    IReadOnlyList<MainCityView> Cities,
    IReadOnlyList<MainPersonView> Persons,
    IReadOnlyList<MainOrderView> Orders,
    IReadOnlyList<ReportEntryView> Reports);
public sealed record ReportEntryView(int Turn, string Message);
