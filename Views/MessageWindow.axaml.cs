using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ReLPC;

public partial class MessageWindow : Window
{
    public MessageWindow()
    {
        InitializeComponent();
    }

    public MessageWindow(string title, string message) : this()
    {
        TitleText.Text = title;
        MessageText.Text = message;
    }

    public static async Task ShowAsync(Window owner, string title, string message)
    {
        var window = new MessageWindow(title, message);
        await window.ShowDialog(owner);
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
