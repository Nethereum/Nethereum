using Nethereum.Geth.RPC.GethEth;

namespace Nethereum.Geth
{
    public interface IGethEthApiService
    {
        IEthPendingTransactions PendingTransactions { get; }
    }
}