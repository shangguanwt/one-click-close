using System;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace OneClickClose.WinUI.Controls;

/// <summary>
/// Draws a simple sparkline / waveform chart from an array of double values.
/// Y axis is normalized: 0 maps to the bottom, max maps to the top.
/// </summary>
public sealed partial class SparklineControl : UserControl
{
    public static readonly DependencyProperty ValuesProperty =
        DependencyProperty.Register(nameof(Values), typeof(double[]), typeof(SparklineControl),
            new PropertyMetadata(null, OnValuesChanged));

    public static readonly DependencyProperty LineColorProperty =
        DependencyProperty.Register(nameof(LineColor), typeof(Brush), typeof(SparklineControl),
            new PropertyMetadata(null, OnValuesChanged));

    /// <summary>Array of values to plot. Fewer than 2 points draws nothing.</summary>
    public double[] Values
    {
        get => (double[])GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    /// <summary>Stroke brush for the waveform line. Defaults to DodgerBlue.</summary>
    public Brush LineColor
    {
        get => (Brush)GetValue(LineColorProperty);
        set => SetValue(LineColorProperty, value);
    }

    public SparklineControl()
    {
        this.InitializeComponent();
        this.SizeChanged += (_, _) => Redraw();
        LineColor = new SolidColorBrush(ColorHelper.FromArgb(255, 30, 144, 255));
    }

    private static void OnValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SparklineControl ctrl) ctrl.Redraw();
    }

    private void Redraw()
    {
        PlotCanvas.Children.Clear();

        double[] values = Values;
        if (values == null || values.Length < 2) return;

        double w = ActualWidth > 0 ? ActualWidth : 100;
        double h = ActualHeight > 0 ? ActualHeight : 36;

        // Normalize Y: map 0~max -> height~0 (inverted because screen Y is top-down)
        double max = values.Max();
        if (max <= 0) max = 1;   // all-zero or all-negative -> flat line at bottom

        var points = new Point[values.Length];
        double stepX = w / (values.Length - 1);
        for (int i = 0; i < values.Length; i++)
        {
            double x = i * stepX;
            double y = h - (values[i] / max) * (h - 2) - 1;
            points[i] = new Point(x, Math.Clamp(y, 0, h));
        }

        // Resolve line color (fallback to DodgerBlue when null or non-solid)
        Windows.UI.Color lineColor;
        if (LineColor is SolidColorBrush scb)
            lineColor = scb.Color;
        else
            lineColor = ColorHelper.FromArgb(255, 30, 144, 255);

        // -- Semi-transparent fill area below the line ------------------
        var figure = new PathFigure { StartPoint = points[0], IsClosed = true };
        for (int i = 1; i < points.Length; i++)
            figure.Segments.Add(new LineSegment { Point = points[i] });
        // Drop to bottom-right corner then bottom-left corner to close the area
        figure.Segments.Add(new LineSegment { Point = new Point(points[points.Length - 1].X, h) });
        figure.Segments.Add(new LineSegment { Point = new Point(points[0].X, h) });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        var fillPath = new Microsoft.UI.Xaml.Shapes.Path
        {
            Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(50, lineColor.R, lineColor.G, lineColor.B)),
            Data = geometry,
        };
        PlotCanvas.Children.Add(fillPath);

        // -- Polyline stroke on top ------------------------------------
        var line = new Polyline
        {
            Stroke = LineColor ?? new SolidColorBrush(ColorHelper.FromArgb(255, 30, 144, 255)),
            StrokeThickness = 1.5,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        };
        foreach (var p in points) line.Points.Add(p);
        PlotCanvas.Children.Add(line);
    }
}
