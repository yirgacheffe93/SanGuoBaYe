using Game.Application;
using Godot;

namespace Game.Godot;

public partial class MainStrategicMap : Control
{
    private MainGameView? view;
    private string? hoveredCityId;

    public UiPalette Palette { private get; set; } = UiThemes.Get(UiThemeMode.Dark);
    public string SelectedCityId { get; private set; } = "city-005";
    public event Action<string>? CitySelected;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
    }

    public void SetView(MainGameView value, string selectedCityId)
    {
        view = value;
        SelectedCityId = selectedCityId;
        QueueRedraw();
    }

    public void ActivateCity(string cityId)
    {
        if (view?.Cities.Any(city => city.Id == cityId) != true) return;
        SelectedCityId = cityId;
        CitySelected?.Invoke(cityId);
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), Palette.Map);
        DrawTerrain();
        if (view is null) return;
        foreach (var city in view.Cities) DrawCity(city);
        var font = GetThemeDefaultFont();
        DrawString(font, new Vector2(18, 28), "190 年 · 群雄并起", HorizontalAlignment.Left, -1, 19, Palette.Text);
        DrawString(font, new Vector2(Size.X - 235, 28), "重写网格布局 · 非原图坐标", HorizontalAlignment.Left, -1, 12, Palette.Muted);
    }

    private void DrawTerrain()
    {
        for (var band = 0; band < 8; band++)
        {
            var points = new List<Vector2>();
            for (var point = 0; point < 12; point++)
                points.Add(new Vector2(Size.X * (0.03f + point * 0.088f), Size.Y * (0.12f + band * 0.11f) + Mathf.Sin(point * 0.8f + band) * 9));
            DrawPolyline(points.ToArray(), Palette.MapDetail, 1, true);
        }
        var river = new[]
        {
            new Vector2(Size.X * 0.03f, Size.Y * 0.68f), new Vector2(Size.X * 0.26f, Size.Y * 0.60f),
            new Vector2(Size.X * 0.46f, Size.Y * 0.72f), new Vector2(Size.X * 0.72f, Size.Y * 0.63f),
            new Vector2(Size.X * 0.97f, Size.Y * 0.70f)
        };
        DrawPolyline(river, Palette.Road with { A = 0.30f }, 5, true);
    }

    private void DrawCity(MainCityView city)
    {
        var center = CityPosition(city);
        var selected = city.Id == SelectedCityId;
        var hovered = city.Id == hoveredCityId;
        var mine = city.Belong == view?.PlayerRulerId;
        var font = GetThemeDefaultFont();
        if (selected) DrawArc(center, 17, 0, Mathf.Tau, 32, Palette.Selection, 2, true);
        if (hovered && !selected) DrawArc(center, 15, 0, Mathf.Tau, 32, Palette.Text, 1, true);
        DrawCircle(center, 11, Palette.Border);
        DrawCircle(center, 9, selected ? Palette.Selection : mine ? Palette.Accent : Palette.City);
        if (view?.Orders.Any(order => order.CityId == city.Id) == true)
        {
            DrawCircle(center + new Vector2(9, -9), 4, Palette.Accent);
        }
        DrawCentered(font, city.Name, center + new Vector2(0, 24), selected ? 14 : 12, Palette.Text);
    }

    private void DrawCentered(Font font, string text, Vector2 center, int fontSize, Color color)
    {
        var width = font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize).X;
        DrawString(font, center + new Vector2(-width / 2, fontSize * 0.35f), text, HorizontalAlignment.Left, -1, fontSize, color);
    }

    private Vector2 CityPosition(MainCityView city)
    {
        var index = city.Id == "city-005" ? 5 : view?.Cities.ToList().FindIndex(item => item.Id == city.Id) ?? 0;
        var column = index % 6;
        var row = index / 6;
        return new Vector2(Size.X * (0.09f + column * 0.164f), Size.Y * (0.15f + row * 0.135f));
    }

    private string? FindCity(Vector2 position) => view?.Cities.FirstOrDefault(city => position.DistanceTo(CityPosition(city)) <= 20)?.Id;

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
}
