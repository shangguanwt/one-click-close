using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
        Loaded += (_, _) => UpdateArc();
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
        double x2 = cx + radius * Math.Cos(endRad);
        double y2 = cy + radius * Math.Sin(endRad);

        var figure = new PathFigure
        {
            StartPoint = new Point(x1, y1),
            IsClosed = false
        };
        figure.Segments.Add(new ArcSegment
        {
            Point = new Point(x2, y2),
            Size = new Size(radius, radius),
            RotationAngle = 0,
            IsLargeArc = sweepAngle > 180,
            SweepDirection = SweepDirection.Clockwise
        });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        ValueArc.Data = geometry;

        // Accent blue → Copper gradient based on value
        // 0% = #4A90D9 (74,144,217)  100% = #E09040 (224,144,64)
        byte r = (byte)(74 + (int)(224 - 74) * percent / 100);
        byte g = 144;
        byte b = (byte)(217 + (int)(64 - 217) * percent / 100);
        ValueArc.Stroke = new SolidColorBrush(ColorHelper.FromArgb(255, r, g, b));

        ValueText.Text = percent.ToString("F0") + "%";
    }
}
