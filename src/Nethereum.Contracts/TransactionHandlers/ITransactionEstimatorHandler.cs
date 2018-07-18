using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;

namespace Nethereum.Contracts.TransactionHandlers
{
    public interface ITransactionEstimatorHandler<TFunctionMessage> where TFunctionMessage : FunctionMessage, new()
    {
        Task<HexBigInteger> EstimateGasAsync(string contractAddress, TFunctionMessage functionMessage = null);
    }
}