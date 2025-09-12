using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.Wallet;
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.UI.Components.Abstractions;
using Nethereum.Wallet.UI.Components.Core.Localization;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Web3;
using Nethereum.Util;
using System;
using System.Numerics;
using static Nethereum.Wallet.UI.Components.WalletOverview.WalletOverviewLocalizer;
using Nethereum.Wallet.Services.Transactions;
using Nethereum.Wallet.Services.Network;

namespace Nethereum.Wallet.UI.Components.WalletOverview
{
    public partial class WalletOverviewViewModel : ObservableObject, IDisposable
    {
        private readonly IWalletVaultService _walletVaultService;
        private readonly IWalletNotificationService _notificationService;
        private readonly IWalletLoadingService _loadingService;
        private readonly NethereumWalletHostProvider _walletHostProvider;
        private readonly IComponentLocalizer<WalletOverviewViewModel> _localizer;
        private readonly IPendingTransactionService _pendingTransactionService;
        private readonly IChainManagementService _chainManagementService;

        [ObservableProperty]
        private IWalletAccount? _selectedAccount;

        [ObservableProperty]
        private ObservableCollection<IWalletAccount> _accounts = new();

        [ObservableProperty]
        private BigInteger _balanceWei = BigInteger.Zero;

        [ObservableProperty]
        private string _balanceUsd = "$0.00";

        [ObservableProperty]
        private bool _hasBalanceError;

        [ObservableProperty]
        private bool _isLoadingPrice;

        [ObservableProperty]
        private bool _hasFiatBalance;

        public string FormattedBalance
        {
            get
            {
                if (BalanceWei == BigInteger.Zero)
                    return "0";

                // Use Nethereum's UnitConversion for proper Wei to Ether conversion
                var etherValue = UnitConversion.Convert.FromWei(BalanceWei);

                if (etherValue >= 1000)
                    return etherValue.ToString("N2");
                else if (etherValue >= 1)
                    return etherValue.ToString("F4").TrimEnd('0').TrimEnd('.');
                else if (etherValue >= 0.001m)
                    return etherValue.ToString("F6").TrimEnd('0').TrimEnd('.');
                else
                    return etherValue.ToString("F8").TrimEnd('0').TrimEnd('.');
            }
        }
        
        public string CurrencySymbol => GetCurrencySymbol(ChainId);

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isRefreshing;

        [ObservableProperty]
        private string _networkName = "Ethereum";

        [ObservableProperty]
        private long _chainId = 0;

        [ObservableProperty]
        private ObservableCollection<TransactionSummary> _recentTransactions = new();

        [ObservableProperty]
        private bool _hasAccounts;

        [ObservableProperty]
        private int _selectedAccountIndex;

        [ObservableProperty]
        private ObservableCollection<TransactionInfo> _pendingTransactions = new();

        [ObservableProperty]
        private bool _hasPendingTransactions;

        [ObservableProperty]
        private int _pendingTransactionCount;

        public Action? OnSendTransactionRequested { get; set; }
        public Action? OnNavigateToTransactionHistory { get; set; }
        public Action? OnNavigateToAccountDetails { get; set; }

        public WalletOverviewViewModel(
            IWalletVaultService walletVaultService,
            IWalletNotificationService notificationService,
            IWalletLoadingService loadingService,
            NethereumWalletHostProvider walletHostProvider,
            IComponentLocalizer<WalletOverviewViewModel> localizer,
            IPendingTransactionService pendingTransactionService,
            IChainManagementService chainManagementService)
        {
            _walletVaultService = walletVaultService;
            _notificationService = notificationService;
            _loadingService = loadingService;
            _walletHostProvider = walletHostProvider;
            _localizer = localizer;
            _pendingTransactionService = pendingTransactionService;
            _chainManagementService = chainManagementService;

            _walletHostProvider.SelectedAccountChanged += OnSelectedAccountChangedAsync;
            _walletHostProvider.NetworkChanged += OnNetworkChangedAsync;
            _pendingTransactionService.TransactionStatusChanged += OnTransactionStatusChanged;
        }
        [RelayCommand]
        public async Task InitializeAsync()
        {
            IsLoading = true;
            try
            {
                ChainId = _walletHostProvider.SelectedNetworkChainId;
                NetworkName = GetNetworkName(_walletHostProvider.SelectedNetworkChainId);

                await LoadAccountsAsync();
                if (SelectedAccount != null)
                {
                    await LoadAccountBalanceAsync();
                    await LoadPendingTransactionsAsync();
                    await LoadRecentTransactionsAsync();
                }
            }
            finally
            {
                IsLoading = false;
            }
        }
        [RelayCommand]
        public async Task RefreshAsync()
        {
            IsRefreshing = true;
            try
            {
                await LoadAccountsAsync();
                if (SelectedAccount != null)
                {
                    await LoadAccountBalanceAsync();
                    await LoadPendingTransactionsAsync();
                    await LoadRecentTransactionsAsync();
                }
                _notificationService.ShowSuccess(_localizer.GetString(Keys.RefreshSuccess));
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"{_localizer.GetString(Keys.RefreshError)}: {ex.Message}");
            }
            finally
            {
                IsRefreshing = false;
            }
        }
        [RelayCommand]
        public async Task SwitchAccountAsync(IWalletAccount account)
        {
            if (account == null || account == SelectedAccount) return;

            foreach (var acc in Accounts)
            {
                acc.IsSelected = false;
            }

            account.IsSelected = true;
            SelectedAccount = account;

            try
            {
                await _walletVaultService.SaveAsync();
            }
            catch
            {
            }

            await LoadAccountBalanceAsync();
            await LoadPendingTransactionsAsync();
            await LoadRecentTransactionsAsync();

            _notificationService.ShowSuccess($"{_localizer.GetString(Keys.AccountSwitched)}: {account.Name}");
        }
        [RelayCommand]
        public async Task SendTransactionAsync()
        {
            if (SelectedAccount == null)
            {
                _notificationService.ShowWarning(_localizer.GetString(Keys.SelectAccountFirst));
                return;
            }

            OnSendTransactionRequested?.Invoke();
        }
        [RelayCommand]
        public async Task ReceiveAsync()
        {
            if (SelectedAccount == null)
            {
                _notificationService.ShowWarning(_localizer.GetString(Keys.SelectAccountFirst));
                return;
            }

        }
        [RelayCommand]
        public async Task ManageAccountsAsync()
        {
            
        }
        [RelayCommand]
        public Task CopyAddressAsync()
        {
            if (SelectedAccount?.Address != null)
            {
                _notificationService.ShowSuccess(_localizer.GetString(Keys.AddressCopied));
            }
            return Task.CompletedTask;
        }

        private async Task LoadAccountsAsync()
        {
            var accounts = await _walletVaultService.GetAccountsAsync();
            Accounts.Clear();
            
            foreach (var account in accounts)
            {
                Accounts.Add(account);
            }

            HasAccounts = Accounts.Any();
            SelectedAccount = accounts.FirstOrDefault(a => a.IsSelected) ?? accounts.FirstOrDefault();
            
            if (SelectedAccount != null)
            {
                var accountsList = accounts.ToList();
                SelectedAccountIndex = accountsList.IndexOf(SelectedAccount);
            }
        }

        private async Task LoadAccountBalanceAsync()
        {
            if (SelectedAccount == null) return;

            HasBalanceError = false;
            try
            {
                var web3 = await _walletHostProvider.GetWeb3Async();
                
                var balanceWei = await web3.Eth.GetBalance.SendRequestAsync(SelectedAccount.Address);
                BalanceWei = balanceWei.Value;

                BalanceUsd = _localizer.GetString(Keys.PriceUnavailable);
                HasFiatBalance = false;
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"{_localizer.GetString(Keys.BalanceLoadError)}: {ex.Message}");
                BalanceWei = BigInteger.Zero;
                BalanceUsd = _localizer.GetString(Keys.ErrorText);
                HasBalanceError = true;
                HasFiatBalance = false;
            }
        }

        [RelayCommand]
        private async Task LoadPendingTransactionsAsync()
        {
            if (SelectedAccount == null) return;
            
            try
            {
                var pending = await _pendingTransactionService.GetPendingTransactionsAsync(new BigInteger(ChainId));
                
                // Filter for current account and ONLY pending/mining transactions
                var accountPending = pending.Where(t => 
                    t.From.Equals(SelectedAccount.Address, StringComparison.OrdinalIgnoreCase) &&
                    (t.Status == TransactionStatus.Pending || t.Status == TransactionStatus.Mining))
                    .OrderByDescending(t => t.SubmittedAt)
                    .Take(5)
                    .ToList();
                
                PendingTransactions.Clear();
                foreach (var tx in accountPending)
                {
                    PendingTransactions.Add(tx);
                }
                
                HasPendingTransactions = PendingTransactions.Any();
                PendingTransactionCount = PendingTransactions.Count;
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Failed to load pending transactions: {ex.Message}");
            }
        }

        [RelayCommand]
        private void NavigateToTransactionHistory()
        {
            OnNavigateToTransactionHistory?.Invoke();
        }

        [RelayCommand]
        private void NavigateToAccountDetails()
        {
            OnNavigateToAccountDetails?.Invoke();
        }

        private async void OnTransactionStatusChanged(object? sender, TransactionStatusChangedEventArgs e)
        {
            if (SelectedAccount != null && 
                e.Transaction.From.Equals(SelectedAccount.Address, StringComparison.OrdinalIgnoreCase))
            {
                if (e.NewStatus == TransactionStatus.Confirmed || 
                    e.NewStatus == TransactionStatus.Failed ||
                    e.NewStatus == TransactionStatus.Dropped)
                {
                    var txToRemove = PendingTransactions.FirstOrDefault(t => 
                        t.Hash.Equals(e.Transaction.Hash, StringComparison.OrdinalIgnoreCase));
                    if (txToRemove != null)
                    {
                        PendingTransactions.Remove(txToRemove);
                        HasPendingTransactions = PendingTransactions.Any();
                        PendingTransactionCount = PendingTransactions.Count;
                    }
                    
                    // Refresh balance for confirmed or failed transactions
                    if (e.NewStatus == TransactionStatus.Confirmed || 
                        e.NewStatus == TransactionStatus.Failed)
                    {
                        await LoadAccountBalanceAsync();
                        await LoadRecentTransactionsAsync();
                    }
                }
                else
                {
                    var existingTx = PendingTransactions.FirstOrDefault(t => 
                        t.Hash.Equals(e.Transaction.Hash, StringComparison.OrdinalIgnoreCase));
                    if (existingTx != null)
                    {
                        existingTx.Status = e.NewStatus;
                        OnPropertyChanged(nameof(PendingTransactions));
                    }
                }
            }
        }

        private Task LoadRecentTransactionsAsync()
        {
            return Task.CompletedTask;
        }

        private async Task OnSelectedAccountChangedAsync(string accountAddress)
        {
            SelectedAccount = _walletHostProvider.GetSelectedAccount();
            if (SelectedAccount != null)
            {
                await LoadAccountBalanceAsync();
                await LoadPendingTransactionsAsync();
            }
        }

        private async Task OnNetworkChangedAsync(long chainId)
        {
            ChainId = chainId;
            NetworkName = GetNetworkName(chainId);
            
            _notificationService.ShowInfo($"{_localizer.GetString(Keys.NetworkSwitched)}: {NetworkName}");
            
            if (SelectedAccount != null)
            {
                await LoadAccountBalanceAsync();
                await LoadPendingTransactionsAsync();
            }
        }

        private string GetNetworkName(long chainId) => chainId switch
        {
            1 => "Ethereum Mainnet",
            11155111 => "Sepolia Testnet",
            137 => "Polygon",
            80001 => "Polygon Mumbai",
            _ => $"Chain {chainId}"
        };

        private string GetCurrencySymbol(long chainId) => chainId switch
        {
            1 => "ETH",
            11155111 => "ETH",
            137 => "MATIC",
            80001 => "MATIC",
            _ => "ETH"
        };
        [RelayCommand]
        public async Task ViewTransactionOnExplorerAsync(TransactionInfo transaction)
        {
            try
            {
                if (string.IsNullOrEmpty(transaction.Hash)) return;
                
                // Use ChainManagementService to get configured explorer URL
                var chain = await _chainManagementService.GetChainAsync(new BigInteger(ChainId));
                
                if (chain?.Explorers?.Count > 0)
                {
                    var explorerUrl = chain.Explorers.First();
                    var fullUrl = $"{explorerUrl.TrimEnd('/')}/tx/{transaction.Hash}";
                    
                    OnOpenUrlRequested?.Invoke(fullUrl);
                }
                else
                {
                    _notificationService.ShowWarning(_localizer.GetString(Keys.NoExplorerConfigured));
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"{_localizer.GetString(Keys.FailedToOpenExplorer)}: {ex.Message}");
            }
        }

        [RelayCommand]
        public void CopyTransactionHash(TransactionInfo transaction)
        {
            if (!string.IsNullOrEmpty(transaction.Hash))
            {
                OnCopyToClipboardRequested?.Invoke(transaction.Hash);
                _notificationService.ShowSuccess(_localizer.GetString(Keys.TransactionHashCopied));
            }
        }

        [RelayCommand]
        public void ShowTransactionDetails(TransactionInfo transaction)
        {
            // This will be handled by the Blazor component to show details
            OnShowTransactionDetailsRequested?.Invoke(transaction);
        }

        public Action<string>? OnOpenUrlRequested { get; set; }
        public Action<string>? OnCopyToClipboardRequested { get; set; }
        public Action<TransactionInfo>? OnShowTransactionDetailsRequested { get; set; }

        public void Dispose()
        {
            _walletHostProvider.SelectedAccountChanged -= OnSelectedAccountChangedAsync;
            _walletHostProvider.NetworkChanged -= OnNetworkChangedAsync;
            _pendingTransactionService.TransactionStatusChanged -= OnTransactionStatusChanged;
        }
    }
    public class TransactionSummary
    {
        public string Hash { get; set; } = string.Empty;
        public string Amount { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string FormattedTimestamp => Timestamp.ToString("MMM dd, HH:mm");
    }
}