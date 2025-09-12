
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.Signer;
using Nethereum.Wallet.Bip32;
using Nethereum.Wallet.WalletAccounts;
using Nethereum.Wallet;
using Nethereum.Wallet.UI.Components.Core.Localization;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Nethereum.Wallet.UI.Components.WalletAccounts;
using Nethereum.Wallet.Services;
using Nethereum.Wallet.Hosting;

namespace Nethereum.Wallet.UI.Components.WalletAccounts.Mnemonic
{
    public partial class MnemonicAccountCreationViewModel : AccountCreationViewModelBase
    {
        [ObservableProperty] private string _mnemonic = string.Empty;
        [ObservableProperty] private string _mnemonicLabel = string.Empty;
        [ObservableProperty] private string _mnemonicPassphrase = string.Empty;
        [ObservableProperty] private bool _isRevealed = false;
        [ObservableProperty] private bool _isBackedUp = false;
        [ObservableProperty] private bool _isGenerateMode = true;
        [ObservableProperty] private string _errorMessage = string.Empty;
        [ObservableProperty] private string _validationMessage = string.Empty;
        [ObservableProperty] private string _derivedAddress = string.Empty;
        [ObservableProperty] private string _finalAccountName = string.Empty;

        private readonly IComponentLocalizer<MnemonicAccountCreationViewModel> _localizer;

        public MnemonicAccountCreationViewModel(
            IComponentLocalizer<MnemonicAccountCreationViewModel> localizer,
            IWalletVaultService vaultService,
            NethereumWalletHostProvider walletHostProvider) : base(vaultService, walletHostProvider)
        {
            _localizer = localizer;
        }

        public override string DisplayName => _localizer.GetString(MnemonicAccountEditorLocalizer.Keys.DisplayName);
        public override string Description => _localizer.GetString(MnemonicAccountEditorLocalizer.Keys.Description);
        public override string Icon => "vpn_key";
        public override int SortOrder => 1;
        public override bool IsVisible => true;

        public bool IsValidMnemonic => ValidateMnemonic().IsValid;
        public int WordCount => string.IsNullOrWhiteSpace(Mnemonic) ? 0 : Mnemonic.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        public bool HasValidWordCount => WordCount == 12 || WordCount == 24;
        public string MnemonicStrength => CalculateMnemonicStrength();
        public override bool CanCreateAccount => IsValidMnemonic && (IsGenerateMode ? IsBackedUp : true);

        partial void OnMnemonicChanged(string value)
        {
            ValidateAndUpdateAddress();
        }

        partial void OnMnemonicPassphraseChanged(string value)
        {
            ValidateAndUpdateAddress();
        }

        [RelayCommand]
        public Task GenerateMnemonicAsync()
        {
            try
            {
                Mnemonic = Bip39.GenerateMnemonic(12);
                IsGenerateMode = true;
                IsRevealed = true;
                IsBackedUp = false;
                ErrorMessage = string.Empty;
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to generate mnemonic: {ex.Message}";
                return Task.CompletedTask;
            }
        }

        [RelayCommand]
        public Task GenerateMnemonic24Async()
        {
            try
            {
                Mnemonic = Bip39.GenerateMnemonic(24);
                IsGenerateMode = true;
                IsRevealed = true;
                IsBackedUp = false;
                ErrorMessage = string.Empty;
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to generate mnemonic: {ex.Message}";
                return Task.CompletedTask;
            }
        }

        [RelayCommand]
        public Task ToggleRevealAsync()
        {
            IsRevealed = !IsRevealed;
            return Task.CompletedTask;
        }

        [RelayCommand]
        public Task ConfirmBackupAsync()
        {
            IsBackedUp = true;
            return Task.CompletedTask;
        }

        [RelayCommand]
        public Task SwitchToImportModeAsync()
        {
            IsGenerateMode = false;
            Mnemonic = string.Empty;
            IsRevealed = true;
            IsBackedUp = true;
            ErrorMessage = string.Empty;
            DerivedAddress = string.Empty;
            ValidationMessage = string.Empty;
            
            OnPropertyChanged(nameof(CanCreateAccount));
            
            return Task.CompletedTask;
        }

        [RelayCommand]
        public Task SwitchToGenerateModeAsync()
        {
            IsGenerateMode = true;
            Mnemonic = string.Empty;
            IsRevealed = false;
            IsBackedUp = false;
            ErrorMessage = string.Empty;
            DerivedAddress = string.Empty;
            ValidationMessage = string.Empty;
            
            OnPropertyChanged(nameof(CanCreateAccount));
            
            return Task.CompletedTask;
        }

        private void ValidateAndUpdateAddress()
        {
            var validation = ValidateMnemonic();
            ValidationMessage = validation.Message;
            ErrorMessage = validation.IsValid ? string.Empty : validation.Message;

            if (validation.IsValid && !string.IsNullOrWhiteSpace(Mnemonic))
            {
                try
                {
                    var hdWallet = new MinimalHDWallet(Mnemonic, MnemonicPassphrase);
                    DerivedAddress = hdWallet.GetEthereumAddress(0);
                }
                catch (Exception ex)
                {
                    DerivedAddress = string.Empty;
                    ErrorMessage = $"Invalid mnemonic: {ex.Message}";
                }
            }
            else
            {
                DerivedAddress = string.Empty;
            }

            OnPropertyChanged(nameof(IsValidMnemonic));
            OnPropertyChanged(nameof(WordCount));
            OnPropertyChanged(nameof(HasValidWordCount));
            OnPropertyChanged(nameof(MnemonicStrength));
            OnPropertyChanged(nameof(CanCreateAccount));
        }

        private (bool IsValid, string Message) ValidateMnemonic()
        {
            if (string.IsNullOrWhiteSpace(Mnemonic))
                return (false, "Mnemonic is required");

            var words = Mnemonic.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            if (words.Length != 12 && words.Length != 24)
                return (false, $"Mnemonic must be 12 or 24 words, got {words.Length}");

            var invalidWords = new List<string>();
            foreach (var word in words)
            {
                if (!Bip39.WordList.Contains(word.ToLowerInvariant()))
                    invalidWords.Add(word);
            }

            if (invalidWords.Any())
                return (false, $"Invalid words: {string.Join(", ", invalidWords)}");

            try
            {
                var hdWallet = new MinimalHDWallet(Mnemonic, null);
                var address = hdWallet.GetEthereumAddress(0);
                return (true, $"Valid {words.Length}-word mnemonic");
            }
            catch
            {
                return (false, "Invalid mnemonic checksum");
            }
        }

        private string CalculateMnemonicStrength()
        {
            if (!IsValidMnemonic) return "Invalid";
            
            return WordCount switch
            {
                12 => "Strong (128-bit)",
                24 => "Very Strong (256-bit)",
                _ => "Unknown"
            };
        }

        public override IWalletAccount CreateAccount(WalletVault vault)
        {
            if (!CanCreateAccount)
                throw new InvalidOperationException("Cannot create account: validation failed");

            try
            {
                if (string.IsNullOrWhiteSpace(MnemonicLabel))
                {
                    var existingAccounts = vault.Accounts.Count;
                    MnemonicLabel = existingAccounts == 0 ? "Main Wallet" : $"Account {existingAccounts + 1}";
                }

                var mnemonicInfo = new MnemonicInfo(MnemonicLabel, Mnemonic, MnemonicPassphrase);
                vault.AddMnemonic(mnemonicInfo);
                var hdWallet = new MinimalHDWallet(Mnemonic, MnemonicPassphrase);
                var address = hdWallet.GetEthereumAddress(0);

                // Use FinalAccountName for the account name, fallback to generated name if empty
                var accountName = !string.IsNullOrWhiteSpace(FinalAccountName) ? FinalAccountName : MnemonicLabel;
                
                return new MnemonicWalletAccount(
                    address,
                    accountName,
                    0,
                    mnemonicInfo.Id,
                    hdWallet
                );
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create account: {ex.Message}", ex);
            }
        }

        public override void Reset()
        {
            Mnemonic = string.Empty;
            MnemonicLabel = string.Empty;
            MnemonicPassphrase = string.Empty;
            IsRevealed = false;
            IsBackedUp = false;
            IsGenerateMode = true;
            ErrorMessage = string.Empty;
            ValidationMessage = string.Empty;
            DerivedAddress = string.Empty;
        }
        public IEnumerable<(int Index, string Word)> GetMnemonicWords()
        {
            if (string.IsNullOrWhiteSpace(Mnemonic))
                return Enumerable.Empty<(int, string)>();

            var words = Mnemonic.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return words.Select((word, index) => (index + 1, word.Trim()));
        }
        public event Func<string, Task>? CopyToClipboardRequested;
        [RelayCommand]
        public async Task CopyMnemonicToClipboardAsync()
        {
            if (!string.IsNullOrWhiteSpace(Mnemonic) && CopyToClipboardRequested != null)
            {
                await CopyToClipboardRequested(Mnemonic);
            }
        }
        [RelayCommand]
        public async Task CopyAddressToClipboardAsync()
        {
            if (!string.IsNullOrWhiteSpace(DerivedAddress) && CopyToClipboardRequested != null)
            {
                await CopyToClipboardRequested(DerivedAddress);
            }
        }
        public string GetFormattedAddress()
        {
            if (string.IsNullOrEmpty(DerivedAddress) || DerivedAddress.Length < 10)
                return DerivedAddress;

            return $"{DerivedAddress[..6]}...{DerivedAddress[^4..]}";
        }
    }
}
