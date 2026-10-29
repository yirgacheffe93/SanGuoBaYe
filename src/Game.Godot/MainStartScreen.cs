using Game.Domain;
using Godot;

namespace Game.Godot;

public partial class MainStartScreen : PanelContainer
{
    private IReadOnlyList<MainPeriod> periods = [];
    private OptionButton periodOptions = null!;
    private OptionButton rulerOptions = null!;
    private Button startButton = null!;

    private Button continueButton = null!;
    private Button resumeButton = null!;
    public event Action<int, string>? StartRequested;
    public event Action? ContinueRequested;
    public event Action? LoadRequested;
    public event Action? ResumeRequested;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var center = new CenterContainer();
        AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(540, 410) };
        center.AddChild(panel);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 16);
        panel.AddChild(content);
        var title = new Label { Text = "三国霸业", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 36);
        content.AddChild(title);
        var subtitle = new Label { Text = "单机策略 · 四时期征战", HorizontalAlignment = HorizontalAlignment.Center };
        subtitle.AddThemeFontSizeOverride("font_size", 17);
        content.AddChild(subtitle);
        content.AddChild(new Label { Text = "选择时期" });
        periodOptions = new OptionButton();
        periodOptions.ItemSelected += _ => PopulateRulers();
        content.AddChild(periodOptions);
        content.AddChild(new Label { Text = "选择君主" });
        rulerOptions = new OptionButton();
        content.AddChild(rulerOptions);
        startButton = new Button { Text = "开始游戏" };
        startButton.Pressed += OnStart;
        content.AddChild(startButton);
        continueButton = new Button { Text = "继续最近存档", Disabled = true };
        continueButton.Pressed += () => ContinueRequested?.Invoke();
        content.AddChild(continueButton);
        var load = new Button { Text = "读取存档" };
        load.Pressed += () => LoadRequested?.Invoke();
        content.AddChild(load);
        resumeButton = new Button { Text = "返回当前游戏", Visible = false };
        resumeButton.Pressed += () => ResumeRequested?.Invoke();
        content.AddChild(resumeButton);
        content.AddChild(new Label
        {
            Text = "选择时期与君主开始征战，\n或读取存档继续未完的战局。",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
    }

    public void Configure(IReadOnlyList<MainPeriod> definitions)
    {
        periods = definitions;
        periodOptions.Clear();
        foreach (var period in periods) periodOptions.AddItem($"{period.Year} 年 · {period.RulerIds.Count} 位君主", period.Period);
        periodOptions.Select(0);
        PopulateRulers();
    }

    public void SetContinue(bool available, bool hasSession)
    {
        continueButton.Disabled = !available;
        resumeButton.Visible = hasSession;
    }

    private void PopulateRulers()
    {
        rulerOptions.Clear();
        var period = periods[periodOptions.Selected];
        foreach (var rulerId in period.RulerIds)
        {
            var ruler = period.Persons.Single(person => person.Id == rulerId);
            rulerOptions.AddItem(ruler.Name, ruler.SourceIndex);
            rulerOptions.SetItemMetadata(rulerOptions.ItemCount - 1, ruler.Id);
        }
        var liuBei = period.RulerIds.ToList().FindIndex(id => id == "person-013");
        rulerOptions.Select(liuBei >= 0 ? liuBei : 0);
        startButton.Text = $"进入 {period.Year} 年 {period.Month} 月";
    }

    private void OnStart()
    {
        var period = periods[periodOptions.Selected];
        StartRequested?.Invoke(period.Period, rulerOptions.GetItemMetadata(rulerOptions.Selected).AsString());
    }
}
