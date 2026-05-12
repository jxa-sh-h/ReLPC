using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ReLPC;

public partial class ConfirmLogoutWindow : Window
{
    public ConfirmLogoutWindow()
    {
        InitializeComponent();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void OnLogoutClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }
}
