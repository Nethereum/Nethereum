using Nethereum.Parity.RPC.Network;

namespace Nethereum.Parity
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.ClientExtensions, "parity-client", "INetworkApiService")]
    public interface INetworkApiService
    {
        IParityChainStatus ChainStatus { get; }
        IParityGasPriceHistogram GasPriceHistogram { get; }
        IParityNetPeers NetPeers { get; }
        IParityNetPort NetPort { get; }
        IParityPendingTransactions PendingTransactions { get; }
    }
}