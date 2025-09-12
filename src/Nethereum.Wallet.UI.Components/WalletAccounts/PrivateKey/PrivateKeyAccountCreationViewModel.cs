
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.Signer;
using Nethereum.Wallet;
using Nethereum.Wallet.WalletAccounts;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.WalletAccounts;
using Nethereum.Wallet.Hosting;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using static Nethereum.Wallet.UI.Components.WalletAccounts.PrivateKey.PrivateKeyAccountEditorLocalizer;

namespace Nethereum.Wallet.UI.Components.WalletAccounts.PrivateKey
{
    public partial class PrivateKeyAccountCreationViewModel : ObservableObject, IAccountCreationViewModel
    {
        private readonly IComponentLocalizer<PrivateKeyAccountCreationViewModel> _localizer;
        private readonly IWalletVaultService _vaultService;
        private readonly NethereumWalletHostProvider _walletHostProvider;
        
        public PrivateKeyAccountCreationViewModel(
            IComponentLocalizer<PrivateKeyAccountCreationViewModel> localizer,
            IWalletVaultService vaultService,
            NethereumWalletHostProvider walletHostProvider)
        {
            _localizer = localizer;
            _vaultService = vaultService;
            _walletHostProvider = walletHostProvider;
        }

        public string DisplayName => _localizer.GetString(Keys.DisplayName);
        public string Description => _localizer.GetString(Keys.Description);
        public string Icon => "key";
        public int SortOrder => 2;
        public bool IsVisible => true;

        [ObservableProperty] private string _privateKey = string.Empty;
        [ObservableProperty] private string _label = string.Empty;
        [ObservableProperty] private bool _isRevealed = false;
        [ObservableProperty] private string _errorMessage = string.Empty;
        [ObservableProperty] private string _validationMessage = string.Empty;
        [ObservableProperty] private string _derivedAddress = string.Empty;

        public bool IsValidPrivateKey => ValidatePrivateKey().IsValid;
        public string PrivateKeyFormat => DetectPrivateKeyFormat();
        public bool CanCreateAccount => IsValidPrivateKey && !string.IsNullOrWhiteSpace(Label);

        partial void OnPrivateKeyChanged(string value)
        {
            ValidateAndUpdateAddress();
        }

        [RelayCommand]
        public Task ToggleRevealAsync()
        {
            IsRevealed = !IsRevealed;
            return Task.CompletedTask;
        }

        private void ValidateAndUpdateAddress()
        {
            var validation = ValidatePrivateKey();
            ValidationMessage = validation.Message;
            ErrorMessage = validation.IsValid ? string.Empty : validation.Message;

            if (validation.IsValid && !string.IsNullOrWhiteSpace(PrivateKey))
            {
                try
                {
                    var cleanPrivateKey = CleanPrivateKey(PrivateKey);
                    var ethKey = new EthECKey(cleanPrivateKey);
                    DerivedAddress = ethKey.GetPublicAddress();
                }
                catch (Exception ex)
                {
                    DerivedAddress = string.Empty;
                    ErrorMessage = _localizer.GetString(Keys.InvalidPrivateKeyError, ex.Message);
                }
            }
            else
            {
                DerivedAddress = string.Empty;
            }

            OnPropertyChanged(nameof(IsValidPrivateKey));
            OnPropertyChanged(nameof(PrivateKeyFormat));
            OnPropertyChanged(nameof(CanCreateAccount));
        }

        private (bool IsValid, string Message) ValidatePrivateKey()
        {
            if (string.IsNullOrWhiteSpace(PrivateKey))
                return (false, _localizer.GetString(Keys.PrivateKeyRequiredError));

            var cleanKey = CleanPrivateKey(PrivateKey);

            if (!Regex.IsMatch(cleanKey, "^[0-9a-fA-F]+$"))
                return (false, _localizer.GetString(Keys.InvalidHexStringError));

            // Check length (32 bytes = 64 hex characters)
            if (cleanKey.Length != 64)
                return (false, _localizer.GetString(Keys.InvalidLengthError, cleanKey.Length));

            if (cleanKey.All(c => c == '0'))
                return (false, _localizer.GetString(Keys.PrivateKeyCannotBeZeroError));

            try
            {
                var ethKey = new EthECKey(cleanKey);
                var address = ethKey.GetPublicAddress();
                return (true, _localizer.GetString(Keys.ValidPrivateKeySuccess, address));
            }
            catch (Exception ex)
            {
                return (false, _localizer.GetString(Keys.InvalidPrivateKeyError, ex.Message));
            }
        }

        private string CleanPrivateKey(string privateKey)
        {
            if (string.IsNullOrWhiteSpace(privateKey))
                return string.Empty;

            var cleaned = privateKey.Trim();
            
            if (cleaned.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned.Substring(2);

            return cleaned;
        }

        private string DetectPrivateKeyFormat()
        {
            if (string.IsNullOrWhiteSpace(PrivateKey))
                return _localizer.GetString(Keys.UnknownFormat);

            var original = PrivateKey.Trim();
            var cleaned = CleanPrivateKey(PrivateKey);

            if (original.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return _localizer.GetString(Keys.HexWithPrefixFormat);
            else if (Regex.IsMatch(cleaned, "^[0-9a-fA-F]+$"))
                return _localizer.GetString(Keys.HexWithoutPrefixFormat);
            else
                return _localizer.GetString(Keys.InvalidFormat);
        }

        public IWalletAccount CreateAccount(WalletVault vault)
        {
            if (!CanCreateAccount)
                throw new InvalidOperationException("Cannot create account: validation failed");

            try
            {
                if (string.IsNullOrWhiteSpace(Label))
                    Label = _localizer.GetString(Keys.DefaultAccountName);

                var cleanPrivateKey = CleanPrivateKey(PrivateKey);
                var pkAddress = new EthECKey(cleanPrivateKey).GetPublicAddress();
                return new PrivateKeyWalletAccount(pkAddress, Label, cleanPrivateKey);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(_localizer.GetString(Keys.CreateAccountFailedError, ex.Message), ex);
            }
        }

        public async Task<AccountCreationResult> CreateAndAddAccountAsync()
        {
            try
            {
                var vault = _vaultService.GetCurrentVault();
                if (vault == null)
                    return AccountCreationResult.Failure(_localizer.GetString("VaultNotAvailable"));

                var account = CreateAccount(vault);
                vault.AddAccount(account, setAsSelected: true);
                await _vaultService.SaveAsync();
                await _walletHostProvider.RefreshAccountsAsync();

                return AccountCreationResult.Success(account);
            }
            catch (Exception ex)
            {
                return AccountCreationResult.Failure(_localizer.GetString(Keys.CreateAccountFailedError, ex.Message));
            }
        }

        public void Reset()
        {
            PrivateKey = string.Empty;
            Label = string.Empty;
            IsRevealed = false;
            ErrorMessage = string.Empty;
            ValidationMessage = string.Empty;
            DerivedAddress = string.Empty;
        }
    }
}
