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
    private MainCityListPanel cityListPanel = null!;
    private MainCityDetailPanel detailPanel = null!;
    private MainOrderReportPanel orderReportPanel = null!;
    private Control gameRoot = null!;
    private string selectedCityId = "city-005";
    private string selectedPersonId = "person-089";
    private UiThemeMode themeMode = UiThemeMode.Dark;
    private UiPalette palette = UiThemes.Get(UiThemeMode.Dark);
    private PanelContainer background = null!;
    private MainStrategicMap map = null!;
    private Label title = null!;
    private Label summary = null!;
    private Label feedback = null!;
    private Button themeButton = null!;
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
        cityListPanel = new MainCityListPanel { CustomMinimumSize = new Vector2(200, 350) };
        cityListPanel.CitySelected += SelectCity;
        columns.AddChild(cityListPanel);
        map = new MainStrategicMap
        {
            CustomMinimumSize = new Vector2(420, 350),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            Palette = palette
        };
        map.CitySelected += SelectCity;
        columns.AddChild(map);
        detailPanel = new MainCityDetailPanel { CustomMinimumSize = new Vector2(325, 350) };
        detailPanel.PersonSelected += personId => { selectedPersonId = personId; RefreshPerson(); };
        detailPanel.CommandSelectionChanged += RefreshPerson;
        detailPanel.CommandConfirmed += OnCommand;
        columns.AddChild(detailPanel);

        orderReportPanel = new MainOrderReportPanel { CustomMinimumSize = new Vector2(0, 145) };
        gameRoot.AddChild(orderReportPanel);
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
        endMonthButton.Pressed += () => OnEndMonth();
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
        cityListPanel.SetView(view, city.Id);
        map.SetView(view, city.Id);
        var persons = CityPersons();
        if (persons.Count > 0 && persons.All(person => person.Id != selectedPersonId)) selectedPersonId = persons[0].Id;
        var discoveredTools = city.DiscoveredToolIds.Select(id => (id, session.GetToolName(id))).ToList();
        detailPanel.SetView(view, city, selectedPersonId, city.Belong == view.PlayerRulerId ? "我方" : PersonName(city.Belong), discoveredTools);
        RefreshPerson();
        orderReportPanel.SetView(view);
    }

    private void RefreshPerson()
    {
        var person = currentView.Persons.FirstOrDefault(item => item.Id == selectedPersonId);
        var preview = session.PreviewCommand(CreateCommandRequest(person?.Id ?? ""));
        var hint = detailPanel.SelectedCommandId == MainCommandCatalog.SearchOrderId && person is not null
            ? session.GetSearchDiagnostics(selectedCityId, person.Id).Message
            : null;
        detailPanel.SetPerson(person, preview, person is null ? "" : session.GetToolName(person.Tool1Id), hint);
    }

    private void OnCommand()
    {
        var result = session.IssueCommand(CreateCommandRequest(selectedPersonId));
        Refresh();
        feedback.Text = result.Message;
    }

    private MainCommandRequest CreateCommandRequest(string personId) => new(
        detailPanel.SelectedCommandId, selectedCityId, personId,
        detailPanel.SelectedTargetCityId, detailPanel.SelectedToolId, detailPanel.TradeDirection,
        detailPanel.Quantity, detailPanel.TransportFood, detailPanel.TransportMoney, detailPanel.Arms,
        detailPanel.SelectedTargetPersonId);

    private void OnEndMonth(bool runAi = true)
    {
        endMonthButton.Disabled = true;
        try { session.EndMonth(runAi); }
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

    private List<MainPersonView> CityPersons() => currentView.Persons.Where(person => person.CityId == selectedCityId).ToList();
    private string PersonName(string id) => currentView.Persons.FirstOrDefault(person => person.Id == id)?.Name ?? "无所属";

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
            detailPanel.SelectCommand(2);
            selectedPersonId = "person-089";
            session.World.Month = 5;
            session.World.RandomState = 1;
            Refresh();
            OnCommand();
            session.World.RandomState = 1;
            OnEndMonth(false);
            if (session.GetView().Month != 6 || session.World.Persons.Single(person => person.Id == "person-172").Belong != "person-013")
                throw new InvalidOperationException("人物搜寻或六月月度流程失败。");
            if (!currentView.Reports.Any(entry => entry.Message.Contains("收获") || entry.Message.Contains("税收")))
                throw new InvalidOperationException("月度收入/收获报告缺失。");
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
            StartGame(1, "person-016");
            map.ActivateCity("city-001");
            selectedPersonId = "person-016";
            detailPanel.SelectCommand(2);
            session.World.RandomState = 43;
            Refresh();
            OnCommand();
            session.World.RandomState = 43;
            OnEndMonth(false);
            if (!session.World.Cities.Single(city => city.Id == "city-001").ToolEntries[0].Discovered)
                throw new InvalidOperationException("道具发现位未提交。");
            OnSave();
            var discoveredSave = MainSaveService.Load(SavePath, content);
            if (!discoveredSave.World.Cities.Single(city => city.Id == "city-001").ToolEntries[0].Discovered)
                throw new InvalidOperationException("道具发现位未持久化。");
            var banquetCity = session.World.Cities.Single(city => city.Id == "city-001");
            var banquetPerson = session.World.Persons.Single(person => person.Id == "person-016");
            banquetPerson.Thew = 40;
            var banquetMoney = banquetCity.Money;
            detailPanel.SelectCommand(14);
            Refresh();
            selectedPersonId = "person-016";
            RefreshPerson();
            OnCommand();
            if (banquetPerson.Thew != 90 || banquetPerson.OrderId is not null || banquetCity.Money != banquetMoney - 100)
                throw new InvalidOperationException("宴请即时效果失败。");
            StartGame(1, "person-005");
            var transportPersonId = selectedPersonId;
            var transportCity = session.World.Cities.Single(city => city.Id == selectedCityId);
            var transportFood = transportCity.Food;
            var transportMoney = transportCity.Money;
            var transportArms = transportCity.MothballArms;
            detailPanel.SelectCommand(15);
            detailPanel.SetTransportAmounts(1, 2, 0);
            Refresh();
            selectedPersonId = transportPersonId;
            RefreshPerson();
            var transportTarget = detailPanel.SelectedTargetCityId ?? throw new InvalidOperationException("输送没有同归属目标城市。");
            OnCommand();
            var transportOrder = session.World.Orders.Single(order => order.PersonId == transportPersonId);
            if (transportOrder.OrderId != MainCommandCatalog.TransportOrderId || transportOrder.ObjectId != transportTarget ||
                transportOrder.Food != 1 || transportOrder.Money != 2 || transportOrder.Arms != 0 ||
                transportCity.Food != transportFood - 1 || transportCity.Money != transportMoney - 2 || transportCity.MothballArms != transportArms)
                throw new InvalidOperationException("输送请求或即时扣减失败。");

            StartGame(1, "person-013");
            map.ActivateCity("city-005");
            selectedPersonId = "person-089";
            var conscriptCity = session.World.Cities.Single(item => item.Id == "city-005");
            var conscriptPerson = session.World.Persons.Single(item => item.Id == "person-089");
            conscriptCity.PeopleDevotion = 50;
            conscriptCity.Money = 1000;
            conscriptCity.MothballArms = 100;
            conscriptPerson.Thew = 80;
            var conscriptPopulation = conscriptCity.Population;
            detailPanel.SelectCommand(8);
            detailPanel.SetCommandQuantity(1000);
            Refresh();
            OnCommand();
            var conscriptOrder = currentView.Orders.Single();
            if (conscriptCity.Population != conscriptPopulation || conscriptCity.MothballArms != 1100 || conscriptCity.Money != 900 ||
                conscriptOrder.OrderId != MainCommandCatalog.ConscriptOrderId || conscriptOrder.PersonName != conscriptPerson.Name)
                throw new InvalidOperationException("征兵人口、后备兵、金钱或报告队列不符合预期。");

            StartGame(1, "person-013");
            map.ActivateCity("city-005");
            selectedPersonId = "person-172";
            var executeTarget = session.World.Persons.Single(item => item.Id == selectedPersonId);
            var executeCity = session.World.Cities.Single(item => item.Id == selectedCityId);
            executeTarget.Status = MainPersonStatus.Captive;
            executeTarget.Tool1Id = "tool-000";
            executeTarget.Tool2Id = "tool-001";
            var executeOrderCount = session.World.Orders.Count;
            detailPanel.SelectCommand(6);
            Refresh();
            OnCommand();
            if (executeTarget.Status != MainPersonStatus.Executed || executeTarget.Tool1Id != "tool-000" || executeTarget.Tool2Id != "tool-001" ||
                executeTarget.OrderId is not null || session.World.Orders.Count != executeOrderCount ||
                !executeCity.ToolEntries.Any(entry => entry.ToolId == "tool-000" && entry.Discovered) ||
                !executeCity.ToolEntries.Any(entry => entry.ToolId == "tool-001" && entry.Discovered))
                throw new InvalidOperationException("处斩状态、装备或即时命令队列不符合预期。");

            StartGame(1, "person-013");
            map.ActivateCity("city-005");
            selectedPersonId = "person-172";
            var exileTarget = session.World.Persons.Single(item => item.Id == selectedPersonId);
            exileTarget.Status = MainPersonStatus.Captive;
            exileTarget.Tool1Id = "tool-000";
            exileTarget.Tool2Id = "tool-001";
            var exileOrderCount = session.World.Orders.Count;
            detailPanel.SelectCommand(7);
            Refresh();
            OnCommand();
            if (exileTarget.Status != MainPersonStatus.Free || exileTarget.Belong != "" || exileTarget.CityId == "city-005" ||
                exileTarget.Tool1Id != "tool-000" || exileTarget.Tool2Id != "tool-001" || exileTarget.OrderId is not null ||
                session.World.Orders.Count != exileOrderCount || !session.World.Cities.Single(item => item.Id == exileTarget.CityId).PersonIds.Contains(exileTarget.Id))
                throw new InvalidOperationException("流放状态、装备或即时命令队列不符合预期。");

            GD.Print($"MAIN_M2B_SMOKE_OK periods=4 commands=17 hash={expected}");
            GD.Print("MAIN_M2C_SMOKE_OK conscript=1000 execute+exile=equipment-preserved");
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError($"MAIN_M2B_SMOKE_FAILED: {error}");
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
