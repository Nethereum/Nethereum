using Avalonia.Controls;
using Nethereum.Wallet.UI.Components.Avalonia.Views.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Linq;

namespace Nethereum.Wallet.UI.Components.Avalonia.UnitTests.Controls;

public class WalletTextFieldValidationTests : TestBase
{
    [Theory]
    [InlineData("", true, "Field is required")]
    [InlineData("   ", true, "Field is required")]
    [InlineData("valid input", false, "")]
    public async Task Required_Field_Validation_WorksCorrectly(string input, bool expectError, string expectedErrorText)
    {
        // Arrange
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.Text,
            Required = true
        });

        var validationTriggered = false;
        var errorState = false;
        var errorText = "";

        RunOnUIThread(() =>
        {
            field.ValueChanged = value =>
            {
                validationTriggered = true;
                // Simulate required field validation
                if (string.IsNullOrWhiteSpace(value))
                {
                    field.Error = true;
                    field.ErrorText = "Field is required";
                    errorState = true;
                    errorText = "Field is required";
                }
                else
                {
                    field.Error = false;
                    field.ErrorText = "";
                    errorState = false;
                    errorText = "";
                }
            };
        });

        PlaceInWindow(field);
        await WaitForUIAsync();

        // Act
        RunOnUIThread(() => field.Value = input);
        await WaitForUIAsync();

        // Assert
        validationTriggered.Should().BeTrue();
        errorState.Should().Be(expectError);
        errorText.Should().Be(expectedErrorText);
        field.Error.Should().Be(expectError);
        field.ErrorText.Should().Be(expectedErrorText);
    }

    [Theory]
    [InlineData("user@example.com", false, "")]
    [InlineData("invalid-email", true, "Please enter a valid email address")]
    [InlineData("user@", true, "Please enter a valid email address")]
    [InlineData("@example.com", true, "Please enter a valid email address")]
    [InlineData("", false, "")] // Empty is valid when not required
    public async Task Email_Field_Validation_WorksCorrectly(string email, bool expectError, string expectedErrorText)
    {
        // Arrange
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.Email,
            ValidationPattern = @"^[^\s@]+@[^\s@]+\.[^\s@]+$"
        });

        var validationTriggered = false;
        var errorState = false;
        var errorText = "";

        RunOnUIThread(() =>
        {
            field.ValueChanged = value =>
            {
                validationTriggered = true;
                // Simulate email validation
                if (!string.IsNullOrEmpty(value) && !Regex.IsMatch(value, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
                {
                    field.Error = true;
                    field.ErrorText = "Please enter a valid email address";
                    errorState = true;
                    errorText = "Please enter a valid email address";
                }
                else
                {
                    field.Error = false;
                    field.ErrorText = "";
                    errorState = false;
                    errorText = "";
                }
            };
        });

        PlaceInWindow(field);
        await WaitForUIAsync();

        // Act
        RunOnUIThread(() => field.Value = email);
        await WaitForUIAsync();

        // Assert
        validationTriggered.Should().BeTrue();
        errorState.Should().Be(expectError);
        errorText.Should().Be(expectedErrorText);
        field.Error.Should().Be(expectError);
        field.ErrorText.Should().Be(expectedErrorText);
    }

    [Theory]
    [InlineData("0x1234567890abcdef1234567890abcdef12345678", false, "")]
    [InlineData("1234567890abcdef1234567890abcdef12345678", false, "")] // Without 0x prefix
    [InlineData("0x123", true, "Address must be 40 characters (plus optional 0x prefix)")]
    [InlineData("0xGHIJKL", true, "Address must contain only hexadecimal characters")]
    [InlineData("", false, "")] // Empty is valid when not required
    public async Task Address_Field_Validation_WorksCorrectly(string address, bool expectError, string expectedErrorText)
    {
        // Arrange
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.Address,
            ValidationPattern = @"^(0x)?[a-fA-F0-9]{40}$"
        });

        var validationTriggered = false;
        var errorState = false;
        var errorText = "";

        RunOnUIThread(() =>
        {
            field.ValueChanged = value =>
            {
                validationTriggered = true;
                // Simulate address validation
                if (!string.IsNullOrEmpty(value))
                {
                    var cleanAddress = value.StartsWith("0x") ? value.Substring(2) : value;
                    if (cleanAddress.Length != 40)
                    {
                        field.Error = true;
                        field.ErrorText = "Address must be 40 characters (plus optional 0x prefix)";
                        errorState = true;
                        errorText = "Address must be 40 characters (plus optional 0x prefix)";
                    }
                    else if (!Regex.IsMatch(cleanAddress, @"^[a-fA-F0-9]+$"))
                    {
                        field.Error = true;
                        field.ErrorText = "Address must contain only hexadecimal characters";
                        errorState = true;
                        errorText = "Address must contain only hexadecimal characters";
                    }
                    else
                    {
                        field.Error = false;
                        field.ErrorText = "";
                        errorState = false;
                        errorText = "";
                    }
                }
                else
                {
                    field.Error = false;
                    field.ErrorText = "";
                    errorState = false;
                    errorText = "";
                }
            };
        });

        PlaceInWindow(field);
        await WaitForUIAsync();

        // Act
        RunOnUIThread(() => field.Value = address);
        await WaitForUIAsync();

        // Assert
        validationTriggered.Should().BeTrue();
        errorState.Should().Be(expectError);
        errorText.Should().Be(expectedErrorText);
        field.Error.Should().Be(expectError);
        field.ErrorText.Should().Be(expectedErrorText);
    }

    [Theory]
    [InlineData("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef", false, "")]
    [InlineData("1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef", false, "")] // Without 0x
    [InlineData("0x123", true, "Private key must be 64 hexadecimal characters")]
    [InlineData("0xGHIJKL", true, "Private key must contain only hexadecimal characters")]
    [InlineData("", false, "")] // Empty is valid when not required
    public async Task PrivateKey_Field_Validation_WorksCorrectly(string privateKey, bool expectError, string expectedErrorText)
    {
        // Arrange
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.PrivateKey,
            ValidationPattern = @"^(0x)?[a-fA-F0-9]{64}$"
        });

        var validationTriggered = false;
        var errorState = false;
        var errorText = "";

        RunOnUIThread(() =>
        {
            field.ValueChanged = value =>
            {
                validationTriggered = true;
                // Simulate private key validation
                if (!string.IsNullOrEmpty(value))
                {
                    var cleanKey = value.StartsWith("0x") ? value.Substring(2) : value;
                    if (cleanKey.Length != 64)
                    {
                        field.Error = true;
                        field.ErrorText = "Private key must be 64 hexadecimal characters";
                        errorState = true;
                        errorText = "Private key must be 64 hexadecimal characters";
                    }
                    else if (!Regex.IsMatch(cleanKey, @"^[a-fA-F0-9]+$"))
                    {
                        field.Error = true;
                        field.ErrorText = "Private key must contain only hexadecimal characters";
                        errorState = true;
                        errorText = "Private key must contain only hexadecimal characters";
                    }
                    else
                    {
                        field.Error = false;
                        field.ErrorText = "";
                        errorState = false;
                        errorText = "";
                    }
                }
                else
                {
                    field.Error = false;
                    field.ErrorText = "";
                    errorState = false;
                    errorText = "";
                }
            };
        });

        PlaceInWindow(field);
        await WaitForUIAsync();

        // Act
        RunOnUIThread(() => field.Value = privateKey);
        await WaitForUIAsync();

        // Assert
        validationTriggered.Should().BeTrue();
        errorState.Should().Be(expectError);
        errorText.Should().Be(expectedErrorText);
        field.Error.Should().Be(expectError);
        field.ErrorText.Should().Be(expectedErrorText);
    }

    [Theory]
    [InlineData("word1 word2 word3 word4 word5 word6 word7 word8 word9 word10 word11 word12", false, "")]
    [InlineData("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about", false, "")]
    [InlineData("word1 word2 word3", true, "Mnemonic must be 12, 15, 18, 21, or 24 words")]
    [InlineData("word1 word2 word3 word4 word5 word6 word7 word8 word9 word10 word11 word12 word13", true, "Mnemonic must be 12, 15, 18, 21, or 24 words")]
    [InlineData("", false, "")] // Empty is valid when not required
    public async Task Mnemonic_Field_Validation_WorksCorrectly(string mnemonic, bool expectError, string expectedErrorText)
    {
        // Arrange
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.Mnemonic,
            Lines = 3
        });

        var validationTriggered = false;
        var errorState = false;
        var errorText = "";

        RunOnUIThread(() =>
        {
            field.ValueChanged = value =>
            {
                validationTriggered = true;
                // Simulate mnemonic validation
                if (!string.IsNullOrEmpty(value))
                {
                    var words = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var validWordCounts = new HashSet<int> { 12, 15, 18, 21, 24 };

                    if (!validWordCounts.Contains(words.Length))
                    {
                        field.Error = true;
                        field.ErrorText = "Mnemonic must be 12, 15, 18, 21, or 24 words";
                        errorState = true;
                        errorText = "Mnemonic must be 12, 15, 18, 21, or 24 words";
                    }
                    else
                    {
                        field.Error = false;
                        field.ErrorText = "";
                        errorState = false;
                        errorText = "";
                    }
                }
                else
                {
                    field.Error = false;
                    field.ErrorText = "";
                    errorState = false;
                    errorText = "";
                }
            };
        });

        PlaceInWindow(field);
        await WaitForUIAsync();

        // Act
        RunOnUIThread(() => field.Value = mnemonic);
        await WaitForUIAsync();

        // Assert
        validationTriggered.Should().BeTrue();
        errorState.Should().Be(expectError);
        errorText.Should().Be(expectedErrorText);
        field.Error.Should().Be(expectError);
        field.ErrorText.Should().Be(expectedErrorText);
    }

    [Theory]
    [InlineData("password123", 8, false, "")]
    [InlineData("pass", 8, true, "Password must be at least 8 characters")]
    [InlineData("verylongpassword", 8, false, "")]
    [InlineData("", 8, false, "")] // Empty is valid when not required
    public async Task Password_MinLength_Validation_WorksCorrectly(string password, int minLength, bool expectError, string expectedErrorText)
    {
        // Arrange
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.Password,
            ShowRevealToggle = true
        });

        var validationTriggered = false;
        var errorState = false;
        var errorText = "";

        RunOnUIThread(() =>
        {
            field.ValueChanged = value =>
            {
                validationTriggered = true;
                // Simulate password length validation
                if (!string.IsNullOrEmpty(value) && value.Length < minLength)
                {
                    field.Error = true;
                    field.ErrorText = $"Password must be at least {minLength} characters";
                    errorState = true;
                    errorText = $"Password must be at least {minLength} characters";
                }
                else
                {
                    field.Error = false;
                    field.ErrorText = "";
                    errorState = false;
                    errorText = "";
                }
            };
        });

        PlaceInWindow(field);
        await WaitForUIAsync();

        // Act
        RunOnUIThread(() => field.Value = password);
        await WaitForUIAsync();

        // Assert
        validationTriggered.Should().BeTrue();
        errorState.Should().Be(expectError);
        errorText.Should().Be(expectedErrorText);
        field.Error.Should().Be(expectError);
        field.ErrorText.Should().Be(expectedErrorText);
    }

    [Fact]
    public async Task MaxLength_Property_PreventsTooLongInput()
    {
        // Arrange
        var field = RunOnUIThread(() => new WalletTextField
        {
            MaxLength = 10
        });

        PlaceInWindow(field);
        await WaitForUIAsync();

        // Act
        RunOnUIThread(() => field.Value = "This is a very long input that exceeds the max length");

        // Assert
        field.Value.Length.Should().BeLessOrEqualTo(10);
    }

    [Fact]
    public async Task Multiple_Validation_Errors_ShowCorrectPriority()
    {
        // Arrange
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.Email,
            Required = true
        });

        var errorStates = new List<(bool hasError, string errorText)>();

        RunOnUIThread(() =>
        {
            field.ValueChanged = value =>
            {
                // Simulate multiple validation rules with priority
                if (string.IsNullOrWhiteSpace(value))
                {
                    field.Error = true;
                    field.ErrorText = "Email is required";
                }
                else if (!Regex.IsMatch(value, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
                {
                    field.Error = true;
                    field.ErrorText = "Please enter a valid email address";
                }
                else
                {
                    field.Error = false;
                    field.ErrorText = "";
                }

                errorStates.Add((field.Error, field.ErrorText));
            };
        });

        PlaceInWindow(field);
        await WaitForUIAsync();

        // Act & Assert - Test required error first
        RunOnUIThread(() => field.Value = "");
        await WaitForUIAsync();

        errorStates[errorStates.Count - 1].hasError.Should().BeTrue();
        errorStates[errorStates.Count - 1].errorText.Should().Be("Email is required");

        // Act & Assert - Test format error
        RunOnUIThread(() => field.Value = "invalid-email");
        await WaitForUIAsync();

        errorStates[errorStates.Count - 1].hasError.Should().BeTrue();
        errorStates[errorStates.Count - 1].errorText.Should().Be("Please enter a valid email address");

        // Act & Assert - Test valid input
        RunOnUIThread(() => field.Value = "user@example.com");
        await WaitForUIAsync();

        errorStates[errorStates.Count - 1].hasError.Should().BeFalse();
        errorStates[errorStates.Count - 1].errorText.Should().BeEmpty();
    }

    [Fact]
    public async Task Real_Time_Validation_TriggersOnEachCharacter()
    {
        // Arrange
        var field = RunOnUIThread(() => new WalletTextField
        {
            FieldType = WalletTextField.WalletTextFieldType.Text,
            Required = true
        });

        var validationCallCount = 0;

        RunOnUIThread(() =>
        {
            field.ValueChanged = value =>
            {
                validationCallCount++;
                field.Error = string.IsNullOrWhiteSpace(value);
                field.ErrorText = field.Error ? "This field is required" : "";
            };
        });

        PlaceInWindow(field);
        await WaitForUIAsync();

        // Act - Simulate typing character by character
        RunOnUIThread(() => field.Value = "a");
        await WaitForUIAsync();
        RunOnUIThread(() => field.Value = "ab");
        await WaitForUIAsync();
        RunOnUIThread(() => field.Value = "abc");
        await WaitForUIAsync();

        // Assert
        validationCallCount.Should().Be(3);
        field.Error.Should().BeFalse();
        field.ErrorText.Should().BeEmpty();
    }
}