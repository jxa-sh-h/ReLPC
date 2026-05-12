using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using ReLPC.Services;
using ReLPC.ViewModels;
using System;
using System.Globalization;

namespace ReLPC;

public partial class LoginWindow : Window
{
    private readonly DispatcherTimer _gradientTimer;
    private readonly DateTime _gradientAnimationStartedAt = DateTime.Now;

    public LoginWindow()
    {
        Console.WriteLine("LoginWindow constructor.");
        InitializeComponent();
        DataContext = new LoginWindowViewModel(AppServices.Session, AppServices.Database, AppServices.Windows);
        _gradientTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        _gradientTimer.Tick += OnGradientTimerTick;
        _gradientTimer.Start();
        Console.WriteLine("LoginWindow initialized.");
    }

    private void OnGradientTimerTick(object? sender, EventArgs e)
    {
        var elapsedSeconds = (DateTime.Now - _gradientAnimationStartedAt).TotalSeconds;
        var pageFlow = (Math.Sin(elapsedSeconds * 0.9) + 1) / 2;
        var panelFlow = (Math.Sin(elapsedSeconds * 1.15 + 1.2) + 1) / 2;

        PageRoot.Background = CreateAnimatedGradient(pageFlow, false);
        LoginPanel.Background = CreateAnimatedGradient(panelFlow, true);
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

public class BoolToPasswordIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        if (value is bool isVisible)
        {
            var path = isVisible 
                ? "avares://ReLPC/assets/elements/pass-open.png"
                : "avares://ReLPC/assets/elements/pass-close.png";
            
            try
            {
                var assetLoader = AssetLoader.Open(new Uri(path));
                return new Bitmap(assetLoader);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading image: {ex.Message}");
                return null;
            }
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        throw new NotImplementedException();
    }
}
