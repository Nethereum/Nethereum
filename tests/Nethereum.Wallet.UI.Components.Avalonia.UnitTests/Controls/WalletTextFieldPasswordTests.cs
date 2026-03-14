using Avalonia.Controls;
using Nethereum.Wallet.UI.Components.Avalonia.Views.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Nethereum.Wallet.UI.Components.Avalonia.UnitTests.Controls;

/// <summary>
/// Specialized tests for password reveal functionality and related behaviors
/// </summary>
public class WalletTextFieldPasswordTests : TestBase
{
    [Fact]
    public async Task PasswordReveal_ToggleButton_ChangesIconCorrectly()
    {
        // Arrange
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.Password,
            ShowRevealToggle = true,
            IsRevealed = false
        });

        PlaceInWindow(field);
        await WaitForUIAsync();

        // Assert initial state
        field.ComputedActionIcon.Should().Be("visibility");
        field.ComputedPasswordChar.Should().Be('●');

        // Act - Toggle reveal
        await RunOnUIThreadAsync(async () => await field.HandleAdornmentClick());

        // Assert revealed state
        field.IsRevealed.Should().BeTrue();
        field.ComputedActionIcon.Should().Be("visibility_off");
        field.ComputedPasswordChar.Should().Be('\0');

        // Act - Toggle back to hidden
        await RunOnUIThreadAsync(async () => await field.HandleAdornmentClick());

        // Assert hidden state
        field.IsRevealed.Should().BeFalse();
        field.ComputedActionIcon.Should().Be("visibility");
        field.ComputedPasswordChar.Should().Be('●');
    }

    [Fact]
    public async Task PrivateKeyField_PasswordReveal_WorksCorrectly()
    {
        // Arrange
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.PrivateKey,
            ShowRevealToggle = true,
            IsRevealed = false,
            Value = "0x1234567890abcdef"
        });

        PlaceInWindow(field);
        await WaitForUIAsync();

        // Assert initial state - private key is masked
        field.ComputedPasswordChar.Should().Be('●');
        field.ComputedActionIcon.Should().Be("visibility");

        // Act - Reveal private key
        await RunOnUIThreadAsync(async () => await field.HandleAdornmentClick());

        // Assert revealed state - private key is visible
        field.IsRevealed.Should().BeTrue();
        field.ComputedPasswordChar.Should().Be('\0');
        field.ComputedActionIcon.Should().Be("visibility_off");
    }

    [Fact]
    public void PasswordField_WithoutRevealToggle_DoesNotShowIcon()
    {
        // Arrange & Act
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.Password,
            ShowRevealToggle = false
        });

        // Assert
        field.ComputedActionIcon.Should().BeEmpty();
        field.ComputedPasswordChar.Should().Be('●'); // Still masked
    }

    [Fact]
    public void NonPasswordField_DoesNotMaskCharacters()
    {
        // Arrange & Act
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.Text,
            ShowRevealToggle = true // Should be ignored for non-password fields
        });

        // Assert
        field.ComputedPasswordChar.Should().Be('\0'); // No masking
        field.ComputedActionIcon.Should().BeEmpty(); // No reveal button
    }

    [Fact]
    public async Task CustomRevealCommand_OverridesDefaultBehavior()
    {
        // Arrange
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

        // Act
        await RunOnUIThreadAsync(async () => await field.HandleAdornmentClick());

        // Assert
        customCommandExecuted.Should().BeTrue();
        field.IsRevealed.Should().BeFalse(); // Should not change because custom command doesn't modify it
    }

    [Fact]
    public async Task PasswordField_IconMapping_CorrectlyMapsToPathData()
    {
        // Arrange
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.Password,
            ShowRevealToggle = true,
            IsRevealed = false
        });

        PlaceInWindow(field);
        await WaitForUIAsync();

        // Act - Get the icon and test mapping
        var visibilityIcon = field.ComputedActionIcon;
        var iconData = Nethereum.Wallet.UI.Components.Avalonia.Extensions.IconMappingExtensions
            .ToAvaloniaPathIconData(visibilityIcon);

        // Assert
        visibilityIcon.Should().Be("visibility");
        iconData.Should().NotBeEmpty();
        iconData.Should().NotBe("M0 0h24v24H0z"); // Should not be the default fallback

        // Act - Toggle and test the other icon
        await RunOnUIThreadAsync(async () => await field.HandleAdornmentClick());
        var visibilityOffIcon = field.ComputedActionIcon;
        var iconOffData = Nethereum.Wallet.UI.Components.Avalonia.Extensions.IconMappingExtensions
            .ToAvaloniaPathIconData(visibilityOffIcon);

        // Assert
        visibilityOffIcon.Should().Be("visibility_off");
        iconOffData.Should().NotBeEmpty();
        iconOffData.Should().NotBe("M0 0h24v24H0z"); // Should not be the default fallback
        iconOffData.Should().NotBe(iconData); // Should be different from visibility icon
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("this-is-a-very-long-password-that-should-still-work-correctly")]
    public async Task PasswordReveal_WorksWithDifferentPasswordLengths(string password)
    {
        // Arrange
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.Password,
            ShowRevealToggle = true,
            Value = password,
            IsRevealed = false
        });

        PlaceInWindow(field);
        await WaitForUIAsync();

        // Assert initial state - password is masked
        field.ComputedPasswordChar.Should().Be('●');

        // Act - Reveal password
        await RunOnUIThreadAsync(async () => await field.HandleAdornmentClick());

        // Assert revealed state - password is visible
        field.IsRevealed.Should().BeTrue();
        field.ComputedPasswordChar.Should().Be('\0');
        field.Value.Should().Be(password);
    }

    [Fact]
    public void PasswordChar_Property_OverridesComputedValue()
    {
        // Arrange & Act
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.Password,
            PasswordChar = '*', // Custom password character
            IsRevealed = false
        });

        // Assert - Custom password char should be ignored, ComputedPasswordChar takes precedence
        field.ComputedPasswordChar.Should().Be('●'); // Standard computed value
        field.PasswordChar.Should().Be('*'); // Property value preserved
    }

    [Fact]
    public async Task PasswordReveal_PropertyChangedEvents_FiredCorrectly()
    {
        // Arrange
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

        // Act
        RunOnUIThread(() => field.IsRevealed = true);

        // Assert
        propertyChangedEvents.Should().Contain(nameof(field.ComputedPasswordChar));
        propertyChangedEvents.Should().Contain(nameof(field.ComputedActionIcon));
    }

    /// <summary>
    /// Test command for custom reveal behavior
    /// </summary>
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