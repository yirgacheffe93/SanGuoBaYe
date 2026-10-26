namespace Game.Application;

public enum MainTradeDirection { Buy, Sell }

public sealed record MainCommandRequest(
    int CommandId,
    string CityId,
    string PersonId,
    string? TargetCityId = null,
    string? ToolId = null,
    MainTradeDirection TradeDirection = MainTradeDirection.Buy,
    int Quantity = 0,
    int Food = 0,
    int Money = 0,
    int Arms = 0,
    string? TargetPersonId = null);
