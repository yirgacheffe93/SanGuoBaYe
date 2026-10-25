using Game.Application;
using Godot;

namespace Game.Godot;

public partial class StrategicMap : Control
{
    private static readonly IReadOnlyDictionary<string, Vector2> Positions = new Dictionary<string, Vector2>(StringComparer.Ordinal)
    {
        ["city.anling"] = new(0.34f, 0.60f),
        ["city.beiping"] = new(0.67f, 0.35f)
    };

    private GameView? view;
    private string? hoveredCityId;

    public UiPalette Palette { private get; set; } = UiThemes.Get(UiThemeMode.Dark);
    public string SelectedCityId { get; private set; } = "city.anling";
    public event Action<string>? CitySelected;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
    }

    public void SetView(GameView value, string selectedCityId)
    {
        view = value;
        SelectedCityId = selectedCityId;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), Palette.Map);
        DrawTerrain();
        if (view is null) return;
        DrawRoads();
        foreach (var city in view.Cities) DrawCity(city);
        var font = GetThemeDefaultFont();
        DrawString(font, new Vector2(22, 34), "山河形势", HorizontalAlignment.Left, -1, 20, Palette.Text);
        DrawString(font, new Vector2(22, 58), "城池与道路 · 低保真战略图", HorizontalAlignment.Left, -1, 13, Palette.Muted);
    }

    private void DrawTerrain()
    {
        for (var band = 0; band < 7; band++)
        {
            var points = new List<Vector2>();
            for (var point = 0; point < 12; point++)
                points.Add(new Vector2(Size.X * (0.03f + point * 0.088f), Size.Y * (0.15f + band * 0.115f) + Mathf.Sin(point * 0.8f + band) * 11));
            DrawPolyline(points.ToArray(), Palette.MapDetail, 1, true);
        }
        var river = new[]
        {
            new Vector2(Size.X * 0.04f, Size.Y * 0.79f), new Vector2(Size.X * 0.25f, Size.Y * 0.72f),
            new Vector2(Size.X * 0.46f, Size.Y * 0.79f), new Vector2(Size.X * 0.72f, Size.Y * 0.69f),
            new Vector2(Size.X * 0.96f, Size.Y * 0.74f)
        };
        DrawPolyline(river, Palette.Road with { A = 0.35f }, 5, true);
        foreach (var anchor in new[] { new Vector2(0.15f, 0.30f), new Vector2(0.80f, 0.68f), new Vector2(0.48f, 0.20f) })
        {
            var center = anchor * Size;
            DrawPolyline(new[] { center + new Vector2(-22, 12), center + new Vector2(0, -14), center + new Vector2(22, 12) }, Palette.MapDetail, 2, true);
        }
    }

    private void DrawRoads()
    {
        if (view is null) return;
        foreach (var city in view.Cities)
        foreach (var target in city.Connections.Where(id => StringComparer.Ordinal.Compare(city.Id, id) < 0))
        {
            var other = view.Cities.FirstOrDefault(c => c.Id == target);
            if (other is null) continue;
            DrawLine(CityPosition(city.Id), CityPosition(other.Id), Palette.Map, 9, true);
            DrawLine(CityPosition(city.Id), CityPosition(other.Id), Palette.Road, 3, true);
            var middle = (CityPosition(city.Id) + CityPosition(other.Id)) / 2;
            DrawString(GetThemeDefaultFont(), middle + new Vector2(-25, -10), "官道", HorizontalAlignment.Left, -1, 13, Palette.Muted);
        }
    }

    private void DrawCity(CityView city)
    {
        var center = CityPosition(city.Id);
        var selected = city.Id == SelectedCityId;
        var hovered = city.Id == hoveredCityId;
        var busy = view?.Orders.Any(o => o.CityId == city.Id) == true;
        var font = GetThemeDefaultFont();
        if (selected) DrawArc(center, 39, 0, Mathf.Tau, 64, Palette.Selection, 3, true);
        if (hovered && !selected) DrawArc(center, 36, 0, Mathf.Tau, 64, Palette.Text, 2, true);
        DrawCircle(center, 30, Palette.Border);
        DrawCircle(center, 27, selected ? Palette.Selection : Palette.City);
        DrawCentered(font, city.Name[..1], center + new Vector2(0, 1), 23, selected ? Palette.AccentText : Palette.Text);
        DrawRect(new Rect2(center + new Vector2(20, -48), new Vector2(25, 30)), Palette.Accent);
        DrawLine(center + new Vector2(20, -51), center + new Vector2(20, -17), Palette.Border, 2, true);
        DrawCentered(font, city.Name, center + new Vector2(0, 53), 19, Palette.Text);
        DrawCentered(font, $"守军 {city.Troops}", center + new Vector2(0, 75), 13, Palette.Muted);
        if (busy)
        {
            var badge = center + new Vector2(27, -27);
            DrawCircle(badge, 11, Palette.Accent);
            DrawCentered(font, "令", badge + new Vector2(0, 0), 12, Palette.AccentText);
        }
    }

    private void DrawCentered(Font font, string text, Vector2 center, int fontSize, Color color)
    {
        var width = font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize).X;
        DrawString(font, center + new Vector2(-width / 2, fontSize * 0.35f), text, HorizontalAlignment.Left, -1, fontSize, color);
    }

    private Vector2 CityPosition(string cityId) => Positions.TryGetValue(cityId, out var position) ? position * Size : new Vector2(0.5f, 0.5f) * Size;

    private string? FindCity(Vector2 position) => view?.Cities.FirstOrDefault(city => position.DistanceTo(CityPosition(city.Id)) <= 42)?.Id;

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion)
        {
            hoveredCityId = FindCity(motion.Position);
            QueueRedraw();
        }
        else if (@event is InputEventMouseButton button && button.ButtonIndex == MouseButton.Left && button.Pressed)
        {
            var cityId = FindCity(button.Position);
            if (cityId is null) return;
            ActivateCity(cityId);
            AcceptEvent();
        }
    }

    public void ActivateCity(string cityId)
    {
        if (view?.Cities.Any(city => city.Id == cityId) != true) return;
        SelectedCityId = cityId;
        CitySelected?.Invoke(cityId);
    }
}
