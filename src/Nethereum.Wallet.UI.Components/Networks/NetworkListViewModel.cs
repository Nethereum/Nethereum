using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.RPC.Chain;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.Hosting;

namespace Nethereum.Wallet.UI.Components.Networks
{
    public partial class NetworkListViewModel : ObservableObject
    {
        private readonly IChainManagementService _chainManagementService;
        private readonly IComponentLocalizer<NetworkListViewModel> _localizer;
        private readonly NethereumWalletHostProvider _walletHostProvider;

        [ObservableProperty] private ObservableCollection<NetworkItemViewModel> _networks;
        [ObservableProperty] private string _searchText = "";
        [ObservableProperty] private bool _showTestnets = false;
        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private string? _errorMessage;
        
        public Action<BigInteger>? OnNavigateToDetails { get; set; }

        public NetworkListViewModel(
            IChainManagementService chainManagementService,
            IComponentLocalizer<NetworkListViewModel> localizer,
            NethereumWalletHostProvider walletHostProvider)
        {
            _chainManagementService = chainManagementService;
            _localizer = localizer;
            _walletHostProvider = walletHostProvider;
            _networks = new ObservableCollection<NetworkItemViewModel>();
        }

        [RelayCommand]
        private async Task InitializeAsync()
        {
            IsLoading = true;
            ErrorMessage = null;

            try
            {
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
        private async Task ShowNetworkDetailsAsync(NetworkItemViewModel networkItem)
        {
            if (networkItem == null) return;

            OnNavigateToDetails?.Invoke(networkItem.ChainId);
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
                    OnNavigateToDetails?.Invoke(parsedChainId);
                }
            }
        }

        [ObservableProperty]
        private ObservableCollection<NetworkItemViewModel> _filteredNetworks = new();

        private void UpdateFilteredNetworks()
        {
            var filtered = Networks.AsEnumerable();

            if (!ShowTestnets)
            {
                filtered = filtered.Where(n => !n.IsTestnet);
            }

            if (!string.IsNullOrEmpty(SearchText))
            {
                var searchLower = SearchText.ToLower();
                filtered = filtered.Where(n => 
                    n.ChainName.ToLower().Contains(searchLower) ||
                    n.ChainId.ToString().Contains(searchLower));
            }

            FilteredNetworks.Clear();
            foreach (var network in filtered)
            {
                FilteredNetworks.Add(network);
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            UpdateFilteredNetworks();
        }

        partial void OnShowTestnetsChanged(bool value)
        {
            UpdateFilteredNetworks();
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
}