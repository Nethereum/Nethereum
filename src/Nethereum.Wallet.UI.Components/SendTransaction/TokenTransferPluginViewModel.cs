using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Dashboard;

namespace Nethereum.Wallet.UI.Components.SendTransaction
{
    public partial class TokenTransferPluginViewModel : ObservableObject, IDashboardPluginViewModel, INavigatablePlugin
    {
        private readonly IComponentLocalizer<SendNativeTokenViewModel> _localizer;

        [ObservableProperty] private string _preSelectedAccountAddress;
        [ObservableProperty] private long? _preSelectedChainId;
        [ObservableProperty] private string _preSelectedTokenContract;
        [ObservableProperty] private string _preSelectedTokenSymbol;

        public TokenTransferPluginViewModel(IComponentLocalizer<SendNativeTokenViewModel> localizer)
        {
            _localizer = localizer;
        }

        public string PluginId => "token-transfer";

        public string DisplayName => _localizer.GetString(SendNativeTokenLocalizer.Keys.PluginName);

        public string Description => _localizer.GetString(SendNativeTokenLocalizer.Keys.PluginDescription);

        public string Icon => "send";

        public int SortOrder => 10;

        public bool IsVisible => true;

        public bool IsEnabled => true;

        public bool IsAvailable() => true;

        public void ClearPreSelection()
        {
            PreSelectedAccountAddress = null;
            PreSelectedChainId = null;
            PreSelectedTokenContract = null;
            PreSelectedTokenSymbol = null;
        }

        public void SetPreSelection(string accountAddress, long? chainId, string tokenContract)
        {
            PreSelectedAccountAddress = accountAddress;
            PreSelectedChainId = chainId;
            PreSelectedTokenContract = tokenContract;
        }

        public Task NavigateWithParametersAsync(Dictionary<string, object> parameters)
        {
            if (parameters.TryGetValue("TokenContract", out var contract))
                PreSelectedTokenContract = contract as string;
            if (parameters.TryGetValue("TokenSymbol", out var symbol))
                PreSelectedTokenSymbol = symbol as string;
            if (parameters.TryGetValue("ChainId", out var chainId))
            {
                if (chainId is long cid)
                    PreSelectedChainId = cid;
                else if (chainId is int intCid)
                    PreSelectedChainId = intCid;
            }
            if (parameters.TryGetValue("AccountAddress", out var account))
                PreSelectedAccountAddress = account as string;
            return Task.CompletedTask;
        }
    }
}
