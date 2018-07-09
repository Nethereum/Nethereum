using System.Threading.Tasks;

namespace Nethereum.Contracts.CQS
{
    public interface ITransactionSigner<TFunctionMessage> where TFunctionMessage : FunctionMessage, new()
    {
        Task<string> SignTransactionAsync(string contractAddress, TFunctionMessage functionMessage = null);
    }
}