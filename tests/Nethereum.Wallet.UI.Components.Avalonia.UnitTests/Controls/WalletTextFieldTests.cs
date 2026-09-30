using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Nethereum.Wallet.UI.Components.Avalonia.Views.Shared;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Nethereum.Wallet.UI.Components.Avalonia.UnitTests.Controls;

public class WalletTextFieldTests : TestBase
{
    [Fact]
    public void Constructor_SetsDataContextToSelf()
    {
        var field = RunOnUIThread(() => new WalletTextField());

        field.DataContext.Should().Be(field);
    }

    [Theory]
    [InlineData("Test Label")]
    [InlineData("Password")]
    [InlineData("")]
    [InlineData(null)]
    public void Label_Property_SetsCorrectly(string? label)
    {
        var field = RunOnUIThread(() => new WalletTextField());

        RunOnUIThread(() => field.Label = label);

        field.Label.Should().Be(label);
    }

    [Theory]
    [InlineData("initial value")]
    [InlineData("")]
    [InlineData(null)]
    public void Value_Property_SetsAndGetsCorrectly(string? value)
    {
        var field = RunOnUIThread(() => new WalletTextField());

        RunOnUIThread(() => field.Value = value ?? "");

        field.Value.Should().Be(value ?? "");
    }

    [Fact]
    public void Value_Property_HasTwoWayBinding()
    {
        var field = RunOnUIThread(() => new WalletTextField());

        var property = WalletTextField.ValueProperty;
        property.Should().NotBeNull();
    }

    [Theory]
    [InlineData(WalletTextField.WalletTextFieldType.Text, '\0')]
    [InlineData(WalletTextField.WalletTextFieldType.Password, '●')]
    [InlineData(WalletTextField.WalletTextFieldType.PrivateKey, '●')]
    [InlineData(WalletTextField.WalletTextFieldType.Email, '\0')]
    public void ComputedPasswordChar_ReturnsCorrectChar_WhenNotRevealed(WalletTextField.WalletTextFieldType fieldType, char expectedChar)
    {
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = fieldType,
            IsRevealed = false
        });

        var passwordChar = field.ComputedPasswordChar;

        passwordChar.Should().Be(expectedChar);
    }

    [Theory]
    [InlineData(WalletTextField.WalletTextFieldType.Password)]
    [InlineData(WalletTextField.WalletTextFieldType.PrivateKey)]
    public void ComputedPasswordChar_ReturnsNull_WhenRevealed(WalletTextField.WalletTextFieldType fieldType)
    {
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = fieldType,
            IsRevealed = true
        });

        var passwordChar = field.ComputedPasswordChar;

        passwordChar.Should().Be('\0');
    }

    [Theory]
    [InlineData(WalletTextField.WalletTextFieldType.Password, true, "visibility_off")]
    [InlineData(WalletTextField.WalletTextFieldType.Password, false, "visibility")]
    [InlineData(WalletTextField.WalletTextFieldType.PrivateKey, true, "visibility_off")]
    [InlineData(WalletTextField.WalletTextFieldType.PrivateKey, false, "visibility")]
    public void ComputedActionIcon_ReturnsCorrectIcon_ForPasswordFields(WalletTextField.WalletTextFieldType fieldType, bool isRevealed, string expectedIcon)
    {
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = fieldType,
            ShowRevealToggle = true,
            IsRevealed = isRevealed
        });

        var icon = field.ComputedActionIcon;

        icon.Should().Be(expectedIcon);
    }

    [Theory]
    [InlineData("copy")]
    [InlineData("search")]
    [InlineData("custom-icon")]
    public void ComputedActionIcon_ReturnsActionIcon_WhenSet(string actionIcon)
    {
        var field = RunOnUIThread(() => new WalletTextField
        {
            ActionIcon = actionIcon
        });

        var icon = field.ComputedActionIcon;

        icon.Should().Be(actionIcon);
    }

    [Fact]
    public void ComputedActionIcon_ReturnsEmpty_WhenNoIconSet()
    {
        var field = RunOnUIThread(() => new WalletTextField
        {
            ShowRevealToggle = false,
            ActionIcon = ""
        });

        var icon = field.ComputedActionIcon;

        icon.Should().BeEmpty();
    }

    [Fact]
    public void Error_Property_SetsCorrectly()
    {
        var field = RunOnUIThread(() => new WalletTextField());

        RunOnUIThread(() => field.Error = true);

        field.Error.Should().BeTrue();
    }

    [Theory]
    [InlineData("This field is required")]
    [InlineData("Invalid email format")]
    [InlineData("")]
    public void ErrorText_Property_SetsCorrectly(string errorText)
    {
        var field = RunOnUIThread(() => new WalletTextField());

        RunOnUIThread(() => field.ErrorText = errorText);

        field.ErrorText.Should().Be(errorText);
    }

    [Fact]
    public void ValueChanged_Callback_InvokesCorrectly()
    {
        var field = RunOnUIThread(() => new WalletTextField());
        var callbackInvoked = false;
        var callbackValue = "";

        RunOnUIThread(() =>
        {
            field.ValueChanged = (value) =>
            {
                callbackInvoked = true;
                callbackValue = value;
            };
        });

        RunOnUIThread(() => field.Value = "new value");

        callbackInvoked.Should().BeTrue();
        callbackValue.Should().Be("new value");
    }

    [Fact]
    public void ShowRevealToggle_Property_SetsCorrectly()
    {
        var field = RunOnUIThread(() => new WalletTextField());

        RunOnUIThread(() => field.ShowRevealToggle = true);

        field.ShowRevealToggle.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAdornmentClick_TogglesIsRevealed_ForPasswordField()
    {
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.Password,
            ShowRevealToggle = true,
            IsRevealed = false
        });

        PlaceInWindow(field);
        await WaitForUIAsync();

        await RunOnUIThreadAsync(async () => await field.HandleAdornmentClick());

        field.IsRevealed.Should().BeTrue();

        await RunOnUIThreadAsync(async () => await field.HandleAdornmentClick());

        field.IsRevealed.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAdornmentClick_ClearsValue_ForSearchField()
    {
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.Search,
            Value = "search text"
        });

        var valueChangedCalled = false;
        var newValue = "";

        RunOnUIThread(() =>
        {
            field.ValueChanged = (value) =>
            {
                valueChangedCalled = true;
                newValue = value;
            };
        });

        PlaceInWindow(field);
        await WaitForUIAsync();

        await RunOnUIThreadAsync(async () => await field.HandleAdornmentClick());

        valueChangedCalled.Should().BeTrue();
        newValue.Should().BeEmpty();
    }

    [Theory]
    [InlineData(WalletTextField.WalletTextFieldType.Text, "Text")]
    [InlineData(WalletTextField.WalletTextFieldType.Password, "Password")]
    [InlineData(WalletTextField.WalletTextFieldType.Email, "Email")]
    [InlineData(WalletTextField.WalletTextFieldType.Url, "Url")]
    [InlineData(WalletTextField.WalletTextFieldType.Search, "Search")]
    public void GetInputType_ReturnsCorrectType(WalletTextField.WalletTextFieldType fieldType, string expectedType)
    {
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = fieldType
        });

        var inputType = field.GetInputType();

        inputType.Should().Be(expectedType);
    }

    [Fact]
    public void HasActionButton_ReturnsTrue_WhenActionIconAndCommandSet()
    {
        var field = RunOnUIThread(() => new WalletTextField
        {
            ActionIcon = "copy",
            OnActionClickCommand = new TestCommand()
        });

        var hasActionButton = field.HasActionButton();

        hasActionButton.Should().BeTrue();
    }

    [Fact]
    public void HasActionButton_ReturnsFalse_WhenActionIconEmpty()
    {
        var field = RunOnUIThread(() => new WalletTextField
        {
            ActionIcon = "",
            OnActionClickCommand = new TestCommand()
        });

        var hasActionButton = field.HasActionButton();

        hasActionButton.Should().BeFalse();
    }

    [Fact]
    public void HasActionButton_ReturnsFalse_WhenCommandNull()
    {
        var field = RunOnUIThread(() => new WalletTextField
        {
            ActionIcon = "copy",
            OnActionClickCommand = null
        });

        var hasActionButton = field.HasActionButton();

        hasActionButton.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Disabled_Property_SetsCorrectly(bool disabled)
    {
        var field = RunOnUIThread(() => new WalletTextField());

        RunOnUIThread(() => field.Disabled = disabled);

        field.Disabled.Should().Be(disabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadOnly_Property_SetsCorrectly(bool readOnly)
    {
        var field = RunOnUIThread(() => new WalletTextField());

        RunOnUIThread(() => field.ReadOnly = readOnly);

        field.ReadOnly.Should().Be(readOnly);
    }

    [Theory]
    [InlineData("Enter your password")]
    [InlineData("Search...")]
    [InlineData("")]
    public void Placeholder_Property_SetsCorrectly(string placeholder)
    {
        var field = RunOnUIThread(() => new WalletTextField());

        RunOnUIThread(() => field.Placeholder = placeholder);

        field.Placeholder.Should().Be(placeholder);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(0)]
    public void MaxLength_Property_SetsCorrectly(int maxLength)
    {
        var field = RunOnUIThread(() => new WalletTextField());

        RunOnUIThread(() => field.MaxLength = maxLength);

        field.MaxLength.Should().Be(maxLength);
    }

    [Fact]
    public void IsRevealed_PropertyChanged_NotifiesComputedProperties()
    {
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.Password,
            ShowRevealToggle = true
        });

        var passwordCharChanged = false;
        var actionIconChanged = false;

        RunOnUIThread(() =>
        {
            field.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(field.ComputedPasswordChar))
                    passwordCharChanged = true;
                if (e.PropertyName == nameof(field.ComputedActionIcon))
                    actionIconChanged = true;
            };
        });

        RunOnUIThread(() => field.IsRevealed = true);

        passwordCharChanged.Should().BeTrue();
        actionIconChanged.Should().BeTrue();
    }

    [Fact]
    public void GetAdornmentAriaLabel_ReturnsToggleRevealAriaLabel_WhenShowRevealToggle()
    {
        var ariaLabel = "Toggle password visibility";
        var field = RunOnUIThread(() => new WalletTextField
        {
            ShowRevealToggle = true,
            ToggleRevealAriaLabel = ariaLabel
        });

        var result = field.GetAdornmentAriaLabel();

        result.Should().Be(ariaLabel);
    }

    [Fact]
    public void GetAdornmentAriaLabel_ReturnsClearSearch_ForSearchFieldWithValue()
    {
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.Search,
            Value = "search term"
        });

        var result = field.GetAdornmentAriaLabel();

        result.Should().Be("Clear search");
    }

    [Fact]
    public void GetAdornmentAriaLabel_ReturnsActionTooltip_WhenHasActionButton()
    {
        var tooltip = "Copy to clipboard";
        var field = RunOnUIThread(() => new WalletTextField
        {
            ActionIcon = "copy",
            OnActionClickCommand = new TestCommand(),
            ActionTooltip = tooltip
        });

        var result = field.GetAdornmentAriaLabel();

        result.Should().Be(tooltip);
    }

    private class TestCommand : System.Windows.Input.ICommand
    {
        public event EventHandler CanExecuteChanged = delegate { };
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { }
    }
}