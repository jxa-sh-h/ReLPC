using System.Windows.Input;
using ReLPC.Services;

namespace ReLPC.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private bool _isMenuOpen;

    public MainWindowViewModel()
    {
        ToggleMenuCommand = new RelayCommand(ToggleMenu);
    }

    public MainWindowViewModel(
        ISessionService sessionService,
        IDatabaseService databaseService,
        IWindowService windowService) : this()
    {
        _ = (sessionService, databaseService, windowService);
    }

    public bool IsMenuOpen
    {
        get => _isMenuOpen;
        set
        {
            if (SetProperty(ref _isMenuOpen, value))
            {
                OnPropertyChanged(nameof(IsMenuMinimized));
            }
        }
    }

    public bool IsMenuMinimized => !IsMenuOpen;

    public ICommand ToggleMenuCommand { get; }

    private void ToggleMenu()
    {
        IsMenuOpen = !IsMenuOpen;
    }
}
