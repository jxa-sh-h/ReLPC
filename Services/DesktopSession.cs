using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace ReLPC.Services;

public static class DesktopSession
{
    /// <summary>Shows <paramref name="window"/> and makes it the application main window so closing the previous main window does not shut down the process.</summary>
    public static void ShowAsMainWindow(Window window)
    {
        window.Show();
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = window;
    }
}
