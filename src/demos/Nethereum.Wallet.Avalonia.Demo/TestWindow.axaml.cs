using Avalonia.Controls;
using Nethereum.Wallet.UI.Components.Avalonia.Views.Shared;
using System.ComponentModel;

namespace Nethereum.Wallet.Avalonia.Demo;

public partial class TestWindow : Window
{
    private string _testValue = "test";
    private TestViewModel _viewModel;

    public TestWindow()
    {
        InitializeComponent();

        _viewModel = new TestViewModel();

        SetupSimpleCallbackTest();

        SetupViewModelTest();
    }

    private void SetupSimpleCallbackTest()
    {
        var testField = this.FindControl<WalletTextField>("TestField");
        var resultLabel = this.FindControl<TextBlock>("CallbackResult");
        var validationLabel = this.FindControl<TextBlock>("ValidationResult");

        if (testField != null)
        {
            testField.ValueChanged = (newValue) =>
            {
                _testValue = newValue;

                if (resultLabel != null)
                {
                    resultLabel.Text = $"ValueChanged fired! New value: '{newValue}'";
                }

                if (validationLabel != null)
                {
                    if (string.IsNullOrWhiteSpace(newValue))
                    {
                        validationLabel.Text = "❌ Validation: Field is required";
                        testField.Error = true;
                        testField.ErrorText = "This field is required";
                    }
                    else if (newValue.Length < 3)
                    {
                        validationLabel.Text = "❌ Validation: Must be at least 3 characters";
                        testField.Error = true;
                        testField.ErrorText = "Must be at least 3 characters";
                    }
                    else
                    {
                        validationLabel.Text = "✅ Validation: Valid!";
                        testField.Error = false;
                        testField.ErrorText = "";
                    }
                }
            };
        }
    }

    private void SetupViewModelTest()
    {
        var usernameField = this.FindControl<WalletTextField>("UsernameField");
        var emailField = this.FindControl<WalletTextField>("EmailField");
        var passwordField = this.FindControl<WalletTextField>("PasswordField");
        var formStatusLabel = this.FindControl<TextBlock>("FormStatusLabel");
        var submitButton = this.FindControl<Button>("SubmitButton");

        _viewModel.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.UsernameError))
            {
                if (usernameField != null)
                {
                    usernameField.Error = !string.IsNullOrEmpty(_viewModel.UsernameError);
                    usernameField.ErrorText = _viewModel.UsernameError;
                }
            }
            else if (e.PropertyName == nameof(_viewModel.EmailError))
            {
                if (emailField != null)
                {
                    emailField.Error = !string.IsNullOrEmpty(_viewModel.EmailError);
                    emailField.ErrorText = _viewModel.EmailError;
                }
            }
            else if (e.PropertyName == nameof(_viewModel.PasswordError))
            {
                if (passwordField != null)
                {
                    passwordField.Error = !string.IsNullOrEmpty(_viewModel.PasswordError);
                    passwordField.ErrorText = _viewModel.PasswordError;
                }
            }

            UpdateFormStatus(formStatusLabel, submitButton);
        };

        if (usernameField != null)
        {
            usernameField.ValueChanged = (newValue) => _viewModel.Username = newValue;
        }

        if (emailField != null)
        {
            emailField.ValueChanged = (newValue) => _viewModel.Email = newValue;
        }

        if (passwordField != null)
        {
            passwordField.ValueChanged = (newValue) => _viewModel.Password = newValue;
        }

        UpdateFormStatus(formStatusLabel, submitButton);
    }

    private void UpdateFormStatus(TextBlock? statusLabel, Button? submitButton)
    {
        if (statusLabel != null)
        {
            if (_viewModel.IsFormValid)
            {
                statusLabel.Text = "✅ Form is valid - ready to submit!";
            }
            else
            {
                statusLabel.Text = "❌ Form has validation errors";
            }
        }

        if (submitButton != null)
        {
            submitButton.IsEnabled = _viewModel.IsFormValid;
        }
    }
}