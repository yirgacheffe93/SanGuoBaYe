using Godot;

namespace Game.Godot;

public enum UiThemeMode { Dark, Light }

public sealed record UiPalette(
    Color Window,
    Color Panel,
    Color PanelRaised,
    Color Border,
    Color Text,
    Color Muted,
    Color Accent,
    Color AccentText,
    Color Map,
    Color MapDetail,
    Color Road,
    Color City,
    Color Selection);

public static class UiThemes
{
    public static UiPalette Get(UiThemeMode mode) => mode == UiThemeMode.Dark
        ? new UiPalette(
            new Color("151B1A"), new Color("202825"), new Color("29332F"), new Color("49574F"),
            new Color("EEE8D8"), new Color("AAA99C"), new Color("B9503F"), new Color("FFF4E2"),
            new Color("26332E"), new Color("405047"), new Color("9B8A68"), new Color("D5C7A4"), new Color("E1B55A"))
        : new UiPalette(
            new Color("E8E1D2"), new Color("F5F0E5"), new Color("FFFDF6"), new Color("B7AA91"),
            new Color("302F2A"), new Color("716C61"), new Color("A54235"), new Color("FFF9ED"),
            new Color("D8D6C2"), new Color("B5B9A5"), new Color("8C7757"), new Color("F5EFD9"), new Color("9A342B"));

    public static Theme CreateTheme(UiPalette palette)
    {
        var theme = new Theme { DefaultFontSize = 16 };
        theme.SetColor("font_color", "Label", palette.Text);
        theme.SetColor("font_color", "Button", palette.Text);
        theme.SetColor("font_hover_color", "Button", palette.Text);
        theme.SetColor("font_pressed_color", "Button", palette.AccentText);
        theme.SetColor("font_disabled_color", "Button", palette.Muted);
        theme.SetColor("font_color", "OptionButton", palette.Text);
        theme.SetColor("font_color", "ItemList", palette.Text);
        theme.SetColor("font_color", "TextEdit", palette.Text);
        theme.SetColor("font_readonly_color", "TextEdit", palette.Text);
        theme.SetColor("font_selected_color", "ItemList", palette.AccentText);
        theme.SetColor("font_hovered_color", "ItemList", palette.Text);
        theme.SetColor("default_color", "RichTextLabel", palette.Text);
        theme.SetStylebox("panel", "PanelContainer", Box(palette.Panel, palette.Border, 10));
        theme.SetStylebox("panel", "ItemList", Box(palette.PanelRaised, palette.Border, 6));
        theme.SetStylebox("normal", "TextEdit", Box(palette.PanelRaised, palette.Border, 6));
        theme.SetStylebox("read_only", "TextEdit", Box(palette.PanelRaised, palette.Border, 6));
        theme.SetStylebox("normal", "Button", Box(palette.PanelRaised, palette.Border, 6));
        theme.SetStylebox("hover", "Button", Box(palette.MapDetail, palette.Selection, 6));
        theme.SetStylebox("pressed", "Button", Box(palette.Accent, palette.Accent, 6));
        theme.SetStylebox("disabled", "Button", Box(palette.Panel, palette.Border, 6));
        theme.SetStylebox("focus", "Button", Box(new Color(0, 0, 0, 0), palette.Selection, 6));
        theme.SetStylebox("normal", "OptionButton", Box(palette.PanelRaised, palette.Border, 6));
        theme.SetStylebox("hover", "OptionButton", Box(palette.MapDetail, palette.Selection, 6));
        theme.SetStylebox("selected", "ItemList", Box(palette.Accent, palette.Accent, 4));
        theme.SetStylebox("hovered", "ItemList", Box(palette.MapDetail, palette.Border, 4));
        theme.SetColor("font_color", "PopupMenu", palette.Text);
        theme.SetStylebox("panel", "PopupMenu", Box(palette.PanelRaised, palette.Border, 6));
        return theme;
    }

    private static StyleBoxFlat Box(Color fill, Color border, int radius) => new()
    {
        BgColor = fill,
        BorderColor = border,
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
        ContentMarginLeft = 12,
        ContentMarginRight = 12,
        ContentMarginTop = 8,
        ContentMarginBottom = 8
    };
}
