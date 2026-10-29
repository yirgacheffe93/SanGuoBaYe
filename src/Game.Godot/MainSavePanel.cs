using Game.Infrastructure;
using Godot;

namespace Game.Godot;

public partial class MainSavePanel : PanelContainer
{
    private VBoxContainer rows = null!;
    private Label feedback = null!;
    private ConfirmationDialog overwrite = null!;
    private int pendingSlot;
    public event Action<int>? SaveRequested;
    public event Action<MainSaveSlot>? LoadRequested;
    public event Action? Closed;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var center = new CenterContainer();
        AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(850, 440) };
        center.AddChild(panel);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 12);
        panel.AddChild(content);
        content.AddChild(new Label { Text = "存档与读取", HorizontalAlignment = HorizontalAlignment.Center });
        rows = new VBoxContainer();
        content.AddChild(rows);
        feedback = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        content.AddChild(feedback);
        var close = new Button { Text = "返回" };
        close.Pressed += () => { Hide(); Closed?.Invoke(); };
        content.AddChild(close);
        overwrite = new ConfirmationDialog { Title = "覆盖存档", DialogText = "覆盖这个位置的存档？" };
        overwrite.Confirmed += () => SaveRequested?.Invoke(pendingSlot);
        AddChild(overwrite);
    }

    public void SetSlots(IReadOnlyList<MainSaveSlot> slots, bool canSave)
    {
        foreach (var child in rows.GetChildren()) { rows.RemoveChild(child); child.QueueFree(); }
        foreach (var slot in slots)
        {
            var row = new HBoxContainer();
            rows.AddChild(row);
            row.AddChild(new Label
            {
                Text = $"{slot.Title}  ·  {slot.Summary}" + (slot.CanLoad ? $"\n保存于 {slot.LastWriteUtc.ToLocalTime():yyyy-MM-dd HH:mm}" : ""),
                CustomMinimumSize = new Vector2(630, 42), SizeFlagsHorizontal = SizeFlags.ExpandFill,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            });
            var load = new Button { Text = "读取", Disabled = !slot.CanLoad, TooltipText = slot.Summary };
            load.Pressed += () => LoadRequested?.Invoke(slot);
            row.AddChild(load);
            if (slot.Index > 0)
            {
                var save = new Button { Text = "保存", Disabled = !canSave };
                save.Pressed += () =>
                {
                    if (!slot.Exists) SaveRequested?.Invoke(slot.Index);
                    else { pendingSlot = slot.Index; overwrite.PopupCentered(); }
                };
                row.AddChild(save);
            }
        }
        feedback.Text = canSave ? "读取存档会替换当前进度。保存可保留当前战略或战斗局面。" : "请选择要继续的存档，也可以返回开始新游戏。";
    }

    public void SetMessage(string message) => feedback.Text = message;
}
