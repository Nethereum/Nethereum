using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.RPC.Eth.Services
{
    public class EthApiTransactionsService : RpcClientWrapper, IEthApiTransactionsService
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

        public IEthGetTransactionByBlockHashAndIndex GetTransactionByBlockHashAndIndex { get; }
        public IEthGetTransactionByBlockNumberAndIndex GetTransactionByBlockNumberAndIndex { get; }
        public IEthGetTransactionByHash GetTransactionByHash { get; }
        public IEthGetTransactionCount GetTransactionCount { get; }
        public IEthGetTransactionReceipt GetTransactionReceipt { get; }
        public IEthSendRawTransaction SendRawTransaction { get; }
        public IEthSendTransaction SendTransaction { get; }
        public IEthCall Call { get; }
        public IEthEstimateGas EstimateGas { get; }

        public void SetDefaultBlock(BlockParameter blockParameter)
        {
            Call.DefaultBlock = blockParameter;
            GetTransactionCount.DefaultBlock = blockParameter;
        }
    }
}