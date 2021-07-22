using System.Threading.Tasks;
using Nethereum.Contracts.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.Contracts.TransactionHandlers
{
#if !DOTNET35
    public class TransactionSenderHandler<TFunctionMessage> :
        TransactionHandlerBase<TFunctionMessage>, 
        ITransactionSenderHandler<TFunctionMessage> where TFunctionMessage : FunctionMessage, new()
    {
        private readonly ITransactionEstimatorHandler<TFunctionMessage> _contractTransactionEstimatorHandler;

        public TransactionSenderHandler(ITransactionManager transactionManager) : this(transactionManager,
            new TransactionEstimatorHandler<TFunctionMessage>(transactionManager))
        {

        }

        public TransactionSenderHandler(ITransactionManager transactionManager,
            ITransactionEstimatorHandler<TFunctionMessage> contractTransactionEstimatorHandler) : base(transactionManager)
        {
            _contractTransactionEstimatorHandler = contractTransactionEstimatorHandler;
        }

        public async Task<string> SendTransactionAsync(string contractAddress, TFunctionMessage functionMessage = null)
        {
            if (functionMessage == null) functionMessage = new TFunctionMessage();
            SetEncoderContractAddress(contractAddress);
            functionMessage.Gas = await GetOrEstimateMaximumGasAsync(functionMessage, contractAddress).ConfigureAwait(false);
            var transactionInput = FunctionMessageEncodingService.CreateTransactionInput(functionMessage);
            return await TransactionManager.SendTransactionAsync(transactionInput).ConfigureAwait(false);
        }

        protected virtual async Task<HexBigInteger> GetOrEstimateMaximumGasAsync(
            TFunctionMessage functionMessage, string contractAddress)
        {
            return functionMessage.GetHexMaximumGas()
                   ?? await _contractTransactionEstimatorHandler.EstimateGasAsync(contractAddress, functionMessage).ConfigureAwait(false);
        }
    }
#endif
}
