using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.Wallet;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.Services.Tokens;
using Nethereum.Wallet.Services.Tokens.Models;
using Nethereum.Wallet.Storage;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Utils;

namespace Nethereum.Wallet.UI.Components.Holdings
{
    public enum HoldingsTab
    {
        Accounts,
        Networks,
        Tokens
    }

    public partial class HoldingsViewModel : ObservableObject, IDisposable
    {
        private readonly IChainManagementService _chainService;
        private readonly ITokenStorageService _tokenStorage;
        private readonly ITokenManagementService _tokenManagementService;
        private readonly IWalletVaultService _walletVaultService;
        private readonly IHoldingsSettingsStorage _holdingsSettingsStorage;
        private readonly IComponentLocalizer<HoldingsViewModel> _localizer;

        private System.Timers.Timer _priceRefreshTimer;
        private bool _disposed;
        private readonly SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);

        [ObservableProperty] private HoldingsTab _selectedTab = HoldingsTab.Accounts;
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private bool _isScanning;
        [ObservableProperty] private bool _isUpdating;
        [ObservableProperty] private bool _isForceRefreshing;
        [ObservableProperty] private bool _isPriceRefreshing;
        [ObservableProperty] private string _errorMessage;
        [ObservableProperty] private string _successMessage;
        [ObservableProperty] private string _scanStatusMessage;
        [ObservableProperty] private int _scanProgressPercent;
        [ObservableProperty] private int _forceRefreshProgressPercent;

        [ObservableProperty] private decimal _totalValue;
        [ObservableProperty] private string _currency = "usd";
        [ObservableProperty] private string _currencySymbol = "$";
        [ObservableProperty] private DateTime? _lastUpdated;

        [ObservableProperty] private ObservableCollection<HoldingsAccountItemViewModel> _accountItems = new();
        [ObservableProperty] private ObservableCollection<HoldingsNetworkItemViewModel> _networkItems = new();
        [ObservableProperty] private ObservableCollection<HoldingsTokenItemViewModel> _tokenItems = new();

        [ObservableProperty] private HoldingsSettings _settings;

        [ObservableProperty] private bool _isShowingEditPage;

        public string FormattedTotalValue => CurrencyFormatter.FormatValue(TotalValue, CurrencySymbol);

        public string LastUpdatedFormatted => LastUpdated.HasValue
            ? GetRelativeTimeString(LastUpdated.Value)
            : null;

        public int SelectedAccountCount => Settings?.SelectedAccountAddresses?.Count ?? 0;
        public int SelectedNetworkCount => Settings?.SelectedChainIds?.Count ?? 0;

        public Action OnEditRequested { get; set; }
        public Action OnEditCompleted { get; set; }
        public Action<HoldingsTokenItemViewModel> OnSendToken { get; set; }
        public Action<HoldingsTokenItemViewModel, ChainBalanceItemViewModel> OnSendTokenOnChain { get; set; }

        public HoldingsViewModel(
            IChainManagementService chainService,
            ITokenStorageService tokenStorage,
            ITokenManagementService tokenManagementService,
            IWalletVaultService walletVaultService,
            IHoldingsSettingsStorage holdingsSettingsStorage,
            IComponentLocalizer<HoldingsViewModel> localizer)
        {
            _chainService = chainService ?? throw new ArgumentNullException(nameof(chainService));
            _tokenStorage = tokenStorage ?? throw new ArgumentNullException(nameof(tokenStorage));
            _tokenManagementService = tokenManagementService ?? throw new ArgumentNullException(nameof(tokenManagementService));
            _walletVaultService = walletVaultService ?? throw new ArgumentNullException(nameof(walletVaultService));
            _holdingsSettingsStorage = holdingsSettingsStorage ?? throw new ArgumentNullException(nameof(holdingsSettingsStorage));
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        }

        [RelayCommand]
        public async Task InitializeAsync()
        {
            IsLoading = true;
            ErrorMessage = null;

            try
            {
                await _chainService.SyncMissingDefaultChainsAsync();

                Settings = await _holdingsSettingsStorage.GetSettingsAsync();

                var tokenSettings = await _tokenStorage.GetTokenSettingsAsync();
                Currency = tokenSettings?.Currency ?? "usd";
                CurrencySymbol = tokenSettings?.CurrencySymbol ?? SupportedCurrencies.Currencies.GetValueOrDefault(Currency, "$");

                await LoadAccountsAndNetworksAsync();
                await LoadHoldingsDataAsync();

                _ = BackgroundRefreshAsync();
                StartPriceRefreshTimer();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadAccountsAndNetworksAsync()
        {
            var accounts = await _walletVaultService.GetAccountsAsync();
            var chains = await _chainService.GetAllChainsAsync();

            AccountItems.Clear();
            foreach (var account in accounts)
            {
                var isSelected = Settings.SelectedAccountAddresses.Contains(account.Address, StringComparer.OrdinalIgnoreCase);
                AccountItems.Add(new HoldingsAccountItemViewModel
                {
                    Address = account.Address,
                    Name = account.Name ?? FormatAddress(account.Address),
                    IsIncluded = isSelected
                });
            }

            NetworkItems.Clear();
            foreach (var chain in chains.Where(c => !c.IsTestnet))
            {
                var isSelected = Settings.SelectedChainIds.Contains((long)chain.ChainId);
                NetworkItems.Add(new HoldingsNetworkItemViewModel
                {
                    ChainId = (long)chain.ChainId,
                    Name = chain.ChainName,
                    NativeSymbol = chain.NativeCurrency?.Symbol ?? "ETH",
                    IsIncluded = isSelected
                });
            }
        }

        private async Task LoadHoldingsDataAsync()
        {
            if (!Settings.SelectedAccountAddresses.Any() || !Settings.SelectedChainIds.Any())
            {
                TotalValue = 0;
                TokenItems.Clear();
                return;
            }

            try
            {
                var allTokens = new List<(string AccountAddress, AccountToken Token)>();

                foreach (var accountAddress in Settings.SelectedAccountAddresses)
                {
                    foreach (var chainId in Settings.SelectedChainIds)
                    {
                        var tokens = await _tokenManagementService.GetAccountTokensAsync(accountAddress, chainId);
                        foreach (var token in tokens.Where(t => t.Balance > 0))
                        {
                            allTokens.Add((accountAddress, token));
                        }
                    }
                }

                UpdateAccountItemValues(allTokens);
                UpdateNetworkItemValues(allTokens);
                UpdateTokenItems(allTokens);

                TotalValue = allTokens.Sum(t => t.Token.Value ?? 0);

                OnPropertyChanged(nameof(FormattedTotalValue));
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        private void UpdateAccountItemValues(List<(string AccountAddress, AccountToken Token)> allTokens)
        {
            var accountTotals = allTokens
                .GroupBy(t => t.AccountAddress, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(t => t.Token.Value ?? 0),
                    StringComparer.OrdinalIgnoreCase);

            foreach (var account in AccountItems)
            {
                account.TotalValue = accountTotals.GetValueOrDefault(account.Address, 0);
            }
        }

        private void UpdateNetworkItemValues(List<(string AccountAddress, AccountToken Token)> allTokens)
        {
            var networkTotals = allTokens
                .GroupBy(t => t.Token.ChainId)
                .ToDictionary(
                    g => g.Key,
                    g => (Value: g.Sum(t => t.Token.Value ?? 0), TokenCount: g.Select(t => t.Token.ContractAddress ?? t.Token.Symbol).Distinct().Count()));

            foreach (var network in NetworkItems)
            {
                if (networkTotals.TryGetValue(network.ChainId, out var data))
                {
                    network.TotalValue = data.Value;
                    network.TokenCount = data.TokenCount;
                }
                else
                {
                    network.TotalValue = 0;
                    network.TokenCount = 0;
                }
            }
        }

        private void UpdateTokenItems(List<(string AccountAddress, AccountToken Token)> allTokens)
        {
            TokenItems.Clear();

            var tokenGroups = allTokens
                .GroupBy(t => t.Token.IsNative
                    ? $"native_{t.Token.ChainId}"
                    : t.Token.ContractAddress.ToUpperInvariant())
                .OrderByDescending(g => g.Sum(t => t.Token.Value ?? 0));

            foreach (var group in tokenGroups)
            {
                var firstToken = group.First().Token;
                var chainBalances = BuildChainBalances(group);

                var tokenItem = new HoldingsTokenItemViewModel
                {
                    Symbol = firstToken.Symbol,
                    Name = firstToken.Name,
                    LogoUri = firstToken.LogoURI,
                    Decimals = firstToken.Decimals,
                    TotalBalance = chainBalances.Sum(c => c.Balance),
                    TotalValue = group.Sum(t => t.Token.Value ?? 0),
                    Price = firstToken.Price,
                    CurrencySymbol = CurrencySymbol,
                    ChainBalances = chainBalances
                };

                TokenItems.Add(tokenItem);
            }
        }

        private List<ChainBalanceItemViewModel> BuildChainBalances(
            IGrouping<string, (string AccountAddress, AccountToken Token)> tokenGroup)
        {
            var chainBalances = new List<ChainBalanceItemViewModel>();
            var firstToken = tokenGroup.First().Token;

            var byChain = tokenGroup.GroupBy(t => t.Token.ChainId);

            foreach (var chainGroup in byChain)
            {
                var chainId = chainGroup.Key;
                var chainName = NetworkItems.FirstOrDefault(n => n.ChainId == chainId)?.Name ?? $"Chain {chainId}";

                var accountBalances = chainGroup
                    .GroupBy(t => t.AccountAddress, StringComparer.OrdinalIgnoreCase)
                    .Select(ag =>
                    {
                        var accountName = AccountItems.FirstOrDefault(a =>
                            string.Equals(a.Address, ag.Key, StringComparison.OrdinalIgnoreCase))?.Name ?? FormatAddress(ag.Key);
                        var balance = ag.Sum(t => ConvertToDecimal(t.Token.Balance, t.Token.Decimals));
                        var value = ag.Sum(t => t.Token.Value ?? 0);

                        return new AccountBalanceItemViewModel
                        {
                            Address = ag.Key,
                            Name = accountName,
                            Balance = balance,
                            Value = value,
                            CurrencySymbol = CurrencySymbol
                        };
                    })
                    .ToList();

                chainBalances.Add(new ChainBalanceItemViewModel
                {
                    ChainId = chainId,
                    ChainName = chainName,
                    Balance = accountBalances.Sum(a => a.Balance),
                    Value = accountBalances.Sum(a => a.Value ?? 0),
                    Decimals = firstToken.Decimals,
                    CurrencySymbol = CurrencySymbol,
                    AccountBalances = accountBalances
                });
            }

            return chainBalances;
        }

        private static decimal ConvertToDecimal(BigInteger balance, int decimals)
        {
            if (balance == 0) return 0;

            var divisor = BigInteger.Pow(10, decimals);
            var quotient = BigInteger.DivRem(balance, divisor, out var remainder);

            var decimalPart = decimals > 0
                ? (decimal)remainder / (decimal)Math.Pow(10, decimals)
                : 0;

            return (decimal)quotient + decimalPart;
        }

        private CancellationTokenSource _scanCts;
        [ObservableProperty] private Dictionary<string, MultiChainDiscoveryProgress> _accountProgress = new();

        [RelayCommand]
        public async Task ScanAsync(bool forceRescan = false)
        {
            if (!await _refreshLock.WaitAsync(0)) return;

            try
            {
                await ScanAsyncInternal(forceRescan);
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        private async Task ScanAsyncInternal(bool forceRescan = false)
        {
            if (!Settings.SelectedAccountAddresses.Any() || !Settings.SelectedChainIds.Any())
            {
                ErrorMessage = _localizer.GetString(HoldingsLocalizer.Keys.NoAccountsOrNetworksSelected);
                return;
            }

            IsScanning = true;
            IsUpdating = false;
            ErrorMessage = null;
            ScanProgressPercent = 0;
            AccountProgress.Clear();
            _scanCts = new CancellationTokenSource();

            try
            {
                var accountsToScan = new List<string>();

                foreach (var account in Settings.SelectedAccountAddresses)
                {
                    if (forceRescan)
                    {
                        accountsToScan.Add(account);
                        continue;
                    }

                    var allComplete = true;
                    foreach (var chainId in Settings.SelectedChainIds)
                    {
                        var isComplete = await _tokenManagementService.IsDiscoveryCompleteAsync(account, chainId);
                        if (!isComplete)
                        {
                            allComplete = false;
                            break;
                        }
                    }

                    if (!allComplete)
                    {
                        accountsToScan.Add(account);
                    }
                }

                if (!accountsToScan.Any())
                {
                    ScanStatusMessage = _localizer.GetString(HoldingsLocalizer.Keys.Scanned);
                    await LoadHoldingsDataAsync();
                    return;
                }

                var totalAccounts = accountsToScan.Count;
                var completedAccounts = 0;

                foreach (var accountAddress in accountsToScan)
                {
                    if (_scanCts.Token.IsCancellationRequested) break;

                    var accountName = AccountItems.FirstOrDefault(a =>
                        string.Equals(a.Address, accountAddress, StringComparison.OrdinalIgnoreCase))?.Name ?? FormatAddress(accountAddress);

                    ScanStatusMessage = $"Scanning {accountName}...";

                    var progress = new Progress<MultiChainDiscoveryProgress>(p =>
                    {
                        AccountProgress[accountAddress] = p;
                        OnPropertyChanged(nameof(AccountProgress));
                        UpdateOverallProgress(totalAccounts);

                        foreach (var chainProgress in p.ChainProgress)
                        {
                            var network = NetworkItems.FirstOrDefault(n => n.ChainId == chainProgress.Key);
                            if (network != null)
                            {
                                network.IsScanning = !chainProgress.Value.IsComplete;
                                network.ScanProgressText = $"{chainProgress.Value.CheckedTokens}/{chainProgress.Value.TotalTokens}, {chainProgress.Value.TokensFoundSoFar} found";
                            }
                        }
                    });

                    var result = await _tokenManagementService.DiscoverAllChainsAsync(
                        accountAddress,
                        Settings.SelectedChainIds,
                        progress,
                        _scanCts.Token);

                    completedAccounts++;
                    ScanProgressPercent = (int)((double)completedAccounts / totalAccounts * 100);
                }

                Settings.LastScanned = DateTime.UtcNow;
                await _holdingsSettingsStorage.SaveSettingsAsync(Settings);
                await LoadHoldingsDataAsync();

                LastUpdated = DateTime.UtcNow;
                OnPropertyChanged(nameof(LastUpdatedFormatted));
            }
            catch (OperationCanceledException)
            {
                ScanStatusMessage = _localizer.GetString(HoldingsLocalizer.Keys.Cancel);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsScanning = false;
                ScanStatusMessage = null;
                _scanCts?.Dispose();
                _scanCts = null;

                foreach (var network in NetworkItems)
                {
                    network.IsScanning = false;
                    network.ScanProgressText = "";
                }
            }
        }

        [RelayCommand]
        public void CancelScan()
        {
            _scanCts?.Cancel();
        }

        private void UpdateOverallProgress(int totalAccounts)
        {
            if (AccountProgress.Count == 0 || totalAccounts == 0)
            {
                ScanProgressPercent = 0;
                return;
            }

            var totalProgress = AccountProgress.Values.Sum(p => p.OverallPercentComplete);
            ScanProgressPercent = (int)(totalProgress / totalAccounts);
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            if (Settings?.SelectedAccountAddresses?.Any() != true ||
                Settings?.SelectedChainIds?.Any() != true)
                return;

            if (!await _refreshLock.WaitAsync(0)) return;

            IsUpdating = true;
            ErrorMessage = null;
            SuccessMessage = null;

            try
            {
                // Step 1: Scan for transfers (last 100 blocks)
                ScanStatusMessage = _localizer.GetString(HoldingsLocalizer.Keys.ScanningTransfers);
                await _tokenManagementService.RefreshAllChainsAsync(
                    Settings.SelectedAccountAddresses,
                    Settings.SelectedChainIds);

                // Step 2: Update balances (already done by RefreshAllChainsAsync via multicall)
                ScanStatusMessage = _localizer.GetString(HoldingsLocalizer.Keys.UpdatingBalances);
                await LoadHoldingsDataAsync();

                // Step 3: Update prices
                ScanStatusMessage = _localizer.GetString(HoldingsLocalizer.Keys.UpdatingPrices);
                await _tokenManagementService.DecorateWithPricesAsync(
                    Settings.SelectedAccountAddresses,
                    Settings.SelectedChainIds);

                await LoadHoldingsDataAsync();

                LastUpdated = DateTime.UtcNow;
                OnPropertyChanged(nameof(LastUpdatedFormatted));
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsUpdating = false;
                ScanStatusMessage = null;
                _refreshLock.Release();
            }
        }

        [RelayCommand]
        public void ShowEditPage()
        {
            IsShowingEditPage = true;
            OnEditRequested?.Invoke();
        }

        [RelayCommand]
        public async Task ApplySettingsAsync(HoldingsSettings newSettings)
        {
            Settings = newSettings;

            if (Settings.ForceRescanAccountAddresses?.Any() == true)
            {
                foreach (var accountAddress in Settings.ForceRescanAccountAddresses)
                {
                    await _tokenManagementService.ResetDiscoveryAsync(accountAddress, Settings.SelectedChainIds);
                }
                Settings.ForceRescanAccountAddresses.Clear();
            }

            await _holdingsSettingsStorage.SaveSettingsAsync(Settings);

            await LoadAccountsAndNetworksAsync();

            IsShowingEditPage = false;
            OnEditCompleted?.Invoke();

            OnPropertyChanged(nameof(SelectedAccountCount));
            OnPropertyChanged(nameof(SelectedNetworkCount));

            await ScanAsync(false);
        }

        [RelayCommand]
        public void CancelEdit()
        {
            IsShowingEditPage = false;
        }

        private void StartPriceRefreshTimer()
        {
            _priceRefreshTimer?.Dispose();
            _priceRefreshTimer = new System.Timers.Timer(60000);
            _priceRefreshTimer.Elapsed += async (s, e) => await BackgroundRefreshAsync();
            _priceRefreshTimer.AutoReset = true;
            _priceRefreshTimer.Start();
        }

        private async Task BackgroundRefreshAsync()
        {
            try
            {
                await RefreshAsync();
            }
            catch
            {
            }
        }

        public async Task RefreshPricesAsync()
        {
            if (Settings?.SelectedAccountAddresses == null || !Settings.SelectedAccountAddresses.Any()) return;
            if (Settings?.SelectedChainIds == null || !Settings.SelectedChainIds.Any()) return;

            IsPriceRefreshing = true;
            try
            {
                await _tokenManagementService.DecorateWithPricesAsync(
                    Settings.SelectedAccountAddresses,
                    Settings.SelectedChainIds);

                await LoadHoldingsDataAsync();
                OnPropertyChanged(nameof(FormattedTotalValue));
            }
            catch
            {
            }
            finally
            {
                IsPriceRefreshing = false;
            }
        }

        private static string FormatAddress(string address)
        {
            if (string.IsNullOrEmpty(address) || address.Length < 10) return address ?? "";
            return $"{address.Substring(0, 6)}...{address.Substring(address.Length - 4)}";
        }

        private static string GetRelativeTimeString(DateTime time)
        {
            var diff = DateTime.UtcNow - time;
            if (diff.TotalSeconds < 60) return "just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            return $"{(int)diff.TotalDays}d ago";
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _priceRefreshTimer?.Stop();
            _priceRefreshTimer?.Dispose();
            _priceRefreshTimer = null;
        }
    }
}
