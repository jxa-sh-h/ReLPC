using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReLPC.Models;
using ReLPC.Services;

namespace ReLPC.ViewModels;

public partial class SignUpWindowViewModel : ViewModelBase
{
    private readonly ISessionService _sessionService;
    private readonly IDatabaseService _databaseService;
    private readonly IWindowService _windowService;

    public SignUpWindowViewModel(
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
    private string _passwordConfirmation = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PasswordChar1))]
    [NotifyPropertyChangedFor(nameof(IsPasswordVisible1))]
    private bool _hidePassword1 = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PasswordChar2))]
    [NotifyPropertyChangedFor(nameof(IsPasswordVisible2))]
    private bool _hidePassword2 = true;

    [ObservableProperty]
    private string? _signupStatus;

    public string PasswordChar1 => HidePassword1 ? "\u25cf" : string.Empty;

    public string PasswordChar2 => HidePassword2 ? "\u25cf" : string.Empty;

    public bool IsPasswordVisible1 => !HidePassword1;

    public bool IsPasswordVisible2 => !HidePassword2;

    [RelayCommand]
    private void TogglePassword1Visibility() => HidePassword1 = !HidePassword1;

    [RelayCommand]
    private void TogglePassword2Visibility() => HidePassword2 = !HidePassword2;

    [RelayCommand]
    private void AttemptSignup()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrEmpty(Password) ||
            string.IsNullOrEmpty(PasswordConfirmation))
        {
            SignupStatus = "Fill in all user details!";
            return;
        }

        if (!Username.All(c => char.IsDigit(c) || c == '-'))
        {
            SignupStatus = "Invalid ID! Example ID: 12-3456-789";
            return;
        }

        if (Password != PasswordConfirmation)
        {
            SignupStatus = "Passwords don't match!";
            return;
        }

        if (_databaseService.GetProfileByUsername(Username) is not null)
        {
            SignupStatus = "ID already registered!";
            return;
        }

        var profile = new UserProfile
        {
            Username = Username.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(Password),
        };

        try
        {
            _databaseService.UpsertUser(profile);
            SignupStatus = "Successfully signed up!";
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            SignupStatus = "An error occured while signing up!";
        }
    }
}