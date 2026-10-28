using Game.Application;
using Godot;

namespace Game.Godot;

public partial class MainBattleGrid : Control
{
    private MainBattleView? view;
    public UiPalette Palette { get; set; } = UiThemes.Get(UiThemeMode.Dark);
    public string? SelectedPersonId { get; set; }
    public event Action<int, int>? CellSelected;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(512, 512);
        MouseFilter = MouseFilterEnum.Stop;
    }

    public void SetView(MainBattleView value)
    {
        view = value;
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (view is null || inputEvent is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click) return;
        var x = (int)(click.Position.X / 16);
        var y = (int)(click.Position.Y / 16);
        if (x >= 0 && y >= 0 && x < view.MapWidth && y < view.MapHeight) CellSelected?.Invoke(x, y);
        AcceptEvent();
    }

    public override void _Draw()
    {
        if (view is null) return;
        var colors = new[]
        {
            new Color("71815B"), new Color("8D936B"), new Color("716450"), new Color("425B43"),
            new Color("8A7653"), new Color("B08A4E"), new Color("7D725B"), new Color("426879")
        };
        for (var y = 0; y < view.MapHeight; y++)
        for (var x = 0; x < view.MapWidth; x++)
        {
            var terrain = view.Terrains[y * view.MapWidth + x];
            DrawRect(new Rect2(x * 16, y * 16, 16, 16), colors[terrain % colors.Length]);
            DrawRect(new Rect2(x * 16, y * 16, 16, 16), Palette.Border, false, 0.35f);
            if (terrain == 3) DrawRect(new Rect2(x * 16 + 4, y * 16 + 4, 8, 8), Palette.City);
        }
        foreach (var unit in view.Units.Where(unit => unit.State != 8))
        {
            var center = new Vector2(unit.X * 16 + 8, unit.Y * 16 + 8);
            DrawCircle(center, 6, unit.IsAttacker ? Palette.Accent : new Color("47758C"));
            if (unit.PersonId == SelectedPersonId) DrawArc(center, 8, 0, Mathf.Tau, 20, Palette.Selection, 2, true);
        }
    }
}
