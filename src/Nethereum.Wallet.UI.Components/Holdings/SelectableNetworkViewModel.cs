using CommunityToolkit.Mvvm.ComponentModel;
using Nethereum.RPC.Chain;

namespace Nethereum.Wallet.UI.Components.Holdings
{
    public partial class SelectableNetworkViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected;

        public long ChainId { get; }
        public string ChainName { get; }
        public string NativeCurrencySymbol { get; }
        public bool IsTestnet { get; }
        public ChainFeature ChainFeature { get; }

        public SelectableNetworkViewModel(ChainFeature chain)
        {
            ChainFeature = chain;
            ChainId = (long)chain.ChainId;
            ChainName = chain.ChainName ?? $"Chain {chain.ChainId}";
            NativeCurrencySymbol = chain.NativeCurrency?.Symbol ?? "ETH";
            IsTestnet = chain.IsTestnet;
        }

        public string DisplayName => ChainName;
        public string SubtitleText => $"{NativeCurrencySymbol} - Chain {ChainId}";
    }
}
