using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using Nethereum.RPC.Chain;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Core.Validation;
using Nethereum.Wallet.Services.Network;
using static Nethereum.Wallet.Services.Network.NetworkInputValidator;

namespace Nethereum.Wallet.UI.Components.Networks.Models
{
    public partial class NetworkConfiguration : LocalizedValidationModel
    {
        public NetworkConfiguration(IComponentLocalizer localizer) : base(localizer)
        {
            RpcEndpoints = new ObservableCollection<RpcEndpointInfo>();
            BlockExplorers = new ObservableCollection<string>();
        }

        [ObservableProperty] private string _chainId = "";
        [ObservableProperty] private string _networkName = "";
        [ObservableProperty] private string _currencySymbol = "";
        [ObservableProperty] private string _currencyName = "";
        [ObservableProperty] private int _currencyDecimals = 18;
        [ObservableProperty] private bool _isTestnet = false;
        [ObservableProperty] private bool _supportEip155 = true;
        [ObservableProperty] private bool _supportEip1559 = true;

        [ObservableProperty] private ObservableCollection<RpcEndpointInfo> _rpcEndpoints;
        [ObservableProperty] private ObservableCollection<string> _blockExplorers;
        [ObservableProperty] private string _newRpcUrl = "";
        [ObservableProperty] private string _newExplorerUrl = "";

        public bool IsValid => !HasErrors && RpcEndpoints.Any();
        public BigInteger ChainIdValue => BigInteger.TryParse(ChainId, out var id) ? id : 0;
        public bool CanAddRpcEndpoint => !string.IsNullOrWhiteSpace(NewRpcUrl) && !HasFieldErrors(nameof(NewRpcUrl));
        public bool CanAddExplorer => !string.IsNullOrWhiteSpace(NewExplorerUrl) && !HasFieldErrors(nameof(NewExplorerUrl));

        private static string? MapError(NetworkValidationError err) => err switch
        {
            NetworkValidationError.None => null,
            NetworkValidationError.ChainIdRequired => AddCustomNetworkLocalizer.Keys.ChainIdRequired,
            NetworkValidationError.InvalidChainId => AddCustomNetworkLocalizer.Keys.InvalidChainId,
            NetworkValidationError.NetworkNameRequired => AddCustomNetworkLocalizer.Keys.NetworkNameRequired,
            NetworkValidationError.NetworkNameTooShort => AddCustomNetworkLocalizer.Keys.NetworkNameTooShort,
            NetworkValidationError.CurrencySymbolRequired => AddCustomNetworkLocalizer.Keys.CurrencySymbolRequired,
            NetworkValidationError.InvalidCurrencySymbol => AddCustomNetworkLocalizer.Keys.InvalidCurrencySymbol,
            NetworkValidationError.CurrencyNameRequired => AddCustomNetworkLocalizer.Keys.CurrencyNameRequired,
            NetworkValidationError.CurrencyNameTooShort => AddCustomNetworkLocalizer.Keys.CurrencyNameTooShort,
            NetworkValidationError.InvalidCurrencyDecimals => AddCustomNetworkLocalizer.Keys.InvalidCurrencyDecimals,
            NetworkValidationError.RpcUrlRequired => AddCustomNetworkLocalizer.Keys.RpcUrlRequired,
            NetworkValidationError.InvalidRpcUrlFormat => AddCustomNetworkLocalizer.Keys.InvalidRpcUrlFormat,
            NetworkValidationError.DuplicateRpcUrl => AddCustomNetworkLocalizer.Keys.DuplicateRpcUrl,
            NetworkValidationError.ExplorerUrlRequired => AddCustomNetworkLocalizer.Keys.ExplorerUrlRequired,
            NetworkValidationError.InvalidExplorerUrlFormat => AddCustomNetworkLocalizer.Keys.InvalidExplorerUrlFormat,
            NetworkValidationError.DuplicateExplorerUrl => AddCustomNetworkLocalizer.Keys.DuplicateExplorerUrl,
            NetworkValidationError.AtLeastOneRpcRequired => AddCustomNetworkLocalizer.Keys.AtLeastOneRpcRequired,
            _ => "Error"
        };

        #region Change handlers

        partial void OnChainIdChanged(string value)
        {
            var err = NetworkInputValidator.ValidateChainId(ChainId);
            ValidateField(nameof(ChainId), (MapError(err) != null, MapError(err)));
        }

        partial void OnNetworkNameChanged(string value)
        {
            var err = NetworkInputValidator.ValidateNetworkName(NetworkName);
            ValidateField(nameof(NetworkName), (MapError(err) != null, MapError(err)));
        }

        partial void OnCurrencySymbolChanged(string value)
        {
            var err = NetworkInputValidator.ValidateCurrencySymbol(CurrencySymbol);
            ValidateField(nameof(CurrencySymbol), (MapError(err) != null, MapError(err)));
        }

        partial void OnCurrencyNameChanged(string value)
        {
            var err = NetworkInputValidator.ValidateCurrencyName(CurrencyName);
            ValidateField(nameof(CurrencyName), (MapError(err) != null, MapError(err)));
        }

        partial void OnCurrencyDecimalsChanged(int value)
        {
            var err = NetworkInputValidator.ValidateCurrencyDecimals(CurrencyDecimals);
            ValidateField(nameof(CurrencyDecimals), (MapError(err) != null, MapError(err)));
        }

        partial void OnNewRpcUrlChanged(string value)
        {
            string? key = null;
            if (!string.IsNullOrWhiteSpace(NewRpcUrl))
            {
                var err = NetworkInputValidator.ValidateRpcUrl(NewRpcUrl);
                if (err != NetworkValidationError.None)
                    key = MapError(err);
                else if (RpcEndpoints.Any(r => r.Url.Equals(NewRpcUrl, StringComparison.OrdinalIgnoreCase)))
                    key = AddCustomNetworkLocalizer.Keys.DuplicateRpcUrl;
            }
            ValidateField(nameof(NewRpcUrl), (key != null, key));
        }

        partial void OnNewExplorerUrlChanged(string value)
        {
            string? key = null;
            if (!string.IsNullOrWhiteSpace(NewExplorerUrl))
            {
                var err = NetworkInputValidator.ValidateExplorerUrl(NewExplorerUrl);
                if (err != NetworkValidationError.None)
                    key = MapError(err);
                else if (BlockExplorers.Any(e => e.Equals(NewExplorerUrl, StringComparison.OrdinalIgnoreCase)))
                    key = AddCustomNetworkLocalizer.Keys.DuplicateExplorerUrl;
            }
            ValidateField(nameof(NewExplorerUrl), (key != null, key));
        }

        #endregion

        public void AddRpcEndpoint()
        {
            if (!CanAddRpcEndpoint) return;
            var isWebSocket = NewRpcUrl.StartsWith("ws", StringComparison.OrdinalIgnoreCase);
            RpcEndpoints.Add(new RpcEndpointInfo(NewRpcUrl, isWebSocket) { IsCustom = true });
            NewRpcUrl = "";
            ValidateRpcEndpoints();
        }

        public void RemoveRpcEndpoint(RpcEndpointInfo endpoint)
        {
            RpcEndpoints.Remove(endpoint);
            ValidateRpcEndpoints();
        }

        public void AddBlockExplorer()
        {
            if (!CanAddExplorer) return;
            BlockExplorers.Add(NewExplorerUrl);
            NewExplorerUrl = "";
        }

        public void RemoveBlockExplorer(string explorer) => BlockExplorers.Remove(explorer);

        private void ValidateRpcEndpoints()
        {
            var key = !RpcEndpoints.Any()
                ? AddCustomNetworkLocalizer.Keys.AtLeastOneRpcRequired
                : null;
            SetFieldError("RpcEndpoints", key);
        }

        public void ValidateAll()
        {
            OnChainIdChanged(ChainId);
            OnNetworkNameChanged(NetworkName);
            OnCurrencySymbolChanged(CurrencySymbol);
            OnCurrencyNameChanged(CurrencyName);
            OnCurrencyDecimalsChanged(CurrencyDecimals);
            OnNewRpcUrlChanged(NewRpcUrl);
            OnNewExplorerUrlChanged(NewExplorerUrl);
            ValidateRpcEndpoints();
        }

        public string? ValidateExplorerUrl(string explorerUrl)
        {
            var err = NetworkInputValidator.ValidateExplorerUrl(explorerUrl);
            if (err != NetworkValidationError.None)
                return GetLocalizedString(MapError(err)!);
            return null;
        }

        public bool UpdateExplorerUrl(int index, string newUrl)
        {
            if (index < 0 || index >= BlockExplorers.Count) return false;

            var err = NetworkInputValidator.ValidateExplorerUrl(newUrl);
            if (err != NetworkValidationError.None)
            {
                SetFieldError($"Explorer_{index}", MapError(err));
                BlockExplorers[index] = newUrl;
                return false;
            }

            for (int i = 0; i < BlockExplorers.Count; i++)
            {
                if (i != index && string.Equals(BlockExplorers[i], newUrl, StringComparison.OrdinalIgnoreCase))
                {
                    SetFieldError($"Explorer_{index}", AddCustomNetworkLocalizer.Keys.DuplicateExplorerUrl);
                    BlockExplorers[index] = newUrl;
                    return false;
                }
            }

            SetFieldError($"Explorer_{index}", null);
            BlockExplorers[index] = newUrl;
            return true;
        }

        public string? GetExplorerError(int index) => GetFieldError($"Explorer_{index}");
        public bool HasExplorerError(int index) => HasFieldErrors($"Explorer_{index}");

        public void Reset()
        {
            ChainId = "";
            NetworkName = "";
            CurrencySymbol = "";
            CurrencyName = "";
            CurrencyDecimals = 18;
            IsTestnet = false;
            SupportEip155 = true;
            SupportEip1559 = true;
            RpcEndpoints.Clear();
            BlockExplorers.Clear();
            NewRpcUrl = "";
            NewExplorerUrl = "";
            ClearErrors();
        }

        public void LoadFromChainFeature(ChainFeature chainFeature)
        {
            RpcEndpoints.Clear();
            BlockExplorers.Clear();

            ChainId = chainFeature.ChainId.ToString();
            NetworkName = chainFeature.ChainName ?? $"Chain {chainFeature.ChainId}";
            CurrencySymbol = chainFeature.NativeCurrency?.Symbol ?? "";
            CurrencyName = chainFeature.NativeCurrency?.Name ?? "";
            CurrencyDecimals = chainFeature.NativeCurrency?.Decimals ?? 18;
            IsTestnet = chainFeature.IsTestnet;
            SupportEip155 = chainFeature.SupportEIP155;
            SupportEip1559 = chainFeature.SupportEIP1559;

            if (chainFeature.HttpRpcs != null)
                foreach (var rpc in chainFeature.HttpRpcs)
                    RpcEndpoints.Add(new RpcEndpointInfo(rpc, false));

            if (chainFeature.WsRpcs != null)
                foreach (var rpc in chainFeature.WsRpcs)
                    RpcEndpoints.Add(new RpcEndpointInfo(rpc, true));

            if (chainFeature.Explorers != null)
                foreach (var explorer in chainFeature.Explorers)
                    BlockExplorers.Add(explorer);

            ValidateAll();
        }

        public ChainFeature ToChainFeature()
        {
            var httpRpcs = RpcEndpoints.Where(r => !r.IsWebSocket).Select(r => r.Url).ToList();
            var wsRpcs = RpcEndpoints.Where(r => r.IsWebSocket).Select(r => r.Url).ToList();

            return new ChainFeature
            {
                ChainId = ChainIdValue,
                ChainName = NetworkName,
                NativeCurrency = new NativeCurrency
                {
                    Name = CurrencyName,
                    Symbol = string.IsNullOrWhiteSpace(CurrencySymbol) ? CurrencySymbol : CurrencySymbol.ToUpperInvariant(),
                    Decimals = CurrencyDecimals
                },
                HttpRpcs = httpRpcs,
                WsRpcs = wsRpcs,
                Explorers = BlockExplorers.ToList(),
                SupportEIP155 = SupportEip155,
                SupportEIP1559 = SupportEip1559,
                IsTestnet = IsTestnet
            };
        }
    }
}