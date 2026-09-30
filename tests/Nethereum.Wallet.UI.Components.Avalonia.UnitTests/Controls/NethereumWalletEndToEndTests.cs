using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Wallet;
using Nethereum.Wallet.UI.Components.Abstractions;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Core.Configuration;
using Nethereum.Wallet.UI.Components.NethereumWallet;
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.UI;
using Nethereum.UI;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Moq;

namespace Nethereum.Wallet.UI.Components.Avalonia.UnitTests.Controls;

public class NethereumWalletEndToEndTests : TestBase
{
    private Mock<IWalletVaultService> _mockVaultService;
    private Mock<IWalletDialogService> _mockDialogService;
    private Mock<IComponentLocalizer<NethereumWalletViewModel>> _mockLocalizer;
    private Mock<NethereumWalletConfiguration> _mockConfig;
    private Mock<NethereumWalletHostProvider> _mockWalletHostProvider;
    private Mock<SelectedEthereumHostProviderService> _mockSelectedHostProvider;

    public NethereumWalletEndToEndTests()
    {
        _mockVaultService = new Mock<IWalletVaultService>();
        _mockDialogService = new Mock<IWalletDialogService>();
        _mockLocalizer = new Mock<IComponentLocalizer<NethereumWalletViewModel>>();
        _mockConfig = new Mock<NethereumWalletConfiguration>();
        _mockWalletHostProvider = new Mock<NethereumWalletHostProvider>();
        _mockSelectedHostProvider = new Mock<SelectedEthereumHostProviderService>();

        _mockLocalizer.Setup(x => x.GetString(It.IsAny<string>())).Returns("Test String");
        _mockLocalizer.Setup(x => x.GetString(It.IsAny<string>(), It.IsAny<object[]>())).Returns("Test String");
    }

    private NethereumWalletViewModel CreateViewModel()
    {
        return new NethereumWalletViewModel(
            _mockVaultService.Object,
            _mockDialogService.Object,
            _mockLocalizer.Object,
            _mockConfig.Object,
            _mockWalletHostProvider.Object,
            _mockSelectedHostProvider.Object);
    }

    [Fact]
    public async Task CreateNewVault_WithValidPassword_ShouldSucceed()
    {
        var viewModel = CreateViewModel();
        _mockVaultService.Setup(x => x.VaultExistsAsync()).ReturnsAsync(false);
        _mockVaultService.Setup(x => x.CreateNewAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _mockVaultService.Setup(x => x.GetAccountsAsync()).ReturnsAsync(new List<IWalletAccount>());

        viewModel.NewPassword = "ValidPassword123!";
        viewModel.ConfirmPassword = "ValidPassword123!";

        await viewModel.CreateWalletAsync();

        viewModel.CreateError.Should().BeEmpty();
        viewModel.VaultExists.Should().BeTrue();
        viewModel.IsWalletUnlocked.Should().BeTrue();
        _mockVaultService.Verify(x => x.CreateNewAsync("ValidPassword123!"), Times.Once);
    }

    [Theory]
    [InlineData("", "Password123!", "Password required")]
    [InlineData("Password123!", "", "Password mismatch")]
    [InlineData("Password123!", "DifferentPassword!", "Password mismatch")]
    [InlineData("weak", "weak", "Password too short")]
    public async Task CreateNewVault_WithInvalidInput_ShouldFail(string newPassword, string confirmPassword, string expectedErrorType)
    {
        var viewModel = CreateViewModel();
        _mockVaultService.Setup(x => x.VaultExistsAsync()).ReturnsAsync(false);
        _mockLocalizer.Setup(x => x.GetString("PasswordRequired")).Returns("Password required");
        _mockLocalizer.Setup(x => x.GetString("PasswordMismatch")).Returns("Password mismatch");
        _mockLocalizer.Setup(x => x.GetString("PasswordTooShort", It.IsAny<object[]>())).Returns("Password too short");

        viewModel.NewPassword = newPassword;
        viewModel.ConfirmPassword = confirmPassword;

        await viewModel.CreateWalletAsync();

        viewModel.CreateError.Should().Be(expectedErrorType);
        viewModel.VaultExists.Should().BeFalse();
        viewModel.IsWalletUnlocked.Should().BeFalse();
        _mockVaultService.Verify(x => x.CreateNewAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task OpenExistingVault_WithCorrectPassword_ShouldSucceed()
    {
        var viewModel = CreateViewModel();
        _mockVaultService.Setup(x => x.VaultExistsAsync()).ReturnsAsync(true);
        _mockVaultService.Setup(x => x.UnlockAsync("CorrectPassword")).ReturnsAsync(true);
        _mockVaultService.Setup(x => x.GetAccountsAsync()).ReturnsAsync(new List<IWalletAccount> { Mock.Of<IWalletAccount>() });
        _mockLocalizer.Setup(x => x.GetString("VaultUnlockedSuccessfully")).Returns("Vault unlocked successfully");

        viewModel.Password = "CorrectPassword";

        await viewModel.LoginAsync();

        viewModel.LoginError.Should().BeEmpty();
        viewModel.IsWalletUnlocked.Should().BeTrue();
        viewModel.HasAccounts.Should().BeTrue();
        _mockVaultService.Verify(x => x.UnlockAsync("CorrectPassword"), Times.Once);
    }

    [Fact]
    public async Task OpenExistingVault_WithIncorrectPassword_ShouldFail()
    {
        var viewModel = CreateViewModel();
        _mockVaultService.Setup(x => x.VaultExistsAsync()).ReturnsAsync(true);
        _mockVaultService.Setup(x => x.UnlockAsync("IncorrectPassword")).ReturnsAsync(false);
        _mockLocalizer.Setup(x => x.GetString("IncorrectPassword")).Returns("Incorrect password");

        viewModel.Password = "IncorrectPassword";

        await viewModel.LoginAsync();

        viewModel.LoginError.Should().Be("Incorrect password");
        viewModel.IsWalletUnlocked.Should().BeFalse();
        _mockVaultService.Verify(x => x.UnlockAsync("IncorrectPassword"), Times.Once);
    }

    [Fact]
    public async Task OpenExistingVault_WithEmptyPassword_ShouldNotAttemptLogin()
    {
        var viewModel = CreateViewModel();
        _mockVaultService.Setup(x => x.VaultExistsAsync()).ReturnsAsync(true);

        viewModel.Password = "";

        viewModel.CanLogin.Should().BeFalse();

        _mockVaultService.Verify(x => x.UnlockAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task VaultInitialization_ExistingVault_ShouldDetectCorrectly()
    {
        _mockVaultService.Setup(x => x.VaultExistsAsync()).ReturnsAsync(true);
        _mockVaultService.Setup(x => x.GetCurrentVault()).Returns(Mock.Of<WalletVault>());

        var viewModel = CreateViewModel();
        await viewModel.InitializeAsync();

        viewModel.VaultExists.Should().BeTrue();
        viewModel.IsWalletUnlocked.Should().BeTrue();
    }

    [Fact]
    public async Task VaultInitialization_NoVault_ShouldDetectCorrectly()
    {
        _mockVaultService.Setup(x => x.VaultExistsAsync()).ReturnsAsync(false);
        _mockVaultService.Setup(x => x.GetCurrentVault()).Returns((WalletVault)null);

        var viewModel = CreateViewModel();
        await viewModel.InitializeAsync();

        viewModel.VaultExists.Should().BeFalse();
        viewModel.IsWalletUnlocked.Should().BeFalse();
    }



    [Fact]
    public async Task PasswordValidation_ShouldEnforceSecurityRules()
    {
        var viewModel = CreateViewModel();

        var testCases = new[]
        {
            new { Password = "weak", Valid = false, Reason = "Too short" },
            new { Password = "password", Valid = false, Reason = "Not strong enough" },
            new { Password = "Password123!", Valid = true, Reason = "Valid strong password" }
        };

        foreach (var testCase in testCases)
        {
            viewModel.NewPassword = testCase.Password;
            viewModel.ConfirmPassword = testCase.Password;

            viewModel.CanCreateWallet.Should().Be(testCase.Valid, $"Password '{testCase.Password}' should be {(testCase.Valid ? "valid" : "invalid")}: {testCase.Reason}");
        }
    }

    [Fact]
    public async Task CreateVault_WithServiceException_ShouldHandleGracefully()
    {
        var viewModel = CreateViewModel();
        var expectedException = new Exception("Service unavailable");
        _mockVaultService.Setup(x => x.CreateNewAsync(It.IsAny<string>())).ThrowsAsync(expectedException);
        _mockLocalizer.Setup(x => x.GetString("CreateFailed", It.IsAny<object[]>())).Returns("Create failed");

        viewModel.NewPassword = "ValidPassword123!";
        viewModel.ConfirmPassword = "ValidPassword123!";

        await viewModel.CreateWalletAsync();

        viewModel.CreateError.Should().Be("Create failed");
        viewModel.VaultExists.Should().BeFalse();
        viewModel.IsWalletUnlocked.Should().BeFalse();
        _mockNotificationService.Verify(x => x.ShowError("Create failed"), Times.Once);
    }

    [Fact]
    public async Task LoginFlow_WithServiceException_ShouldHandleGracefully()
    {
        var viewModel = CreateViewModel();
        var expectedException = new Exception("Network error");
        _mockVaultService.Setup(x => x.UnlockAsync(It.IsAny<string>())).ThrowsAsync(expectedException);
        _mockLocalizer.Setup(x => x.GetString("LoginError", It.IsAny<object[]>())).Returns("Login error");

        viewModel.Password = "SomePassword";

        await viewModel.LoginAsync();

        viewModel.LoginError.Should().Be("Login error");
        viewModel.IsWalletUnlocked.Should().BeFalse();
        _mockNotificationService.Verify(x => x.ShowError("Login error"), Times.Once);
    }

    [Fact]
    public async Task WalletState_Properties_ShouldReflectCorrectState()
    {
        var viewModel = CreateViewModel();
        _mockVaultService.Setup(x => x.VaultExistsAsync()).ReturnsAsync(false);

        await viewModel.InitializeAsync();
        viewModel.VaultExists.Should().BeFalse();
        viewModel.IsWalletUnlocked.Should().BeFalse();
        viewModel.HasAccounts.Should().BeFalse();

        _mockVaultService.Setup(x => x.CreateNewAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _mockVaultService.Setup(x => x.GetAccountsAsync()).ReturnsAsync(new List<IWalletAccount> { Mock.Of<IWalletAccount>() });

        viewModel.NewPassword = "ValidPassword123!";
        viewModel.ConfirmPassword = "ValidPassword123!";
        await viewModel.CreateWalletAsync();

        viewModel.VaultExists.Should().BeTrue();
        viewModel.IsWalletUnlocked.Should().BeTrue();
        viewModel.HasAccounts.Should().BeTrue();
    }

    [Fact]
    public async Task ValidationProperties_ShouldUpdateCorrectly()
    {
        var viewModel = CreateViewModel();

        viewModel.NewPassword = "";
        viewModel.ConfirmPassword = "";
        viewModel.CanCreateWallet.Should().BeFalse();

        viewModel.NewPassword = "ValidPassword123!";
        viewModel.CanCreateWallet.Should().BeFalse();

        viewModel.ConfirmPassword = "ValidPassword123!";
        viewModel.CanCreateWallet.Should().BeTrue();

        viewModel.Password = "";
        viewModel.CanLogin.Should().BeFalse();

        viewModel.Password = "SomePassword";
        viewModel.CanLogin.Should().BeTrue();
    }
}
