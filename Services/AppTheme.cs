using System.Windows;
using System.Windows.Media;

namespace ProjectCleaner.Services;

/// <summary>One Windows 11 colour set.</summary>
public sealed record ThemePalette
{
    public required Color Background { get; init; }
    public required Color BackgroundTint { get; init; }
    public required Color Layer { get; init; }
    public required Color LayerAlt { get; init; }
    public required Color Card { get; init; }
    public required Color CardBorder { get; init; }

    public required Color SubtleFill { get; init; }
    public required Color SubtleFillHover { get; init; }
    public required Color SubtleFillPressed { get; init; }

    public required Color ControlBackground { get; init; }
    public required Color ControlReadOnly { get; init; }
    public required Color ControlHover { get; init; }
    public required Color ControlBorder { get; init; }
    public required Color ControlStrongBorder { get; init; }

    public required Color Text { get; init; }
    public required Color TextSecondary { get; init; }
    public required Color TextTertiary { get; init; }
    public required Color Divider { get; init; }

    public required Color Accent { get; init; }
    public required Color AccentHover { get; init; }
    public required Color AccentPressed { get; init; }
    public required Color OnAccent { get; init; }
    public required Color AccentSubtle { get; init; }
    public required Color AccentText { get; init; }

    public required Color Danger { get; init; }
    public required Color DangerFill { get; init; }
    public required Color DangerFillPressed { get; init; }

    public required Color FlyoutBackground { get; init; }
    public required Color FlyoutBorder { get; init; }
    public required Color TrackFill { get; init; }

    public required Color ScrollThumb { get; init; }
    public required Color ScrollThumbHover { get; init; }
    public required Color ScrollThumbDragging { get; init; }
}

/// <summary>
/// Applies the Windows 11 palette once, at startup, to follow the OS app-mode setting.
///
/// It deliberately does NOT offer a runtime light/dark toggle. Rewriting ~33 palette entries (or
/// swapping the merged dictionary outright) while a window is alive floods the dispatcher with
/// resource re-resolution and the UI stops responding - measured, not assumed. Overwrite the
/// palette in this file instead if a toggle is ever genuinely needed, and re-verify.
/// </summary>
public static class AppTheme
{
    private const string ProbeKey = "Win11.Background";

    public static ThemePalette Dark { get; } = new()
    {
        Background = Rgb(0x20, 0x20, 0x20),
        BackgroundTint = Rgb(0x1B, 0x1B, 0x1B),
        Layer = Rgb(0x27, 0x27, 0x27),
        LayerAlt = Rgb(0x2D, 0x2D, 0x2D),
        Card = Rgb(0x29, 0x29, 0x29),
        CardBorder = Rgba(0xFF, 0xFF, 0xFF, 0x15),

        SubtleFill = Rgba(0xFF, 0xFF, 0xFF, 0x0A),
        SubtleFillHover = Rgba(0xFF, 0xFF, 0xFF, 0x15),
        SubtleFillPressed = Rgba(0xFF, 0xFF, 0xFF, 0x1F),

        ControlBackground = Rgba(0xFF, 0xFF, 0xFF, 0x0F),
        ControlReadOnly = Rgba(0xFF, 0xFF, 0xFF, 0x0A),
        ControlHover = Rgba(0xFF, 0xFF, 0xFF, 0x17),
        ControlBorder = Rgba(0xFF, 0xFF, 0xFF, 0x12),
        ControlStrongBorder = Rgba(0xFF, 0xFF, 0xFF, 0x4D),

        Text = Rgb(0xFF, 0xFF, 0xFF),
        TextSecondary = Rgb(0xC7, 0xC7, 0xC7),
        TextTertiary = Rgb(0x8A, 0x8A, 0x8A),
        Divider = Rgba(0xFF, 0xFF, 0xFF, 0x12),

        Accent = Rgb(0x00, 0x78, 0xD4),
        AccentHover = Rgb(0x1A, 0x86, 0xD9),
        AccentPressed = Rgb(0x00, 0x5F, 0xB8),
        OnAccent = Rgb(0xFF, 0xFF, 0xFF),
        AccentSubtle = Rgba(0x00, 0x78, 0xD4, 0x1F),
        AccentText = Rgb(0x4C, 0xC2, 0xFF),

        Danger = Rgb(0xFF, 0x99, 0xA4),
        DangerFill = Rgba(0xFF, 0x99, 0xA4, 0x20),
        DangerFillPressed = Rgba(0xFF, 0x99, 0xA4, 0x2B),

        FlyoutBackground = Rgb(0x2C, 0x2C, 0x2C),
        FlyoutBorder = Rgba(0xFF, 0xFF, 0xFF, 0x15),
        TrackFill = Rgba(0xFF, 0xFF, 0xFF, 0x15),

        ScrollThumb = Rgba(0xFF, 0xFF, 0xFF, 0x66),
        ScrollThumbHover = Rgba(0xFF, 0xFF, 0xFF, 0x99),
        ScrollThumbDragging = Rgba(0xFF, 0xFF, 0xFF, 0xB3),
    };

    public static ThemePalette Light { get; } = new()
    {
        Background = Rgb(0xF3, 0xF3, 0xF3),
        BackgroundTint = Rgb(0xEB, 0xEB, 0xEB),
        Layer = Rgb(0xFF, 0xFF, 0xFF),
        LayerAlt = Rgb(0xFA, 0xFA, 0xFA),
        Card = Rgb(0xFF, 0xFF, 0xFF),
        CardBorder = Rgba(0x00, 0x00, 0x00, 0x0D),

        SubtleFill = Rgba(0x00, 0x00, 0x00, 0x0A),
        SubtleFillHover = Rgba(0x00, 0x00, 0x00, 0x0F),
        SubtleFillPressed = Rgba(0x15, 0x1A, 0x1A, 0x15),

        ControlBackground = Rgb(0xFF, 0xFF, 0xFF),
        ControlReadOnly = Rgb(0xF5, 0xF5, 0xF5),
        ControlHover = Rgb(0xF6, 0xF6, 0xF6),
        ControlBorder = Rgba(0x00, 0x00, 0x00, 0x0D),
        ControlStrongBorder = Rgba(0x00, 0x00, 0x00, 0x6D),

        Text = Rgb(0x1A, 0x1A, 0x1A),
        TextSecondary = Rgb(0x5D, 0x5D, 0x5D),
        TextTertiary = Rgb(0x8A, 0x8A, 0x8A),
        Divider = Rgba(0x00, 0x00, 0x00, 0x0D),

        Accent = Rgb(0x00, 0x5F, 0xB8),
        AccentHover = Rgb(0x00, 0x3E, 0x92),
        AccentPressed = Rgb(0x00, 0x33, 0x66),
        OnAccent = Rgb(0xFF, 0xFF, 0xFF),
        AccentSubtle = Rgba(0x00, 0x5F, 0xB8, 0x1F),
        AccentText = Rgb(0x00, 0x5F, 0xB8),

        Danger = Rgb(0xC4, 0x2B, 0x1C),
        DangerFill = Rgba(0xC4, 0x2B, 0x1C, 0x0F),
        DangerFillPressed = Rgba(0xC4, 0x2B, 0x1C, 0x14),

        FlyoutBackground = Rgb(0xFF, 0xFF, 0xFF),
        FlyoutBorder = Rgba(0x00, 0x00, 0x00, 0x0D),
        TrackFill = Rgba(0x00, 0x00, 0x00, 0x0D),

        ScrollThumb = Rgba(0x00, 0x00, 0x00, 0x6D),
        ScrollThumbHover = Rgba(0x00, 0x00, 0x00, 0x99),
        ScrollThumbDragging = Rgba(0x00, 0x00, 0x00, 0xB3),
    };

    /// <summary>Follows the Windows app-mode setting, defaulting to dark when it cannot be read.</summary>
    public static bool PrefersDark()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            // AppsUseLightTheme: 1 = light apps, absent or 0 = dark.
            return key?.GetValue("AppsUseLightTheme") is not int value || value == 0;
        }
        catch (Exception)
        {
            return true;
        }
    }

    /// <summary>The palette currently in force, so surfaces WPF does not paint (the DWM caption) can match it.</summary>
    public static bool IsDark { get; private set; } = true;

    /// <summary>Must run once, before the first window is created.</summary>
    public static void Initialise(bool? darkOverride = null)
    {
        var dark = darkOverride ?? PrefersDark();
        IsDark = dark;
        var target = FindPaletteDictionary();

        if (target is not null)
        {
            Assign(target, dark ? Dark : Light);
        }
    }

    /// <summary>Locates the merged dictionary that owns the palette, so it can be rewritten in place.</summary>
    public static ResourceDictionary? FindPaletteDictionary()
    {
        var resources = Application.Current?.Resources;

        if (resources is null)
        {
            return null;
        }

        if (resources.Contains(ProbeKey))
        {
            return resources;
        }

        foreach (var dictionary in resources.MergedDictionaries)
        {
            if (dictionary.Contains(ProbeKey))
            {
                return dictionary;
            }
        }

        return null;
    }

    private static void Assign(ResourceDictionary target, ThemePalette palette)
    {
        target["Win11.Background"] = Brush(palette.Background);
        target["Win11.BackgroundTint"] = Brush(palette.BackgroundTint);
        target["Win11.Layer"] = Brush(palette.Layer);
        target["Win11.LayerAlt"] = Brush(palette.LayerAlt);
        target["Win11.Card"] = Brush(palette.Card);
        target["Win11.CardBorder"] = Brush(palette.CardBorder);

        target["Win11.SubtleFill"] = Brush(palette.SubtleFill);
        target["Win11.SubtleFillHover"] = Brush(palette.SubtleFillHover);
        target["Win11.SubtleFillPressed"] = Brush(palette.SubtleFillPressed);

        target["Win11.ControlBackground"] = Brush(palette.ControlBackground);
        target["Win11.ControlReadOnly"] = Brush(palette.ControlReadOnly);
        target["Win11.ControlHover"] = Brush(palette.ControlHover);
        target["Win11.ControlBorder"] = Brush(palette.ControlBorder);
        target["Win11.ControlStrongBorder"] = Brush(palette.ControlStrongBorder);

        target["Win11.Text"] = Brush(palette.Text);
        target["Win11.TextSecondary"] = Brush(palette.TextSecondary);
        target["Win11.TextTertiary"] = Brush(palette.TextTertiary);
        target["Win11.Divider"] = Brush(palette.Divider);

        target["Win11.Accent"] = Brush(palette.Accent);
        target["Win11.AccentHover"] = Brush(palette.AccentHover);
        target["Win11.AccentPressed"] = Brush(palette.AccentPressed);
        target["Win11.OnAccent"] = Brush(palette.OnAccent);
        target["Win11.AccentSubtle"] = Brush(palette.AccentSubtle);
        target["Win11.AccentText"] = Brush(palette.AccentText);

        target["Win11.Danger"] = Brush(palette.Danger);
        target["Win11.DangerFill"] = Brush(palette.DangerFill);
        target["Win11.DangerFillPressed"] = Brush(palette.DangerFillPressed);

        target["Win11.FlyoutBackground"] = Brush(palette.FlyoutBackground);
        target["Win11.FlyoutBorder"] = Brush(palette.FlyoutBorder);
        target["Win11.TrackFill"] = Brush(palette.TrackFill);

        target["Win11.ScrollThumb"] = Brush(palette.ScrollThumb);
        target["Win11.ScrollThumbHover"] = Brush(palette.ScrollThumbHover);
        target["Win11.ScrollThumbDragging"] = Brush(palette.ScrollThumbDragging);
    }

    private static SolidColorBrush Brush(Color color)
    {
        // Frozen so every element can share one instance with no per-use freeze cost.
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    private static Color Rgba(byte r, byte g, byte b, byte a) => Color.FromArgb(a, r, g, b);
}