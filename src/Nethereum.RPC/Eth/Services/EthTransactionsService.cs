using edjCase.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.Web3
{
    public class EthTransactionsService : RpcClientWrapper
    {
        public EthGetTransactionByBlockHashAndIndex GetTransactionByBlockHashAndIndex { get; private set; }
        public EthGetTransactionByBlockNumberAndIndex GetTransactionByBlockNumberAndIndex { get; private set; }
        public EthGetTransactionByHash GetTransactionByHash { get; private set; }
        public EthGetTransactionCount GetTransactionCount { get; private set; }
        public EthGetTransactionReceipt GetTransactionReceipt { get; private set; }
        public EthSendRawTransaction SendRawTransaction { get; private set; }
        public EthSendTransaction SendTransaction { get; private set; }
        public EthCall Call { get; private set; }
        public EthEstimateGas EstimateGas { get; private set; }

        public void SetDefaultBlock(BlockParameter blockParameter)
        {
            this.Call.DefaultBlock = blockParameter;
            this.GetTransactionCount.DefaultBlock = blockParameter;
        }

        public EthTransactionsService(RpcClient client) : base(client)
        {
            Call = new EthCall(client);
            EstimateGas = new EthEstimateGas(client);
            GetTransactionByBlockHashAndIndex = new EthGetTransactionByBlockHashAndIndex(client);
            GetTransactionByBlockNumberAndIndex = new EthGetTransactionByBlockNumberAndIndex(client);
            GetTransactionByHash = new EthGetTransactionByHash(client);
            GetTransactionCount = new EthGetTransactionCount(client);
            GetTransactionReceipt = new EthGetTransactionReceipt(client);
            SendRawTransaction = new EthSendRawTransaction(client);
            SendTransaction = new EthSendTransaction(client);
        }
    }
}