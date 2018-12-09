using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.RPC.Eth.Services
{
    public interface IEthApiTransactionsService
    {
        IEthCall Call { get; }
        IEthEstimateGas EstimateGas { get; }
        IEthGetTransactionByBlockHashAndIndex GetTransactionByBlockHashAndIndex { get; }
        IEthGetTransactionByBlockNumberAndIndex GetTransactionByBlockNumberAndIndex { get; }
        IEthGetTransactionByHash GetTransactionByHash { get; }
        IEthGetTransactionCount GetTransactionCount { get; }
        IEthGetTransactionReceipt GetTransactionReceipt { get; }
        IEthSendRawTransaction SendRawTransaction { get; }
        IEthSendTransaction SendTransaction { get; }

        void SetDefaultBlock(BlockParameter blockParameter);
    }
}