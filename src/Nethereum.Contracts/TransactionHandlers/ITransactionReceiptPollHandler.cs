using System.Threading;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.TransactionHandlers
{
    public interface ITransactionReceiptPollHandler<TFunctionMessage> where TFunctionMessage : FunctionMessage, new()
    {
        Task<TransactionReceipt> SendTransactionAsync(string contractAddress, TFunctionMessage functionMessage = null, CancellationTokenSource cancellationTokenSource = null);
    }
}