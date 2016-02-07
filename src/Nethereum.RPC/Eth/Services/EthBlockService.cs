using edjCase.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.Blocks;

namespace Nethereum.Web3
{
    public class EthBlockService : RpcClientWrapper
    {
        public EthBlockNumber GetBlockNumber { get; private set; }
        public EthGetBlockTransactionCountByHash GetBlockTransactionCountByHash { get; private set; }
        public EthGetBlockTransactionCountByNumber GetBlockTransactionCountByNumber { get; private set; }
        public EthGetBlockWithTransactionsByHash GetBlockWithTransactionsByHash { get; private set; }
        public EthGetBlockWithTransactionsByNumber GetBlockWithTransactionsByNumber { get; private set; }
        public EthGetBlockWithTransactionsHashesByHash GetBlockWithTransactionsHashesByHash { get; private set; }
        public EthGetBlockWithTransactionsHashesByNumber GetBlockWithTransactionsHashesByNumber { get; private set; }

        public EthBlockService(RpcClient client) : base(client)
        {
            GetBlockNumber = new EthBlockNumber(client);
            GetBlockTransactionCountByHash = new EthGetBlockTransactionCountByHash(client);
            GetBlockTransactionCountByNumber = new EthGetBlockTransactionCountByNumber(client);
            GetBlockWithTransactionsByHash = new EthGetBlockWithTransactionsByHash(client);
            GetBlockWithTransactionsByNumber = new EthGetBlockWithTransactionsByNumber(client);
            GetBlockWithTransactionsHashesByHash = new EthGetBlockWithTransactionsHashesByHash(client);
            GetBlockWithTransactionsHashesByNumber = new EthGetBlockWithTransactionsHashesByNumber(client);
        }

    }
}