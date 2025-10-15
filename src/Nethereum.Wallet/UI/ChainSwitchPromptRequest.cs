using System.Numerics;
using Nethereum.RPC.Chain;

namespace Nethereum.Wallet.UI
{
    public sealed class ChainSwitchPromptRequest
    {
        public BigInteger ChainId { get; set; }
        public ChainFeature? Chain { get; set; }
        public bool IsKnown { get; set; }
        public bool AllowAdd { get; set; } = true;
        public string? Origin { get; init; }
        public string? DappName { get; init; }
        public string? DappIcon { get; init; }
        public long? CurrentChainId { get; set; }
        public ChainFeature? CurrentChain { get; set; }
    }
}
