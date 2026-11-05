using Nethereum.Rsk.RPC.RskEth;

namespace Nethereum.Rsk
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.ClientExtensions, "rsk-client", "IRskEthApiService")]
    public interface IRskEthApiService
    {
        IRskEthGetBlockWithTransactionsByHash GetBlockWithTransactionsByHash { get; }
        IRskEthGetBlockWithTransactionsByNumber GetBlockWithTransactionsByNumber { get; }
        IRskEthGetBlockWithTransactionsHashesByHash GetBlockWithTransactionsHashesByHash { get; }
        IRskEthGetBlockWithTransactionsHashesByNumber GetBlockWithTransactionsHashesByNumber { get; }
    }
}