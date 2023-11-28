using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Blocks;

namespace Nethereum.RPC.Eth.Services
{
    public class EthApiBlockService : RpcClientWrapper, IEthApiBlockService
    {
        public EthApiBlockService(IClient client) : base(client)
        {
            GetBlockNumber = new EthBlockNumber(client);
            GetBlockTransactionCountByHash = new EthGetBlockTransactionCountByHash(client);
            GetBlockTransactionCountByNumber = new EthGetBlockTransactionCountByNumber(client);
            GetBlockWithTransactionsByHash = new EthGetBlockWithTransactionsByHash(client);
            GetBlockWithTransactionsByNumber = new EthGetBlockWithTransactionsByNumber(client);
            GetBlockWithTransactionsHashesByHash = new EthGetBlockWithTransactionsHashesByHash(client);
            GetBlockWithTransactionsHashesByNumber = new EthGetBlockWithTransactionsHashesByNumber(client);
            GetBlockReceiptsByNumber = new EthGetBlockReceiptsByNumber(client);
        }

        public IEthBlockNumber GetBlockNumber { get; private set; }
        public IEthGetBlockTransactionCountByHash GetBlockTransactionCountByHash { get; private set; }
        public IEthGetBlockTransactionCountByNumber GetBlockTransactionCountByNumber { get; private set; }
        public IEthGetBlockWithTransactionsByHash GetBlockWithTransactionsByHash { get; private set; }
        
        public IEthGetBlockWithTransactionsByNumber GetBlockWithTransactionsByNumber { get; private set; }
        public IEthGetBlockWithTransactionsHashesByHash GetBlockWithTransactionsHashesByHash { get; private set; }
        public IEthGetBlockWithTransactionsHashesByNumber GetBlockWithTransactionsHashesByNumber { get; private set; }
        public IEthGetBlockReceiptsByNumber GetBlockReceiptsByNumber { get; private set; }
    }
}