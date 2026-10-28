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
    private OptionButton targetPersonOptions = null!;
    private HBoxContainer tradeControls = null!;
    private OptionButton tradeDirectionOptions = null!;
    private SpinBox tradeQuantity = null!;
    private HBoxContainer transportControls = null!;
    private SpinBox transportFood = null!;
    private SpinBox transportMoney = null!;
    private SpinBox transportArms = null!;
    private SpinBox commandQuantity = null!;
    private Button commandButton = null!;

    public event Action<string>? PersonSelected;
    public event Action? CommandConfirmed;
    public event Action? CommandSelectionChanged;
    public int SelectedCommandId => commandOptions.GetSelectedId();
    public string? SelectedToolId => SelectedCommandId == MainCommandCatalog.RewardOrderId ? SelectedMetadata(toolOptions) : null;
    public string? SelectedTargetCityId => SelectedCommandId is MainCommandCatalog.TransportOrderId or MainCommandCatalog.MoveOrderId or MainCommandCatalog.ReconnoitreOrderId or MainCommandCatalog.BattleOrderId ? SelectedMetadata(targetCityOptions) : null;
    public List<string> SelectedParticipantIds => personList.GetSelectedItems().Select(index => personList.GetItemMetadata((int)index).AsString()).ToList();
    public string? SelectedTargetPersonId => SelectedCommandId is MainCommandCatalog.RecruitOrderId or MainCommandCatalog.AlienateOrderId or MainCommandCatalog.CanvassOrderId or MainCommandCatalog.CounterespionageOrderId or MainCommandCatalog.InduceOrderId ? SelectedMetadata(targetPersonOptions) : null;
    public MainTradeDirection TradeDirection => tradeDirectionOptions.Selected == 1 ? MainTradeDirection.Sell : MainTradeDirection.Buy;
    public int Quantity => SelectedCommandId switch
    {
        MainCommandCatalog.TradeOrderId => (int)tradeQuantity.Value,
        MainCommandCatalog.ConscriptOrderId => (int)commandQuantity.Value,
        _ => 0
    };
    public int TransportFood => SelectedCommandId is MainCommandCatalog.TransportOrderId or MainCommandCatalog.BattleOrderId ? (int)transportFood.Value : 0;
    public int TransportMoney => SelectedCommandId == MainCommandCatalog.TransportOrderId ? (int)transportMoney.Value : 0;
    public int Arms => SelectedCommandId switch
    {
        MainCommandCatalog.TransportOrderId => (int)transportArms.Value,
        MainCommandCatalog.DistributeOrderId => (int)commandQuantity.Value,
        _ => 0
    };

    public override void _Ready()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 6);
        AddChild(content);
        cityDetails = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        cityDetails.AddThemeFontSizeOverride("font_size", 15);
        content.AddChild(cityDetails);
        content.AddChild(new Label { Text = "城内武将" });
        personList = new ItemList { CustomMinimumSize = new Vector2(280, 92), SizeFlagsVertical = SizeFlags.ExpandFill, SelectMode = ItemList.SelectModeEnum.Multi };
        personList.ItemSelected += index => PersonSelected?.Invoke(personList.GetItemMetadata((int)index).AsString());
        content.AddChild(personList);
        personDetails = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        personDetails.AddThemeFontSizeOverride("font_size", 14);
        content.AddChild(personDetails);
        commandOptions = new OptionButton();
        foreach (var commandId in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 24, 25, 26, 9, 10, 11, 12, 13, 14, 15, 16, 17, 19, 23, 27 })
            commandOptions.AddItem(MainCommandCatalog.Name(commandId), commandId);
        commandOptions.ItemSelected += _ => OnCommandInputChanged();
        content.AddChild(commandOptions);
        toolOptions = new OptionButton();
        toolOptions.ItemSelected += _ => CommandSelectionChanged?.Invoke();
        content.AddChild(toolOptions);
        targetCityOptions = new OptionButton();
        targetCityOptions.ItemSelected += _ => CommandSelectionChanged?.Invoke();
        content.AddChild(targetCityOptions);
        targetPersonOptions = new OptionButton();
        targetPersonOptions.ItemSelected += _ => CommandSelectionChanged?.Invoke();
        content.AddChild(targetPersonOptions);
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
        commandQuantity = QuantityControl(0);
        content.AddChild(commandQuantity);
        commandButton = new Button();
        commandButton.Pressed += () => CommandConfirmed?.Invoke();
        content.AddChild(commandButton);
        UpdateCommandControls();
    }

    public void SetView(MainGameView view, MainCityView city, string selectedPersonId, string ownerName,
        IReadOnlyList<(string Id, string Name)> discoveredTools, IReadOnlyList<(string Id, string Name, int Distance)> travelTargets,
        IReadOnlyList<string> targetPersonIds)
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
        if (SelectedCommandId == MainCommandCatalog.ReconnoitreOrderId)
        {
            foreach (var target in view.Cities.Where(item => item.Id != city.Id && item.Belong != city.Belong))
            {
                targetCityOptions.AddItem($"侦察 {target.Name}");
                targetCityOptions.SetItemMetadata(targetCityOptions.ItemCount - 1, target.Id);
            }
        }
        else if (SelectedCommandId == MainCommandCatalog.BattleOrderId)
        {
            foreach (var target in view.Cities.Where(item => city.Connections.Contains(item.Id) && item.Belong != city.Belong))
            {
                targetCityOptions.AddItem($"出征 {target.Name}");
                targetCityOptions.SetItemMetadata(targetCityOptions.ItemCount - 1, target.Id);
            }
        }
        else
        {
            foreach (var target in travelTargets)
            {
                targetCityOptions.AddItem($"目标 {target.Name} · 距离 {target.Distance}");
                targetCityOptions.SetItemMetadata(targetCityOptions.ItemCount - 1, target.Id);
            }
        }
        targetPersonOptions.Clear();
        foreach (var target in targetPersonIds.Select(id => view.Persons.Single(person => person.Id == id)))
        {
            var targetCityName = view.Cities.FirstOrDefault(city => city.Id == target.CityId)?.Name ?? "在途";
            targetPersonOptions.AddItem($"{MainCommandCatalog.Name(SelectedCommandId)} {target.Name} · {targetCityName} · 忠 {target.Devotion}");
            targetPersonOptions.SetItemMetadata(targetPersonOptions.ItemCount - 1, target.Id);
        }
    }

    public void SetPerson(MainPersonView? person, MainCommandPreview preview, string toolName, string? searchHint = null)
    {
        UpdateCommandControls();
        if (person is null)
        {
            personDetails.Text = "此城没有可安排的己方武将。";
            commandButton.Disabled = true;
            return;
        }
        var returnsAtMonthEnd = MainCommandCatalog.RequiresQueue(preview.CommandId) ? "；月末归队" : "";
        personDetails.Text = $"{person.Name} · 等级 {person.Level}  兵力 {person.Arms}\n忠诚 {person.Devotion}  道具 {toolName}\n{(preview.Available ? $"接受：金 {preview.MoneyAfter + preview.MoneyCost} → {preview.MoneyAfter}，体力 {preview.ThewAfter}{returnsAtMonthEnd}" : preview.Message)}{(searchHint is null ? "" : $"\n{searchHint}")}";
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

    public void SetCommandQuantity(int quantity) => commandQuantity.Value = quantity;

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
        targetCityOptions.Visible = SelectedCommandId is MainCommandCatalog.TransportOrderId or MainCommandCatalog.MoveOrderId or MainCommandCatalog.ReconnoitreOrderId or MainCommandCatalog.BattleOrderId;
        targetPersonOptions.Visible = SelectedCommandId is MainCommandCatalog.RecruitOrderId or MainCommandCatalog.AlienateOrderId or MainCommandCatalog.CanvassOrderId or MainCommandCatalog.CounterespionageOrderId or MainCommandCatalog.InduceOrderId;
        tradeControls.Visible = SelectedCommandId == MainCommandCatalog.TradeOrderId;
        transportControls.Visible = SelectedCommandId is MainCommandCatalog.TransportOrderId or MainCommandCatalog.BattleOrderId;
        transportMoney.Visible = SelectedCommandId != MainCommandCatalog.BattleOrderId;
        transportArms.Visible = SelectedCommandId != MainCommandCatalog.BattleOrderId;
        commandQuantity.Visible = SelectedCommandId is MainCommandCatalog.ConscriptOrderId or MainCommandCatalog.DistributeOrderId;
        commandQuantity.Prefix = SelectedCommandId == MainCommandCatalog.DistributeOrderId ? "新兵力 " : "数量 ";
    }

    private static string? SelectedMetadata(OptionButton options) =>
        options.Selected >= 0 ? options.GetItemMetadata(options.Selected).AsString() : null;

    private static string CityState(int state) => state switch { 0 => "正常", 1 => "饥荒", 2 => "旱灾", 3 => "水灾", 4 => "暴动", _ => "未知" };
}
