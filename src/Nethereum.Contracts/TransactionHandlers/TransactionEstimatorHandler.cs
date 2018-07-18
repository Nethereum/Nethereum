using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.Contracts.TransactionHandlers
{
#if !DOTNET35
    public class TransactionEstimatorHandler<TFunctionMessage> :
        TransactionHandlerBase<TFunctionMessage>, 
        ITransactionEstimatorHandler<TFunctionMessage> where TFunctionMessage : FunctionMessage, new()
    {

        public TransactionEstimatorHandler(ITransactionManager transactionManager) : base(transactionManager)
        {

        }

        public Task<HexBigInteger> EstimateGasAsync(string contractAddress, TFunctionMessage functionMessage = null)
        {
            if (functionMessage == null) functionMessage = new TFunctionMessage();
            SetEncoderContractAddress(contractAddress);
            var callInput = FunctionMessageEncodingService.CreateCallInput(functionMessage);
            return TransactionManager.EstimateGasAsync(callInput);
        }
    }
#endif
}