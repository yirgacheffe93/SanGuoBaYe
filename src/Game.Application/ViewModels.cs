using Game.Domain;

namespace Game.Application;

public sealed record CityView(string Id, string Name, int Gold, int Food, int Agriculture, int Commerce, int Troops, IReadOnlyList<string> Connections);
public sealed record CharacterView(string Id, string Name, string CityId, bool IsBusy);
public sealed record OrderView(string Id, string CityId, string CityName, string CharacterName, DevelopmentTrack Track, int RemainingTurns);
public sealed record ReportView(int Turn, string Message);
public sealed record GameView(
    int Turn,
    int DevelopmentCost,
    IReadOnlyList<CityView> Cities,
    IReadOnlyList<CharacterView> Characters,
    IReadOnlyList<OrderView> Orders,
    IReadOnlyList<ReportView> Reports);
