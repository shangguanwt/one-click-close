using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OneClickClose.WinUI.Services;
using Windows.Foundation;

namespace OneClickClose.WinUI.Controls;

public sealed partial class MemoryRingControl : UserControl
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register("Value", typeof(double), typeof(MemoryRingControl),
            new PropertyMetadata(0d, OnValueChanged));

    public static readonly DependencyProperty UsedTextProperty =
        DependencyProperty.Register("UsedText", typeof(string), typeof(MemoryRingControl),
            new PropertyMetadata("0 / 0 GB", OnTextChanged));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string UsedText
    {
        get => (string)GetValue(UsedTextProperty);
        set => SetValue(UsedTextProperty, value);
    }

    public MemoryRingControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AppThemeService.ThemeChanged += OnThemeChanged;
        UpdateArc();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        AppThemeService.ThemeChanged -= OnThemeChanged;
    }

    private void OnThemeChanged(object sender, EventArgs e)
    {
        UpdateArc();
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MemoryRingControl ctrl) ctrl.UpdateArc();
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MemoryRingControl ctrl) ctrl.UsedTextBlock.Text = (string)e.NewValue;
    }

    private void UpdateArc()
    {
        double percent = Math.Clamp(Value, 0, 100);

        double radius = 86;
        double startAngle = -90;
        double sweepAngle = (percent / 100.0) * 360.0;

        if (sweepAngle < 0.1)
        {
            ValueArc.Data = null;
            return;
        }

        double startRad = startAngle * Math.PI / 180.0;
        double endRad = (startAngle + sweepAngle) * Math.PI / 180.0;

        double cx = 90, cy = 90;
        double x1 = cx + radius * Math.Cos(startRad);
        double y1 = cy + radius * Math.Sin(startRad);

        var figure = new PathFigure
        {
            StartPoint = new Point(x1, y1),
            IsClosed = false
        };

        if (percent >= 99.95)
        {
            double midAngle = startAngle + 180;
            double midRad = midAngle * Math.PI / 180.0;
            double xMid = cx + radius * Math.Cos(midRad);
            double yMid = cy + radius * Math.Sin(midRad);

            figure.Segments.Add(CreateArcSegment(new Point(xMid, yMid), radius, isLargeArc: false));
            figure.Segments.Add(CreateArcSegment(new Point(x1, y1), radius, isLargeArc: false));
        }
        else
        {
            double x2 = cx + radius * Math.Cos(endRad);
            double y2 = cy + radius * Math.Sin(endRad);

            figure.Segments.Add(CreateArcSegment(new Point(x2, y2), radius, sweepAngle > 180));
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        ValueArc.Data = geometry;

        ValueArc.Stroke = Application.Current.Resources["MemoryRingGradientBrush"] as Brush
            ?? Application.Current.Resources["AccentBrush"] as Brush;

        ValueText.Text = percent.ToString("F0") + "%";
    }

    private static ArcSegment CreateArcSegment(Point point, double radius, bool isLargeArc)
    {
        return new ArcSegment
        {
            Point = point,
            Size = new Size(radius, radius),
            RotationAngle = 0,
            IsLargeArc = isLargeArc,
            SweepDirection = SweepDirection.Clockwise
        };
    }
}
