using Nethereum.RPC.HostWallet;

namespace Nethereum.Wallet.UI
{
    public sealed class ChainAdditionPromptRequest
    {
        public AddEthereumChainParameter Parameter { get; init; } = new();
        public bool SwitchAfterAdd { get; init; } = true;
        public string? Origin { get; init; }
        public string? DappName { get; init; }
        public string? DappIcon { get; init; }
    }
}
