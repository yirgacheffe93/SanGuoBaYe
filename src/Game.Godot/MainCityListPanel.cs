using Game.Application;
using Godot;

namespace Game.Godot;

public partial class MainCityListPanel : PanelContainer
{
    private ItemList cityList = null!;
    public event Action<string>? CitySelected;

    public override void _Ready()
    {
        var content = new VBoxContainer();
        AddChild(content);
        content.AddChild(new Label { Text = "三十八城 · 我方标红" });
        cityList = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(180, 350) };
        cityList.ItemSelected += index => CitySelected?.Invoke(cityList.GetItemMetadata((int)index).AsString());
        content.AddChild(cityList);
        content.AddChild(new Label { Text = "点击地图或列表定位\n令 表示城内有待执行命令" });
    }

    public void SetView(MainGameView view, string selectedCityId)
    {
        cityList.Clear();
        var selectedIndex = 0;
        for (var index = 0; index < view.Cities.Count; index++)
        {
            var city = view.Cities[index];
            if (city.Id == selectedCityId) selectedIndex = index;
            var mine = city.Belong == view.PlayerRulerId ? "我" : "·";
            var order = view.Orders.Any(order => order.CityId == city.Id) ? " 令" : "";
            cityList.AddItem($"{mine} {city.Name}{order}");
            cityList.SetItemMetadata(index, city.Id);
        }
        cityList.Select(selectedIndex);
    }
}
