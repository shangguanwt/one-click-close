using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace OneClickClose.WinUI.Helpers;

/// <summary>
/// 通用动画辅助方法 — 基于 WinUI 3 Storyboard API。
/// </summary>
public static class AnimationHelper
{
    /// <summary>
    /// 淡入显示元素。
    /// </summary>
    public static async void FadeIn(UIElement element, double durationMs = 200)
    {
        element.Visibility = Visibility.Visible;
        var sb = new Storyboard();
        var anim = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(durationMs) };
        Storyboard.SetTarget(anim, element);
        Storyboard.SetTargetProperty(anim, "Opacity");
        sb.Children.Add(anim);
        sb.Begin();
        await Task.Delay((int)durationMs + 10);
    }

    /// <summary>
    /// 淡出隐藏元素。
    /// </summary>
    public static async void FadeOut(UIElement element, double durationMs = 150)
    {
        var sb = new Storyboard();
        var anim = new DoubleAnimation { From = 1, To = 0, Duration = TimeSpan.FromMilliseconds(durationMs) };
        Storyboard.SetTarget(anim, element);
        Storyboard.SetTargetProperty(anim, "Opacity");
        sb.Children.Add(anim);
        sb.Begin();
        await Task.Delay((int)durationMs + 10);
        element.Visibility = Visibility.Collapsed;
        element.Opacity = 1;
    }

    /// <summary>
    /// 垂直偏移滑入显示元素（通过 TranslateTransform）。
    /// </summary>
    public static async void SlideUp(UIElement element, double offsetPx = 20, double durationMs = 250)
    {
        element.Visibility = Visibility.Visible;
        element.Opacity = 0;
        var transform = new TranslateTransform { Y = offsetPx };
        element.RenderTransform = transform;

        var sb = new Storyboard();
        var opacityAnim = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(durationMs) };
        Storyboard.SetTarget(opacityAnim, element);
        Storyboard.SetTargetProperty(opacityAnim, "Opacity");
        sb.Children.Add(opacityAnim);

        var translateAnim = new DoubleAnimation { From = offsetPx, To = 0, Duration = TimeSpan.FromMilliseconds(durationMs) };
        Storyboard.SetTarget(translateAnim, transform);
        Storyboard.SetTargetProperty(translateAnim, "Y");
        sb.Children.Add(translateAnim);

        sb.Begin();
        await Task.Delay((int)durationMs + 10);
    }
}
