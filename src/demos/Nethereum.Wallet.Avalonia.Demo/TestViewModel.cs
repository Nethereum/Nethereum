using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Nethereum.Wallet.Avalonia.Demo;

public partial class TestViewModel : ObservableObject
{
    [ObservableProperty]
    private string _username = "";

    [ObservableProperty]
    private string _usernameError = "";

    [ObservableProperty]
    private string _email = "";

    [ObservableProperty]
    private string _emailError = "";

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string _passwordError = "";

    partial void OnUsernameChanged(string value)
    {
        ValidateUsername();
    }

    partial void OnEmailChanged(string value)
    {
        ValidateEmail();
    }

    partial void OnPasswordChanged(string value)
    {
        ValidatePassword();
    }

    private void ValidateUsername()
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            UsernameError = "Username is required";
        }
        else if (Username.Length < 3)
        {
            UsernameError = "Username must be at least 3 characters";
        }
        else
        {
            UsernameError = "";
        }
    }

    private void ValidateEmail()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            EmailError = "Email is required";
        }
        else if (!Email.Contains("@"))
        {
            EmailError = "Please enter a valid email address";
        }
        else
        {
            EmailError = "";
        }
    }

    private void ValidatePassword()
    {
        if (string.IsNullOrWhiteSpace(Password))
        {
            PasswordError = "Password is required";
        }
        else if (Password.Length < 8)
        {
            PasswordError = "Password must be at least 8 characters";
        }
        else
        {
            PasswordError = "";
        }
    }

    public bool IsFormValid =>
        string.IsNullOrEmpty(UsernameError) &&
        string.IsNullOrEmpty(EmailError) &&
        string.IsNullOrEmpty(PasswordError) &&
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(Email) &&
        !string.IsNullOrWhiteSpace(Password);
}