using System.Windows;
using System.Windows.Controls;

namespace Yeobaek.Ui;

/// <summary>
/// 탭을 한 줄에 같은 폭으로 늘어놓는다. 자리가 넉넉하면 최대 폭을 쓰고,
/// 탭이 많아지면 최소 폭까지 줄인다(그래도 넘치면 오른쪽이 잘린다).
/// </summary>
public sealed class TabStripPanel : Panel
{
    public double MaxItemWidth { get; set; } = 220;
    public double MinItemWidth { get; set; } = 56;

    protected override Size MeasureOverride(Size availableSize)
    {
        var itemWidth = ItemWidth(availableSize.Width);
        var height = 0.0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(itemWidth, availableSize.Height));
            height = Math.Max(height, child.DesiredSize.Height);
        }
        var totalWidth = itemWidth * InternalChildren.Count;
        return new Size(double.IsInfinity(availableSize.Width) ? totalWidth : Math.Min(totalWidth, availableSize.Width), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var itemWidth = ItemWidth(finalSize.Width);
        var x = 0.0;
        foreach (UIElement child in InternalChildren)
        {
            child.Arrange(new Rect(x, 0, itemWidth, finalSize.Height));
            x += itemWidth;
        }
        return finalSize;
    }

    private double ItemWidth(double availableWidth)
    {
        var count = InternalChildren.Count;
        if (count == 0) return 0;
        if (double.IsInfinity(availableWidth)) return MaxItemWidth;
        return Math.Clamp(availableWidth / count, MinItemWidth, MaxItemWidth);
    }
}
