using Xunit;
using FluentAssertions;
using Nethereum.Wallet.UI.Components.NethereumWallet;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI.Components.Avalonia.UnitTests.Controls;

/// <summary>
/// Tests for vault creation using full service registration like the Avalonia demo
/// </summary>
public class FullServiceVaultCreationTests : TestBase
{
    [Fact]
    public void ViewModel_CanBeCreated_WithFullServiceRegistration()
    {
        // This test proves that with proper service registration, we can create the ViewModel
        var viewModel = ServiceProvider.GetRequiredService<NethereumWalletViewModel>();

        viewModel.Should().NotBeNull("ViewModel should be created successfully with full service registration");
    }

    [Fact]
    public void CanCreateWallet_InitialState_ShouldBeFalse()
    {
        var viewModel = ServiceProvider.GetRequiredService<NethereumWalletViewModel>();

        // Initially, no passwords are set, so CanCreateWallet should be false
        viewModel.CanCreateWallet.Should().BeFalse("CanCreateWallet should be false initially when no passwords are set");
    }

    [Fact]
    public void CanCreateWallet_WithValidPasswords_ShouldBeTrue()
    {
        var viewModel = ServiceProvider.GetRequiredService<NethereumWalletViewModel>();

        // Set valid matching passwords
        viewModel.NewPassword = "ValidPassword123!";
        viewModel.ConfirmPassword = "ValidPassword123!";

        // Now CanCreateWallet should be true
        viewModel.CanCreateWallet.Should().BeTrue("CanCreateWallet should be true with valid matching passwords");
    }

    [Fact]
    public void CanCreateWallet_WithMismatchedPasswords_ShouldBeFalse()
    {
        var viewModel = ServiceProvider.GetRequiredService<NethereumWalletViewModel>();

        // Set mismatched passwords
        viewModel.NewPassword = "ValidPassword123!";
        viewModel.ConfirmPassword = "DifferentPassword123!";

        // CanCreateWallet should be false
        viewModel.CanCreateWallet.Should().BeFalse("CanCreateWallet should be false with mismatched passwords");
    }

    [Fact]
    public void CanCreateWallet_WithShortPassword_ShouldBeFalse()
    {
        var viewModel = ServiceProvider.GetRequiredService<NethereumWalletViewModel>();

        // Set short matching passwords
        viewModel.NewPassword = "short";
        viewModel.ConfirmPassword = "short";

        // CanCreateWallet should be false
        viewModel.CanCreateWallet.Should().BeFalse("CanCreateWallet should be false with short passwords");
    }

    [Fact]
    public void CanCreateWallet_WithEmptyPasswords_ShouldBeFalse()
    {
        var viewModel = ServiceProvider.GetRequiredService<NethereumWalletViewModel>();

        // Set empty passwords
        viewModel.NewPassword = "";
        viewModel.ConfirmPassword = "";

        // CanCreateWallet should be false
        viewModel.CanCreateWallet.Should().BeFalse("CanCreateWallet should be false with empty passwords");
    }

    [Fact]
    public async Task CreateWalletAsync_WithValidPasswords_ShouldSucceed()
    {
        var viewModel = ServiceProvider.GetRequiredService<NethereumWalletViewModel>();

        // Ensure we start with no vault
        await viewModel.InitializeAsync();
        viewModel.VaultExists.Should().BeFalse("Should start with no vault for this test");

        // Set valid passwords
        viewModel.NewPassword = "ValidPassword123!";
        viewModel.ConfirmPassword = "ValidPassword123!";

        // Verify we can create wallet
        viewModel.CanCreateWallet.Should().BeTrue("Should be able to create wallet with valid passwords");

        // Attempt to create wallet
        await viewModel.CreateWalletAsync();

        // Verify vault was created successfully
        viewModel.VaultExists.Should().BeTrue("Vault should exist after successful creation");
        viewModel.IsWalletUnlocked.Should().BeTrue("Wallet should be unlocked after creation");
        viewModel.CreateError.Should().BeNullOrEmpty("No error should be present after successful creation");
    }

    [Fact]
    public async Task CreateWalletAsync_WithInvalidPasswords_ShouldFail()
    {
        var viewModel = ServiceProvider.GetRequiredService<NethereumWalletViewModel>();

        // Ensure we start with no vault
        await viewModel.InitializeAsync();

        // Set invalid passwords (mismatched)
        viewModel.NewPassword = "ValidPassword123!";
        viewModel.ConfirmPassword = "DifferentPassword123!";

        // Verify we cannot create wallet
        viewModel.CanCreateWallet.Should().BeFalse("Should not be able to create wallet with mismatched passwords");

        // Attempt to create wallet should not proceed
        await viewModel.CreateWalletAsync();

        // Verify vault was not created
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

        // Set the password combination
        viewModel.NewPassword = newPassword;
        viewModel.ConfirmPassword = confirmPassword;

        // Verify the result
        viewModel.CanCreateWallet.Should().Be(expected, reason);
    }

    [Fact]
    public async Task VaultCreationFlow_CompleteWorkflow_ShouldWork()
    {
        var viewModel = ServiceProvider.GetRequiredService<NethereumWalletViewModel>();

        // Step 1: Initialize and verify no vault exists
        await viewModel.InitializeAsync();
        viewModel.VaultExists.Should().BeFalse("Step 1: No vault should exist initially");
        viewModel.CanCreateWallet.Should().BeFalse("Step 1: Cannot create wallet without passwords");

        // Step 2: Enter first password
        viewModel.NewPassword = "ValidPassword123!";
        viewModel.CanCreateWallet.Should().BeFalse("Step 2: Cannot create wallet with only one password");

        // Step 3: Enter matching confirm password
        viewModel.ConfirmPassword = "ValidPassword123!";
        viewModel.CanCreateWallet.Should().BeTrue("Step 3: Can create wallet with both matching passwords");

        // Step 4: Create the wallet
        await viewModel.CreateWalletAsync();
        viewModel.VaultExists.Should().BeTrue("Step 4: Vault should exist after creation");
        viewModel.IsWalletUnlocked.Should().BeTrue("Step 4: Wallet should be unlocked after creation");
        viewModel.CreateError.Should().BeNullOrEmpty("Step 4: No errors should be present");

        // Step 5: Verify wallet is functional
        viewModel.CanCreateWallet.Should().BeFalse("Step 5: Cannot create another wallet when one exists");
    }
}