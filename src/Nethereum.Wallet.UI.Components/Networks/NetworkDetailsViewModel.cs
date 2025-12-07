using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.RPC.Chain;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.Storage;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Networks.Models;
using Nethereum.Wallet.Hosting;
using System.Collections.Generic;

namespace Nethereum.Wallet.UI.Components.Networks
{
    public partial class NetworkDetailsViewModel : ObservableObject, IDisposable
    {
        private readonly IChainManagementService _chainManagementService;
        private readonly IWalletStorageService _storageService;
        private readonly IRpcEndpointService _rpcEndpointService;
        private readonly IComponentLocalizer<NetworkDetailsViewModel> _localizer;
        private readonly NethereumWalletHostProvider _walletHostProvider;

        [ObservableProperty] private ChainFeature? _network;
        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private string? _errorMessage;
        [ObservableProperty] private string? _successMessage;
        [ObservableProperty] private ViewSection _currentSection = ViewSection.Overview;
        [ObservableProperty] private NetworkConfiguration _networkConfig;
        
        public bool IsActive => _walletHostProvider.SelectedNetworkChainId == (long)(Network?.ChainId ?? 0);
        
        [ObservableProperty] private ObservableCollection<RpcEndpointViewModel> _rpcEndpoints;
        [ObservableProperty] private RpcSelectionMode _rpcSelectionMode = RpcSelectionMode.Single;
        
        public bool IsEditFormValid => NetworkConfig.IsValid;
        
        public Action? OnNavigateBack { get; set; }

        public enum ViewSection
        {
            Overview,
            EditNetwork,
            RpcConfiguration,
            Advanced
        }

        public NetworkDetailsViewModel(
            IChainManagementService chainManagementService,
            IWalletStorageService storageService,
            IRpcEndpointService rpcEndpointService,
            IComponentLocalizer<NetworkDetailsViewModel> localizer,
            NethereumWalletHostProvider walletHostProvider)
        {
            _chainManagementService = chainManagementService ?? throw new ArgumentNullException(nameof(chainManagementService));
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
            _rpcEndpointService = rpcEndpointService ?? throw new ArgumentNullException(nameof(rpcEndpointService));
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _walletHostProvider = walletHostProvider ?? throw new ArgumentNullException(nameof(walletHostProvider));
            
            _rpcEndpoints = new ObservableCollection<RpcEndpointViewModel>();
            NetworkConfig = new NetworkConfiguration(_localizer);
            
            _walletHostProvider.NetworkChanged += OnNetworkChanged;
            _rpcEndpointService.HealthChanged += OnRpcHealthChanged;
        }

        #region Initialization

        [RelayCommand]
        private async Task InitializeAsync(BigInteger networkId)
        {
            IsLoading = true;
            ErrorMessage = null;
            SuccessMessage = null;

            try
            {
                Network = await _chainManagementService.GetChainAsync(networkId);
                if (Network != null)
                {
                    await LoadRpcConfigurationAsync();
                    await LoadRpcEndpointsAsync();
                    NetworkConfig.LoadFromChainFeature(Network);
                }
                else
                {
                    ErrorMessage = _localizer.GetString(NetworkDetailsLocalizer.Keys.NetworkNotFoundError);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = string.Format(_localizer.GetString(NetworkDetailsLocalizer.Keys.FailedToLoadNetwork), ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadRpcConfigurationAsync()
        {
            if (Network == null) return;
            
            try
            {
                var config = await _rpcEndpointService.GetConfigurationAsync(Network.ChainId);
                if (config != null)
                {
                    RpcSelectionMode = config.Mode;
                }
                else
                {
                    RpcSelectionMode = RpcSelectionMode.Single;
                }
            }
            catch
            {
                RpcSelectionMode = RpcSelectionMode.Single;
            }
        }

        private async Task LoadRpcEndpointsAsync()
        {
            RpcEndpoints.Clear();
            
            if (Network?.HttpRpcs != null)
            {
                foreach (var rpc in Network.HttpRpcs)
                {
                    var endpoint = new RpcEndpointViewModel(rpc, false, false, false);
                    await LoadCachedHealthAsync(endpoint);
                    RpcEndpoints.Add(endpoint);
                }
            }

            if (Network?.WsRpcs != null)
            {
                foreach (var rpc in Network.WsRpcs)
                {
                    var endpoint = new RpcEndpointViewModel(rpc, true, false, false);
                    await LoadCachedHealthAsync(endpoint);
                    RpcEndpoints.Add(endpoint);
                }
            }
            
            await LoadRpcSelectionStateAsync();
        }

        private async Task LoadRpcSelectionStateAsync()
        {
            if (Network == null) return;
            
            try
            {
                var config = await _rpcEndpointService.GetConfigurationAsync(Network.ChainId);
                if (config != null && config.SelectedRpcUrls != null)
                {
                    foreach (var endpoint in RpcEndpoints)
                    {
                        endpoint.IsEnabled = config.SelectedRpcUrls.Contains(endpoint.Url);
                    }
                }
                else
                {
                    var firstEndpoint = RpcEndpoints.FirstOrDefault();
                    if (firstEndpoint != null)
                    {
                        firstEndpoint.IsEnabled = true;
                    }
                }
            }
            catch
            {
                var firstEndpoint = RpcEndpoints.FirstOrDefault();
                if (firstEndpoint != null)
                {
                    firstEndpoint.IsEnabled = true;
                }
            }
        }

        private async Task LoadCachedHealthAsync(RpcEndpointViewModel endpoint)
        {
            try
            {
                var healthCache = await _rpcEndpointService.GetHealthCacheAsync(endpoint.Url);
                if (healthCache != null)
                {
                    endpoint.IsHealthy = healthCache.IsHealthy;
                    endpoint.TestResult = healthCache.IsHealthy ? "✓ Connected" : "✗ Failed";
                }
                else
                {
                    endpoint.IsHealthy = true;
                    endpoint.TestResult = null;
                }
            }
            catch
            {
                endpoint.IsHealthy = true;
                endpoint.TestResult = null;
            }
        }

        private bool IsTestnetNetwork(ChainFeature network)
        {
            return ChainCategories.IsTestnet(network.ChainId) || network.IsTestnet;
        }

        #endregion

        #region RPC Configuration Management

        partial void OnRpcSelectionModeChanged(RpcSelectionMode value)
        {
            // When switching to Single mode, ensure only one RPC is selected
            if (value == RpcSelectionMode.Single)
            {
                var enabledEndpoints = RpcEndpoints.Where(r => r.IsEnabled).ToList();
                if (enabledEndpoints.Count > 1)
                {
                    var firstEnabled = enabledEndpoints.First();
                    foreach (var endpoint in enabledEndpoints)
                    {
                        endpoint.IsEnabled = endpoint == firstEnabled;
                    }
                }
                else if (!enabledEndpoints.Any())
                {
                    var firstEndpoint = RpcEndpoints.FirstOrDefault();
                    if (firstEndpoint != null)
                    {
                        firstEndpoint.IsEnabled = true;
                    }
                }
            }
        }

        [RelayCommand]
        private async Task SaveRpcConfigurationAsync()
        {
            if (Network == null)
            {
                ErrorMessage = "No network selected";
                return;
            }

            try
            {
                IsLoading = true;
                ErrorMessage = null;

                var selectedUrls = RpcEndpoints
                    .Where(r => r.IsEnabled)
                    .Select(r => r.Url)
                    .ToList();

                if (!selectedUrls.Any())
                {
                    ErrorMessage = "Please select at least one RPC endpoint";
                    return;
                }

                if (RpcSelectionMode == RpcSelectionMode.Single && selectedUrls.Count > 1)
                {
                    ErrorMessage = "Single mode requires exactly one RPC endpoint";
                    return;
                }

                var config = new RpcSelectionConfiguration
                {
                    ChainId = Network.ChainId,
                    Mode = RpcSelectionMode,
                    SelectedRpcUrls = selectedUrls,
                    LastModified = DateTime.UtcNow
                };

                await _rpcEndpointService.SaveConfigurationAsync(config);
                SuccessMessage = "RPC configuration saved successfully";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to save RPC configuration: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task TestRpcEndpointAsync(RpcEndpointViewModel endpoint)
        {
            if (endpoint == null || Network == null) return;

            try
            {
                endpoint.IsTesting = true;
                endpoint.TestResult = "Testing...";
                
                var isHealthy = await _rpcEndpointService.CheckHealthAsync(endpoint.Url, Network.ChainId);
                
                endpoint.TestResult = isHealthy ? "✓ Connected" : "✗ Failed";
                endpoint.IsHealthy = isHealthy;
            }
            catch (Exception ex)
            {
                endpoint.TestResult = $"✗ Error: {ex.Message}";
                endpoint.IsHealthy = false;
            }
            finally
            {
                endpoint.IsTesting = false;
            }
        }

        [RelayCommand]
        private async Task TestNetworkConnectionAsync()
        {
            if (Network == null)
                return;

            try
            {
                IsLoading = true;
                ErrorMessage = null;
                SuccessMessage = null;
                
                var selectedUrl = await _rpcEndpointService.SelectEndpointAsync(Network.ChainId);
                if (!string.IsNullOrEmpty(selectedUrl))
                {
                    var isHealthy = await _rpcEndpointService.CheckHealthAsync(selectedUrl, Network.ChainId);
                    if (isHealthy)
                    {
                        SuccessMessage = $"Connection successful to {selectedUrl}";
                    }
                    else
                    {
                        ErrorMessage = $"Connection failed to {selectedUrl}";
                    }
                }
                else
                {
                    ErrorMessage = "No RPC endpoints configured";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Connection test failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void ToggleRpcEndpoint(RpcEndpointViewModel endpoint)
        {
            if (endpoint == null) return;

            if (RpcSelectionMode == RpcSelectionMode.Single)
            {
                if (!endpoint.IsEnabled)
                {
                    foreach (var rpc in RpcEndpoints)
                    {
                        rpc.IsEnabled = false;
                    }
                    endpoint.IsEnabled = true;
                }
                // If trying to disable the only selected one, don't allow it
                else
                {
                    ErrorMessage = "At least one RPC endpoint must be selected";
                }
            }
            else
            {
                // Multiple modes: can select/deselect freely, but need at least one
                if (endpoint.IsEnabled)
                {
                    var activeCount = RpcEndpoints.Count(r => r.IsEnabled);
                    if (activeCount > 1)
                    {
                        endpoint.IsEnabled = false;
                    }
                    else
                    {
                        ErrorMessage = "At least one RPC endpoint must remain selected";
                    }
                }
                else
                {
                    endpoint.IsEnabled = true;
                }
            }
        }

        [RelayCommand]
        private async Task AddCustomRpcAsync()
        {
            if (Network == null) return;
            
            if (!NetworkConfig.CanAddRpcEndpoint)
            {
                ErrorMessage = NetworkConfig.GetFieldError(nameof(NetworkConfig.NewRpcUrl));
                return;
            }

            try
            {
                IsLoading = true;
                ErrorMessage = null;
                
                var isWebSocket = _networkConfig.NewRpcUrl.StartsWith("ws", StringComparison.OrdinalIgnoreCase);
                var httpRpcs = Network.HttpRpcs?.ToList() ?? new List<string>();
                var wsRpcs = Network.WsRpcs?.ToList() ?? new List<string>();

                if (isWebSocket)
                    wsRpcs.Add(NetworkConfig.NewRpcUrl);
                else
                    httpRpcs.Add(NetworkConfig.NewRpcUrl);

                await _chainManagementService.UpdateChainRpcConfigurationAsync(Network.ChainId, httpRpcs, wsRpcs);
                await InitializeAsync(Network.ChainId);
                
                SuccessMessage = _localizer.GetString(NetworkDetailsLocalizer.Keys.RpcEndpointAddedSuccessfully);
            }
            catch (Exception ex)
            {
                ErrorMessage = string.Format(_localizer.GetString(NetworkDetailsLocalizer.Keys.FailedToAddRpcEndpoint), ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RemoveRpcEndpointAsync(RpcEndpointViewModel endpoint)
        {
            if (endpoint == null || Network == null)
                return;

            try
            {
                IsLoading = true;

                var httpRpcs = Network.HttpRpcs?.ToList() ?? new List<string>();
                var wsRpcs = Network.WsRpcs?.ToList() ?? new List<string>();

                if (endpoint.IsWebSocket)
                {
                    wsRpcs.Remove(endpoint.Url);
                }
                else
                {
                    httpRpcs.Remove(endpoint.Url);
                }

                await _chainManagementService.UpdateChainRpcConfigurationAsync(Network.ChainId, httpRpcs, wsRpcs);
                
                await InitializeAsync(Network.ChainId);
                
                SuccessMessage = "RPC endpoint removed successfully";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to remove RPC endpoint: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Network Management

        [RelayCommand]
        private async Task ActivateNetworkAsync()
        {
            // Network activation is now handled through selection only
            if (Network != null)
            {
                await SelectNetworkAsync();
            }
        }
        
        private async Task SelectNetworkAsync()
        {
            if (Network == null) return;
            
            try
            {
                IsLoading = true;
                ErrorMessage = null;
                
                // Use the same selection logic as NetworkListViewModel
                await _walletHostProvider.SetSelectedNetworkAsync((long)Network.ChainId);
                
                SuccessMessage = _localizer.GetString(NetworkDetailsLocalizer.Keys.NetworkSelectedSuccessfully);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"{_localizer.GetString(NetworkDetailsLocalizer.Keys.FailedToSelectNetwork)}: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task DeactivateNetworkAsync()
        {
            // Network deactivation is no longer supported - networks are either selected or not
            ErrorMessage = "Networks cannot be deactivated. Use network selection instead.";
        }

        [RelayCommand]
        private async Task SaveNetworkChangesAsync()
        {
            if (Network == null) return;
            
            NetworkConfig.ValidateAll();
            
            if (!IsEditFormValid)
            {
                ErrorMessage = NetworkConfig.GetLocalizedString(AddCustomNetworkLocalizer.Keys.FormValidationFailed);
                return;
            }
            
            try
            {
                IsLoading = true;
                ErrorMessage = null;
                
                var updatedNetwork = NetworkConfig.ToChainFeature();
                updatedNetwork.ChainId = Network.ChainId;
                updatedNetwork.HttpRpcs = Network.HttpRpcs ?? new List<string>();
                updatedNetwork.WsRpcs = Network.WsRpcs ?? new List<string>();
                
                await _chainManagementService.UpdateChainAsync(updatedNetwork);
                
                Network = updatedNetwork;
                SuccessMessage = _localizer.GetString(NetworkDetailsLocalizer.Keys.NetworkUpdatedSuccessfully);
                CurrentSection = ViewSection.Overview;
            }
            catch (Exception ex)
            {
                ErrorMessage = string.Format(_localizer.GetString(NetworkDetailsLocalizer.Keys.FailedToUpdateNetwork), ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void AddExplorer()
        {
            NetworkConfig.AddBlockExplorer();
        }

        [RelayCommand]
        private void RemoveExplorer(string explorer)
        {
            NetworkConfig.RemoveBlockExplorer(explorer);
        }

        [RelayCommand]
        private async Task ResetNetworkToDefaultAsync()
        {
            if (Network == null) return;
            
            try
            {
                IsLoading = true;
                ErrorMessage = null;
                
                await _chainManagementService.ResetChainToDefaultAsync(Network.ChainId);
                
                // Reload the network data to show default configuration
                await InitializeAsync(Network.ChainId);
                SuccessMessage = _localizer.GetString(NetworkDetailsLocalizer.Keys.NetworkResetSuccessfully);
            }
            catch (Exception ex)
            {
                ErrorMessage = string.Format(_localizer.GetString(NetworkDetailsLocalizer.Keys.FailedToResetNetwork), ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RemoveNetworkAsync()
        {
            if (Network == null) return;
            
            try
            {
                IsLoading = true;
                ErrorMessage = null;
                
                if (!_chainManagementService.CanRemoveChain(Network.ChainId))
                {
                    ErrorMessage = _localizer.GetString(NetworkDetailsLocalizer.Keys.CannotRemoveCoreNetwork);
                    return;
                }
                
                await _chainManagementService.RemoveCustomChainAsync(Network.ChainId);
                
                SuccessMessage = _localizer.GetString(NetworkDetailsLocalizer.Keys.NetworkRemovedSuccessfully);
                OnNavigateBack?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to remove network: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Navigation

        public void NavigateToSection(ViewSection section)
        {
            CurrentSection = section;
        }

        public async Task LoadNetworkAsync(BigInteger chainId)
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;
                
                var network = await _chainManagementService.GetChainAsync(chainId);
                if (network != null)
                {
                    await InitializeAsync(chainId);
                }
                else
                {
                    ErrorMessage = _localizer.GetString(NetworkDetailsLocalizer.Keys.NetworkNotFoundError);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = string.Format(_localizer.GetString(NetworkDetailsLocalizer.Keys.FailedToLoadNetwork), ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Helper Properties

        public string DisplayName => Network?.ChainName ?? "Unknown Network";
        public bool HasRpcEndpoints => RpcEndpoints?.Any() == true;
        public int TotalRpcCount => RpcEndpoints?.Count ?? 0;
        public int ActiveRpcCount => RpcEndpoints?.Count(r => r.IsEnabled) ?? 0;
        public bool HasExplorers => Network?.Explorers?.Any() == true;
        public List<ExplorerViewModel> Explorers => 
            Network?.Explorers?.Select(url => new ExplorerViewModel(url)).ToList() ?? new List<ExplorerViewModel>();

        #endregion

        #region Event Handlers

        private async Task OnNetworkChanged(long chainId)
        {
            OnPropertyChanged(nameof(IsActive));
        }

        private void OnRpcHealthChanged(object? sender, RpcHealthChangedEventArgs e)
        {
            if (Network == null || e.ChainId != Network.ChainId) return;
            
            var endpoint = RpcEndpoints.FirstOrDefault(ep => ep.Url == e.RpcUrl);
            if (endpoint != null)
            {
                endpoint.IsHealthy = e.IsHealthy;
                endpoint.TestResult = e.IsHealthy ? "✓ Connected" : $"✗ {e.ErrorMessage}";
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _walletHostProvider.NetworkChanged -= OnNetworkChanged;
            _rpcEndpointService.HealthChanged -= OnRpcHealthChanged;
        }

        #endregion
    }
    public partial class RpcEndpointViewModel : ObservableObject
    {
        public string Url { get; }
        public bool IsWebSocket { get; }
        
        [ObservableProperty] private bool _isEnabled = false;
        [ObservableProperty] private bool _isCustom = false;
        [ObservableProperty] private bool _isTesting = false;
        [ObservableProperty] private string? _testResult = null;
        [ObservableProperty] private bool _isHealthy = false;

        public RpcEndpointViewModel(string url, bool isWebSocket, bool isEnabled = false, bool isCustom = false)
        {
            Url = url;
            IsWebSocket = isWebSocket;
            IsEnabled = isEnabled;
            IsCustom = isCustom;
        }

        public string TypeDisplayName => IsWebSocket ? "WebSocket" : "HTTP";
        public string StatusDisplayName => IsEnabled ? "Active" : "Inactive";
        public string HealthDisplayName => IsHealthy ? "Healthy" : TestResult ?? "Unknown";
    }
    public class ExplorerViewModel
    {
        public string Url { get; }
        public string Name { get; }

        public ExplorerViewModel(string url)
        {
            Url = url;
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                Name = uri.Host.Replace("www.", "");
            }
            else
            {
                Name = "Explorer";
            }
        }
    }
}