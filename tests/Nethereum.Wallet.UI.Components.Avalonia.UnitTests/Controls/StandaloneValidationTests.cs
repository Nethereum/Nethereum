namespace Nethereum.Wallet.UI.Components.Avalonia.UnitTests.Controls;

public class StandaloneValidationTests
{
    [Theory]
    [InlineData("", "", false, "Password required")]
    [InlineData("password", "", false, "Password mismatch")]
    [InlineData("", "password", false, "Password required")]
    [InlineData("password", "different", false, "Password mismatch")]
    [InlineData("weak", "weak", false, "Password too short")]
    [InlineData("ValidPassword123!", "ValidPassword123!", true, "")]
    public void NewVaultValidation_WorksCorrectly(string newPassword, string confirmPassword, bool shouldBeValid, string expectedError)
    {
        string errorMessage = "";
        bool isValid = false;

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
        else
        {
            isValid = true;
        }

        isValid.Should().Be(shouldBeValid, $"Validation should return {shouldBeValid} for passwords '{newPassword}'/'{confirmPassword}'");
        errorMessage.Should().Be(expectedError, $"Error message should be '{expectedError}' for passwords '{newPassword}'/'{confirmPassword}'");
    }

    [Theory]
    [InlineData("", false, "Password required")]
    [InlineData("   ", false, "Password required")]
    [InlineData("ValidPassword", true, "")]
    public void LoginValidation_WorksCorrectly(string password, bool shouldBeValid, string expectedError)
    {
        string errorMessage = "";
        bool isValid = false;

        if (string.IsNullOrWhiteSpace(password))
        {
            errorMessage = "Password required";
        }
        else
        {
            isValid = true;
        }

        isValid.Should().Be(shouldBeValid, $"Login validation should return {shouldBeValid} for password '{password}'");
        errorMessage.Should().Be(expectedError, $"Error message should be '{expectedError}' for password '{password}'");
    }

    [Fact]
    public void VaultCreationFlow_ValidatesCorrectly()
    {

        var newPassword = "";
        var confirmPassword = "";
        var canCreate = CanCreateVault(newPassword, confirmPassword);

        canCreate.Should().BeFalse("Empty passwords should not allow vault creation");

        newPassword = "ValidPassword123!";
        confirmPassword = "";
        canCreate = CanCreateVault(newPassword, confirmPassword);

        canCreate.Should().BeFalse("Mismatched passwords should not allow vault creation");

        newPassword = "weak";
        confirmPassword = "weak";
        canCreate = CanCreateVault(newPassword, confirmPassword);

        canCreate.Should().BeFalse("Weak passwords should not allow vault creation");

        newPassword = "ValidPassword123!";
        confirmPassword = "ValidPassword123!";
        canCreate = CanCreateVault(newPassword, confirmPassword);

        canCreate.Should().BeTrue("Valid matching passwords should allow vault creation");
    }

    [Fact]
    public void VaultLoginFlow_ValidatesCorrectly()
    {

        var vaultExists = false;
        var password = "ValidPassword";
        var canLogin = CanLogin(vaultExists, password);

        canLogin.Should().BeFalse("Should not be able to login when no vault exists");

        vaultExists = true;
        password = "";
        canLogin = CanLogin(vaultExists, password);

        canLogin.Should().BeFalse("Should not be able to login with empty password");

        vaultExists = true;
        password = "ValidPassword";
        canLogin = CanLogin(vaultExists, password);

        canLogin.Should().BeTrue("Should be able to login when vault exists and password provided");
    }

    [Fact]
    public void WalletStateLogic_WorksCorrectly()
    {

        bool vaultExists = false;
        bool isUnlocked = false;
        bool hasAccounts = false;

        var state = GetWalletState(vaultExists, isUnlocked, hasAccounts);

        state.CanCreateWallet.Should().BeTrue("Should be able to create wallet initially");
        state.CanLogin.Should().BeFalse("Should not be able to login initially");
        state.ShowWalletContent.Should().BeFalse("Should not show content initially");

        vaultExists = true;
        isUnlocked = true;
        hasAccounts = false;

        state = GetWalletState(vaultExists, isUnlocked, hasAccounts);

        state.CanCreateWallet.Should().BeFalse("Should not create wallet when one exists");
        state.CanLogin.Should().BeFalse("Should not need login when unlocked");
        state.ShowWalletContent.Should().BeFalse("Should not show content without accounts");

        hasAccounts = true;

        state = GetWalletState(vaultExists, isUnlocked, hasAccounts);

        state.ShowWalletContent.Should().BeTrue("Should show content when ready");
    }


    private bool CanCreateVault(string newPassword, string confirmPassword)
    {
        return !string.IsNullOrWhiteSpace(newPassword) &&
               !string.IsNullOrWhiteSpace(confirmPassword) &&
               newPassword == confirmPassword &&
               newPassword.Length >= 8;
    }

    private bool CanLogin(bool vaultExists, string password)
    {
        return vaultExists && !string.IsNullOrWhiteSpace(password);
    }

    private WalletState GetWalletState(bool vaultExists, bool isUnlocked, bool hasAccounts)
    {
        return new WalletState
        {
            CanCreateWallet = !vaultExists,
            CanLogin = vaultExists && !isUnlocked,
            ShowWalletContent = vaultExists && isUnlocked && hasAccounts
        };
    }

    public class WalletState
    {
        public bool CanCreateWallet { get; set; }
        public bool CanLogin { get; set; }
        public bool ShowWalletContent { get; set; }
    }
}