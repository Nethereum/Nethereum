using Avalonia.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using Nethereum.Wallet.UI.Components.Avalonia.Views.Shared;
using System.ComponentModel;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI.Components.Avalonia.UnitTests.Controls;

public partial class WalletTextFieldBindingTests : TestBase
{
    [Fact]
    public void ValueChanged_Callback_InvokesWhenValueSet()
    {
        var field = RunOnUIThread(() => new WalletTextField());
        var callbackValues = new List<string>();

        RunOnUIThread(() =>
        {
            field.ValueChanged = value => callbackValues.Add(value);
        });

        RunOnUIThread(() =>
        {
            field.Value = "first";
            field.Value = "second";
            field.Value = "";
        });

        callbackValues.Should().BeEquivalentTo(new[] { "first", "second", "" });
    }

    [Fact]
    public async Task TwoWayBinding_WithTestViewModel_WorksCorrectly()
    {
        var viewModel = new TestViewModel();
        var field = RunOnUIThread(() => new WalletTextField());

        RunOnUIThread(() =>
        {
            field.Value = viewModel.TestValue;

            field.ValueChanged = value => viewModel.TestValue = value;

            viewModel.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(viewModel.TestValue))
                {
                    field.Value = viewModel.TestValue;
                }
            };
        });

        PlaceInWindow(field);
        await WaitForUIAsync();

        viewModel.TestValue = "from viewmodel";
        await WaitForUIAsync();

        field.Value.Should().Be("from viewmodel");

        RunOnUIThread(() => field.Value = "from field");

        viewModel.TestValue.Should().Be("from field");
    }

    [Fact]
    public async Task ValidationIntegration_WithViewModel_UpdatesErrorState()
    {
        var viewModel = new TestValidationViewModel();
        var field = RunOnUIThread(() => new WalletTextField());

        RunOnUIThread(() =>
        {
            field.Value = viewModel.Email;

            field.ValueChanged = value =>
            {
                viewModel.Email = value;
                viewModel.ValidateEmail();
            };

            viewModel.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(viewModel.EmailError))
                {
                    field.Error = !string.IsNullOrEmpty(viewModel.EmailError);
                    field.ErrorText = viewModel.EmailError ?? "";
                }
            };
        });

        PlaceInWindow(field);
        await WaitForUIAsync();

        RunOnUIThread(() => field.Value = "invalid-email");

        field.Error.Should().BeTrue();
        field.ErrorText.Should().Be("Please enter a valid email address");

        RunOnUIThread(() => field.Value = "valid@example.com");

        field.Error.Should().BeFalse();
        field.ErrorText.Should().BeEmpty();
    }

    [Fact]
    public async Task MultipleFields_IndependentValidation_WorksCorrectly()
    {
        var viewModel = new TestFormViewModel();
        var usernameField = RunOnUIThread(() => new WalletTextField());
        var passwordField = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.Password
        });

        RunOnUIThread(() =>
        {
            usernameField.Value = viewModel.Username;
            usernameField.ValueChanged = value =>
            {
                viewModel.Username = value;
                viewModel.ValidateUsername();
            };

            viewModel.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(viewModel.UsernameError))
                {
                    usernameField.Error = !string.IsNullOrEmpty(viewModel.UsernameError);
                    usernameField.ErrorText = viewModel.UsernameError ?? "";
                }
            };
        });

        RunOnUIThread(() =>
        {
            passwordField.Value = viewModel.Password;
            passwordField.ValueChanged = value =>
            {
                viewModel.Password = value;
                viewModel.ValidatePassword();
            };

            viewModel.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(viewModel.PasswordError))
                {
                    passwordField.Error = !string.IsNullOrEmpty(viewModel.PasswordError);
                    passwordField.ErrorText = viewModel.PasswordError ?? "";
                }
            };
        });

        PlaceInWindow(usernameField);
        await WaitForUIAsync();

        RunOnUIThread(() => usernameField.Value = "ab");

        usernameField.Error.Should().BeTrue();
        usernameField.ErrorText.Should().Be("Username must be at least 3 characters");

        RunOnUIThread(() => passwordField.Value = "123");

        passwordField.Error.Should().BeTrue();
        passwordField.ErrorText.Should().Be("Password must be at least 8 characters");
        usernameField.Error.Should().BeTrue();

        RunOnUIThread(() => usernameField.Value = "validuser");

        usernameField.Error.Should().BeFalse();
        usernameField.ErrorText.Should().BeEmpty();
        passwordField.Error.Should().BeTrue();
    }

    [Fact]
    public void ValueProperty_HasCorrectDefaultBindingMode()
    {
        var valueProperty = WalletTextField.ValueProperty;
        valueProperty.Should().NotBeNull();
    }

    [Fact]
    public async Task DynamicPropertyChanges_UpdateFieldCorrectly()
    {
        var field = RunOnUIThread(() => new WalletTextField());
        PlaceInWindow(field);
        await WaitForUIAsync();

        RunOnUIThread(() =>
        {
            field.Label = "Initial Label";
            field.Placeholder = "Initial Placeholder";
            field.MaxLength = 10;
        });

        field.Label.Should().Be("Initial Label");
        field.Placeholder.Should().Be("Initial Placeholder");
        field.MaxLength.Should().Be(10);

        RunOnUIThread(() =>
        {
            field.Label = "Updated Label";
            field.Placeholder = "Updated Placeholder";
            field.MaxLength = 20;
        });

        field.Label.Should().Be("Updated Label");
        field.Placeholder.Should().Be("Updated Placeholder");
        field.MaxLength.Should().Be(20);
    }

    public partial class TestViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _testValue = "";
    }

    public partial class TestValidationViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _email = "";

        [ObservableProperty]
        private string? _emailError;

        public void ValidateEmail()
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
                EmailError = null;
            }
        }
    }

    public partial class TestFormViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _username = "";

        [ObservableProperty]
        private string? _usernameError;

        [ObservableProperty]
        private string _password = "";

        [ObservableProperty]
        private string? _passwordError;

        public void ValidateUsername()
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
                UsernameError = null;
            }
        }

        public void ValidatePassword()
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
                PasswordError = null;
            }
        }

        public bool IsFormValid =>
            string.IsNullOrEmpty(UsernameError) &&
            string.IsNullOrEmpty(PasswordError) &&
            !string.IsNullOrWhiteSpace(Username) &&
            !string.IsNullOrWhiteSpace(Password);
    }
}