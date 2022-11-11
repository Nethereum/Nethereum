using Nethereum.Parity.RPC.Network;

namespace Nethereum.Parity
{
    public interface INetworkApiService
    {
        IParityChainStatus ChainStatus { get; }
        IParityGasPriceHistogram GasPriceHistogram { get; }
        IParityNetPeers NetPeers { get; }
        IParityNetPort NetPort { get; }
        IParityPendingTransactions PendingTransactions { get; }
    }
}