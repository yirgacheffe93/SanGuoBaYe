using Game.Application;
using Godot;

namespace Game.Godot;

public partial class MainBattleGrid : Control
{
    private MainBattleView? view;
    public UiPalette Palette { get; set; } = UiThemes.Get(UiThemeMode.Dark);
    public string? SelectedPersonId { get; set; }
    public string? SelectedTargetId { get; set; }
    public IReadOnlyList<(int X, int Y)> MovementCells { get; set; } = [];
    public IReadOnlyList<(int X, int Y)> ActionCells { get; set; } = [];
    public bool SkillRange { get; set; }
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
            var terrain = view.TerrainKinds[y * view.MapWidth + x];
            DrawRect(new Rect2(x * 16, y * 16, 16, 16), colors[terrain % colors.Length]);
            DrawRect(new Rect2(x * 16, y * 16, 16, 16), Palette.Border, false, 0.35f);
            if (x == view.CityX && y == view.CityY) DrawRect(new Rect2(x * 16 + 4, y * 16 + 4, 8, 8), Palette.City);
        }
        foreach (var cell in MovementCells)
            DrawRect(new Rect2(cell.X * 16 + 1, cell.Y * 16 + 1, 14, 14), new Color(0.2f, 0.95f, 0.45f, 0.45f));
        foreach (var cell in ActionCells)
            DrawRect(new Rect2(cell.X * 16 + 1, cell.Y * 16 + 1, 14, 14), SkillRange ? new Color("70CFFF") : new Color("FFE277"), false, 1.5f);
        foreach (var unit in view.Units.Where(unit => unit.State != 8))
        {
            var center = new Vector2(unit.X * 16 + 8, unit.Y * 16 + 8);
            DrawCircle(center, 7, unit.IsAttacker == view.PlayerIsAttacker ? new Color("236F3D") : new Color("A53632"));
            DrawString(ThemeDB.FallbackFont, center + new Vector2(-6, 4), unit.Name.Length > 0 ? unit.Name[..1] : "?", fontSize: 12, modulate: Colors.White);
            if (unit.HasActed) DrawLine(center + new Vector2(-5, 6), center + new Vector2(5, -6), new Color("F3D2CD"), 1.5f);
            if (unit.PersonId == SelectedTargetId) DrawRect(new Rect2(unit.X * 16, unit.Y * 16, 16, 16), new Color("FF8F73"), false, 2);
            if (unit.PersonId == SelectedPersonId) DrawArc(center, 8, 0, Mathf.Tau, 20, Palette.Selection, 2, true);
        }
    }
}
