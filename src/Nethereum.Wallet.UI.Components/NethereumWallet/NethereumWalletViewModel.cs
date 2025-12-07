using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.Wallet;
using Nethereum.Wallet.UI.Components.Abstractions;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.UI;
using Nethereum.UI;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace Nethereum.Wallet.UI.Components.NethereumWallet
{
    public partial class NethereumWalletViewModel : ObservableObject
    {
        private readonly IWalletVaultService _walletVaultService;
        private readonly IWalletNotificationService _notificationService;
        private readonly IWalletDialogService _dialogService;
        private readonly IComponentLocalizer<NethereumWalletViewModel> _localizer;
        private readonly NethereumWalletConfiguration _config;
        private readonly NethereumWalletHostProvider _walletHostProvider;
        private readonly SelectedEthereumHostProviderService _selectedHostProvider;

        public Func<Task>? OnWalletConnected { get; set; }

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private string _confirmPassword = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _vaultExists;

        [ObservableProperty]
        private bool _isWalletUnlocked;

        [ObservableProperty]
        private bool _hasAccounts;

        [ObservableProperty]
        private string _newPassword = string.Empty;

        [ObservableProperty]
        private string _createError = string.Empty;

        [ObservableProperty]
        private string _loginError = string.Empty;

        public bool CanCreateWallet => 
            !string.IsNullOrEmpty(NewPassword) &&
            NewPassword == ConfirmPassword &&
            IsPasswordValid(NewPassword);

        public bool CanLogin => !string.IsNullOrEmpty(Password);

        public int PasswordStrength => GetPasswordStrengthValue(NewPassword);

        public string PasswordStrengthColor => GetPasswordStrengthColor();

        public bool ShowPasswordStrengthIndicator => 
            _config.ShowPasswordStrengthIndicator && !string.IsNullOrEmpty(NewPassword);

        public NethereumWalletViewModel(
            IWalletVaultService walletVaultService,
            IWalletNotificationService notificationService,
            IWalletDialogService dialogService,
            IComponentLocalizer<NethereumWalletViewModel> localizer,
            NethereumWalletConfiguration config,
            NethereumWalletHostProvider walletHostProvider,
            SelectedEthereumHostProviderService selectedHostProvider)
        {
            _walletVaultService = walletVaultService;
            _notificationService = notificationService;
            _dialogService = dialogService;
            _localizer = localizer;
            _config = config;
            _walletHostProvider = walletHostProvider;
            _selectedHostProvider = selectedHostProvider;
        }

        public async Task InitializeAsync()
        {
            VaultExists = await _walletVaultService.VaultExistsAsync();

            if (VaultExists && _walletVaultService.GetCurrentVault() != null)
            {
                IsWalletUnlocked = true;
                await CheckAccountsAsync();

                if (HasAccounts)
                {
                    await _walletHostProvider.EnableProviderAsync();
                    await _selectedHostProvider.SetSelectedEthereumHostProvider(_walletHostProvider);
                }
            }
        }

        public async Task CheckAccountsAsync()
        {
            try
            {
                var accounts = await _walletVaultService.GetAccountsAsync();
                HasAccounts = accounts.Count > 0;
            }
            catch
            {
                HasAccounts = false;
            }
        }

        [RelayCommand]
        public async Task LoginAsync()
        {
            IsBusy = true;
            LoginError = string.Empty;

            try
            {
                var success = await _walletVaultService.UnlockAsync(Password);
                if (success)
                {
                    _notificationService.ShowSuccess(_localizer.GetString("VaultUnlockedSuccessfully"));
                    
                    IsWalletUnlocked = true;
                    
                    await CheckAccountsAsync();
                    
                    // If we have accounts, enable the provider and select one
                    if (HasAccounts)
                    {
                        await _walletHostProvider.EnableProviderAsync();
                        await _selectedHostProvider.SetSelectedEthereumHostProvider(_walletHostProvider);
                    }
                    
                    if (OnWalletConnected != null)
                    {
                        await OnWalletConnected();
                    }
                }
                else
                {
                    LoginError = _localizer.GetString("IncorrectPassword");
                    _notificationService.ShowError(LoginError);
                }
            }
            catch (System.Exception ex)
            {
                LoginError = _localizer.GetString("LoginError", ex.Message);
                _notificationService.ShowError(LoginError);
            }
            finally
            {
                IsBusy = false;
                Password = string.Empty;
            }
        }

        [RelayCommand]
        private async Task LogoutAsync()
        {
            await _walletHostProvider.LogoutAsync();
            await _walletVaultService.LockAsync();
            IsWalletUnlocked = false;
            HasAccounts = false;
            VaultExists = await _walletVaultService.VaultExistsAsync();
            Password = string.Empty;
            ConfirmPassword = string.Empty;
            await _selectedHostProvider.ClearSelectedEthereumHostProvider();
        }

        [RelayCommand]
        public async Task CreateWalletAsync()
        {
            CreateError = string.Empty;

            if (string.IsNullOrEmpty(NewPassword))
            {
                CreateError = _localizer.GetString("PasswordRequired");
                return;
            }

            if (NewPassword != ConfirmPassword)
            {
                CreateError = _localizer.GetString("PasswordMismatch");
                return;
            }

            if (NewPassword.Length < _config.Security.MinPasswordLength)
            {
                CreateError = _localizer.GetString("PasswordTooShort", _config.Security.MinPasswordLength);
                return;
            }

            if (!IsPasswordValid(NewPassword))
            {
                CreateError = _localizer.GetString("PasswordNotStrongEnough");
                return;
            }

            IsBusy = true;

            try
            {
                await _walletVaultService.CreateNewAsync(NewPassword);
                _notificationService.ShowSuccess(_localizer.GetString("WalletCreated"));
                
                // Mark wallet as unlocked - it's automatically unlocked after creation
                IsWalletUnlocked = true;
                VaultExists = true;
                
                NewPassword = string.Empty;
                ConfirmPassword = string.Empty;
                
                await CheckAccountsAsync();
                
                // New wallets won't have accounts, so we'll go to account creation state
                // Don't enable provider yet - wait until we have an account
                
                if (OnWalletConnected != null)
                {
                    await OnWalletConnected();
                }
            }
            catch (System.Exception ex)
            {
                CreateError = _localizer.GetString("CreateFailed", ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public bool IsPasswordValid(string password)
        {
            if (string.IsNullOrEmpty(password)) return false;

            if (_config.Security.RequireUppercasePassword && !password.Any(char.IsUpper))
                return false;
            if (_config.Security.RequireLowercasePassword && !password.Any(char.IsLower))
                return false;
            if (_config.Security.RequireNumericPassword && !password.Any(char.IsDigit))
                return false;
            if (_config.Security.RequireSpecialCharacterPassword && !password.Any(c => !char.IsLetterOrDigit(c)))
                return false;

            return true;
        }

        public int GetPasswordStrengthValue(string password)
        {
            if (string.IsNullOrEmpty(password)) return 0;

            var score = 0;
            if (password.Length >= 8) score += 20;
            if (password.Length >= 12) score += 20;
            if (password.Any(char.IsUpper)) score += 20;
            if (password.Any(char.IsLower)) score += 20;
            if (password.Any(char.IsDigit)) score += 10;
            if (password.Any(c => !char.IsLetterOrDigit(c))) score += 10;

            return System.Math.Min(100, score);
        }

        public string GetPasswordStrengthColor()
        {
            var strength = PasswordStrength;
            return strength switch
            {
                < 40 => "error",
                < 70 => "warning", 
                _ => "success"
            };
        }

        // Properties change notifications for computed properties
        partial void OnNewPasswordChanged(string value)
        {
            OnPropertyChanged(nameof(CanCreateWallet));
            OnPropertyChanged(nameof(PasswordStrength));
            OnPropertyChanged(nameof(PasswordStrengthColor));
            OnPropertyChanged(nameof(ShowPasswordStrengthIndicator));
        }

        partial void OnConfirmPasswordChanged(string value)
        {
            OnPropertyChanged(nameof(CanCreateWallet));
        }

        partial void OnPasswordChanged(string value)
        {
            OnPropertyChanged(nameof(CanLogin));
        }

        public async Task ShowResetWalletConfirmationAsync()
        {
            var confirmed = await _dialogService.ShowConfirmationAsync(
                _localizer.GetString("ResetWalletConfirmTitle"),
                _localizer.GetString("ResetWalletConfirmMessage"));
                
            if (confirmed)
            {
                await ResetWalletAsync();
            }
        }

        private async Task ResetWalletAsync()
        {
            IsBusy = true;
            try
            {
                await _walletVaultService.ResetAsync();
                VaultExists = false;
                await _selectedHostProvider.ClearSelectedEthereumHostProvider();
                _notificationService.ShowSuccess(_localizer.GetString("WalletResetSuccess"));
            }
            catch (System.Exception ex)
            {
                _notificationService.ShowError(_localizer.GetString("WalletResetError", ex.Message));
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}

