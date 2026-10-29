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
    private OptionButton skillOptions = null!;
    private Button targetButton = null!;
    private Button resolveButton = null!;
    public Func<string, IReadOnlyList<(string Id, string Name)>>? SkillProvider { get; set; }
    public event Action<MainBattleUiAction, string?, string?, string?, int, int>? CommandRequested;

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
        side.AddChild(skillOptions);
        targetButton = new Button { Text = "选择目标" };
        targetButton.Pressed += ToggleTargetMode;
        side.AddChild(targetButton);
        var attack = new Button { Text = "普通攻击" };
        attack.Pressed += () => Emit(MainBattleUiAction.Attack);
        side.AddChild(attack);
        var skill = new Button { Text = "使用技能" };
        skill.Pressed += () => Emit(MainBattleUiAction.Skill);
        side.AddChild(skill);
        var wait = new Button { Text = "待机" };
        wait.Pressed += () => Emit(MainBattleUiAction.Wait);
        side.AddChild(wait);
        var end = new Button { Text = "回合结束" };
        end.Pressed += () => Emit(MainBattleUiAction.EndTurn);
        side.AddChild(end);
        var retreat = new Button { Text = "全军撤退" };
        retreat.Pressed += () => Emit(MainBattleUiAction.Retreat);
        side.AddChild(retreat);
        resolveButton = new Button { Text = "战斗结算并返回战略地图" };
        resolveButton.Pressed += () => Emit(MainBattleUiAction.Resolve);
        side.AddChild(resolveButton);
    }

    public void SetPalette(UiPalette palette) => grid.Palette = palette;

    public void SetView(MainBattleView value)
    {
        view = value;
        if (selectedPersonId is null || value.Units.All(unit => unit.PersonId != selectedPersonId || unit.State == 8))
            selectedPersonId = value.Units.FirstOrDefault(unit => unit.IsAttacker == value.PlayerIsAttacker && unit.State != 8 && !unit.HasActed)?.PersonId;
        if (selectedTargetId is not null && value.Units.All(unit => unit.PersonId != selectedTargetId || unit.State == 8)) selectedTargetId = null;
        grid.SelectedPersonId = selectedPersonId;
        grid.SetView(value);
        summary.Text = $"{value.TargetCityName} · 第 {value.Day}/{value.MaxDays} 天 · 天气 {Weather(value.Weather)}\n攻方粮 {value.AttackerFood} · 守方粮 {value.DefenderFood} · 20 槽战场";
        details.Text = DescribeSelection() + "\n" + string.Join("\n", value.Logs.TakeLast(3));
        resolveButton.Visible = value.Result != MainBattleResult.Ongoing;
        skillOptions.Clear();
        if (selectedPersonId is not null && SkillProvider is not null)
        {
            foreach (var skill in SkillProvider(selectedPersonId))
            {
                skillOptions.AddItem(skill.Name);
                skillOptions.SetItemMetadata(skillOptions.ItemCount - 1, skill.Id);
            }
        }
        targetButton.Text = targetMode ? "取消选择目标" : "选择目标";
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
            if (target is null)
            {
                feedback.Text = "目标格没有可选择的武将。";
                return;
            }
            selectedTargetId = target.PersonId;
            targetMode = false;
            SetView(view);
            return;
        }
        if (target is not null)
        {
            if (target.IsAttacker == view.PlayerIsAttacker)
            {
                selectedPersonId = target.PersonId;
                selectedTargetId = null;
            }
            else selectedTargetId = target.PersonId;
            SetView(view);
            return;
        }
        if (selectedPersonId is not null)
            CommandRequested?.Invoke(MainBattleUiAction.Move, selectedPersonId, null, null, x, y);
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
        var skillId = action == MainBattleUiAction.Skill && skillOptions.Selected >= 0
            ? skillOptions.GetItemMetadata(skillOptions.Selected).AsString() : null;
        CommandRequested?.Invoke(action, selectedPersonId, selectedTargetId, skillId, 0, 0);
    }

    private string DescribeSelection()
    {
        if (view is null) return "";
        var actor = view.Units.FirstOrDefault(unit => unit.PersonId == selectedPersonId);
        var target = view.Units.FirstOrDefault(unit => unit.PersonId == selectedTargetId);
        return $"当前：{Describe(actor)}\n目标：{Describe(target)}";
    }

    private static string Describe(MainBattleUnitView? unit) => unit is null ? "未选择" :
        $"{unit.Name} · 兵 {unit.Arms} · HP {unit.Hp} · MP {unit.Mp} · 移动 {unit.Move}{(unit.HasActed ? " · 已行动" : "")}";
    private static string Weather(int weather) => weather switch { 1 => "晴", 2 => "阴", 3 => "风", 4 => "雨", 5 => "冰雹", _ => "未知" };
}
