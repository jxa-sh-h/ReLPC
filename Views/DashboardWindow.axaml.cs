using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using ReLPC.Services;

namespace ReLPC
{
    public partial class DashboardWindow : Window
    {
        private readonly DispatcherTimer _gradientTimer;
        private readonly DateTime _gradientAnimationStartedAt = DateTime.Now;

        public DashboardWindow()
        {
            InitializeComponent();

            _gradientTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _gradientTimer.Tick += OnGradientTimerTick;
            Closing += (_, _) =>
            {
                _gradientTimer.Stop();
                _gradientTimer.Tick -= OnGradientTimerTick;
            };
            _gradientTimer.Start();
        }

        private void OnNewFileClick(object? sender, RoutedEventArgs e)
        {
            var userId = AppServices.Session.CurrentUser?.Id ?? 0;
            var dataset = AppServices.Database.CreateDataset(
                userId,
                $"Untitled Dataset {DateTime.Now:yyyy-MM-dd HH-mm}");
            var mainWindow = new MainWindow(dataset);
            DesktopSession.ShowAsMainWindow(mainWindow);
            Close();
        }

        private async void OnExportClick(object? sender, RoutedEventArgs e)
        {
            var userId = AppServices.Session.CurrentUser?.Id ?? 0;
            var datasets = AppServices.Database.GetDatasets(userId);
            var picker = new DatasetPickerWindow(datasets);
            var dataset = await picker.ShowDialog<ReLPC.Models.DatasetRecord?>(this);
            if (dataset is null)
            {
                return;
            }

            var mainWindow = new MainWindow(dataset);
            DesktopSession.ShowAsMainWindow(mainWindow);
            Close();
        }

        private void OnHistoryClick(object? sender, RoutedEventArgs e)
        {
            // TODO: Implement history action
        }

        private async void OnLogoutClick(object? sender, RoutedEventArgs e)
        {
            var confirmLogoutWindow = new ConfirmLogoutWindow();
            var shouldLogout = await confirmLogoutWindow.ShowDialog<bool>(this);
            if (!shouldLogout)
            {
                return;
            }

            _gradientTimer.Stop();
            var loginWindow = new LoginWindow();
            DesktopSession.ShowAsMainWindow(loginWindow);
            Close();
        }

        private void OnGradientTimerTick(object? sender, EventArgs e)
        {
            if (SidebarPanel is null)
                return;

            var elapsedSeconds = (DateTime.Now - _gradientAnimationStartedAt).TotalSeconds;
            var sidebarFlow = (Math.Sin(elapsedSeconds * 1.15 + 1.2) + 1) / 2;

            SidebarPanel.Background = CreateAnimatedGradient(sidebarFlow);
        }

        private static LinearGradientBrush CreateAnimatedGradient(double flow)
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
            brush.GradientStops.Add(new GradientStop(Color.Parse("#FF3A34"), highlightStart));
            brush.GradientStops.Add(new GradientStop(Color.Parse("#FF7550"), highlightMiddle));
            brush.GradientStops.Add(new GradientStop(Color.Parse("#FF553C"), highlightEnd));
            brush.GradientStops.Add(new GradientStop(Color.Parse("#FF7442"), 1));

            return brush;
        }
    }
}
