using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System;

namespace ReLPC;

public partial class SignUpWindow : Window
{
    private readonly DispatcherTimer _gradientTimer;
    private readonly DateTime _gradientAnimationStartedAt = DateTime.Now;

    public SignUpWindow()
    {
        InitializeComponent();
        _gradientTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        _gradientTimer.Tick += OnGradientTimerTick;
        _gradientTimer.Start();
    }

    private void OnGradientTimerTick(object? sender, EventArgs e)
    {
        var elapsedSeconds = (DateTime.Now - _gradientAnimationStartedAt).TotalSeconds;
        var pageFlow = (Math.Sin(elapsedSeconds * 0.9) + 1) / 2;
        var panelFlow = (Math.Sin(elapsedSeconds * 1.15 + 1.2) + 1) / 2;

        PageRoot.Background = CreateAnimatedGradient(pageFlow, false);
        SignUpPanel.Background = CreateAnimatedGradient(panelFlow, true);
    }

    private static LinearGradientBrush CreateAnimatedGradient(double flow, bool isPanel)
    {
        var highlightStart = Math.Clamp(flow - 0.18, 0, 1);
        var highlightMiddle = Math.Clamp(flow, 0, 1);
        var highlightEnd = Math.Clamp(flow + 0.18, 0, 1);

        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative)
        };

        brush.GradientStops.Add(new GradientStop(Color.Parse("#FF2D32"), 0));
        brush.GradientStops.Add(new GradientStop(Color.Parse(isPanel ? "#FF3A34" : "#FF4438"), highlightStart));
        brush.GradientStops.Add(new GradientStop(Color.Parse(isPanel ? "#FF7550" : "#FF8650"), highlightMiddle));
        brush.GradientStops.Add(new GradientStop(Color.Parse(isPanel ? "#FF553C" : "#FF4B3A"), highlightEnd));
        brush.GradientStops.Add(new GradientStop(Color.Parse("#FF7442"), 1));

        return brush;
    }
}
