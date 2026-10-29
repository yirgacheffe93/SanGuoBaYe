using Game.Application;
using Game.Domain;
using Godot;

namespace Game.Godot;

public enum MainBattleUiAction { Move, Attack, Skill, Wait, EndTurn, Retreat, Resolve }

public partial class MainBattlePanel : PanelContainer
{
    private MainBattleView? view;
    private string? selectedPersonId;
    private string? selectedTargetId;
    private bool targetMode;
    private MainBattleGrid grid = null!;
    private Label summary = null!;
    private Label details = null!;
    private Label feedback = null!;
    private Label availability = null!;
    private OptionButton skillOptions = null!;
    private OptionButton rangeMode = null!;
    private Button targetButton = null!;
    private Button attackButton = null!;
    private Button skillButton = null!;
    private Button waitButton = null!;
    private Button endButton = null!;
    private Button retreatButton = null!;
    private Button resolveButton = null!;
    public Func<string, IReadOnlyList<(string Id, string Name)>>? SkillProvider { get; set; }
    public Func<string, IReadOnlyList<(int X, int Y)>>? MovementProvider { get; set; }
    public Func<string, string?, IReadOnlyList<(int X, int Y)>>? RangeProvider { get; set; }
    public Func<string, string?, string?>? AttackFailure { get; set; }
    public Func<string, string?, string?, string?>? SkillFailure { get; set; }
    public event Action<MainBattleUiAction, string?, string?, string?, int, int>? CommandRequested;
    public event Action? SaveRequested;
    public event Action? MenuRequested;

    public override void _Ready()
    {
        var content = new VBoxContainer();
        AddChild(content);
        summary = new Label();
        content.AddChild(summary);
        var columns = new HBoxContainer();
        content.AddChild(columns);
        grid = new MainBattleGrid();
        grid.CellSelected += OnCell;
        columns.AddChild(grid);
        var side = new VBoxContainer { CustomMinimumSize = new Vector2(650, 512) };
        columns.AddChild(side);
        details = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        side.AddChild(details);
        feedback = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        side.AddChild(feedback);
        skillOptions = new OptionButton();
        skillOptions.ItemSelected += _ => UpdateHints();
        side.AddChild(skillOptions);
        rangeMode = new OptionButton();
        rangeMode.AddItem("显示普通攻击范围");
        rangeMode.AddItem("显示所选技能范围");
        rangeMode.ItemSelected += _ => UpdateHints();
        side.AddChild(rangeMode);
        side.AddChild(new Label { Text = "绿色：可移动格  ·  金色：攻击范围  ·  蓝色：技能范围\n点击己方选择武将，点击空格移动；目标可选敌军、友军或自身。" });
        availability = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        side.AddChild(availability);
        var commands = new GridContainer { Columns = 3 };
        side.AddChild(commands);
        targetButton = AddButton(commands, "选择目标", ToggleTargetMode);
        attackButton = AddButton(commands, "普通攻击", () => Emit(MainBattleUiAction.Attack));
        skillButton = AddButton(commands, "使用技能", () => Emit(MainBattleUiAction.Skill));
        waitButton = AddButton(commands, "待机 · 恢复 1 MP", () => Emit(MainBattleUiAction.Wait));
        endButton = AddButton(commands, "回合结束", () => Emit(MainBattleUiAction.EndTurn));
        retreatButton = AddButton(commands, "全军撤退", () => Emit(MainBattleUiAction.Retreat));
        resolveButton = AddButton(side, "战斗结算并返回战略地图", () => Emit(MainBattleUiAction.Resolve));
        var actions = new HBoxContainer();
        content.AddChild(actions);
        AddButton(actions, "存档 / 读档", () => SaveRequested?.Invoke());
        AddButton(actions, "主菜单", () => MenuRequested?.Invoke());
    }

    private static Button AddButton(Node parent, string text, Action action)
    {
        var button = new Button { Text = text };
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    public void ResetSelection()
    {
        view = null;
        selectedPersonId = selectedTargetId = null;
        targetMode = false;
        feedback.Text = "";
    }

    public void SetPalette(UiPalette palette) { grid.Palette = palette; grid.QueueRedraw(); }

    public void SetView(MainBattleView value)
    {
        var previousSkill = SelectedSkill();
        view = value;
        if (selectedPersonId is null || value.Units.All(unit => unit.PersonId != selectedPersonId || unit.State == 8 || unit.IsAttacker != value.PlayerIsAttacker))
            selectedPersonId = value.Units.FirstOrDefault(unit => unit.IsAttacker == value.PlayerIsAttacker && unit.State != 8 && !unit.HasActed)?.PersonId;
        if (selectedTargetId is not null && value.Units.All(unit => unit.PersonId != selectedTargetId || unit.State == 8)) selectedTargetId = null;
        grid.SelectedPersonId = selectedPersonId;
        grid.SelectedTargetId = selectedTargetId;
        grid.SetView(value);
        var result = value.Result == MainBattleResult.Ongoing ? "战斗进行中" : value.Result == MainBattleResult.AttackerWon ? "攻方获胜" : "守方获胜";
        summary.Text = $"{value.TargetCityName} · 第 {value.Day}/{value.MaxDays} 天 · 天气 {Weather(value.Weather)} · {result}\n我方为{(value.PlayerIsAttacker ? "攻方" : "守方")} · 攻方粮 {value.AttackerFood} · 守方粮 {value.DefenderFood}";
        details.Text = DescribeSelection() + "\n" + string.Join("\n", value.Logs.TakeLast(3));
        resolveButton.Visible = value.Result != MainBattleResult.Ongoing;
        skillOptions.Clear();
        if (selectedPersonId is not null && SkillProvider is not null)
        {
            foreach (var skill in SkillProvider(selectedPersonId))
            {
                skillOptions.AddItem(skill.Name);
                skillOptions.SetItemMetadata(skillOptions.ItemCount - 1, skill.Id);
                if (skill.Id == previousSkill) skillOptions.Select(skillOptions.ItemCount - 1);
            }
        }
        targetButton.Text = targetMode ? "取消选择目标" : "选择目标";
        UpdateHints();
    }

    private string? SelectedSkill() => skillOptions is not null && skillOptions.Selected >= 0
        ? skillOptions.GetItemMetadata(skillOptions.Selected).AsString() : null;

    private void UpdateHints()
    {
        if (view is null) return;
        var actor = view.Units.FirstOrDefault(unit => unit.PersonId == selectedPersonId);
        var canAct = view.Result == MainBattleResult.Ongoing && actor is { HasActed: false, State: not 8 };
        var attackReason = selectedPersonId is null ? "请先选择武将。" : AttackFailure?.Invoke(selectedPersonId, selectedTargetId);
        var skillReason = selectedPersonId is null ? "请先选择武将。" : SkillFailure?.Invoke(selectedPersonId, SelectedSkill(), selectedTargetId);
        attackButton.Disabled = !canAct || attackReason is not null;
        skillButton.Disabled = !canAct || SelectedSkill() is null || (selectedTargetId is not null && skillReason is not null);
        waitButton.Disabled = targetButton.Disabled = !canAct;
        endButton.Disabled = retreatButton.Disabled = view.Result != MainBattleResult.Ongoing;
        attackButton.TooltipText = attackReason ?? "攻击当前目标。";
        skillButton.TooltipText = skillReason ?? "对当前目标使用技能。";
        availability.Text = $"攻击：{attackReason ?? "可用"}\n技能：{skillReason ?? "可用"}";
        grid.MovementCells = selectedPersonId is not null && MovementProvider is not null ? MovementProvider(selectedPersonId) : [];
        grid.SkillRange = rangeMode.Selected == 1;
        grid.ActionCells = selectedPersonId is not null && RangeProvider is not null && (rangeMode.Selected == 0 || SelectedSkill() is not null)
            ? RangeProvider(selectedPersonId, rangeMode.Selected == 1 ? SelectedSkill() : null) : [];
        grid.QueueRedraw();
    }

    public void SetMessage(string message) => feedback.Text = message;

    private void ToggleTargetMode()
    {
        if (selectedPersonId is null) return;
        targetMode = !targetMode;
        targetButton.Text = targetMode ? "取消选择目标" : "选择目标";
        feedback.Text = targetMode ? "请点击敌军、友军或当前武将作为目标。" : "已取消目标选择。";
    }

    private void OnCell(int x, int y)
    {
        if (view is null || view.Result != MainBattleResult.Ongoing) return;
        var target = view.Units.FirstOrDefault(unit => unit.State != 8 && unit.X == x && unit.Y == y);
        if (targetMode)
        {
            if (target is null) { feedback.Text = "目标格没有可选择的武将。"; return; }
            selectedTargetId = target.PersonId;
            targetMode = false;
            SetView(view);
            return;
        }
        if (target is not null)
        {
            if (target.IsAttacker == view.PlayerIsAttacker) { selectedPersonId = target.PersonId; selectedTargetId = null; }
            else selectedTargetId = target.PersonId;
            SetView(view);
            return;
        }
        if (selectedPersonId is not null) CommandRequested?.Invoke(MainBattleUiAction.Move, selectedPersonId, null, null, x, y);
    }

    private void Emit(MainBattleUiAction action)
    {
        if (view is null) return;
        if (action == MainBattleUiAction.Skill && selectedTargetId is null)
        {
            targetMode = true;
            targetButton.Text = "取消选择目标";
            feedback.Text = "请先选择技能目标，可以是敌军、友军或当前武将。";
            return;
        }
        targetMode = false;
        targetButton.Text = "选择目标";
        CommandRequested?.Invoke(action, selectedPersonId, selectedTargetId, action == MainBattleUiAction.Skill ? SelectedSkill() : null, 0, 0);
    }

    private string DescribeSelection()
    {
        if (view is null) return "";
        return $"当前：{Describe(view.Units.FirstOrDefault(unit => unit.PersonId == selectedPersonId))}\n目标：{Describe(view.Units.FirstOrDefault(unit => unit.PersonId == selectedTargetId))}";
    }

    private static string Describe(MainBattleUnitView? unit) => unit is null ? "未选择" :
        $"{unit.Name} · {ArmName(unit.ArmsType)} · 兵 {unit.Arms} · HP {unit.Hp} · MP {unit.Mp}\n{StateName(unit.State)} · 移动力 {unit.Move} · {(unit.HasActed ? "已行动" : unit.HasMoved ? "已移动，可攻击 / 技能 / 待机" : "可移动与行动")}";
    private static string ArmName(int type) => type switch { 0 => "骑兵", 1 => "步兵", 2 => "弓兵", 3 => "水军", 4 => "极兵", 5 => "玄兵", _ => "未知兵种" };
    private static string StateName(int state) => state switch { 0 => "正常", 1 => "混乱", 2 => "禁咒", 3 => "定身", 4 => "奇门", 5 => "遁甲", 6 => "石阵", 7 => "潜踪", 8 => "击溃", _ => $"状态 {state}" };
    private static string Weather(int weather) => weather switch { 1 => "晴", 2 => "阴", 3 => "风", 4 => "雨", 5 => "冰雹", _ => "未知" };
}
