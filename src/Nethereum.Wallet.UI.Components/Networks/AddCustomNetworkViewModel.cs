using System;
using System.Numerics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Networks.Models;

namespace Nethereum.Wallet.UI.Components.Networks
{
    public partial class AddCustomNetworkViewModel : ObservableObject, IDisposable
    {
        private readonly IChainManagementService _chainManagementService;
        private readonly IRpcEndpointService _rpcEndpointService;
        
        [ObservableProperty] private NetworkConfiguration _network;
        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private string? _errorMessage;
        [ObservableProperty] private string? _successMessage;
        
        public Action<BigInteger>? OnNetworkAdded { get; set; }
        public Action? OnCancel { get; set; }
        
        public bool IsFormValid => _network.IsValid;

        public AddCustomNetworkViewModel(
            IChainManagementService chainManagementService,
            IRpcEndpointService rpcEndpointService,
            IComponentLocalizer<AddCustomNetworkViewModel> localizer)
        {
            _chainManagementService = chainManagementService;
            _rpcEndpointService = rpcEndpointService;
            _network = new NetworkConfiguration(localizer);
        }

        [RelayCommand]
        public async Task SaveNetworkAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;
                SuccessMessage = null;

                _network.ValidateAll();
                
                if (!IsFormValid)
                {
                    ErrorMessage = _network.GetLocalizedString(AddCustomNetworkLocalizer.Keys.FormValidationFailed);
                    return;
                }

                if (await ChainIdExistsAsync(_network.ChainIdValue))
                {
                    _network.SetFieldError(nameof(_network.ChainId), AddCustomNetworkLocalizer.Keys.NetworkAlreadyExists);
                    ErrorMessage = _network.GetLocalizedString(AddCustomNetworkLocalizer.Keys.NetworkAlreadyExists);
                    return;
                }

                var chainFeature = _network.ToChainFeature();
                await _chainManagementService.AddCustomChainAsync(chainFeature);
                
                SuccessMessage = _network.GetLocalizedString(AddCustomNetworkLocalizer.Keys.NetworkAddedSuccessfully);
                OnNetworkAdded?.Invoke(chainFeature.ChainId);
            }
            catch (Exception ex)
            {
                ErrorMessage = string.Format(_network.GetLocalizedString(AddCustomNetworkLocalizer.Keys.FailedToAddNetwork), ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public void Cancel()
        {
            OnCancel?.Invoke();
        }

        [RelayCommand]
        public void Reset()
        {
            _network.Reset();
            ErrorMessage = null;
            SuccessMessage = null;
        }

        [RelayCommand]
        public void AddRpcEndpoint()
        {
            _network.AddRpcEndpoint();
        }

        [RelayCommand]
        public void RemoveRpcEndpoint(RpcEndpointInfo endpoint)
        {
            _network.RemoveRpcEndpoint(endpoint);
        }

        [RelayCommand]
        public void AddBlockExplorer()
        {
            _network.AddBlockExplorer();
        }

        [RelayCommand]
        public void RemoveBlockExplorer(string explorer)
        {
            _network.RemoveBlockExplorer(explorer);
        }

        [RelayCommand]
        public async Task TestRpcEndpointAsync(RpcEndpointInfo endpoint)
        {
            if (endpoint == null || _network.ChainIdValue <= 0) return;

            try
            {
                endpoint.IsTesting = true;
                endpoint.TestResult = _network.GetLocalizedString(AddCustomNetworkLocalizer.Keys.Testing);
                
                var isHealthy = await _rpcEndpointService.CheckHealthAsync(endpoint.Url, _network.ChainIdValue);
                
                endpoint.TestResult = isHealthy 
                    ? _network.GetLocalizedString(AddCustomNetworkLocalizer.Keys.TestSuccess)
                    : _network.GetLocalizedString(AddCustomNetworkLocalizer.Keys.TestFailed);
                endpoint.IsHealthy = isHealthy;
            }
            catch (Exception ex)
            {
                endpoint.TestResult = string.Format(_network.GetLocalizedString(AddCustomNetworkLocalizer.Keys.TestError), ex.Message);
                endpoint.IsHealthy = false;
            }
            finally
            {
                endpoint.IsTesting = false;
            }
        }

        private async Task<bool> ChainIdExistsAsync(BigInteger chainId)
        {
            try
            {
                var existingNetwork = await _chainManagementService.GetChainAsync(chainId);
                return existingNetwork != null;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
        }
    }
}