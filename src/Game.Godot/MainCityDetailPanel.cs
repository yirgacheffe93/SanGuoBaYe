using Game.Application;
using Godot;

namespace Game.Godot;

public partial class MainCityDetailPanel : PanelContainer
{
    private Label cityDetails = null!;
    private Label personDetails = null!;
    private ItemList personList = null!;
    private OptionButton commandOptions = null!;
    private OptionButton toolOptions = null!;
    private OptionButton targetCityOptions = null!;
    private HBoxContainer tradeControls = null!;
    private OptionButton tradeDirectionOptions = null!;
    private SpinBox tradeQuantity = null!;
    private HBoxContainer transportControls = null!;
    private SpinBox transportFood = null!;
    private SpinBox transportMoney = null!;
    private SpinBox transportArms = null!;
    private Button commandButton = null!;

    public event Action<string>? PersonSelected;
    public event Action? CommandConfirmed;
    public event Action? CommandSelectionChanged;
    public int SelectedCommandId => commandOptions.GetSelectedId();
    public string? SelectedToolId => SelectedCommandId == MainCommandCatalog.RewardOrderId ? SelectedMetadata(toolOptions) : null;
    public string? SelectedTargetCityId => SelectedCommandId is MainCommandCatalog.TransportOrderId or MainCommandCatalog.MoveOrderId ? SelectedMetadata(targetCityOptions) : null;
    public MainTradeDirection TradeDirection => tradeDirectionOptions.Selected == 1 ? MainTradeDirection.Sell : MainTradeDirection.Buy;
    public int TradeQuantity => SelectedCommandId == MainCommandCatalog.TradeOrderId ? (int)tradeQuantity.Value : 0;
    public int TransportFood => SelectedCommandId == MainCommandCatalog.TransportOrderId ? (int)transportFood.Value : 0;
    public int TransportMoney => SelectedCommandId == MainCommandCatalog.TransportOrderId ? (int)transportMoney.Value : 0;
    public int TransportArms => SelectedCommandId == MainCommandCatalog.TransportOrderId ? (int)transportArms.Value : 0;

    public override void _Ready()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 6);
        AddChild(content);
        cityDetails = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        cityDetails.AddThemeFontSizeOverride("font_size", 15);
        content.AddChild(cityDetails);
        content.AddChild(new Label { Text = "城内武将" });
        personList = new ItemList { CustomMinimumSize = new Vector2(280, 92), SizeFlagsVertical = SizeFlags.ExpandFill };
        personList.ItemSelected += index => PersonSelected?.Invoke(personList.GetItemMetadata((int)index).AsString());
        content.AddChild(personList);
        personDetails = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        personDetails.AddThemeFontSizeOverride("font_size", 14);
        content.AddChild(personDetails);
        commandOptions = new OptionButton();
        foreach (var commandId in new[] { 1, 2, 3, 4, 5, 9, 10, 11, 12, 13, 14 })
            commandOptions.AddItem(MainCommandCatalog.Name(commandId), commandId);
        commandOptions.ItemSelected += _ => OnCommandInputChanged();
        content.AddChild(commandOptions);
        toolOptions = new OptionButton();
        toolOptions.ItemSelected += _ => CommandSelectionChanged?.Invoke();
        content.AddChild(toolOptions);
        targetCityOptions = new OptionButton();
        targetCityOptions.ItemSelected += _ => CommandSelectionChanged?.Invoke();
        content.AddChild(targetCityOptions);
        tradeControls = new HBoxContainer();
        tradeDirectionOptions = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        tradeDirectionOptions.AddItem("买入粮食 · 5金/粮");
        tradeDirectionOptions.AddItem("卖出粮食 · 2金/粮");
        tradeDirectionOptions.ItemSelected += _ => CommandSelectionChanged?.Invoke();
        tradeControls.AddChild(tradeDirectionOptions);
        tradeQuantity = QuantityControl(1);
        tradeControls.AddChild(tradeQuantity);
        content.AddChild(tradeControls);
        transportControls = new HBoxContainer();
        transportFood = QuantityControl(0, "粮 ");
        transportMoney = QuantityControl(0, "金 ");
        transportArms = QuantityControl(0, "兵 ");
        transportControls.AddChild(transportFood);
        transportControls.AddChild(transportMoney);
        transportControls.AddChild(transportArms);
        content.AddChild(transportControls);
        commandButton = new Button();
        commandButton.Pressed += () => CommandConfirmed?.Invoke();
        content.AddChild(commandButton);
        UpdateCommandControls();
    }

    public void SetView(MainGameView view, MainCityView city, string selectedPersonId, string ownerName,
        IReadOnlyList<(string Id, string Name)> discoveredTools)
    {
        cityDetails.Text = $"{city.Name} · {ownerName}\n金 {city.Money}  粮 {city.Food}  后备兵 {city.MothballArms}\n农业 {city.Farming}/{city.FarmingLimit}\n商业 {city.Commerce}/{city.CommerceLimit}\n民忠 {city.PeopleDevotion}  防灾 {city.AvoidCalamity} · {CityState(city.State)}\n人口 {city.Population}/{city.PopulationLimit}";
        personList.Clear();
        var persons = view.Persons.Where(person => person.CityId == city.Id).ToList();
        foreach (var person in persons)
        {
            personList.AddItem($"{person.Name} · 武 {person.Force} 智 {person.Iq} 体 {person.Thew}{(person.IsBusy ? " · 忙碌" : "")}");
            personList.SetItemMetadata(personList.ItemCount - 1, person.Id);
        }
        var selectedIndex = Math.Max(0, persons.FindIndex(person => person.Id == selectedPersonId));
        if (persons.Count > 0) personList.Select(selectedIndex);
        toolOptions.Clear();
        foreach (var tool in discoveredTools)
        {
            toolOptions.AddItem($"赏赐 {tool.Name}");
            toolOptions.SetItemMetadata(toolOptions.ItemCount - 1, tool.Id);
        }
        targetCityOptions.Clear();
        foreach (var target in view.Cities.Where(item => item.Id != city.Id && item.Belong == city.Belong))
        {
            targetCityOptions.AddItem($"目标 {target.Name}");
            targetCityOptions.SetItemMetadata(targetCityOptions.ItemCount - 1, target.Id);
        }
    }

    public void SetPerson(MainPersonView? person, MainCommandPreview preview, string toolName)
    {
        UpdateCommandControls();
        if (person is null)
        {
            personDetails.Text = "此城没有可安排的己方武将。";
            commandButton.Disabled = true;
            return;
        }
        var returnsAtMonthEnd = MainCommandCatalog.RequiresQueue(preview.CommandId) ? "；月末归队" : "";
        personDetails.Text = $"{person.Name} · 等级 {person.Level}  兵力 {person.Arms}\n忠诚 {person.Devotion}  道具 {toolName}\n{(preview.Available ? $"接受：金 {preview.MoneyAfter + preview.MoneyCost} → {preview.MoneyAfter}，体力 {preview.ThewAfter}{returnsAtMonthEnd}" : preview.Message)}";
        commandButton.Text = $"{MainGameSession.CommandName(preview.CommandId)} · {preview.MoneyCost} 金 · {preview.ThewCost} 体力";
        commandButton.Disabled = !preview.Available;
    }

    public void SelectCommand(int index) => commandOptions.Select(index);

    public void SetTransportAmounts(int food, int money, int arms)
    {
        transportFood.Value = food;
        transportMoney.Value = money;
        transportArms.Value = arms;
    }

    private SpinBox QuantityControl(double value, string prefix = "数量 ")
    {
        var control = new SpinBox { MinValue = value == 0 ? 0 : 1, MaxValue = 65535, Step = 1, Value = value, Prefix = prefix };
        control.ValueChanged += _ => CommandSelectionChanged?.Invoke();
        return control;
    }

    private void OnCommandInputChanged()
    {
        UpdateCommandControls();
        CommandSelectionChanged?.Invoke();
    }

    private void UpdateCommandControls()
    {
        toolOptions.Visible = SelectedCommandId == MainCommandCatalog.RewardOrderId;
        targetCityOptions.Visible = SelectedCommandId is MainCommandCatalog.TransportOrderId or MainCommandCatalog.MoveOrderId;
        tradeControls.Visible = SelectedCommandId == MainCommandCatalog.TradeOrderId;
        transportControls.Visible = SelectedCommandId == MainCommandCatalog.TransportOrderId;
    }

    private static string? SelectedMetadata(OptionButton options) =>
        options.Selected >= 0 ? options.GetItemMetadata(options.Selected).AsString() : null;

    private static string CityState(int state) => state switch { 0 => "正常", 1 => "饥荒", 2 => "旱灾", 3 => "水灾", 4 => "暴动", _ => "未知" };
}
