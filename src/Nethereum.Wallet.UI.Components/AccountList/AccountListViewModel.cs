using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.Wallet;
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.UI.Components.Abstractions;
using Nethereum.Wallet.UI.Components.Core.Configuration;
using Nethereum.Wallet.UI.Components.WalletAccounts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Wallet.WalletAccounts;
using Nethereum.Wallet.Services;
using Nethereum.Wallet.UI.Components.AccountDetails;

namespace Nethereum.Wallet.UI.Components.AccountList
{
    public partial class AccountListViewModel : ObservableObject
    {
        private readonly NethereumWalletHostProvider _walletHostProvider;
        private readonly IWalletVaultService _vaultService;
        private readonly IWalletNotificationService _notificationService;
        private readonly BaseWalletConfiguration _vaultConfiguration;
        private readonly IAccountTypeMetadataRegistry _metadataRegistry;
        private readonly IEnsService? _ensService;
        public IEnumerable<IWalletAccount> AllAccounts =>
            AccountGroups.SelectMany(group => group.Accounts);

        [ObservableProperty]
        private IWalletAccount? _selectedAccount;

        [ObservableProperty]
        private string _selectedAccountAddress = "";

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private string _errorMessage = "";

        [ObservableProperty]
        private string _successMessage = "";

        [ObservableProperty]
        private string _editingAccountAddress = "";

        [ObservableProperty]
        private string _editingAccountName = "";

        [ObservableProperty]
        private bool _isEditingAccount = false;

        [ObservableProperty]
        private ObservableCollection<AccountGroupViewModel> _accountGroups = new();

        [ObservableProperty]
        private bool _isManageView = false;

        public event EventHandler<AccountSelectedEventArgs>? AccountSelected;
        public event EventHandler<AccountActionEventArgs>? AccountAdded;
        public event EventHandler<AccountActionEventArgs>? AccountRemoved;
        public event EventHandler<AccountActionEventArgs>? AccountUpdated;

        public string CurrencySymbol => _walletHostProvider.ActiveChain?.NativeCurrency.Symbol ?? Core.Constants.DefaultNativeCurrencySymbol;

        public AccountListViewModel(
            NethereumWalletHostProvider walletHostProvider,
            IWalletVaultService vaultService,
            IWalletNotificationService notificationService,
            BaseWalletConfiguration vaultConfiguration,
            IAccountTypeMetadataRegistry metadataRegistry,
            IEnsService? ensService = null)
        {
            _walletHostProvider = walletHostProvider;
            _vaultService = vaultService;
            _notificationService = notificationService;
            _vaultConfiguration = vaultConfiguration;
            _metadataRegistry = metadataRegistry;
            _ensService = ensService;

            _walletHostProvider.SelectedAccountChanged += OnSelectedAccountChangedAsync;
        }
        [RelayCommand]
        public async Task InitializeAsync()
        {
            try
            {
                await LoadAccountsAsync();
                SelectedAccount = _walletHostProvider.GetSelectedAccount();
                SelectedAccountAddress = SelectedAccount?.Address ?? "";
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Failed to initialize: {ex.Message}");
            }
        }
        [RelayCommand]
        public async Task SwitchAccountAsync(IWalletAccount account)
        {
            try
            {
                await _walletHostProvider.SetSelectedAccountAsync(account);
                SelectedAccount = account;
                _notificationService.ShowSuccess($"Switched to account '{account.Name}'");
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Failed to switch account: {ex.Message}");
            }
        }
        [RelayCommand]
        public async Task DeleteAccountAsync(IWalletAccount account)
        {
            try
            {
                var vault = _vaultService.GetCurrentVault();
                if (vault != null)
                {
                    var accountToRemove = vault.Accounts.FirstOrDefault(a => a.Address == account.Address);
                    if (accountToRemove != null)
                    {
                        vault.Accounts.Remove(accountToRemove);
                        await _vaultService.SaveAsync();
                    }
                }

                await LoadAccountsAsync();
                
                AccountRemoved?.Invoke(this, new AccountActionEventArgs { Account = account, Details = $"Account '{account.Name}' removed" });
                
                _notificationService.ShowSuccess($"Account '{account.Name}' deleted");

                if (account == SelectedAccount && AllAccounts.Any())
                {
                    await SwitchAccountAsync(AllAccounts.First());
                }
                
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Failed to delete account: {ex.Message}");
            }
        }

        public async Task LoadAccountsAsync()
        {
            await LoadAccountGroupsAsync();
        }

        private async Task LoadAccountGroupsAsync()
        {
            try
            {
                var accountGroups = await _vaultService.GetAccountGroupsAsync();
                Console.WriteLine($"LoadAccountGroupsAsync: Loaded {accountGroups.Count} groups from service");
                
                AccountGroups.Clear();

                foreach (var group in accountGroups)
                {
                    Console.WriteLine($"Processing group: key='{group.GroupId ?? "NULL"}', {group.Count} accounts");
                    var groupViewModel = CreateAccountGroupViewModel(group);
                    Console.WriteLine($"Created group: {groupViewModel.GroupName}");
                    AccountGroups.Add(groupViewModel);
                }
                
                Console.WriteLine($"LoadAccountGroupsAsync: Final result - {AccountGroups.Count} groups in UI");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in LoadAccountGroupsAsync: {ex.Message}");
                _notificationService.ShowError($"Failed to load account groups: {ex.Message}");
            }
        }

        private AccountGroupViewModel CreateAccountGroupViewModel(AccountGroup accountGroup)
        {
            if (accountGroup.IsStandalone)
            {
                return new AccountGroupViewModel(null, "Individual Accounts", "standalone", accountGroup.Accounts);
            }
            else
            {
                var firstAccount = accountGroup.Accounts.First();
                var groupName = GetGroupDisplayName(accountGroup);
                var groupType = firstAccount.Type;

                return new AccountGroupViewModel(accountGroup.GroupId, groupName, groupType, accountGroup.Accounts);
            }
        }

        private string GetGroupDisplayName(AccountGroup accountGroup)
        {
            var firstAccount = accountGroup.Accounts.First();
            
            var mnemonicInfo = accountGroup.GetGroupMetadata<MnemonicInfo>();
            if (mnemonicInfo != null && !string.IsNullOrEmpty(mnemonicInfo.Label))
            {
                return mnemonicInfo.Label;
            }
            
            // Use metadata registry for all account types (including mnemonic without custom label)
            var metadata = _metadataRegistry.GetMetadata(firstAccount.Type);
            if (metadata != null)
            {
                return metadata.DisplayName;
            }
            
            return GetAccountTypeDisplayName(firstAccount.Type);
        }

        private async Task OnSelectedAccountChangedAsync(string accountAddress)
        {
            SelectedAccount = _walletHostProvider.GetSelectedAccount();
            SelectedAccountAddress = accountAddress;
        }
        public string GetAccountTypeDisplayName(string accountType) => 
            _vaultConfiguration.GetAccountTypeDisplayName(accountType);
        public string GetAccountTypeDescription(string accountType) => 
            _vaultConfiguration.GetAccountTypeDescription(accountType);
        public string GetAccountTypeName(IWalletAccount account)
        {
            return account.Type;
        }
        public string GetAccountTypeDisplayName(IWalletAccount account)
        {
            var metadata = _metadataRegistry.GetMetadata(account.Type);
            return metadata?.DisplayName ?? account.Type;
        }
        public string GetAccountTypeDescription(IWalletAccount account)
        {
            var metadata = _metadataRegistry.GetMetadata(account.Type);
            return metadata?.Description ?? $"{account.Type} account";
        }
        public string GetAccountColorTheme(IWalletAccount account)
        {
            var metadata = _metadataRegistry.GetMetadata(account.Type);
            return metadata?.ColorTheme ?? "primary";
        }
        public string GetAccountIcon(IWalletAccount account)
        {
            var metadata = _metadataRegistry.GetMetadata(account.Type);
            return metadata?.Icon ?? "account_circle";
        }
        public string GetAccountDisplayName(IWalletAccount account)
        {
            if (!string.IsNullOrEmpty(account.Name)) return account.Name;
            if (!string.IsNullOrEmpty(account.Label)) return account.Label;
            
            var localizedAccountType = GetAccountTypeDisplayName(account);
            return $"{localizedAccountType} ({FormatAddress(account.Address)})";
        }
        public string FormatAddress(string address, bool isCompact = false, int componentWidth = 800)
        {
            if (string.IsNullOrEmpty(address) || address.Length < 10)
                return address;

            if (componentWidth < 600)
            {
                return $"{address[..6]}...{address[^4..]}";
            }

            return address;
        }
        public string GetAccountTypeIcon(string? accountType) => accountType?.ToLower() switch
        {
            "mnemonic" => "key",
            "privatekey" => "vpn_key",
            "viewonly" => "visibility", 
            "smartcontract" => "smart_toy",
            _ => "account_circle"
        };
        [RelayCommand]
        public async Task StartEditAccountAsync(IWalletAccount account)
        {
            EditingAccountAddress = account.Address;
            EditingAccountName = account.Name;
            IsEditingAccount = true;
        }
        [RelayCommand]
        public async Task SaveAccountNameAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = "";

                if (string.IsNullOrWhiteSpace(EditingAccountName))
                {
                    ErrorMessage = "Account name cannot be empty";
                    return;
                }

                var account = AllAccounts.FirstOrDefault(a => a.Address == EditingAccountAddress);
                if (account != null)
                {
                    account.Label = EditingAccountName;
                    
                    var vault = _vaultService.GetCurrentVault();
                    if (vault != null)
                    {
                        try
                        {
                            await _vaultService.SaveAsync();
                        }
                        catch
                        {
                        }
                    }
                    
                    SuccessMessage = "Account name updated successfully";
                }

                var accountToUpdate = AllAccounts.FirstOrDefault(a => a.Address == EditingAccountAddress);
                if (accountToUpdate != null)
                {
                    AccountUpdated?.Invoke(this, new AccountActionEventArgs { Account = accountToUpdate, Details = $"Account name updated to '{EditingAccountName}'" });
                }
                
                IsEditingAccount = false;
                EditingAccountAddress = "";
                EditingAccountName = "";
                await LoadAccountsAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to update account name: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
        [RelayCommand]
        public async Task CancelEditAccountAsync()
        {
            IsEditingAccount = false;
            EditingAccountAddress = "";
            EditingAccountName = "";
            ErrorMessage = "";
        }
        [RelayCommand]
        public async Task SelectAccountByAddressAsync(string address)
        {
            var account = AllAccounts.FirstOrDefault(a => a.Address == address);
            if (account != null)
            {
                await SwitchAccountAsync(account);
                SelectedAccountAddress = address;
                AccountSelected?.Invoke(this, new AccountSelectedEventArgs { Account = account });
            }
        }
        [RelayCommand]
        public void ClearMessages()
        {
            ErrorMessage = "";
            SuccessMessage = "";
        }
        public async Task<string?> GetEnsNameAsync(string address)
        {
            return _ensService != null ? await _ensService.ResolveAddressToNameAsync(address) : null;
        }
        [RelayCommand]
        public async Task PreloadEnsNamesAsync()
        {
            if (_ensService != null)
            {
                var addresses = AllAccounts.Select(account => account.Address).ToList();
                await _ensService.BatchResolveAddressesToNamesAsync(addresses);
            }
        }
        [RelayCommand]
        public void ClearEnsCache()
        {
            _ensService?.ClearCache();
        }
        [RelayCommand]
        public async Task ShowAccountDetailsAsync(string accountAddress)
        {
            var account = AllAccounts.FirstOrDefault(a => a.Address.Equals(accountAddress, StringComparison.OrdinalIgnoreCase));
            if (account != null)
            {
                await SelectAccountByAddressAsync(accountAddress);
            }
        }
        [RelayCommand]
        public async Task NavigateToGroupDetailsAsync(AccountGroupViewModel group)
        {
            // This would be handled by the UI component that uses this ViewModel
            GroupDetailsRequested?.Invoke(this, new GroupDetailsEventArgs 
            { 
                GroupId = group.GroupId,
                GroupAccounts = group.Accounts.ToList()
            });
        }
        [RelayCommand]
        public void ToggleGroupExpanded(AccountGroupViewModel group)
        {
            group.IsExpanded = !group.IsExpanded;
        }

        public event EventHandler<GroupDetailsEventArgs>? GroupDetailsRequested;
    }

    public class AccountSelectedEventArgs : EventArgs
    {
        public IWalletAccount Account { get; set; } = null!;
    }

    public class AccountActionEventArgs : EventArgs
    {
        public IWalletAccount Account { get; set; } = null!;
        public string Details { get; set; } = "";
    }

    public class GroupDetailsEventArgs : EventArgs
    {
        public string? GroupId { get; set; }
        public List<IWalletAccount> GroupAccounts { get; set; } = new();
    }
}