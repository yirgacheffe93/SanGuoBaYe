using Game.Domain;

namespace Game.Application;

public static class TurnCoordinator
{
    public static IReadOnlyList<string> EndMonth(MainWorld world, MainContent content, bool runAi)
    {
        if (world.Outcome != MainGameOutcome.InProgress || world.ActiveBattle is not null) return [];
        var messages = new List<string>();
        while (true)
        {
            switch (world.TurnPhase)
            {
                case MainTurnPhase.Player:
                    world.TurnPhase = MainTurnPhase.Ai;
                    MainFactionRules.UpdateSatraps(world);
                    if (runAi) MainAiPlanner.Plan(world);
                    world.TurnPhase = MainTurnPhase.Orders;
                    continue;
                case MainTurnPhase.Orders:
                    var defensiveOrder = world.Orders.FirstOrDefault(order => order.OrderId == MainCommandCatalog.BattleOrderId &&
                        world.Cities.Single(city => city.Id == order.ObjectId).Belong == world.PlayerRulerId);
                    if (defensiveOrder is not null)
                    {
                        BeginBattle(world, content, defensiveOrder.Id, MainTurnPhase.Orders);
                        return ["敌军来袭，进入守城战。"];
                    }
                    messages.AddRange(MainOrderExecutor.Execute(world, content));
                    AppendOutcome(world, messages);
                    if (world.Outcome != MainGameOutcome.InProgress)
                    {
                        world.TurnPhase = MainTurnPhase.Player;
                        return messages;
                    }
                    world.TurnPhase = MainTurnPhase.MonthUpdate;
                    continue;
                case MainTurnPhase.MonthUpdate:
                    messages.AddRange(MainMonthService.Update(world, content));
                    AppendOutcome(world, messages);
                    world.TurnPhase = MainTurnPhase.Player;
                    return messages;
                case MainTurnPhase.AwaitBattle:
                    return messages;
                default:
                    throw new InvalidOperationException("无法从月度中间阶段恢复；请使用安全边界存档。");
            }
        }
    }

    public static MainBattleState EnterBattle(MainWorld world, MainContent content, string orderId)
    {
        if (world.ActiveBattle is not null) return world.ActiveBattle;
        if (world.Outcome != MainGameOutcome.InProgress || world.TurnPhase != MainTurnPhase.Player)
            throw new InvalidOperationException("当前阶段不能进入主动战斗。");
        return BeginBattle(world, content, orderId, MainTurnPhase.Player);
    }

    public static string ResolveBattle(MainWorld world)
    {
        if (world.TurnPhase != MainTurnPhase.AwaitBattle || world.ActiveBattle is null) return "";
        var returnPhase = world.BattleReturnPhase;
        var message = MainBattleService.Resolve(world);
        world.TurnPhase = world.Outcome == MainGameOutcome.InProgress ? returnPhase : MainTurnPhase.Player;
        world.BattleReturnPhase = MainTurnPhase.Player;
        return message;
    }

    private static MainBattleState BeginBattle(MainWorld world, MainContent content, string orderId, MainTurnPhase returnPhase)
    {
        var battle = MainBattleService.Begin(world, content, orderId);
        world.TurnPhase = MainTurnPhase.AwaitBattle;
        world.BattleReturnPhase = returnPhase;
        return battle;
    }

    private static void AppendOutcome(MainWorld world, List<string> messages)
    {
        var reportCount = world.Reports.Count;
        MainFactionRules.UpdateOutcome(world);
        messages.AddRange(world.Reports.Skip(reportCount).Select(report => report.Message));
    }
}
