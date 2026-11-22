using System.Windows;
using System.Windows.Controls;

namespace GG.App.Controls;

/// <summary>
/// Lays children out in a grid of equal cards that always fills the available width.
/// The number of columns comes from <see cref="ItemWidth"/> (the grid-size slider); the cards
/// are then stretched slightly so there is no ragged gap on the right. Height = width × <see cref="AspectRatio"/>.
/// </summary>
public sealed class AdaptiveGridPanel : Panel
{
    public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
        nameof(ItemWidth), typeof(double), typeof(AdaptiveGridPanel),
        new FrameworkPropertyMetadata(180.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty AspectRatioProperty = DependencyProperty.Register(
        nameof(AspectRatio), typeof(double), typeof(AdaptiveGridPanel),
        new FrameworkPropertyMetadata(1.5, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing), typeof(double), typeof(AdaptiveGridPanel),
        new FrameworkPropertyMetadata(18.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>Desired card width in pixels.</summary>
    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    /// <summary>Card height divided by width (1.5 = 600x900 covers).</summary>
    public double AspectRatio
    {
        get => (double)GetValue(AspectRatioProperty);
        set => SetValue(AspectRatioProperty, value);
    }

    /// <summary>Gap between cards, both directions.</summary>
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? ItemWidth * 5 : availableSize.Width;
        var (columns, cardSize) = Layout(width);

        foreach (UIElement child in InternalChildren)
            child.Measure(cardSize);

        var rows = (int)Math.Ceiling(InternalChildren.Count / (double)columns);
        var height = rows == 0 ? 0 : rows * cardSize.Height + (rows - 1) * Spacing;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (columns, cardSize) = Layout(finalSize.Width);

        for (var i = 0; i < InternalChildren.Count; i++)
        {
            var row = i / columns;
            var col = i % columns;
            var x = col * (cardSize.Width + Spacing);
            var y = row * (cardSize.Height + Spacing);
            InternalChildren[i].Arrange(new Rect(new Point(x, y), cardSize));
        }

        return finalSize;
    }

    /// <summary>How many columns fit, and the resulting card size.</summary>
    private (int Columns, Size CardSize) Layout(double width)
    {
        var desired = Math.Max(60, ItemWidth);
        var columns = Math.Max(1, (int)((width + Spacing) / (desired + Spacing)));
        var cardWidth = Math.Floor((width - Spacing * (columns - 1)) / columns);
        cardWidth = Math.Max(40, cardWidth);
        return (columns, new Size(cardWidth, Math.Floor(cardWidth * AspectRatio)));
    }
}
