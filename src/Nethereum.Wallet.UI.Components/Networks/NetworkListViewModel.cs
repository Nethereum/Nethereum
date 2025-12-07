using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.DataServices.Chainlist;
using Nethereum.DataServices.Chainlist.Responses;
using Nethereum.RPC.Chain;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.Storage;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.Hosting;

namespace Nethereum.Wallet.UI.Components.Networks
{
    public partial class NetworkListViewModel : ObservableObject
    {
        private const string ShowTestnetsStorageKey = "Wallet.Networks.ShowTestnets";

        private readonly IChainManagementService _chainManagementService;
        private readonly IComponentLocalizer<NetworkListViewModel> _localizer;
        private readonly NethereumWalletHostProvider _walletHostProvider;
        private readonly IWalletStorageService _walletStorageService;
        private readonly ChainlistRpcApiService _chainlistService;
        private readonly IReadOnlyList<ChainFeature> _preconfiguredNetworks;
        private bool _suppressTestnetPersistence;
        private bool _preferencesLoaded;
        private CancellationTokenSource? _chainlistSearchCts;
        private List<ChainlistChainInfo>? _chainlistCache;

        [ObservableProperty] private ObservableCollection<NetworkItemViewModel> _networks;
        [ObservableProperty] private string _searchText = "";
        [ObservableProperty] private bool _showTestnets = false;
        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private string? _errorMessage;
        [ObservableProperty] private ObservableCollection<ChainlistNetworkResult> _chainlistResults = new();
        [ObservableProperty] private bool _isChainlistLoading;
        [ObservableProperty] private string? _chainlistMessage;
        [ObservableProperty] private ObservableCollection<InternalNetworkSuggestion> _internalSuggestions = new();
        
        public Func<BigInteger, Task>? OnNavigateToDetails { get; set; }

        public NetworkListViewModel(
            IChainManagementService chainManagementService,
            IComponentLocalizer<NetworkListViewModel> localizer,
            NethereumWalletHostProvider walletHostProvider,
            IWalletStorageService walletStorageService,
            ChainlistRpcApiService chainlistService,
            IReadOnlyList<ChainFeature>? preconfiguredNetworks = null)
        {
            _chainManagementService = chainManagementService;
            _localizer = localizer;
            _walletHostProvider = walletHostProvider;
            _walletStorageService = walletStorageService;
            _chainlistService = chainlistService;
            _preconfiguredNetworks = preconfiguredNetworks ?? Array.Empty<ChainFeature>();
            _networks = new ObservableCollection<NetworkItemViewModel>();
        }

        private async Task LoadPreferencesAsync()
        {
            if (_preferencesLoaded)
            {
                return;
            }

            try
            {
                _suppressTestnetPersistence = true;
                var storedValue = await _walletStorageService
                    .GetNetworkPreferenceAsync(ShowTestnetsStorageKey);

                if (storedValue.HasValue)
                {
                    ShowTestnets = storedValue.Value;
                }

                _preferencesLoaded = true;
            }
            finally
            {
                _suppressTestnetPersistence = false;
            }
        }

        private Task PersistShowTestnetsPreferenceAsync(bool value)
        {
            if (!_preferencesLoaded || _suppressTestnetPersistence)
            {
                return Task.CompletedTask;
            }

            return _walletStorageService.SaveNetworkPreferenceAsync(ShowTestnetsStorageKey, value);
        }

        [RelayCommand]
        private async Task InitializeAsync()
        {
            IsLoading = true;
            ErrorMessage = null;

            try
            {
                await LoadPreferencesAsync();

                var chains = await _chainManagementService.GetAllChainsAsync();
                
                Networks.Clear();
                foreach (var chain in chains)
                {
                    var networkItem = new NetworkItemViewModel(chain);
                    Networks.Add(networkItem);
                }

                // Don't auto-select networks - let users choose explicitly
                
                UpdateActiveStatesFromHostProvider();
                
                UpdateFilteredNetworks();
                await RefreshChainlistResultsAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load networks: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task SelectNetworkAsync(NetworkItemViewModel networkItem)
        {
            if (networkItem != null)
            {
                if (_walletHostProvider.SelectedNetworkChainId == (long)networkItem.ChainId)
                {
                    return;
                }
                
                UpdateActiveStates(networkItem);
                
                // Set active network through wallet host provider - this is the source of truth
                await _walletHostProvider.SetSelectedNetworkAsync((long)networkItem.ChainId);
                
                // This ensures consistency if the selection failed or was modified
                UpdateActiveStatesFromHostProvider();
            }
        }
        
        private void UpdateActiveStates(NetworkItemViewModel selectedNetwork)
        {
            foreach (var network in Networks)
            {
                network.IsActive = network.ChainId == selectedNetwork.ChainId;
            }
        }
        
        private void UpdateActiveStatesFromHostProvider()
        {
            var activeChainId = _walletHostProvider.SelectedNetworkChainId;
            
            foreach (var network in Networks)
            {
                network.IsActive = (long)network.ChainId == activeChainId;
            }
        }

        [RelayCommand]
        private async Task RefreshNetworksAsync()
        {
            await _chainManagementService.RefreshChainDataAsync();
            await InitializeAsync();
        }

        [RelayCommand]
        private async Task ActivateNetworkAsync(NetworkItemViewModel networkItem)
        {
            // Network activation is now handled through selection
            if (networkItem != null)
            {
                await SelectNetworkAsync(networkItem);
            }
        }

        [RelayCommand]
        private async Task AddChainlistNetworkAsync(ChainlistNetworkResult? result)
        {
            if (result == null || result.IsAdding)
            {
                return;
            }

            try
            {
                result.IsAdding = true;
                var added = await _chainManagementService
                    .AddNetworkFromChainListAsync(result.ChainId);

                if (added != null && !Networks.Any(n => n.ChainId == added.ChainId))
                {
                    Networks.Add(new NetworkItemViewModel(added));
                }

                ChainlistResults.Remove(result);
                ChainlistMessage = null;
                UpdateFilteredNetworks();

                if (added != null && OnNavigateToDetails != null)
                {
                    await OnNavigateToDetails.Invoke(added.ChainId);
                }
            }
            catch (Exception ex)
            {
                ChainlistMessage = string.Format(
                    _localizer.GetString(NetworkListLocalizer.Keys.ChainlistAddError),
                    ex.Message);
            }
            finally
            {
                result.IsAdding = false;
            }
        }

        [RelayCommand]
        private async Task ShowNetworkDetailsAsync(NetworkItemViewModel networkItem)
        {
            if (networkItem == null) return;

            if (OnNavigateToDetails != null)
            {
                await OnNavigateToDetails.Invoke(networkItem.ChainId);
            }
        }
        [RelayCommand]
        public async Task ShowNetworkDetailsByChainIdAsync(string chainId)
        {
            if (BigInteger.TryParse(chainId, out var parsedChainId))
            {
                var networkItem = Networks.FirstOrDefault(n => n.ChainId == parsedChainId);
                if (networkItem != null)
                {
                    await ShowNetworkDetailsAsync(networkItem);
                }
                else
                {
                    // Network not found in list, but still trigger navigation with the chain ID
                    if (OnNavigateToDetails != null)
                    {
                        await OnNavigateToDetails.Invoke(parsedChainId);
                    }
                }
            }
        }

        [RelayCommand]

        private async Task AddInternalNetworkAsync(InternalNetworkSuggestion? suggestion)
        {
            if (suggestion == null || suggestion.IsAdding)
            {
                return;
            }

            try
            {
                suggestion.IsAdding = true;
                await _chainManagementService.AddCustomChainAsync(CloneChainFeature(suggestion.ChainFeature));

                var added = await _chainManagementService.GetCompleteChainAsync(suggestion.ChainId);
                if (added != null)
                {
                    Networks.Add(new NetworkItemViewModel(added));
                }

                UpdateFilteredNetworks();

                if (OnNavigateToDetails != null)
                {
                    await OnNavigateToDetails.Invoke(suggestion.ChainId);
                }
            }
            finally
            {
                suggestion.IsAdding = false;
            }
        }

        private static ChainFeature CloneChainFeature(ChainFeature original)
        {
            if (original == null)
            {
                throw new ArgumentNullException(nameof(original));
            }

            return new ChainFeature
            {
                ChainId = original.ChainId,
                ChainName = original.ChainName,
                IsTestnet = original.IsTestnet,
                NativeCurrency = original.NativeCurrency == null
                    ? null
                    : new NativeCurrency
                    {
                        Name = original.NativeCurrency.Name,
                        Symbol = original.NativeCurrency.Symbol,
                        Decimals = original.NativeCurrency.Decimals
                    },
                SupportEIP155 = original.SupportEIP155,
                SupportEIP1559 = original.SupportEIP1559,
                HttpRpcs = original.HttpRpcs?.ToList() ?? new List<string>(),
                WsRpcs = original.WsRpcs?.ToList() ?? new List<string>(),
                Explorers = original.Explorers?.ToList() ?? new List<string>()
            };
        }

        [ObservableProperty]
        private ObservableCollection<NetworkItemViewModel> _filteredNetworks = new();

        private void UpdateFilteredNetworks()
        {
            var filtered = Networks.AsEnumerable();

            if (!ShowTestnets && string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = filtered.Where(n => !n.IsTestnet);
            }

            string? searchLower = null;
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                searchLower = SearchText.Trim().ToLowerInvariant();
                filtered = filtered.Where(n =>
                {
                    var nameMatch = !string.IsNullOrEmpty(n.ChainName) && n.ChainName.Contains(searchLower, StringComparison.OrdinalIgnoreCase);
                    var idMatch = n.ChainId.ToString().Contains(searchLower, StringComparison.OrdinalIgnoreCase);
                    var currencyMatch = (!string.IsNullOrEmpty(n.NativeCurrencySymbol) && n.NativeCurrencySymbol.Contains(searchLower, StringComparison.OrdinalIgnoreCase));
                    var rpcMatch = (n.ChainFeature.HttpRpcs?.Any(r => !string.IsNullOrWhiteSpace(r) && r.Contains(searchLower, StringComparison.OrdinalIgnoreCase)) ?? false)
                                   || (n.ChainFeature.WsRpcs?.Any(r => !string.IsNullOrWhiteSpace(r) && r.Contains(searchLower, StringComparison.OrdinalIgnoreCase)) ?? false);
                    var explorerMatch = n.ChainFeature.Explorers?.Any(e => !string.IsNullOrWhiteSpace(e) && e.Contains(searchLower, StringComparison.OrdinalIgnoreCase)) ?? false;

                    return nameMatch || idMatch || currencyMatch || rpcMatch || explorerMatch;
                });
            }

            FilteredNetworks.Clear();
            foreach (var network in filtered)
            {
                FilteredNetworks.Add(network);
            }

            UpdateInternalSuggestions(searchLower);
            _ = RefreshChainlistResultsAsync();
        }

        private async Task RefreshChainlistResultsAsync()
        {
            _chainlistSearchCts?.Cancel();

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                ClearChainlistResults();
                return;
            }

            var tokenSource = new CancellationTokenSource();
            _chainlistSearchCts = tokenSource;
            var token = tokenSource.Token;

            try
            {
                IsChainlistLoading = true;
                ChainlistMessage = _localizer.GetString(NetworkListLocalizer.Keys.ChainlistSearching);

                _chainlistCache ??= await _chainlistService.GetAllChainsAsync();
                token.ThrowIfCancellationRequested();

                var searchLower = SearchText.Trim().ToLowerInvariant();
                var existingChainIds = Networks
                    .Select(n => (long)n.ChainId)
                    .ToHashSet();

                var matches = _chainlistCache
                    .Where(c => MatchesChainlistSearch(c, searchLower))
                    .OrderBy(c => c.Name)
                    .Take(10)
                    .ToList();

                ChainlistResults.Clear();
                foreach (var match in matches)
                {
                    var result = new ChainlistNetworkResult(match)
                    {
                        IsAlreadyAdded = existingChainIds.Contains(match.ChainId)
                    };
                    ChainlistResults.Add(result);
                }

                ChainlistMessage = ChainlistResults.Count == 0
                    ? _localizer.GetString(NetworkListLocalizer.Keys.ChainlistNoResults)
                    : null;
            }
            catch (OperationCanceledException)
            {
                // Ignore - a new search was triggered
            }
            catch (Exception ex)
            {
                ChainlistMessage = string.Format(
                    _localizer.GetString(NetworkListLocalizer.Keys.ChainlistError),
                    ex.Message);
            }
            finally
            {
                if (_chainlistSearchCts == tokenSource)
                {
                    IsChainlistLoading = false;
                }
            }
        }

        private static bool MatchesChainlistSearch(ChainlistChainInfo chain, string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return true;
            }

            return (!string.IsNullOrWhiteSpace(chain.Name) && chain.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                   || (!string.IsNullOrWhiteSpace(chain.Chain) && chain.Chain.Contains(term, StringComparison.OrdinalIgnoreCase))
                   || (!string.IsNullOrWhiteSpace(chain.ShortName) && chain.ShortName.Contains(term, StringComparison.OrdinalIgnoreCase))
                   || chain.ChainId.ToString().Contains(term, StringComparison.OrdinalIgnoreCase)
                   || chain.NetworkId.ToString().Contains(term, StringComparison.OrdinalIgnoreCase);
        }

        private void ClearChainlistResults()
        {
            if (ChainlistResults.Count > 0)
            {
                ChainlistResults.Clear();
            }

            ChainlistMessage = null;
            IsChainlistLoading = false;
        }

        private void UpdateInternalSuggestions(string? searchLower)
        {
            InternalSuggestions.Clear();
            if (string.IsNullOrWhiteSpace(searchLower))
            {
                return;
            }

            var existingIds = Networks.Select(n => n.ChainId).ToHashSet();
            var matches = _preconfiguredNetworks
                .Where(feature => !existingIds.Contains(feature.ChainId) &&
                                  MatchesInternalSearch(feature, searchLower))
                .Take(8)
                .Select(feature => new InternalNetworkSuggestion(CloneChainFeature(feature)))
                .ToList();

            foreach (var suggestion in matches)
            {
                InternalSuggestions.Add(suggestion);
            }
        }

        private static bool MatchesInternalSearch(ChainFeature feature, string term)
        {
            if (feature == null)
            {
                return false;
            }

            return (!string.IsNullOrWhiteSpace(feature.ChainName) && feature.ChainName.Contains(term, StringComparison.OrdinalIgnoreCase))
                   || feature.ChainId.ToString().Contains(term, StringComparison.OrdinalIgnoreCase)
                   || (!string.IsNullOrWhiteSpace(feature.NativeCurrency?.Symbol) && feature.NativeCurrency.Symbol.Contains(term, StringComparison.OrdinalIgnoreCase))
                   || (feature.HttpRpcs?.Any(r => !string.IsNullOrWhiteSpace(r) && r.Contains(term, StringComparison.OrdinalIgnoreCase)) ?? false)
                   || (feature.WsRpcs?.Any(r => !string.IsNullOrWhiteSpace(r) && r.Contains(term, StringComparison.OrdinalIgnoreCase)) ?? false);
        }

        partial void OnSearchTextChanged(string value)
        {
            UpdateFilteredNetworks();
        }

        partial void OnShowTestnetsChanged(bool value)
        {
            UpdateFilteredNetworks();
            _ = PersistShowTestnetsPreferenceAsync(value);
        }
    }
    public partial class NetworkItemViewModel : ObservableObject
    {
        public ChainFeature ChainFeature { get; }

        [ObservableProperty] private bool _isActive = false;
        
        public NetworkItemViewModel(ChainFeature chainFeature)
        {
            ChainFeature = chainFeature;
        }

        public BigInteger ChainId => ChainFeature.ChainId;
        public string ChainName => ChainFeature.ChainName;
        public string? NativeCurrencySymbol => ChainFeature.NativeCurrency?.Symbol;
        public bool HasRpcEndpoints => (ChainFeature.HttpRpcs?.Any() == true) || (ChainFeature.WsRpcs?.Any() == true);
        public int RpcCount => (ChainFeature.HttpRpcs?.Count ?? 0) + (ChainFeature.WsRpcs?.Count ?? 0);
        
        public bool IsTestnet => ChainFeature.IsTestnet || ChainCategories.IsTestnet(ChainId);

        public string DisplayName => ChainName;
        public string SubtitleText => NativeCurrencySymbol != null ? 
            $"{NativeCurrencySymbol} • Chain {ChainId}" : 
            $"Chain {ChainId}";
    }

    public partial class ChainlistNetworkResult : ObservableObject
    {
        public ChainlistChainInfo ChainInfo { get; }

        public ChainlistNetworkResult(ChainlistChainInfo chainInfo)
        {
            ChainInfo = chainInfo ?? throw new ArgumentNullException(nameof(chainInfo));
        }

        public BigInteger ChainId => new BigInteger(ChainInfo.ChainId);
        public string DisplayName => string.IsNullOrWhiteSpace(ChainInfo.Name) ? $"Chain {ChainInfo.ChainId}" : ChainInfo.Name;
        public string SubtitleText => ChainInfo.NativeCurrency?.Symbol != null
            ? $"{ChainInfo.NativeCurrency.Symbol} • Chain {ChainInfo.ChainId}"
            : $"Chain {ChainInfo.ChainId}";
        public bool HasRpcEndpoints => ChainInfo.Rpc?.Any(r => !string.IsNullOrWhiteSpace(r?.Url)) == true;
        public int RpcCount => ChainInfo.Rpc?.Count(r => !string.IsNullOrWhiteSpace(r?.Url)) ?? 0;
        public string? PrimaryRpc => ChainInfo.Rpc?.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r?.Url))?.Url;
        public string AvatarText => GenerateAvatarText(DisplayName);
        public string? IconUrl => _iconUrl ??= ResolveIconUrl();
        private string? _iconUrl;
        public bool IsKnownTestnet => ChainCategories.IsTestnet(ChainId);

        [ObservableProperty] private bool _isAdding;
        [ObservableProperty] private bool _isAlreadyAdded;

        private static string GenerateAvatarText(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "?";
            }

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return "?";
            }

            if (parts.Length == 1)
            {
                return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
            }

            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
        }

        private string? ResolveIconUrl()
        {
            var icon = ChainInfo.Icon;
            if (string.IsNullOrWhiteSpace(icon))
            {
                return null;
            }

            var trimmed = icon.Trim();
            if (trimmed.StartsWith("{"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    if (doc.RootElement.TryGetProperty("url", out var urlProp))
                    {
                        return NormalizeIconUrl(urlProp.GetString());
                    }
                }
                catch
                {
                    return null;
                }
            }

            return NormalizeIconUrl(trimmed);
        }

        private static string? NormalizeIconUrl(string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return null;
            }

            var trimmed = candidate.Trim();
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            {
                return null;
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return uri.ToString();
        }
    }

    public partial class InternalNetworkSuggestion : ObservableObject
    {
        public InternalNetworkSuggestion(ChainFeature chainFeature)
        {
            ChainFeature = chainFeature ?? throw new ArgumentNullException(nameof(chainFeature));
        }

        public ChainFeature ChainFeature { get; }
        public BigInteger ChainId => ChainFeature.ChainId;
        public string DisplayName => string.IsNullOrWhiteSpace(ChainFeature.ChainName)
            ? $"Chain {ChainId}"
            : ChainFeature.ChainName;
        public bool IsTestnet => ChainFeature.IsTestnet || ChainCategories.IsTestnet(ChainId);
        public int RpcCount =>
            (ChainFeature.HttpRpcs?.Count(r => !string.IsNullOrWhiteSpace(r)) ?? 0) +
            (ChainFeature.WsRpcs?.Count(r => !string.IsNullOrWhiteSpace(r)) ?? 0);
        public string? PrimaryRpc =>
            ChainFeature.HttpRpcs?.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r)) ??
            ChainFeature.WsRpcs?.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r));

        [ObservableProperty] private bool _isAdding;
    }
}
