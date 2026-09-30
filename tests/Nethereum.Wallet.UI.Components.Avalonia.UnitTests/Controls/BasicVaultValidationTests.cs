using Xunit;
using FluentAssertions;

namespace Nethereum.Wallet.UI.Components.Avalonia.UnitTests.Controls;

public class BasicVaultValidationTests
{
    [Fact]
    public void VaultCreation_ValidPasswords_ShouldAllowCreation()
    {
        var newPassword = "ValidPassword123!";
        var confirmPassword = "ValidPassword123!";

        var result = ValidateVaultCreation(newPassword, confirmPassword);

        result.IsValid.Should().BeTrue("Valid matching passwords should allow vault creation");
        result.ErrorMessage.Should().BeEmpty("No error message should be present for valid passwords");
    }

    [Fact]
    public void VaultCreation_EmptyNewPassword_ShouldRejectCreation()
    {
        var newPassword = "";
        var confirmPassword = "ValidPassword123!";

        var result = ValidateVaultCreation(newPassword, confirmPassword);

        result.IsValid.Should().BeFalse("Empty new password should reject vault creation");
        result.ErrorMessage.Should().Be("Password required");
    }

    [Fact]
    public void VaultCreation_EmptyConfirmPassword_ShouldRejectCreation()
    {
        var newPassword = "ValidPassword123!";
        var confirmPassword = "";

        var result = ValidateVaultCreation(newPassword, confirmPassword);

        result.IsValid.Should().BeFalse("Empty confirm password should reject vault creation");
        result.ErrorMessage.Should().Be("Password mismatch");
    }

    [Fact]
    public void VaultCreation_MismatchedPasswords_ShouldRejectCreation()
    {
        var newPassword = "ValidPassword123!";
        var confirmPassword = "DifferentPassword123!";

        var result = ValidateVaultCreation(newPassword, confirmPassword);

        result.IsValid.Should().BeFalse("Mismatched passwords should reject vault creation");
        result.ErrorMessage.Should().Be("Password mismatch");
    }

    [Fact]
    public void VaultCreation_ShortPassword_ShouldRejectCreation()
    {
        var newPassword = "short";
        var confirmPassword = "short";

        var result = ValidateVaultCreation(newPassword, confirmPassword);

        result.IsValid.Should().BeFalse("Short passwords should reject vault creation");
        result.ErrorMessage.Should().Be("Password too short");
    }

    [Theory]
    [InlineData("", "", false, "Password required")]
    [InlineData("ValidPassword123!", "", false, "Password mismatch")]
    [InlineData("", "ValidPassword123!", false, "Password required")]
    [InlineData("password", "different", false, "Password mismatch")]
    [InlineData("weak", "weak", false, "Password too short")]
    [InlineData("ValidPassword123!", "ValidPassword123!", true, "")]
    public void VaultCreation_VariousInputs_ValidatesCorrectly(string newPassword, string confirmPassword, bool expectedValid, string expectedError)
    {
        var result = ValidateVaultCreation(newPassword, confirmPassword);

        result.IsValid.Should().Be(expectedValid, $"Validation should return {expectedValid} for passwords '{newPassword}'/'{confirmPassword}'");
        result.ErrorMessage.Should().Be(expectedError, $"Error message should be '{expectedError}' for passwords '{newPassword}'/'{confirmPassword}'");
    }

    [Fact]
    public void VaultCreation_StepByStepFlow_ValidatesCorrectly()
    {

        var step1 = ValidateVaultCreation("V", "");
        step1.IsValid.Should().BeFalse("Step 1: Partial password should not allow creation");

        var step2 = ValidateVaultCreation("ValidPassword123!", "");
        step2.IsValid.Should().BeFalse("Step 2: Missing confirm password should not allow creation");

        var step3 = ValidateVaultCreation("ValidPassword123!", "Valid");
        step3.IsValid.Should().BeFalse("Step 3: Partial confirm password should not allow creation");

        var step4 = ValidateVaultCreation("ValidPassword123!", "ValidPassword123!");
        step4.IsValid.Should().BeTrue("Step 4: Complete matching passwords should allow creation");
    }

    [Fact]
    public void CanCreateVault_BasicLogic_WorksCorrectly()
    {
        var testCases = new[]
        {
            new { New = "", Confirm = "", Expected = false },
            new { New = "ValidPassword123!", Confirm = "", Expected = false },
            new { New = "", Confirm = "ValidPassword123!", Expected = false },
            new { New = "ValidPassword123!", Confirm = "DifferentPassword123!", Expected = false },
            new { New = "weak", Confirm = "weak", Expected = false },
            new { New = "ValidPassword123!", Confirm = "ValidPassword123!", Expected = true }
        };

        foreach (var testCase in testCases)
        {
            var canCreate = CanCreateVault(testCase.New, testCase.Confirm);
            canCreate.Should().Be(testCase.Expected,
                $"CanCreateVault should return {testCase.Expected} for '{testCase.New}'/'{testCase.Confirm}'");
        }
    }

    private ValidationResult ValidateVaultCreation(string newPassword, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            return new ValidationResult(false, "Password required");
        }

        if (string.IsNullOrWhiteSpace(confirmPassword))
        {
            return new ValidationResult(false, "Password mismatch");
        }

        if (newPassword != confirmPassword)
        {
            return new ValidationResult(false, "Password mismatch");
        }

        if (newPassword.Length < 8)
        {
            return new ValidationResult(false, "Password too short");
        }

        return new ValidationResult(true, "");
    }

    private bool CanCreateVault(string newPassword, string confirmPassword)
    {
        return !string.IsNullOrWhiteSpace(newPassword) &&
               !string.IsNullOrWhiteSpace(confirmPassword) &&
               newPassword == confirmPassword &&
               newPassword.Length >= 8;
    }

    private record ValidationResult(bool IsValid, string ErrorMessage);
}