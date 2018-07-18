using System.Threading.Tasks;

namespace Nethereum.Contracts.TransactionHandlers
{
    public interface ITransactionSenderHandler<TFunctionMessage> where TFunctionMessage : FunctionMessage, new()
    {
        Task<string> SendTransactionAsync(string contractAddress, TFunctionMessage functionMessage = null);
    }
}