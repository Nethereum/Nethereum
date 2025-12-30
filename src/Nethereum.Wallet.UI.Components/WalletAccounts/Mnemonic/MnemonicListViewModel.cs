using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.Wallet;
using Nethereum.Wallet.WalletAccounts;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.WalletAccounts.Mnemonic
{
    public partial class MnemonicListViewModel : ObservableObject
    {
        private readonly IComponentLocalizer<MnemonicListViewModel> _localizer;
        private readonly IWalletVaultService _walletVaultService;

        public MnemonicListViewModel(
            IComponentLocalizer<MnemonicListViewModel> localizer,
            IWalletVaultService walletVaultService)
        {
            _localizer = localizer;
            _walletVaultService = walletVaultService;
        }

        [ObservableProperty] private ObservableCollection<MnemonicItemViewModel> _mnemonics = new();
        [ObservableProperty] private ObservableCollection<MnemonicItemViewModel> _filteredMnemonics = new();
        [ObservableProperty] private MnemonicItemViewModel? _selectedMnemonic;
        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private string _errorMessage = "";
        [ObservableProperty] private string _searchText = "";
        [ObservableProperty] private bool _showEmptyState = false;

        public int TotalMnemonics => Mnemonics?.Count ?? 0;
        public int TotalAccounts => Mnemonics?.Sum(m => m.AccountCount) ?? 0;
        public bool HasMnemonics => TotalMnemonics > 0;

        partial void OnSearchTextChanged(string value)
        {
            ApplyFilter();
        }

        [RelayCommand]
        public async Task InitializeAsync()
        {
            await LoadMnemonicsAsync();
        }

        [RelayCommand]
        public async Task LoadMnemonicsAsync()
        {
            IsLoading = true;
            ErrorMessage = "";

            try
            {
                var vault = _walletVaultService.GetCurrentVault();
                if (vault?.Mnemonics != null)
                {
                    Mnemonics.Clear();
                    
                    foreach (var mnemonicInfo in vault.Mnemonics)
                    {
                        var itemViewModel = new MnemonicItemViewModel
                        {
                            MnemonicInfo = mnemonicInfo,
                            AccountCount = GetAccountCount(vault, mnemonicInfo.Id)
                        };
                        
                        Mnemonics.Add(itemViewModel);
                    }
                    
                    ApplyFilter();
                }
                
                ShowEmptyState = !HasMnemonics;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading mnemonics: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            await LoadMnemonicsAsync();
        }

        [RelayCommand]
        public void SelectMnemonic(MnemonicItemViewModel? mnemonic)
        {
            SelectedMnemonic = mnemonic;
            OnMnemonicSelected?.Invoke(mnemonic?.MnemonicInfo);
        }

        [RelayCommand]
        public async Task<bool> DeleteMnemonicAsync(MnemonicItemViewModel mnemonic)
        {
            if (mnemonic == null) return false;

            IsLoading = true;
            ErrorMessage = "";

            try
            {
                var vault = _walletVaultService.GetCurrentVault();
                if (vault == null)
                {
                    ErrorMessage = _localizer.GetString(MnemonicListViewModelLocalizer.Keys.NoVaultAvailable);
                    return false;
                }

                if (mnemonic.AccountCount > 0)
                {
                    ErrorMessage = _localizer.GetString(MnemonicListViewModelLocalizer.Keys.CannotDeleteMnemonicWithAccounts);
                    return false;
                }

                if (vault.Mnemonics != null && mnemonic.MnemonicInfo != null)
                {
                    vault.Mnemonics.Remove(mnemonic.MnemonicInfo);
                    await _walletVaultService.SaveAsync();
                    
                    Mnemonics.Remove(mnemonic);
                    FilteredMnemonics.Remove(mnemonic);
                    
                    ShowEmptyState = !HasMnemonics;
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error deleting mnemonic: {ex.Message}";
                return false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredMnemonics = new ObservableCollection<MnemonicItemViewModel>(Mnemonics);
            }
            else
            {
                var filtered = Mnemonics.Where(m => 
                    m.MnemonicInfo?.Label?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true ||
                    m.MnemonicInfo?.Id?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true);
                    
                FilteredMnemonics = new ObservableCollection<MnemonicItemViewModel>(filtered);
            }
        }

        private int GetAccountCount(WalletVault vault, string mnemonicId)
        {
            if (vault?.Accounts == null) return 0;
            
            return vault.Accounts
                .Count(account => account is MnemonicWalletAccount mnemonicAccount && 
                                mnemonicAccount.MnemonicId == mnemonicId);
        }

        public void Reset()
        {
            Mnemonics.Clear();
            FilteredMnemonics.Clear();
            SelectedMnemonic = null;
            SearchText = "";
            ErrorMessage = "";
            ShowEmptyState = false;
            IsLoading = false;
        }

        public Action<MnemonicInfo?>? OnMnemonicSelected { get; set; }
    }
    public partial class MnemonicItemViewModel : ObservableObject
    {
        [ObservableProperty] private MnemonicInfo? _mnemonicInfo;
        [ObservableProperty] private int _accountCount;
        [ObservableProperty] private bool _isSelected;

        public string DisplayName => MnemonicInfo?.Label ?? "Unnamed Mnemonic";
        public bool HasPassphrase => !string.IsNullOrEmpty(MnemonicInfo?.Passphrase);
        public string Id => MnemonicInfo?.Id ?? "";
    }
}