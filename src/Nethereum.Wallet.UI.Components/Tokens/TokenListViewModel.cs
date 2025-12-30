using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.Services.Tokens;
using Nethereum.Wallet.Services.Tokens.Models;
using Nethereum.Wallet.Storage;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Utils;

namespace Nethereum.Wallet.UI.Components.Tokens
{
    public partial class TokenListViewModel : ObservableObject
    {
        private readonly ITokenManagementService _tokenManagementService;
        private readonly ITokenStorageService _tokenStorageService;
        private readonly IComponentLocalizer<TokenListViewModel> _localizer;
        private readonly NethereumWalletHostProvider _walletHostProvider;

        [ObservableProperty] private ObservableCollection<TokenItemViewModel> _tokens = new();
        [ObservableProperty] private ObservableCollection<TokenItemViewModel> _filteredTokens = new();
        [ObservableProperty] private string _searchText = "";
        [ObservableProperty] private bool _showZeroBalances = false;
        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private bool _isScanning = false;
        [ObservableProperty] private int _scanProgress = 0;
        [ObservableProperty] private string? _errorMessage;
        [ObservableProperty] private string _currency = "usd";
        [ObservableProperty] private string _currencySymbol = "$";

        public decimal TotalPortfolioValue => Tokens?.Sum(t => t.Token.Value ?? 0) ?? 0;
        public string FormattedTotalValue => CurrencyFormatter.FormatValue(TotalPortfolioValue, CurrencySymbol);

        public Func<string, Task>? OnNavigateToTokenDetails { get; set; }
        public Action? OnAddCustomToken { get; set; }
        public Action? OnOpenSettings { get; set; }

        public TokenListViewModel(
            ITokenManagementService tokenManagementService,
            ITokenStorageService tokenStorageService,
            IComponentLocalizer<TokenListViewModel> localizer,
            NethereumWalletHostProvider walletHostProvider)
        {
            _tokenManagementService = tokenManagementService ?? throw new ArgumentNullException(nameof(tokenManagementService));
            _tokenStorageService = tokenStorageService ?? throw new ArgumentNullException(nameof(tokenStorageService));
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _walletHostProvider = walletHostProvider ?? throw new ArgumentNullException(nameof(walletHostProvider));

            _tokenManagementService.TokensUpdated += OnTokensUpdated;
        }

        private void OnTokensUpdated(object sender, TokensUpdatedEventArgs e)
        {
            if (_walletHostProvider.SelectedAccount == e.AccountAddress &&
                _walletHostProvider.SelectedNetworkChainId == e.ChainId)
            {
                _ = RefreshTokenListAsync();
            }
        }

        [RelayCommand]
        public async Task InitializeAsync()
        {
            IsLoading = true;
            ErrorMessage = null;

            try
            {
                var settings = await _tokenStorageService.GetTokenSettingsAsync();
                Currency = settings.Currency;
                CurrencySymbol = GetCurrencySymbol(settings.Currency);

                await RefreshTokenListAsync();
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

        [RelayCommand]
        public async Task DiscoverTokensAsync()
        {
            if (string.IsNullOrEmpty(_walletHostProvider.SelectedAccount))
            {
                ErrorMessage = _localizer.GetString(TokenListLocalizer.Keys.NoAccountSelected);
                return;
            }

            IsScanning = true;
            ScanProgress = 0;
            ErrorMessage = null;

            try
            {
                var progress = new Progress<TokenDiscoveryProgress>(p =>
                {
                    ScanProgress = (int)p.PercentComplete;
                });

                var result = await _tokenManagementService.StartOrResumeDiscoveryAsync(
                    _walletHostProvider.SelectedAccount,
                    _walletHostProvider.SelectedNetworkChainId,
                    progress);

                if (!result.Success)
                {
                    ErrorMessage = result.ErrorMessage;
                }
                else
                {
                    await RefreshTokenListAsync();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsScanning = false;
                ScanProgress = 100;
            }
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            if (string.IsNullOrEmpty(_walletHostProvider.SelectedAccount)) return;

            IsLoading = true;
            ErrorMessage = null;

            try
            {
                var result = await _tokenManagementService.RefreshAsync(
                    _walletHostProvider.SelectedAccount,
                    _walletHostProvider.SelectedNetworkChainId);

                if (result.HasBalanceError)
                {
                    ErrorMessage = result.BalanceError;
                }

                await RefreshTokenListAsync();
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

        [RelayCommand]
        public async Task RefreshPricesAsync()
        {
            if (string.IsNullOrEmpty(_walletHostProvider.SelectedAccount)) return;

            IsLoading = true;

            try
            {
                await _tokenManagementService.DecorateWithPricesAsync(
                    _walletHostProvider.SelectedAccount,
                    _walletHostProvider.SelectedNetworkChainId);
                await RefreshTokenListAsync();
            }
            catch
            {
                // Price decoration is non-blocking - failures are silently ignored
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task ChangeCurrencyAsync(string currency)
        {
            if (string.IsNullOrEmpty(currency)) return;

            var settings = await _tokenStorageService.GetTokenSettingsAsync();
            settings.Currency = currency;
            settings.CurrencySymbol = GetCurrencySymbol(currency);
            await _tokenStorageService.SaveTokenSettingsAsync(settings);

            Currency = currency;
            CurrencySymbol = settings.CurrencySymbol;

            await RefreshPricesAsync();
        }

        private async Task RefreshTokenListAsync()
        {
            if (string.IsNullOrEmpty(_walletHostProvider.SelectedAccount)) return;

            var tokens = await _tokenManagementService.GetAccountTokensAsync(
                _walletHostProvider.SelectedAccount,
                _walletHostProvider.SelectedNetworkChainId);

            Tokens.Clear();
            foreach (var token in tokens)
            {
                Tokens.Add(new TokenItemViewModel(token, CurrencySymbol));
            }

            UpdateFilteredTokens();
            OnPropertyChanged(nameof(TotalPortfolioValue));
            OnPropertyChanged(nameof(FormattedTotalValue));
        }

        private void UpdateFilteredTokens()
        {
            var filtered = Tokens.AsEnumerable();

            if (!ShowZeroBalances)
            {
                filtered = filtered.Where(t => t.HasBalance);
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var searchLower = SearchText.Trim().ToLowerInvariant();
                filtered = filtered.Where(t =>
                    t.Token.Symbol?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) == true ||
                    t.Token.Name?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) == true ||
                    t.Token.ContractAddress?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) == true);
            }

            FilteredTokens.Clear();
            foreach (var token in filtered.OrderByDescending(t => t.Token.Value ?? 0))
            {
                FilteredTokens.Add(token);
            }
        }

        partial void OnSearchTextChanged(string value) => UpdateFilteredTokens();
        partial void OnShowZeroBalancesChanged(bool value) => UpdateFilteredTokens();

        private static string GetCurrencySymbol(string currency)
        {
            return SupportedCurrencies.Currencies.TryGetValue(currency?.ToLower() ?? "usd", out var symbol)
                ? symbol
                : "$";
        }
    }

    public partial class TokenItemViewModel : ObservableObject
    {
        public AccountToken Token { get; }
        public string CurrencySymbol { get; set; }

        [ObservableProperty] private bool _isRefreshing;

        public TokenItemViewModel(AccountToken token, string currencySymbol = "$")
        {
            Token = token ?? throw new ArgumentNullException(nameof(token));
            CurrencySymbol = currencySymbol;
        }

        public string FormattedBalance
        {
            get
            {
                var balance = Nethereum.Web3.Web3.Convert.FromWei(Token.Balance, Token.Decimals);
                return balance.ToString("N4");
            }
        }

        public string FormattedPrice => CurrencyFormatter.FormatPrice(Token.Price, CurrencySymbol);

        public string FormattedValue => CurrencyFormatter.FormatValue(Token.Value, CurrencySymbol);

        public bool HasBalance => Token.Balance > 0;
        public bool HasPrice => Token.Price.HasValue;
    }
}
