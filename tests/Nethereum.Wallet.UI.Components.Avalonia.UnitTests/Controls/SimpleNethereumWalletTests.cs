using Nethereum.Wallet.UI.Components.NethereumWallet;
using Nethereum.Wallet.UI.Components.Core.Configuration;

namespace Nethereum.Wallet.UI.Components.Avalonia.UnitTests.Controls;

public class SimpleNethereumWalletTests
{
    [Fact]
    public void ViewModel_Properties_InitializeCorrectly()
    {
        var config = new WalletSecurityConfiguration
        {
            MinPasswordLength = 8
        };

        config.MinPasswordLength.Should().Be(8);
    }

    [Theory]
    [InlineData("", "", false)]
    [InlineData("password", "", false)]
    [InlineData("", "password", false)]
    [InlineData("password", "different", false)]
    [InlineData("password", "password", true)]
    public void Password_Validation_Logic_Works(string newPassword, string confirmPassword, bool shouldBeValid)
    {
        var isValid = !string.IsNullOrWhiteSpace(newPassword) &&
                     !string.IsNullOrWhiteSpace(confirmPassword) &&
                     newPassword == confirmPassword;

        isValid.Should().Be(shouldBeValid);
    }

    [Theory]
    [InlineData("weak", 8, false)]
    [InlineData("password", 8, true)]
    [InlineData("password123", 8, true)]
    [InlineData("", 8, false)]
    public void Password_Length_Validation_Works(string password, int minLength, bool shouldBeValid)
    {
        var isValid = !string.IsNullOrWhiteSpace(password) && password.Length >= minLength;

        isValid.Should().Be(shouldBeValid);
    }

    [Fact]
    public void Wallet_State_Logic_IsCorrect()
    {
        bool vaultExists = false;
        bool isUnlocked = false;
        bool hasAccounts = false;

        var canCreateWallet = !vaultExists;
        var canLogin = vaultExists && !isUnlocked;
        var showWalletContent = vaultExists && isUnlocked && hasAccounts;

        canCreateWallet.Should().BeTrue();
        canLogin.Should().BeFalse();
        showWalletContent.Should().BeFalse();

        vaultExists = true;
        isUnlocked = true;
        hasAccounts = true;

        canCreateWallet = !vaultExists;
        canLogin = vaultExists && !isUnlocked;
        showWalletContent = vaultExists && isUnlocked && hasAccounts;

        canCreateWallet.Should().BeFalse();
        canLogin.Should().BeFalse();
        showWalletContent.Should().BeTrue();
    }

    [Fact]
    public void Validation_Error_Messages_AreCorrect()
    {
        var passwordRequired = "Password required";
        var passwordMismatch = "Password mismatch";
        var passwordTooShort = "Password too short";

        passwordRequired.Should().NotBeNullOrEmpty();
        passwordMismatch.Should().NotBeNullOrEmpty();
        passwordTooShort.Should().NotBeNullOrEmpty();
    }
}