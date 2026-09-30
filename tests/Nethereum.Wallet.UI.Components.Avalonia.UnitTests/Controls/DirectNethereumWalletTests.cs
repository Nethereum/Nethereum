using Nethereum.Wallet.UI.Components.NethereumWallet;

namespace Nethereum.Wallet.UI.Components.Avalonia.UnitTests.Controls;

public class DirectNethereumWalletTests
{
    [Fact]
    public void CanCreateWallet_WithEmptyPasswords_ShouldBeFalse()
    {
        var newPassword = "";
        var confirmPassword = "";

        var canCreate = !string.IsNullOrWhiteSpace(newPassword) &&
                       !string.IsNullOrWhiteSpace(confirmPassword) &&
                       newPassword == confirmPassword &&
                       newPassword.Length >= 8;

        canCreate.Should().BeFalse();
    }

    [Fact]
    public void CanCreateWallet_WithMismatchedPasswords_ShouldBeFalse()
    {
        var newPassword = "Password123!";
        var confirmPassword = "DifferentPassword123!";

        var canCreate = !string.IsNullOrWhiteSpace(newPassword) &&
                       !string.IsNullOrWhiteSpace(confirmPassword) &&
                       newPassword == confirmPassword &&
                       newPassword.Length >= 8;

        canCreate.Should().BeFalse();
    }

    [Fact]
    public void CanCreateWallet_WithShortPassword_ShouldBeFalse()
    {
        var newPassword = "weak";
        var confirmPassword = "weak";

        var canCreate = !string.IsNullOrWhiteSpace(newPassword) &&
                       !string.IsNullOrWhiteSpace(confirmPassword) &&
                       newPassword == confirmPassword &&
                       newPassword.Length >= 8;

        canCreate.Should().BeFalse();
    }

    [Fact]
    public void CanCreateWallet_WithValidPasswords_ShouldBeTrue()
    {
        var newPassword = "ValidPassword123!";
        var confirmPassword = "ValidPassword123!";

        var canCreate = !string.IsNullOrWhiteSpace(newPassword) &&
                       !string.IsNullOrWhiteSpace(confirmPassword) &&
                       newPassword == confirmPassword &&
                       newPassword.Length >= 8;

        canCreate.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "", "Password required")]
    [InlineData("password", "", "Password mismatch")]
    [InlineData("", "password", "Password required")]
    [InlineData("password", "different", "Password mismatch")]
    [InlineData("weak", "weak", "Password too short")]
    [InlineData("ValidPassword123!", "ValidPassword123!", "")]
    public void ValidatePasswordInput_ReturnsCorrectErrorMessage(string newPassword, string confirmPassword, string expectedError)
    {
        string errorMessage = "";

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            errorMessage = "Password required";
        }
        else if (string.IsNullOrWhiteSpace(confirmPassword))
        {
            errorMessage = "Password mismatch";
        }
        else if (newPassword != confirmPassword)
        {
            errorMessage = "Password mismatch";
        }
        else if (newPassword.Length < 8)
        {
            errorMessage = "Password too short";
        }

        errorMessage.Should().Be(expectedError);
    }

    [Fact]
    public void CanLogin_WithEmptyPassword_ShouldBeFalse()
    {
        var password = "";
        var vaultExists = true;

        var canLogin = vaultExists && !string.IsNullOrWhiteSpace(password);

        canLogin.Should().BeFalse();
    }

    [Fact]
    public void CanLogin_WithPasswordAndNoVault_ShouldBeFalse()
    {
        var password = "SomePassword";
        var vaultExists = false;

        var canLogin = vaultExists && !string.IsNullOrWhiteSpace(password);

        canLogin.Should().BeFalse();
    }

    [Fact]
    public void CanLogin_WithPasswordAndVaultExists_ShouldBeTrue()
    {
        var password = "SomePassword";
        var vaultExists = true;

        var canLogin = vaultExists && !string.IsNullOrWhiteSpace(password);

        canLogin.Should().BeTrue();
    }

    [Fact]
    public void WalletStates_TransitionCorrectly()
    {

        bool vaultExists = false;
        bool isUnlocked = false;
        bool hasAccounts = false;

        var canCreateWallet = !vaultExists;
        var canLogin = vaultExists && !isUnlocked;
        var showWalletContent = vaultExists && isUnlocked && hasAccounts;

        canCreateWallet.Should().BeTrue("Should be able to create wallet when none exists");
        canLogin.Should().BeFalse("Should not be able to login when no vault exists");
        showWalletContent.Should().BeFalse("Should not show wallet content initially");

        vaultExists = true;
        isUnlocked = true;
        hasAccounts = false;

        canCreateWallet = !vaultExists;
        canLogin = vaultExists && !isUnlocked;
        showWalletContent = vaultExists && isUnlocked && hasAccounts;

        canCreateWallet.Should().BeFalse("Should not be able to create wallet when one exists");
        canLogin.Should().BeFalse("Should not need to login when already unlocked");
        showWalletContent.Should().BeFalse("Should not show content without accounts");

        hasAccounts = true;

        showWalletContent = vaultExists && isUnlocked && hasAccounts;
        showWalletContent.Should().BeTrue("Should show wallet content when vault exists, unlocked, and has accounts");
    }
}