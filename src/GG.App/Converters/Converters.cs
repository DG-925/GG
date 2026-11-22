using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using GG.Core.Models;

namespace GG.App.Converters;

/// <summary>true → Visible, false → Collapsed (or the opposite with <see cref="Invert"/>).</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (Invert) flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>non-null → Visible, null → Collapsed (or the opposite with <see cref="Invert"/>).</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var visible = value is not null && value is not string { Length: 0 };
        if (Invert) visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>0 → Visible, anything else → Collapsed. Used for "empty" placeholders.</summary>
public sealed class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Brand-ish color for each source (used for tinting fallback cards).</summary>
internal static class SourceColors
{
    public static Color For(GameSource source) => source switch
    {
        GameSource.Steam => Color.FromRgb(0x2A, 0x47, 0x5E),
        GameSource.Epic => Color.FromRgb(0x4A, 0x4A, 0x4A),
        GameSource.Xbox => Color.FromRgb(0x10, 0x7C, 0x10),
        GameSource.GOG => Color.FromRgb(0x86, 0x32, 0x8A),
        GameSource.EA => Color.FromRgb(0xC8, 0x3A, 0x3A),
        GameSource.Ubisoft => Color.FromRgb(0x00, 0x5C, 0xD6),
        GameSource.BattleNet => Color.FromRgb(0x14, 0x8E, 0xFF),
        GameSource.Shortcut => Color.FromRgb(0x7C, 0x5C, 0xFF),
        GameSource.FolderScan => Color.FromRgb(0xC2, 0x7C, 0x0E),
        _ => Color.FromRgb(0x5A, 0x62, 0x70),
    };
}

/// <summary>GameSource → solid brush.</summary>
public sealed class SourceToBrushConverter : IValueConverter
{
    private static readonly Dictionary<GameSource, Brush> Cache = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var source = value is GameSource s ? s : GameSource.Manual;
        if (!Cache.TryGetValue(source, out var brush))
        {
            brush = new SolidColorBrush(SourceColors.For(source));
            brush.Freeze();
            Cache[source] = brush;
        }
        return brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>GameSource → dark vertical gradient tinted with the source color (fallback card background).</summary>
public sealed class SourceToGradientConverter : IValueConverter
{
    private static readonly Dictionary<GameSource, Brush> Cache = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var source = value is GameSource s ? s : GameSource.Manual;
        if (!Cache.TryGetValue(source, out var brush))
        {
            var tint = SourceColors.For(source);
            var top = Blend(tint, Color.FromRgb(0x1E, 0x23, 0x2B), 0.45);
            var bottom = Color.FromRgb(0x16, 0x19, 0x1F);
            brush = new LinearGradientBrush(top, bottom, new Point(0, 0), new Point(0.4, 1));
            brush.Freeze();
            Cache[source] = brush;
        }
        return brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    /// <summary>Mixes <paramref name="a"/> into <paramref name="b"/> by <paramref name="amount"/> (0..1).</summary>
    private static Color Blend(Color a, Color b, double amount) => Color.FromRgb(
        (byte)(a.R * amount + b.R * (1 - amount)),
        (byte)(a.G * amount + b.G * (1 - amount)),
        (byte)(a.B * amount + b.B * (1 - amount)));
}
