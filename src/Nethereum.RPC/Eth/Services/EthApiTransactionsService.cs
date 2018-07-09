using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.RPC.Eth.Services
{
    public class EthApiTransactionsService : RpcClientWrapper
    {
        public EthApiTransactionsService(IClient client) : base(client)
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

        public EthGetTransactionByBlockHashAndIndex GetTransactionByBlockHashAndIndex { get; }
        public EthGetTransactionByBlockNumberAndIndex GetTransactionByBlockNumberAndIndex { get; }
        public EthGetTransactionByHash GetTransactionByHash { get; }
        public EthGetTransactionCount GetTransactionCount { get; }
        public EthGetTransactionReceipt GetTransactionReceipt { get; }
        public EthSendRawTransaction SendRawTransaction { get; }
        public EthSendTransaction SendTransaction { get; }
        public EthCall Call { get; }
        public EthEstimateGas EstimateGas { get; }

        public void SetDefaultBlock(BlockParameter blockParameter)
        {
            Call.DefaultBlock = blockParameter;
            GetTransactionCount.DefaultBlock = blockParameter;
        }
    }
}