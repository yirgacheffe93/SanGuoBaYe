using Game.Application;
using Game.Domain;
using Game.Infrastructure;
using Godot;

namespace Game.Godot;

public partial class Main : Control
{
    private MainContent content = null!;
    private MainGameSession session = null!;
    private MainGameView currentView = null!;
    private MainStartScreen startScreen = null!;
    private Control gameRoot = null!;
    private string selectedCityId = "city-005";
    private string selectedPersonId = "person-089";
    private UiThemeMode themeMode = UiThemeMode.Dark;
    private UiPalette palette = UiThemes.Get(UiThemeMode.Dark);
    private PanelContainer background = null!;
    private MainStrategicMap map = null!;
    private Label title = null!;
    private Label summary = null!;
    private Label cityDetails = null!;
    private Label personDetails = null!;
    private Label feedback = null!;
    private Button themeButton = null!;
    private Button commandButton = null!;
    private OptionButton commandOptions = null!;
    private ItemList cityList = null!;
    private ItemList personList = null!;
    private ItemList orderList = null!;
    private TextEdit report = null!;
    private Button endMonthButton = null!;
    private string SavePath => ProjectSettings.GlobalizePath("user://main-m2a-save.json");

    public override void _Ready()
    {
        ApplyTheme();
        BuildInterface();
        try
        {
            content = MainContentLoader.LoadJson(
                global::Godot.FileAccess.GetFileAsString("res://content/main/content.json"),
                global::Godot.FileAccess.GetFileAsString("res://content/main/scenarios.json"),
                global::Godot.FileAccess.GetFileAsString("res://content/main/content.schema.json"),
                global::Godot.FileAccess.GetFileAsString("res://content/main/scenarios.schema.json"));
        }
        catch (Exception error)
        {
            feedback.Text = error.Message;
            GD.PushError(error);
        }
        startScreen.Configure(content.Periods);
        startScreen.StartRequested += StartGame;
        var arguments = OS.GetCmdlineUserArgs();
        if (arguments.Contains("--spike-smoke") || arguments.Contains("--main-smoke")) CallDeferred(nameof(RunSmoke));
        else if (arguments.Contains("--capture-ui")) CallDeferred(nameof(RunCapture));
    }

    private void BuildInterface()
    {
        background = new PanelContainer();
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        gameRoot = new VBoxContainer();
        gameRoot.AddThemeConstantOverride("separation", 8);
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        background.AddChild(margin);
        margin.AddChild(gameRoot);
        gameRoot.Hide();

        var topPanel = new PanelContainer();
        gameRoot.AddChild(topPanel);
        var top = new HBoxContainer();
        topPanel.AddChild(top);
        title = new Label();
        title.AddThemeFontSizeOverride("font_size", 21);
        top.AddChild(title);
        top.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        summary = new Label();
        summary.AddThemeFontSizeOverride("font_size", 17);
        top.AddChild(summary);
        themeButton = new Button { Text = "切换纸本" };
        themeButton.Pressed += OnThemeToggle;
        top.AddChild(themeButton);

        var columns = new HSplitContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SplitOffsets = [205] };
        gameRoot.AddChild(columns);
        var leftPanel = new PanelContainer { CustomMinimumSize = new Vector2(200, 350) };
        columns.AddChild(leftPanel);
        var left = new VBoxContainer();
        leftPanel.AddChild(left);
        left.AddChild(new Label { Text = "三十八城 · 我方标红" });
        cityList = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(180, 350) };
        cityList.ItemSelected += index => SelectCity(session.GetView().Cities[(int)index].Id);
        left.AddChild(cityList);
        left.AddChild(new Label { Text = "点击地图或列表定位\n令 表示城内有待执行命令" });

        map = new MainStrategicMap
        {
            CustomMinimumSize = new Vector2(420, 350),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            Palette = palette
        };
        map.CitySelected += SelectCity;
        columns.AddChild(map);

        var rightPanel = new PanelContainer { CustomMinimumSize = new Vector2(325, 350) };
        columns.AddChild(rightPanel);
        var right = new VBoxContainer();
        right.AddThemeConstantOverride("separation", 6);
        rightPanel.AddChild(right);
        cityDetails = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        cityDetails.AddThemeFontSizeOverride("font_size", 15);
        right.AddChild(cityDetails);
        right.AddChild(new Label { Text = "城内武将" });
        personList = new ItemList { CustomMinimumSize = new Vector2(280, 92), SizeFlagsVertical = SizeFlags.ExpandFill };
        personList.ItemSelected += index =>
        {
            selectedPersonId = CityPersons()[(int)index].Id;
            RefreshPerson();
        };
        right.AddChild(personList);
        personDetails = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        personDetails.AddThemeFontSizeOverride("font_size", 14);
        right.AddChild(personDetails);
        commandOptions = new OptionButton();
        for (var commandId = 1; commandId <= 5; commandId++) commandOptions.AddItem(MainGameSession.CommandName(commandId), commandId);
        commandOptions.ItemSelected += _ => RefreshPerson();
        right.AddChild(commandOptions);
        commandButton = new Button();
        commandButton.Pressed += OnCommand;
        right.AddChild(commandButton);

        var lower = new HSplitContainer { CustomMinimumSize = new Vector2(0, 145) };
        gameRoot.AddChild(lower);
        var orderPanel = new PanelContainer { CustomMinimumSize = new Vector2(510, 140) };
        lower.AddChild(orderPanel);
        var orders = new VBoxContainer();
        orderPanel.AddChild(orders);
        orders.AddChild(new Label { Text = "武将命令队列 · 接受后月末归队，不可取消" });
        orderList = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(390, 82) };
        orders.AddChild(orderList);
        report = new TextEdit { Editable = false, CustomMinimumSize = new Vector2(360, 140), Text = "月度报告" };
        lower.AddChild(report);

        var actions = new HBoxContainer();
        gameRoot.AddChild(actions);
        feedback = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddChild(feedback);
        var save = new Button { Text = "存档" };
        save.Pressed += OnSave;
        actions.AddChild(save);
        var load = new Button { Text = "读档" };
        load.Pressed += OnLoad;
        actions.AddChild(load);
        endMonthButton = new Button { Text = "策略结束  →" };
        endMonthButton.Pressed += OnEndMonth;
        actions.AddChild(endMonthButton);

        startScreen = new MainStartScreen();
        AddChild(startScreen);
        ApplyTheme();
    }

    private void StartGame(int period, string rulerId)
    {
        session = MainContentLoader.CreateSession(content, period, rulerId);
        selectedCityId = session.GetView().Cities.First(city => city.Belong == rulerId).Id;
        selectedPersonId = session.GetView().Persons.FirstOrDefault(person => person.CityId == selectedCityId && person.Belong == rulerId)?.Id ?? "";
        startScreen.Hide();
        gameRoot.Show();
        Refresh();
    }

    private void ApplyTheme()
    {
        palette = UiThemes.Get(themeMode);
        Theme = UiThemes.CreateTheme(palette);
        if (background is not null)
            background.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = palette.Window });
        if (map is not null)
        {
            map.Palette = palette;
            map.QueueRedraw();
        }
        if (themeButton is not null) themeButton.Text = themeMode == UiThemeMode.Dark ? "切换纸本" : "切换墨色";
    }

    private void OnThemeToggle()
    {
        themeMode = themeMode == UiThemeMode.Dark ? UiThemeMode.Light : UiThemeMode.Dark;
        ApplyTheme();
    }

    private void SelectCity(string cityId)
    {
        selectedCityId = cityId;
        selectedPersonId = "";
        Refresh();
    }

    private void Refresh()
    {
        currentView = session.GetView();
        var view = currentView;
        selectedCityId = view.Cities.FirstOrDefault(city => city.Id == selectedCityId)?.Id ?? "city-005";
        var city = view.Cities.Single(city => city.Id == selectedCityId);
        title.Text = "三国霸业 · 战略地图";
        summary.Text = $"{PersonName(view.PlayerRulerId)}  |  {view.Year} 年 {view.Month} 月  |  38 城  ";
        cityList.Clear();
        foreach (var item in view.Cities)
        {
            var mine = item.Belong == view.PlayerRulerId ? "我" : "·";
            var order = view.Orders.Any(order => order.CityId == item.Id) ? " 令" : "";
            cityList.AddItem($"{mine} {item.Name}{order}");
        }
        cityList.Select(view.Cities.ToList().FindIndex(item => item.Id == city.Id));
        map.SetView(view, city.Id);
        cityDetails.Text = $"{city.Name} · {(city.Belong == view.PlayerRulerId ? "我方" : PersonName(city.Belong))}\n金 {city.Money}  粮 {city.Food}  后备兵 {city.MothballArms}\n农业 {city.Farming}/{city.FarmingLimit}\n商业 {city.Commerce}/{city.CommerceLimit}\n民忠 {city.PeopleDevotion}  防灾 {city.AvoidCalamity} · {CityState(city.State)}\n人口 {city.Population}/{city.PopulationLimit}";
        personList.Clear();
        var persons = CityPersons();
        if (persons.Count > 0 && persons.All(person => person.Id != selectedPersonId)) selectedPersonId = persons[0].Id;
        foreach (var person in persons)
            personList.AddItem($"{person.Name} · 武 {person.Force} 智 {person.Iq} 体 {person.Thew}{(person.IsBusy ? " · 忙碌" : "")}");
        var selectedIndex = Math.Max(0, persons.ToList().FindIndex(person => person.Id == selectedPersonId));
        if (persons.Count > 0) personList.Select(selectedIndex);
        RefreshPerson();
        orderList.Clear();
        foreach (var order in view.Orders)
            orderList.AddItem($"{order.CityName} · {order.PersonName} · {MainGameSession.CommandName(order.OrderId)} · 待月末");
        report.Text = view.Reports.Count == 0 ? "月度报告\n\n安排内政命令后点击“策略结束”，查看搜寻与月度结果。" :
            "月度报告\n\n" + string.Join("\n", view.Reports.Select(entry => entry.Message));
    }

    private void RefreshPerson()
    {
        var person = currentView.Persons.FirstOrDefault(item => item.Id == selectedPersonId);
        if (person is null)
        {
            personDetails.Text = "此城没有可安排的己方武将。";
            commandButton.Disabled = true;
            return;
        }
        var commandId = SelectedCommandId();
        var preview = session.PreviewCommand(commandId, selectedCityId, person.Id);
        personDetails.Text = $"{person.Name} · 等级 {person.Level}  兵力 {person.Arms}\n忠诚 {person.Devotion}  道具 {session.GetToolName(person.Tool1Id)}\n{(preview.Available ? $"接受：金 {preview.MoneyAfter + preview.MoneyCost} → {preview.MoneyAfter}，体力 {preview.ThewAfter}；月末归队" : preview.Message)}";
        commandButton.Text = $"{MainGameSession.CommandName(commandId)} · {preview.MoneyCost} 金 · {preview.ThewCost} 体力";
        commandButton.Disabled = !preview.Available;
    }

    private void OnCommand()
    {
        var result = session.IssueCommand(SelectedCommandId(), selectedCityId, selectedPersonId);
        Refresh();
        feedback.Text = result.Message;
    }

    private void OnEndMonth()
    {
        endMonthButton.Disabled = true;
        try { session.EndMonth(); }
        finally { endMonthButton.Disabled = false; }
        Refresh();
        feedback.Text = "月度结算完成。";
    }

    private void OnSave()
    {
        try { MainSaveService.Save(session, SavePath); feedback.Text = "已保存当前局面。"; }
        catch (Exception error) { feedback.Text = $"存档失败：{error.Message}"; }
    }

    private void OnLoad()
    {
        try { session = MainSaveService.Load(SavePath, content); Refresh(); feedback.Text = "读档成功，地图与命令已恢复。"; }
        catch (Exception error) { feedback.Text = $"读档失败：{error.Message}"; }
    }

    private int SelectedCommandId() => commandOptions.GetSelectedId();
    private List<MainPersonView> CityPersons() => currentView.Persons.Where(person => person.CityId == selectedCityId).ToList();
    private string PersonName(string id) => currentView.Persons.FirstOrDefault(person => person.Id == id)?.Name ?? "无所属";
    private static string CityState(int state) => state switch { 0 => "正常", 1 => "饥荒", 2 => "旱灾", 3 => "水灾", 4 => "暴动", _ => "未知" };

    private void RunSmoke()
    {
        try
        {
            StartGame(1, "person-013");
            map.ActivateCity("city-005");
            selectedPersonId = "person-089";
            Refresh();
            OnCommand();
            var city = session.GetView().Cities.Single(city => city.Id == "city-005");
            if (city.Farming != 1002) throw new InvalidOperationException("开垦接受时效果不符合 golden。");
            var beforeDuplicate = session.GetStateHash();
            var duplicate = session.IssueCommand(1, "city-005", "person-089");
            if (duplicate.Accepted || session.GetStateHash() != beforeDuplicate) throw new InvalidOperationException("重复命令未被拒绝。");
            OnEndMonth();
            city = session.GetView().Cities.Single(city => city.Id == "city-005");
            if (session.GetView().Month != 2 || city.Food != 228 || city.Farming != 1002)
                throw new InvalidOperationException("开垦月末结果不符合 golden。");
            commandOptions.Select(2);
            selectedPersonId = "person-089";
            Refresh();
            OnCommand();
            OnEndMonth();
            OnSave();
            var continuous = MainSaveService.Load(SavePath, content);
            continuous.EndMonth();
            var expected = continuous.GetStateHash();
            OnLoad();
            OnEndMonth();
            if (session.GetStateHash() != expected) throw new InvalidOperationException("读档后续状态不一致。");
            var secondRuler = content.Periods[1].RulerIds[0];
            StartGame(2, secondRuler);
            if (session.GetView().Year != 198 || session.GetView().PlayerRulerId != secondRuler)
                throw new InvalidOperationException("第二时期开局失败。");
            GD.Print($"MAIN_M2A_SMOKE_OK periods=4 commands=5 hash={expected}");
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError($"MAIN_M2A_SMOKE_FAILED: {error}");
            GetTree().Quit(1);
        }
    }

    private async void RunCapture()
    {
        try
        {
            StartGame(1, "person-013");
            var argument = OS.GetCmdlineUserArgs().FirstOrDefault(value => value.StartsWith("--capture-dir=", StringComparison.Ordinal));
            var directory = argument is null ? "build/ui" : argument["--capture-dir=".Length..];
            Directory.CreateDirectory(directory);
            await ToSignal(GetTree().CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var dark = GetViewport().GetTexture().GetImage();
            dark.SavePng(Path.Combine(directory, "ui-dark.png"));
            OnThemeToggle();
            await ToSignal(GetTree().CreateTimer(0.15), SceneTreeTimer.SignalName.Timeout);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var light = GetViewport().GetTexture().GetImage();
            light.SavePng(Path.Combine(directory, "ui-light.png"));
            var comparison = Image.CreateEmpty(dark.GetWidth() * 2, dark.GetHeight(), false, Image.Format.Rgba8);
            comparison.BlitRect(dark, new Rect2I(Vector2I.Zero, dark.GetSize()), Vector2I.Zero);
            comparison.BlitRect(light, new Rect2I(Vector2I.Zero, light.GetSize()), new Vector2I(dark.GetWidth(), 0));
            comparison.SavePng(Path.Combine(directory, "ui-comparison.png"));
            GD.Print($"UI_CAPTURE_OK {Path.GetFullPath(directory)}");
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError($"UI_CAPTURE_FAILED: {error}");
            GetTree().Quit(1);
        }
    }
}
