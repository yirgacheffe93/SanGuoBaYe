using Game.Application;
using Godot;

namespace Game.Godot;

public partial class MainOrderReportPanel : HSplitContainer
{
    private ItemList orderList = null!;
    private TextEdit report = null!;

    public override void _Ready()
    {
        var orderPanel = new PanelContainer { CustomMinimumSize = new Vector2(510, 140) };
        AddChild(orderPanel);
        var orders = new VBoxContainer();
        orderPanel.AddChild(orders);
        orders.AddChild(new Label { Text = "武将命令队列 · 接受后月末归队，不可取消" });
        orderList = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(390, 82) };
        orders.AddChild(orderList);
        report = new TextEdit { Editable = false, CustomMinimumSize = new Vector2(360, 140), Text = "月度报告" };
        AddChild(report);
    }

    public void SetView(MainGameView view)
    {
        orderList.Clear();
        foreach (var order in view.Orders)
            orderList.AddItem($"{order.CityName} · {order.PersonName} · {MainGameSession.CommandName(order.OrderId)} · 待月末");
        report.Text = view.Reports.Count == 0 ? "月度报告\n\n安排内政命令后点击“策略结束”，查看搜寻与月度结果。" :
            "月度报告\n\n" + string.Join("\n", view.Reports.Select(entry => entry.Message));
    }
}
