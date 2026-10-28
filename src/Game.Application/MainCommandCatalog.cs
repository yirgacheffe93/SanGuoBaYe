namespace Game.Application;

public static class MainCommandCatalog
{
    public const int ReclaimOrderId = 1;
    public const int CommerceOrderId = 2;
    public const int SearchOrderId = 3;
    public const int GovernOrderId = 4;
    public const int InspectOrderId = 5;
    public const int RecruitOrderId = 6;
    public const int ExecuteOrderId = 7;
    public const int ExileOrderId = 8;
    public const int ConscriptOrderId = 24;
    public const int DistributeOrderId = 25;
    public const int PlunderOrderId = 26;
    public const int RewardOrderId = 9;
    public const int ConfiscateOrderId = 10;
    public const int TradeOrderId = 11;
    public const int BanquetOrderId = 12;
    public const int TransportOrderId = 13;
    public const int MoveOrderId = 14;
    public const int AlienateOrderId = 15;
    public const int CanvassOrderId = 16;
    public const int CounterespionageOrderId = 17;
    public const int InduceOrderId = 19;
    public const int ReconnoitreOrderId = 23;
    public const int BattleOrderId = 27;
    public const int ReclaimCost = 50;
    public const int ReclaimThewCost = 8;

    public static string Name(int commandId) => commandId switch
    {
        ReclaimOrderId => "开垦",
        CommerceOrderId => "招商",
        SearchOrderId => "搜寻",
        GovernOrderId => "治理",
        InspectOrderId => "出巡",
        RecruitOrderId => "招降",
        ExecuteOrderId => "处斩",
        ExileOrderId => "流放",
        ConscriptOrderId => "征兵",
        DistributeOrderId => "分配",
        PlunderOrderId => "掠夺",
        RewardOrderId => "赏赐",
        ConfiscateOrderId => "没收",
        TradeOrderId => "交易",
        BanquetOrderId => "宴请",
        TransportOrderId => "输送",
        MoveOrderId => "移动",
        AlienateOrderId => "离间",
        CanvassOrderId => "招揽",
        CounterespionageOrderId => "策反",
        InduceOrderId => "劝降（原版无效果）",
        ReconnoitreOrderId => "侦察",
        BattleOrderId => "出征",
        _ => "未知"
    };

    public static (int Money, int Thew) Costs(int commandId) => commandId switch
    {
        ReclaimOrderId or CommerceOrderId or InspectOrderId => (50, 8),
        SearchOrderId => (0, 8),
        GovernOrderId or RewardOrderId or ConfiscateOrderId or ExecuteOrderId or ExileOrderId or DistributeOrderId => (0, 0),
        RecruitOrderId => (0, 15),
        ConscriptOrderId or PlunderOrderId => (0, 12),
        TradeOrderId => (0, 12),
        BanquetOrderId => (100, 0),
        TransportOrderId or MoveOrderId => (0, 8),
        AlienateOrderId or CanvassOrderId or CounterespionageOrderId or InduceOrderId => (50, 20),
        ReconnoitreOrderId => (20, 10),
        BattleOrderId => (0, 0),
        _ => throw new ArgumentOutOfRangeException(nameof(commandId))
    };

    public static bool RequiresQueue(int commandId) => commandId switch
    {
        ReclaimOrderId or CommerceOrderId or SearchOrderId or GovernOrderId or InspectOrderId or
            RecruitOrderId or ConscriptOrderId or PlunderOrderId or TradeOrderId or TransportOrderId or MoveOrderId or
            AlienateOrderId or CanvassOrderId or CounterespionageOrderId or InduceOrderId or ReconnoitreOrderId or BattleOrderId => true,
        RewardOrderId or ConfiscateOrderId or BanquetOrderId or ExecuteOrderId or ExileOrderId or DistributeOrderId => false,
        _ => throw new ArgumentOutOfRangeException(nameof(commandId))
    };
}
