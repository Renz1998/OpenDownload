using System.Windows;
using System.Windows.Media.Animation;

namespace OpenDownload.Desktop;

internal static class Motion
{
    public static void Reveal(UIElement element)
    {
        element.BeginAnimation(UIElement.OpacityProperty, null);
        if (!SystemParameters.ClientAreaAnimation) { element.Opacity = 1; return; }
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160))
        { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
    }
}
