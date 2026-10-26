namespace Game.Application;

public static class MainCommandCatalog
{
    public const int ReclaimOrderId = 1;
    public const int CommerceOrderId = 2;
    public const int SearchOrderId = 3;
    public const int GovernOrderId = 4;
    public const int InspectOrderId = 5;
    public const int RewardOrderId = 9;
    public const int ConfiscateOrderId = 10;
    public const int TradeOrderId = 11;
    public const int BanquetOrderId = 12;
    public const int TransportOrderId = 13;
    public const int MoveOrderId = 14;
    public const int ReclaimCost = 50;
    public const int ReclaimThewCost = 8;

    public static string Name(int commandId) => commandId switch
    {
        ReclaimOrderId => "开垦",
        CommerceOrderId => "招商",
        SearchOrderId => "搜寻",
        GovernOrderId => "治理",
        InspectOrderId => "出巡",
        RewardOrderId => "赏赐",
        ConfiscateOrderId => "没收",
        TradeOrderId => "交易",
        BanquetOrderId => "宴请",
        TransportOrderId => "输送",
        MoveOrderId => "移动",
        _ => "未知"
    };

    public static (int Money, int Thew) Costs(int commandId) => commandId switch
    {
        ReclaimOrderId or CommerceOrderId or InspectOrderId => (50, 8),
        SearchOrderId => (0, 8),
        GovernOrderId or RewardOrderId or ConfiscateOrderId => (0, 0),
        TradeOrderId => (0, 12),
        BanquetOrderId => (100, 0),
        TransportOrderId or MoveOrderId => (0, 8),
        _ => throw new ArgumentOutOfRangeException(nameof(commandId))
    };

    public static bool RequiresQueue(int commandId) => commandId switch
    {
        ReclaimOrderId or CommerceOrderId or SearchOrderId or GovernOrderId or InspectOrderId or
            TradeOrderId or TransportOrderId or MoveOrderId => true,
        RewardOrderId or ConfiscateOrderId or BanquetOrderId => false,
        _ => throw new ArgumentOutOfRangeException(nameof(commandId))
    };
}
