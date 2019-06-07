using Nethereum.Rsk.RPC.RskEth;

namespace Nethereum.Rsk
{
    public interface IRskEthApiService
    {
        IRskEthGetBlockWithTransactionsByHash GetBlockWithTransactionsByHash { get; }
        IRskEthGetBlockWithTransactionsByNumber GetBlockWithTransactionsByNumber { get; }
        IRskEthGetBlockWithTransactionsHashesByHash GetBlockWithTransactionsHashesByHash { get; }
        IRskEthGetBlockWithTransactionsHashesByNumber GetBlockWithTransactionsHashesByNumber { get; }
    }
}