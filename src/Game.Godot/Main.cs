using Game.Application;
using Game.Domain;
using Game.Infrastructure;
using Godot;

namespace Game.Godot;

public partial class Main : Control
{
    private GameSession session = null!;
    private string selectedCityId = "city.anling";
    private UiThemeMode themeMode = UiThemeMode.Dark;
    private UiPalette palette = UiThemes.Get(UiThemeMode.Dark);
    private PanelContainer background = null!;
    private StrategicMap map = null!;
    private Label title = null!;
    private Label summary = null!;
    private Label details = null!;
    private Label feedback = null!;
    private Button themeButton = null!;
    private Button developButton = null!;
    private ItemList cityList = null!;
    private ItemList orderList = null!;
    private OptionButton characterOptions = null!;
    private OptionButton trackOptions = null!;
    private TextEdit report = null!;
    private Button endTurnButton = null!;
    private Button cancelButton = null!;
    private string SavePath => ProjectSettings.GlobalizePath("user://spike-save.json");

    public override void _Ready()
    {
        ApplyTheme();
        BuildInterface();
        try
        {
            session = new GameSession(ContentLoader.Load(global::Godot.FileAccess.GetFileAsString("res://content/spike/scenario.json")));
            Refresh();
        }
        catch (Exception error)
        {
            feedback.Text = error.Message;
            GD.PushError(error);
        }
        var arguments = OS.GetCmdlineUserArgs();
        if (arguments.Contains("--spike-smoke")) CallDeferred(nameof(RunSmoke));
        else if (arguments.Contains("--capture-ui")) CallDeferred(nameof(RunCapture));
    }

    private void BuildInterface()
    {
        background = new PanelContainer();
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        background.AddChild(margin);
        var page = new VBoxContainer();
        page.AddThemeConstantOverride("separation", 8);
        margin.AddChild(page);

        var topPanel = new PanelContainer();
        page.AddChild(topPanel);
        var top = new HBoxContainer();
        topPanel.AddChild(top);
        title = new Label();
        title.AddThemeFontSizeOverride("font_size", 21);
        top.AddChild(title);
        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        top.AddChild(spacer);
        summary = new Label();
        summary.AddThemeFontSizeOverride("font_size", 17);
        top.AddChild(summary);
        themeButton = new Button { Text = "切换纸本" };
        themeButton.Pressed += OnThemeToggle;
        top.AddChild(themeButton);

        var columns = new HSplitContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SplitOffsets = [210] };
        page.AddChild(columns);
        var leftPanel = new PanelContainer { CustomMinimumSize = new Vector2(205, 350) };
        columns.AddChild(leftPanel);
        var left = new VBoxContainer();
        leftPanel.AddChild(left);
        left.AddChild(new Label { Text = "城池与军情" });
        cityList = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(180, 210) };
        cityList.ItemSelected += index => SelectCity(session.GetView().Cities[(int)index].Id);
        left.AddChild(cityList);
        var legend = new Label { Text = "◎ 选中  令 任务\n点击地图或列表定位", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        left.AddChild(legend);

        map = new StrategicMap
        {
            CustomMinimumSize = new Vector2(430, 350),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            Palette = palette
        };
        map.CitySelected += SelectCity;
        columns.AddChild(map);

        var rightPanel = new PanelContainer { CustomMinimumSize = new Vector2(305, 350) };
        columns.AddChild(rightPanel);
        var right = new VBoxContainer();
        right.AddThemeConstantOverride("separation", 7);
        rightPanel.AddChild(right);
        details = new Label { SizeFlagsVertical = SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        details.AddThemeFontSizeOverride("font_size", 16);
        right.AddChild(details);
        right.AddChild(new Label { Text = "执行武将" });
        characterOptions = new OptionButton();
        characterOptions.ItemSelected += _ => UpdatePreview();
        right.AddChild(characterOptions);
        trackOptions = new OptionButton();
        trackOptions.AddItem("开发农业", (int)DevelopmentTrack.Agriculture);
        trackOptions.AddItem("开发商业", (int)DevelopmentTrack.Commerce);
        trackOptions.ItemSelected += _ => UpdatePreview();
        right.AddChild(trackOptions);
        developButton = new Button();
        developButton.Pressed += OnDevelop;
        right.AddChild(developButton);

        var lower = new HSplitContainer { CustomMinimumSize = new Vector2(0, 150) };
        page.AddChild(lower);
        var orderPanel = new PanelContainer { CustomMinimumSize = new Vector2(510, 145) };
        lower.AddChild(orderPanel);
        var orders = new HBoxContainer();
        orderPanel.AddChild(orders);
        var orderText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        orders.AddChild(orderText);
        orderText.AddChild(new Label { Text = "待执行任务" });
        orderList = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(390, 85) };
        orderList.ItemSelected += _ => UpdateCancelButton();
        orderText.AddChild(orderList);
        cancelButton = new Button { Text = "取消\n退款", SizeFlagsVertical = SizeFlags.ExpandFill };
        cancelButton.Pressed += OnCancel;
        orders.AddChild(cancelButton);
        report = new TextEdit { Editable = false, CustomMinimumSize = new Vector2(360, 145), Text = "回合报告" };
        lower.AddChild(report);

        var actions = new HBoxContainer();
        page.AddChild(actions);
        feedback = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddChild(feedback);
        var save = new Button { Text = "存档" };
        save.Pressed += OnSave;
        actions.AddChild(save);
        var load = new Button { Text = "读档" };
        load.Pressed += OnLoad;
        actions.AddChild(load);
        endTurnButton = new Button { Text = "结束回合  →" };
        endTurnButton.Pressed += OnEndTurn;
        actions.AddChild(endTurnButton);
        ApplyTheme();
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
        Refresh();
    }

    private void Refresh()
    {
        var view = session.GetView();
        var selectedIndex = Math.Max(0, view.Cities.ToList().FindIndex(c => c.Id == selectedCityId));
        selectedCityId = view.Cities[selectedIndex].Id;
        var city = view.Cities[selectedIndex];
        title.Text = "山河志 · 战略桌面";
        developButton.Text = $"确认开发 · {view.DevelopmentCost} 金 · 1 回合";
        summary.Text = $"我方势力  |  第 {view.Turn} 回合  |  总金 {view.Cities.Sum(c => c.Gold)}  总粮 {view.Cities.Sum(c => c.Food)}  ";
        cityList.Clear();
        foreach (var item in view.Cities)
        {
            var busy = view.Orders.Any(o => o.CityId == item.Id) ? "  令" : "";
            cityList.AddItem($"{item.Name} · 金 {item.Gold}{busy}");
        }
        cityList.Select(selectedIndex);
        map.SetView(view, selectedCityId);
        var characterCount = view.Characters.Count(c => c.CityId == city.Id && !c.IsBusy);
        details.Text = $"{city.Name} · 我方城池\n当地金  {city.Gold}\n当地粮  {city.Food}\n守军  {city.Troops}\n农业  {city.Agriculture} 级    商业  {city.Commerce} 级\n空闲武将  {characterCount} 人\n开发投入 {view.DevelopmentCost} 金，本回合结算。\n本回合排队中可取消并全额退款。";
        characterOptions.Clear();
        foreach (var character in CharactersForSelectedCity())
            characterOptions.AddItem(character.IsBusy ? $"{character.Name}（忙碌）" : character.Name);
        if (characterOptions.ItemCount > 0) characterOptions.Select(0);
        UpdatePreview();
        orderList.Clear();
        foreach (var order in view.Orders)
            orderList.AddItem($"{order.CityName} · {order.CharacterName} · {(order.Track == DevelopmentTrack.Agriculture ? "农业" : "商业")} · 剩 {order.RemainingTurns} 回合");
        UpdateCancelButton();
        report.Text = view.Reports.Count == 0 ? "回合报告\n\n尚无记录。结束回合后，这里会显示开发与资源变化。" :
            "回合报告\n\n" + string.Join("\n", view.Reports.Select(r => $"第 {r.Turn} 回合 · {r.Message}"));
    }

    private void UpdatePreview()
    {
        if (characterOptions.ItemCount == 0 || characterOptions.Selected < 0) return;
        var character = CharactersForSelectedCity()[characterOptions.Selected];
        var preview = session.PreviewDevelopment(selectedCityId, character.Id, SelectedTrack());
        feedback.Text = preview.Available
            ? $"预计：当地金 {preview.GoldAfter + preview.Cost} → {preview.GoldAfter}，第 {preview.CompletionTurn} 回合结算；可取消退款。"
            : $"当前不可执行：{preview.Message}";
    }

    private void OnDevelop()
    {
        var characters = CharactersForSelectedCity();
        if (characters.Count == 0) { feedback.Text = "该城没有武将。"; return; }
        var result = session.IssueDevelopment(selectedCityId, characters[characterOptions.Selected].Id, SelectedTrack());
        Refresh();
        feedback.Text = result.Message;
    }

    private void OnCancel()
    {
        if (orderList.GetSelectedItems().Length == 0) { feedback.Text = "请先选择任务。"; return; }
        var result = session.CancelOrder(session.GetView().Orders[orderList.GetSelectedItems()[0]].Id);
        Refresh();
        feedback.Text = result.Message;
    }

    private void OnEndTurn()
    {
        endTurnButton.Disabled = true;
        try { session.EndTurn(); }
        finally { endTurnButton.Disabled = false; }
        Refresh();
    }

    private void OnSave()
    {
        try { SaveService.Save(session, SavePath); feedback.Text = "已保存当前局面。"; }
        catch (Exception error) { feedback.Text = $"存档失败：{error.Message}"; }
    }

    private void OnLoad()
    {
        try { session = SaveService.Load(SavePath); Refresh(); feedback.Text = "读档成功，地图与面板已同步。"; }
        catch (Exception error) { feedback.Text = $"读档失败：{error.Message}"; }
    }

    private void UpdateCancelButton() => cancelButton.Disabled = orderList.GetSelectedItems().Length == 0;
    private DevelopmentTrack SelectedTrack() => (DevelopmentTrack)trackOptions.GetSelectedId();
    private List<CharacterView> CharactersForSelectedCity() => session.GetView().Characters.Where(c => c.CityId == selectedCityId).ToList();

    private void RunSmoke()
    {
        try
        {
            map.ActivateCity("city.beiping");
            if (selectedCityId != "city.beiping" || map.SelectedCityId != "city.beiping") throw new InvalidOperationException("地图与详情选择未同步。");
            var initialHash = session.GetStateHash();
            OnDevelop();
            if (session.GetStateHash() != initialHash) throw new InvalidOperationException("资源不足时状态发生变化。");
            OnThemeToggle();
            OnThemeToggle();
            if (session.GetStateHash() != initialHash) throw new InvalidOperationException("主题切换改变了规则状态。");
            map.ActivateCity("city.anling");
            OnDevelop();
            orderList.Select(0);
            OnCancel();
            if (session.GetView().Orders.Count != 0) throw new InvalidOperationException("UI 取消未清理任务。");
            OnDevelop();
            OnEndTurn();
            OnSave();
            var continuous = SaveService.Load(SavePath);
            continuous.IssueDevelopment(selectedCityId, "char.shenyan", DevelopmentTrack.Commerce);
            continuous.EndTurn();
            var expected = continuous.GetStateHash();
            OnLoad();
            session.IssueDevelopment(selectedCityId, "char.shenyan", DevelopmentTrack.Commerce);
            OnEndTurn();
            if (session.GetStateHash() != expected) throw new InvalidOperationException("读档后续状态不一致。");
            GD.Print($"SPIKE_SMOKE_OK turn={session.GetView().Turn} hash={session.GetStateHash()}");
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError($"SPIKE_SMOKE_FAILED: {error}");
            GetTree().Quit(1);
        }
    }

    private async void RunCapture()
    {
        try
        {
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
