using System;
using CommunityToolkit.Mvvm.ComponentModel;
using ReLPC;
using CommunityToolkit.Mvvm.Input;
using ReLPC.Services;

namespace ReLPC.ViewModels;

public partial class LoginWindowViewModel : ViewModelBase
{
    private readonly ISessionService _sessionService;
    private readonly IDatabaseService _databaseService;
    private readonly IWindowService _windowService;

    public LoginWindowViewModel(
        ISessionService sessionService,
        IDatabaseService databaseService,
        IWindowService windowService)
    {
        _sessionService = sessionService;
        _databaseService = databaseService;
        _windowService = windowService;
    }

    [ObservableProperty]
    private string _username = "";

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PasswordChar))]
    [NotifyPropertyChangedFor(nameof(IsPasswordVisible))]
    private bool _hidePassword = true;

    public string PasswordChar => HidePassword ? "\u25cf" : string.Empty;

    public bool IsPasswordVisible => !HidePassword;

    [ObservableProperty]
    private string _loginMessage = "";

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        HidePassword = !HidePassword;
    }

    [RelayCommand]
    private void AttemptLogin()
    {
        LoginMessage = "";

        if (string.IsNullOrWhiteSpace(Username))
        {
            LoginMessage = "Enter your student ID.";
            return;
        }

        if (string.IsNullOrEmpty(Password))
        {
            LoginMessage = "Enter your password.";
            return;
        }

        var id = Username.Trim();
        var profile = _databaseService.GetProfileByUsername(id);
        if (profile is null)
        {
            LoginMessage = "Unknown student ID.";
            return;
        }

        if (!BCrypt.Net.BCrypt.EnhancedVerify(Password, profile.PasswordHash))
        {
            LoginMessage = "Incorrect password.";
            return;
        }

        _sessionService.CurrentUser = profile;

        var dashboard = new DashboardWindow();
        DesktopSession.ShowAsMainWindow(dashboard);

        _windowService.FindWindowFromDataModel(this)?.Close();
    }

    [RelayCommand]
    private void OpenSignupWindow()
    {
        var window = _windowService.FindWindowFromDataModel(typeof(SignUpWindowViewModel));
        if (window is not null)
            window.Activate();
        else
            _windowService.CreateAndShowWindow(new SignUpWindowViewModel(_sessionService, _databaseService,
                _windowService));
    }
}