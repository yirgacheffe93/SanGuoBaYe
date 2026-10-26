namespace Game.Application;

public static class MainCommandCatalog
{
    public const int ReclaimOrderId = 1;
    public const int CommerceOrderId = 2;
    public const int SearchOrderId = 3;
    public const int GovernOrderId = 4;
    public const int InspectOrderId = 5;
    public const int ReclaimCost = 50;
    public const int ReclaimThewCost = 8;

    public static string Name(int commandId) => commandId switch
    {
        ReclaimOrderId => "开垦",
        CommerceOrderId => "招商",
        SearchOrderId => "搜寻",
        GovernOrderId => "治理",
        InspectOrderId => "出巡",
        _ => "未知"
    };

    public static (int Money, int Thew) Costs(int commandId) => commandId switch
    {
        ReclaimOrderId or CommerceOrderId or InspectOrderId => (50, 8),
        SearchOrderId => (0, 8),
        GovernOrderId => (0, 0),
        _ => throw new ArgumentOutOfRangeException(nameof(commandId))
    };
}
