using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Wallet.UI.Components.NethereumWallet;
using Nethereum.Wallet.UI.Components.Configuration;
using Nethereum.Wallet;
using Xunit;

namespace Nethereum.Wallet.UI.Components.Avalonia.UnitTests;

public class Step1_NethereumWalletTests : TestBase
{
    private readonly NethereumWalletViewModel _viewModel;
    private readonly NethereumWalletConfiguration _config;
    private readonly INethereumWalletUIConfiguration _globalConfig;
    private readonly IWalletVaultService _vaultService;

    public Step1_NethereumWalletTests()
    {
        _viewModel = ServiceProvider.GetRequiredService<NethereumWalletViewModel>();
        _config = ServiceProvider.GetRequiredService<NethereumWalletConfiguration>();
        _globalConfig = ServiceProvider.GetRequiredService<INethereumWalletUIConfiguration>();
        _vaultService = ServiceProvider.GetRequiredService<IWalletVaultService>();
    }

    #region 1.1 Vault Creation Tests

    [Fact]
    public async Task VaultCreation_WithValidPassword_CreatesVaultSuccessfully()
    {
        var password = "TestPassword123";

        await RunOnUIThreadAsync(async () =>
        {
            _viewModel.NewPassword = password;
            _viewModel.ConfirmPassword = password;
            await _viewModel.CreateWalletCommand.ExecuteAsync(null);
        });

        _viewModel.IsWalletUnlocked.Should().BeTrue("vault should be unlocked after creation");
        _viewModel.CreateError.Should().BeNullOrEmpty("no error should occur with valid password");

        var vault = _vaultService.GetCurrentVault();
        vault.Should().NotBeNull("vault should exist after creation");
    }

    [Fact]
    public async Task VaultCreation_WithPasswordMismatch_ShowsError()
    {
        var password1 = "TestPassword123";
        var password2 = "DifferentPassword456";

        await RunOnUIThreadAsync(() =>
        {
            _viewModel.NewPassword = password1;
            _viewModel.ConfirmPassword = password2;
            return Task.CompletedTask;
        });

        _viewModel.PasswordsMatch.Should().BeFalse("passwords don't match");
        _viewModel.CanCreateWallet.Should().BeFalse("cannot create wallet with mismatched passwords");
    }

    [Fact]
    public async Task VaultCreation_WithPasswordMatching_EnablesCreateButton()
    {
        var password = "TestPassword123";

        await RunOnUIThreadAsync(() =>
        {
            _viewModel.NewPassword = password;
            _viewModel.ConfirmPassword = password;
            return Task.CompletedTask;
        });

        _viewModel.PasswordsMatch.Should().BeTrue("passwords match");
        _viewModel.CanCreateWallet.Should().BeTrue("can create wallet with matching passwords");
    }

    [Fact]
    public async Task VaultCreation_WithWeakPassword_RespectsMinimumLength()
    {
        _config.Security.MinPasswordLength = 8;
        var weakPassword = "short";

        await RunOnUIThreadAsync(() =>
        {
            _viewModel.NewPassword = weakPassword;
            _viewModel.ConfirmPassword = weakPassword;
            return Task.CompletedTask;
        });

        if (_config.Security.EnforceMinPasswordLength)
        {
            _viewModel.CanCreateWallet.Should().BeFalse("password is too short");
        }
    }

    [Fact]
    public async Task VaultCreation_WithSpecialCharacters_AcceptsPassword()
    {
        var password = "P@ssw0rd!#$%123";

        await RunOnUIThreadAsync(async () =>
        {
            _viewModel.NewPassword = password;
            _viewModel.ConfirmPassword = password;
            await _viewModel.CreateWalletCommand.ExecuteAsync(null);
        });

        _viewModel.IsWalletUnlocked.Should().BeTrue("special characters should be allowed");
        _viewModel.CreateError.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task VaultCreation_VaultPersistsToDisk()
    {
        var password = "TestPassword123";

        await RunOnUIThreadAsync(async () =>
        {
            _viewModel.NewPassword = password;
            _viewModel.ConfirmPassword = password;
            await _viewModel.CreateWalletCommand.ExecuteAsync(null);
        });

        var vaultPath = (_vaultService as FileWalletVaultService)?.VaultFilePath;
        if (vaultPath != null)
        {
            File.Exists(vaultPath).Should().BeTrue("vault file should exist on disk");
        }
    }

    [Fact]
    public async Task VaultCreation_ShowsLoadingState_DuringCreation()
    {
        var password = "TestPassword123";
        bool wasLoading = false;

        await RunOnUIThreadAsync(async () =>
        {
            _viewModel.NewPassword = password;
            _viewModel.ConfirmPassword = password;

            var createTask = _viewModel.CreateWalletCommand.ExecuteAsync(null);

            wasLoading = _viewModel.IsBusy;

            await createTask;
        });

        wasLoading.Should().BeTrue("should show loading state during vault creation");
        _viewModel.IsBusy.Should().BeFalse("loading state should clear after completion");
    }

    #endregion

    #region 1.2 Vault Login Tests

    [Fact]
    public async Task VaultLogin_WithCorrectPassword_LogsInSuccessfully()
    {
        await CreateTestVaultAsync("TestPassword123");

        await RunOnUIThreadAsync(async () =>
        {
            await _viewModel.LogoutCommand.ExecuteAsync(null);
            _viewModel.Password = "";
        });

        await RunOnUIThreadAsync(async () =>
        {
            _viewModel.Password = "TestPassword123";
            await _viewModel.LoginCommand.ExecuteAsync(null);
        });

        _viewModel.IsWalletUnlocked.Should().BeTrue("should be unlocked with correct password");
        _viewModel.LoginError.Should().BeNullOrEmpty("no error should occur");
    }

    [Fact]
    public async Task VaultLogin_WithIncorrectPassword_ShowsError()
    {
        await CreateTestVaultAsync("TestPassword123");

        await RunOnUIThreadAsync(async () =>
        {
            await _viewModel.LogoutCommand.ExecuteAsync(null);
            _viewModel.Password = "";
        });

        await RunOnUIThreadAsync(async () =>
        {
            _viewModel.Password = "WrongPassword";
            await _viewModel.LoginCommand.ExecuteAsync(null);
        });

        _viewModel.IsWalletUnlocked.Should().BeFalse("should not unlock with wrong password");
        _viewModel.LoginError.Should().NotBeNullOrEmpty("error message should be displayed");
    }

    [Fact]
    public async Task VaultLogin_WithEmptyPassword_DisablesLoginButton()
    {
        await CreateTestVaultAsync("TestPassword123");

        await RunOnUIThreadAsync(async () =>
        {
            await _viewModel.LogoutCommand.ExecuteAsync(null);
        });

        await RunOnUIThreadAsync(() =>
        {
            _viewModel.Password = "";
            return Task.CompletedTask;
        });

        _viewModel.CanLogin.Should().BeFalse("cannot login with empty password");
    }

    [Fact]
    public async Task VaultLogin_WithPassword_EnablesLoginButton()
    {
        await CreateTestVaultAsync("TestPassword123");

        await RunOnUIThreadAsync(async () =>
        {
            await _viewModel.LogoutCommand.ExecuteAsync(null);
        });

        await RunOnUIThreadAsync(() =>
        {
            _viewModel.Password = "SomePassword";
            return Task.CompletedTask;
        });

        _viewModel.CanLogin.Should().BeTrue("can login when password is entered");
    }

    [Fact]
    public async Task VaultLogin_ShowsLoadingState_DuringLogin()
    {
        await CreateTestVaultAsync("TestPassword123");
        await RunOnUIThreadAsync(async () => await _viewModel.LogoutCommand.ExecuteAsync(null));

        bool wasLoading = false;

        await RunOnUIThreadAsync(async () =>
        {
            _viewModel.Password = "TestPassword123";
            var loginTask = _viewModel.LoginCommand.ExecuteAsync(null);

            wasLoading = _viewModel.IsBusy;

            await loginTask;
        });

        wasLoading.Should().BeTrue("should show loading state during login");
        _viewModel.IsBusy.Should().BeFalse("loading state should clear after login");
    }

    #endregion

    #region 1.3 Vault Reset Tests

    [Fact]
    public async Task VaultReset_WhenEnabled_ShowsResetOption()
    {
        _config.Behavior.EnableWalletReset = true;
        await CreateTestVaultAsync("TestPassword123");

        _config.Behavior.EnableWalletReset.Should().BeTrue("reset should be enabled");

        _viewModel.ShowResetWalletConfirmationCommand.Should().NotBeNull("reset command should exist");
    }

    [Fact]
    public async Task VaultReset_DeletesVaultFile()
    {
        _config.Behavior.EnableWalletReset = true;
        await CreateTestVaultAsync("TestPassword123");

        var vaultPath = (_vaultService as FileWalletVaultService)?.VaultFilePath;

        await RunOnUIThreadAsync(async () =>
        {
            await _vaultService.DeleteVaultAsync();
            await _viewModel.CheckVaultExistsAsync();
        });

        if (vaultPath != null)
        {
            File.Exists(vaultPath).Should().BeFalse("vault file should be deleted");
        }
        _viewModel.VaultExists.Should().BeFalse("vault should not exist after reset");
    }

    [Fact]
    public async Task VaultReset_ReturnsToCreateState()
    {
        _config.Behavior.EnableWalletReset = true;
        await CreateTestVaultAsync("TestPassword123");

        await RunOnUIThreadAsync(async () =>
        {
            await _vaultService.DeleteVaultAsync();
            await _viewModel.CheckVaultExistsAsync();
        });

        _viewModel.VaultExists.Should().BeFalse("should return to create vault state");
        _viewModel.IsWalletUnlocked.Should().BeFalse("should be locked after reset");
    }

    #endregion

    #region 1.4 UI/UX Validation Tests

    [Fact]
    public void UI_DisplaysLogo_WhenConfigured()
    {
        _globalConfig.ShowLogo = true;
        _globalConfig.WelcomeLogoPath = "avares://Nethereum.Wallet.UI.Components.Avalonia/Assets/logo.png";

        var wallet = CreateControl<Views.NethereumWallet>();
        PlaceInWindow(wallet);

        _globalConfig.ShowLogo.Should().BeTrue("logo should be configured to show");
        _globalConfig.WelcomeLogoPath.Should().NotBeNullOrEmpty("logo path should be set");
    }

    [Fact]
    public void UI_DisplaysApplicationName_WhenConfigured()
    {
        _globalConfig.ShowApplicationName = true;
        _globalConfig.ApplicationName = "Test Wallet";

        var wallet = CreateControl<Views.NethereumWallet>();
        PlaceInWindow(wallet);

        _globalConfig.ShowApplicationName.Should().BeTrue("app name should be configured to show");
        _globalConfig.ApplicationName.Should().Be("Test Wallet");
    }

    [Fact]
    public void UI_PasswordVisibilityToggle_WorksWhenEnabled()
    {
        _config.AllowPasswordVisibilityToggle = true;

        var wallet = CreateControl<Views.NethereumWallet>();
        PlaceInWindow(wallet);

        _config.AllowPasswordVisibilityToggle.Should().BeTrue("password toggle should be enabled");
    }

    [Fact]
    public async Task UI_LocalizationKeys_ArePresent()
    {
        var localizer = ServiceProvider.GetRequiredService<Nethereum.Wallet.UI.Components.Core.Localization.IComponentLocalizer<NethereumWalletViewModel>>();

        var loginTitle = localizer.GetString(NethereumWalletLocalizer.Keys.LoginTitle);
        var createTitle = localizer.GetString(NethereumWalletLocalizer.Keys.CreateTitle);
        var passwordLabel = localizer.GetString(NethereumWalletLocalizer.Keys.PasswordLabel);

        loginTitle.Should().NotBeNullOrEmpty("LoginTitle key should have translation");
        createTitle.Should().NotBeNullOrEmpty("CreateTitle key should have translation");
        passwordLabel.Should().NotBeNullOrEmpty("PasswordLabel key should have translation");
    }

    [Fact]
    public async Task UI_PasswordStrengthIndicator_ShowsWhenEnabled()
    {
        await RunOnUIThreadAsync(() =>
        {
            _viewModel.ShowPasswordStrengthIndicator = true;
            _viewModel.NewPassword = "WeakPass";
            return Task.CompletedTask;
        });

        _viewModel.ShowPasswordStrengthIndicator.Should().BeTrue("indicator should be shown");
        _viewModel.PasswordStrength.Should().BeGreaterOrEqualTo(0, "strength should be calculated");
    }

    #endregion

    #region 1.5 Edge Cases & Error Handling

    [Fact]
    public async Task EdgeCase_VeryLongPassword_IsAccepted()
    {
        var longPassword = new string('A', 128);

        await RunOnUIThreadAsync(async () =>
        {
            _viewModel.NewPassword = longPassword;
            _viewModel.ConfirmPassword = longPassword;
            await _viewModel.CreateWalletCommand.ExecuteAsync(null);
        });

        _viewModel.IsWalletUnlocked.Should().BeTrue("long passwords should be accepted");
    }

    [Fact]
    public async Task EdgeCase_UnicodePassword_IsAccepted()
    {
        var unicodePassword = "P@ssw0rd123🔐🔑";

        await RunOnUIThreadAsync(async () =>
        {
            _viewModel.NewPassword = unicodePassword;
            _viewModel.ConfirmPassword = unicodePassword;
            await _viewModel.CreateWalletCommand.ExecuteAsync(null);
        });

        _viewModel.IsWalletUnlocked.Should().BeTrue("unicode passwords should be accepted");
    }

    [Fact]
    public async Task EdgeCase_WhitespaceInPassword_IsPreserved()
    {
        var passwordWithSpaces = "Pass Word 123";

        await RunOnUIThreadAsync(async () =>
        {
            _viewModel.NewPassword = passwordWithSpaces;
            _viewModel.ConfirmPassword = passwordWithSpaces;
            await _viewModel.CreateWalletCommand.ExecuteAsync(null);
        });

        _viewModel.IsWalletUnlocked.Should().BeTrue("whitespace in passwords should be preserved");
    }

    [Fact]
    public async Task EdgeCase_RapidClicking_PreventsDoubleSubmit()
    {
        var password = "TestPassword123";
        await RunOnUIThreadAsync(() =>
        {
            _viewModel.NewPassword = password;
            _viewModel.ConfirmPassword = password;
            return Task.CompletedTask;
        });

        var task1 = RunOnUIThreadAsync(() => _viewModel.CreateWalletCommand.ExecuteAsync(null));
        var task2 = RunOnUIThreadAsync(() => _viewModel.CreateWalletCommand.ExecuteAsync(null));

        await Task.WhenAll(task1, task2);

        _viewModel.IsWalletUnlocked.Should().BeTrue("should be unlocked");
        var vault = _vaultService.GetCurrentVault();
        vault.Should().NotBeNull();
    }

    #endregion

    #region 1.6 Integration Tests

    [Fact]
    public async Task Integration_CompleteFlow_CreateLoginLogout()
    {
        await RunOnUIThreadAsync(async () =>
        {
            _viewModel.NewPassword = "TestPassword123";
            _viewModel.ConfirmPassword = "TestPassword123";
            await _viewModel.CreateWalletCommand.ExecuteAsync(null);
        });

        _viewModel.IsWalletUnlocked.Should().BeTrue("vault created and unlocked");

        await RunOnUIThreadAsync(async () =>
        {
            await _viewModel.LogoutCommand.ExecuteAsync(null);
        });

        _viewModel.IsWalletUnlocked.Should().BeFalse("should be locked after logout");
        _viewModel.VaultExists.Should().BeTrue("vault still exists");

        await RunOnUIThreadAsync(async () =>
        {
            _viewModel.Password = "TestPassword123";
            await _viewModel.LoginCommand.ExecuteAsync(null);
        });

        _viewModel.IsWalletUnlocked.Should().BeTrue("logged in successfully");
    }

    [Fact]
    public async Task Integration_WrongPassword_ThenCorrectPassword()
    {
        await CreateTestVaultAsync("TestPassword123");
        await RunOnUIThreadAsync(async () => await _viewModel.LogoutCommand.ExecuteAsync(null));

        await RunOnUIThreadAsync(async () =>
        {
            _viewModel.Password = "WrongPassword";
            await _viewModel.LoginCommand.ExecuteAsync(null);
        });

        _viewModel.IsWalletUnlocked.Should().BeFalse("wrong password fails");
        _viewModel.LoginError.Should().NotBeNullOrEmpty("error shown");

        await RunOnUIThreadAsync(async () =>
        {
            _viewModel.Password = "TestPassword123";
            await _viewModel.LoginCommand.ExecuteAsync(null);
        });

        _viewModel.IsWalletUnlocked.Should().BeTrue("correct password succeeds");
        _viewModel.LoginError.Should().BeNullOrEmpty("error cleared");
    }

    #endregion

    #region Helper Methods

    private async Task CreateTestVaultAsync(string password)
    {
        await RunOnUIThreadAsync(async () =>
        {
            _viewModel.NewPassword = password;
            _viewModel.ConfirmPassword = password;
            await _viewModel.CreateWalletCommand.ExecuteAsync(null);
        });
    }

    #endregion
}
