using Xunit;
using FluentAssertions;
using Nethereum.Wallet.UI.Components.NethereumWallet;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI.Components.Avalonia.UnitTests.Controls;

public class FullServiceVaultCreationTests : TestBase
{
    [Fact]
    public void ViewModel_CanBeCreated_WithFullServiceRegistration()
    {
        var viewModel = ServiceProvider.GetRequiredService<NethereumWalletViewModel>();

        viewModel.Should().NotBeNull("ViewModel should be created successfully with full service registration");
    }

    [Fact]
    public void CanCreateWallet_InitialState_ShouldBeFalse()
    {
        var viewModel = ServiceProvider.GetRequiredService<NethereumWalletViewModel>();

        viewModel.CanCreateWallet.Should().BeFalse("CanCreateWallet should be false initially when no passwords are set");
    }

    [Fact]
    public void CanCreateWallet_WithValidPasswords_ShouldBeTrue()
    {
        var viewModel = ServiceProvider.GetRequiredService<NethereumWalletViewModel>();

        viewModel.NewPassword = "ValidPassword123!";
        viewModel.ConfirmPassword = "ValidPassword123!";

        viewModel.CanCreateWallet.Should().BeTrue("CanCreateWallet should be true with valid matching passwords");
    }

    [Fact]
    public void CanCreateWallet_WithMismatchedPasswords_ShouldBeFalse()
    {
        var viewModel = ServiceProvider.GetRequiredService<NethereumWalletViewModel>();

        viewModel.NewPassword = "ValidPassword123!";
        viewModel.ConfirmPassword = "DifferentPassword123!";

        viewModel.CanCreateWallet.Should().BeFalse("CanCreateWallet should be false with mismatched passwords");
    }

    [Fact]
    public void CanCreateWallet_WithShortPassword_ShouldBeFalse()
    {
        var viewModel = ServiceProvider.GetRequiredService<NethereumWalletViewModel>();

        viewModel.NewPassword = "short";
        viewModel.ConfirmPassword = "short";

        viewModel.CanCreateWallet.Should().BeFalse("CanCreateWallet should be false with short passwords");
    }

    [Fact]
    public void CanCreateWallet_WithEmptyPasswords_ShouldBeFalse()
    {
        var viewModel = ServiceProvider.GetRequiredService<NethereumWalletViewModel>();

        viewModel.NewPassword = "";
        viewModel.ConfirmPassword = "";

        viewModel.CanCreateWallet.Should().BeFalse("CanCreateWallet should be false with empty passwords");
    }

    [Fact]
    public async Task CreateWalletAsync_WithValidPasswords_ShouldSucceed()
    {
        var viewModel = ServiceProvider.GetRequiredService<NethereumWalletViewModel>();

        await viewModel.InitializeAsync();
        viewModel.VaultExists.Should().BeFalse("Should start with no vault for this test");

        viewModel.NewPassword = "ValidPassword123!";
        viewModel.ConfirmPassword = "ValidPassword123!";

        viewModel.CanCreateWallet.Should().BeTrue("Should be able to create wallet with valid passwords");

        await viewModel.CreateWalletAsync();

        viewModel.VaultExists.Should().BeTrue("Vault should exist after successful creation");
        viewModel.IsWalletUnlocked.Should().BeTrue("Wallet should be unlocked after creation");
        viewModel.CreateError.Should().BeNullOrEmpty("No error should be present after successful creation");
    }

    [Fact]
    public async Task CreateWalletAsync_WithInvalidPasswords_ShouldFail()
    {
        var viewModel = ServiceProvider.GetRequiredService<NethereumWalletViewModel>();

        await viewModel.InitializeAsync();

        viewModel.NewPassword = "ValidPassword123!";
        viewModel.ConfirmPassword = "DifferentPassword123!";

        viewModel.CanCreateWallet.Should().BeFalse("Should not be able to create wallet with mismatched passwords");

        await viewModel.CreateWalletAsync();

        viewModel.VaultExists.Should().BeFalse("Vault should not exist after failed creation attempt");
        viewModel.IsWalletUnlocked.Should().BeFalse("Wallet should not be unlocked after failed creation");
    }

    [Theory]
    [InlineData("", "", false, "Empty passwords should not allow creation")]
    [InlineData("ValidPassword123!", "", false, "Missing confirm password should not allow creation")]
    [InlineData("", "ValidPassword123!", false, "Missing new password should not allow creation")]
    [InlineData("ValidPassword123!", "DifferentPassword123!", false, "Mismatched passwords should not allow creation")]
    [InlineData("short", "short", false, "Short passwords should not allow creation")]
    [InlineData("ValidPassword123!", "ValidPassword123!", true, "Valid matching passwords should allow creation")]
    public void CanCreateWallet_VariousPasswordCombinations_ValidatesCorrectly(string newPassword, string confirmPassword, bool expected, string reason)
    {
        var viewModel = ServiceProvider.GetRequiredService<NethereumWalletViewModel>();

        viewModel.NewPassword = newPassword;
        viewModel.ConfirmPassword = confirmPassword;

        viewModel.CanCreateWallet.Should().Be(expected, reason);
    }

    [Fact]
    public async Task VaultCreationFlow_CompleteWorkflow_ShouldWork()
    {
        var viewModel = ServiceProvider.GetRequiredService<NethereumWalletViewModel>();

        await viewModel.InitializeAsync();
        viewModel.VaultExists.Should().BeFalse("Step 1: No vault should exist initially");
        viewModel.CanCreateWallet.Should().BeFalse("Step 1: Cannot create wallet without passwords");

        viewModel.NewPassword = "ValidPassword123!";
        viewModel.CanCreateWallet.Should().BeFalse("Step 2: Cannot create wallet with only one password");

        viewModel.ConfirmPassword = "ValidPassword123!";
        viewModel.CanCreateWallet.Should().BeTrue("Step 3: Can create wallet with both matching passwords");

        await viewModel.CreateWalletAsync();
        viewModel.VaultExists.Should().BeTrue("Step 4: Vault should exist after creation");
        viewModel.IsWalletUnlocked.Should().BeTrue("Step 4: Wallet should be unlocked after creation");
        viewModel.CreateError.Should().BeNullOrEmpty("Step 4: No errors should be present");

        viewModel.CanCreateWallet.Should().BeFalse("Step 5: Cannot create another wallet when one exists");
    }
}