using Avalonia.Controls;
using Nethereum.Wallet.UI.Components.Avalonia.Views.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Nethereum.Wallet.UI.Components.Avalonia.UnitTests.Controls;

public class WalletTextFieldPasswordTests : TestBase
{
    [Fact]
    public async Task PasswordReveal_ToggleButton_ChangesIconCorrectly()
    {
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.Password,
            ShowRevealToggle = true,
            IsRevealed = false
        });

        PlaceInWindow(field);
        await WaitForUIAsync();

        field.ComputedActionIcon.Should().Be("visibility");
        field.ComputedPasswordChar.Should().Be('●');

        await RunOnUIThreadAsync(async () => await field.HandleAdornmentClick());

        field.IsRevealed.Should().BeTrue();
        field.ComputedActionIcon.Should().Be("visibility_off");
        field.ComputedPasswordChar.Should().Be('\0');

        await RunOnUIThreadAsync(async () => await field.HandleAdornmentClick());

        field.IsRevealed.Should().BeFalse();
        field.ComputedActionIcon.Should().Be("visibility");
        field.ComputedPasswordChar.Should().Be('●');
    }

    [Fact]
    public async Task PrivateKeyField_PasswordReveal_WorksCorrectly()
    {
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.PrivateKey,
            ShowRevealToggle = true,
            IsRevealed = false,
            Value = "0x1234567890abcdef"
        });

        PlaceInWindow(field);
        await WaitForUIAsync();

        field.ComputedPasswordChar.Should().Be('●');
        field.ComputedActionIcon.Should().Be("visibility");

        await RunOnUIThreadAsync(async () => await field.HandleAdornmentClick());

        field.IsRevealed.Should().BeTrue();
        field.ComputedPasswordChar.Should().Be('\0');
        field.ComputedActionIcon.Should().Be("visibility_off");
    }

    [Fact]
    public void PasswordField_WithoutRevealToggle_DoesNotShowIcon()
    {
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.Password,
            ShowRevealToggle = false
        });

        field.ComputedActionIcon.Should().BeEmpty();
        field.ComputedPasswordChar.Should().Be('●');
    }

    [Fact]
    public void NonPasswordField_DoesNotMaskCharacters()
    {
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.Text,
            ShowRevealToggle = true
        });

        field.ComputedPasswordChar.Should().Be('\0');
        field.ComputedActionIcon.Should().BeEmpty();
    }

    [Fact]
    public async Task CustomRevealCommand_OverridesDefaultBehavior()
    {
        var customCommandExecuted = false;
        var customCommand = new TestRevealCommand(() => customCommandExecuted = true);

        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.Password,
            ShowRevealToggle = true,
            OnToggleRevealCommand = customCommand,
            IsRevealed = false
        });

        PlaceInWindow(field);
        await WaitForUIAsync();

        await RunOnUIThreadAsync(async () => await field.HandleAdornmentClick());

        customCommandExecuted.Should().BeTrue();
        field.IsRevealed.Should().BeFalse();
    }

    [Fact]
    public async Task PasswordField_IconMapping_CorrectlyMapsToPathData()
    {
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.Password,
            ShowRevealToggle = true,
            IsRevealed = false
        });

        PlaceInWindow(field);
        await WaitForUIAsync();

        var visibilityIcon = field.ComputedActionIcon;
        var iconData = Nethereum.Wallet.UI.Components.Avalonia.Extensions.IconMappingExtensions
            .ToAvaloniaPathIconData(visibilityIcon);

        visibilityIcon.Should().Be("visibility");
        iconData.Should().NotBeEmpty();
        iconData.Should().NotBe("M0 0h24v24H0z");

        await RunOnUIThreadAsync(async () => await field.HandleAdornmentClick());
        var visibilityOffIcon = field.ComputedActionIcon;
        var iconOffData = Nethereum.Wallet.UI.Components.Avalonia.Extensions.IconMappingExtensions
            .ToAvaloniaPathIconData(visibilityOffIcon);

        visibilityOffIcon.Should().Be("visibility_off");
        iconOffData.Should().NotBeEmpty();
        iconOffData.Should().NotBe("M0 0h24v24H0z");
        iconOffData.Should().NotBe(iconData);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("this-is-a-very-long-password-that-should-still-work-correctly")]
    public async Task PasswordReveal_WorksWithDifferentPasswordLengths(string password)
    {
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.Password,
            ShowRevealToggle = true,
            Value = password,
            IsRevealed = false
        });

        PlaceInWindow(field);
        await WaitForUIAsync();

        field.ComputedPasswordChar.Should().Be('●');

        await RunOnUIThreadAsync(async () => await field.HandleAdornmentClick());

        field.IsRevealed.Should().BeTrue();
        field.ComputedPasswordChar.Should().Be('\0');
        field.Value.Should().Be(password);
    }

    [Fact]
    public void PasswordChar_Property_OverridesComputedValue()
    {
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.Password,
            PasswordChar = '*',
            IsRevealed = false
        });

        field.ComputedPasswordChar.Should().Be('●');
        field.PasswordChar.Should().Be('*');
    }

    [Fact]
    public async Task PasswordReveal_PropertyChangedEvents_FiredCorrectly()
    {
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.Password,
            ShowRevealToggle = true,
            IsRevealed = false
        });

        var propertyChangedEvents = new List<string>();
        RunOnUIThread(() =>
        {
            field.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName != null)
                    propertyChangedEvents.Add(e.PropertyName);
            };
        });

        PlaceInWindow(field);
        await WaitForUIAsync();

        RunOnUIThread(() => field.IsRevealed = true);

        propertyChangedEvents.Should().Contain(nameof(field.ComputedPasswordChar));
        propertyChangedEvents.Should().Contain(nameof(field.ComputedActionIcon));
    }

    private class TestRevealCommand : System.Windows.Input.ICommand
    {
        private readonly Action _executeAction;

        public TestRevealCommand(Action executeAction)
        {
            _executeAction = executeAction;
        }

        public event EventHandler CanExecuteChanged = delegate { };
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _executeAction();
    }
}