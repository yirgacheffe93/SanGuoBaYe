using Game.Application;
using Godot;

namespace Game.Godot;

public partial class MainCityDetailPanel : PanelContainer
{
    private Label cityDetails = null!;
    private Label personDetails = null!;
    private ItemList personList = null!;
    private OptionButton commandOptions = null!;
    private Button commandButton = null!;

    public event Action<string>? PersonSelected;
    public event Action? CommandConfirmed;
    public event Action? CommandSelectionChanged;
    public int SelectedCommandId => commandOptions.GetSelectedId();

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
        for (var commandId = 1; commandId <= 5; commandId++) commandOptions.AddItem(MainGameSession.CommandName(commandId), commandId);
        commandOptions.ItemSelected += _ => CommandSelectionChanged?.Invoke();
        content.AddChild(commandOptions);
        commandButton = new Button();
        commandButton.Pressed += () => CommandConfirmed?.Invoke();
        content.AddChild(commandButton);
    }

    public void SetView(MainGameView view, MainCityView city, string selectedPersonId, string ownerName)
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
    }

    public void SetPerson(MainPersonView? person, MainCommandPreview preview, string toolName)
    {
        if (person is null)
        {
            personDetails.Text = "此城没有可安排的己方武将。";
            commandButton.Disabled = true;
            return;
        }
        personDetails.Text = $"{person.Name} · 等级 {person.Level}  兵力 {person.Arms}\n忠诚 {person.Devotion}  道具 {toolName}\n{(preview.Available ? $"接受：金 {preview.MoneyAfter + preview.MoneyCost} → {preview.MoneyAfter}，体力 {preview.ThewAfter}；月末归队" : preview.Message)}";
        commandButton.Text = $"{MainGameSession.CommandName(preview.CommandId)} · {preview.MoneyCost} 金 · {preview.ThewCost} 体力";
        commandButton.Disabled = !preview.Available;
    }

    public void SelectCommand(int index) => commandOptions.Select(index);

    private static string CityState(int state) => state switch { 0 => "正常", 1 => "饥荒", 2 => "旱灾", 3 => "水灾", 4 => "暴动", _ => "未知" };
}
