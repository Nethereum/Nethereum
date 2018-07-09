using System.Threading.Tasks;
using Nethereum.Contracts.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Accounts;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.Contracts.CQS
{
#if !DOTNET35
    /// <summary>
    /// Signs a transaction estimating the gas if not set and retrieving the next nonce if not set
    /// </summary>
    public class TransactionSigner<TFunctionMessage> :
        TransactionHandlerBase<TFunctionMessage>,
        ITransactionSigner<TFunctionMessage>
        where TFunctionMessage : FunctionMessage, new()
    {
        private ITransactionEstimatorHandler<TFunctionMessage> _contractTransactionEstimatorHandler;

        public TransactionSigner(IClient client, IAccount account) : this(client, account,
            new TransactionEstimatorHandler<TFunctionMessage>(client, account))
        {

        }

        public TransactionSigner(IClient client, IAccount account,
            ITransactionEstimatorHandler<TFunctionMessage> contractTransactionEstimatorHandler) : base(client, account)
        {
            _contractTransactionEstimatorHandler = contractTransactionEstimatorHandler;
        }

        public TransactionSigner(ITransactionManager transactionManager) : this(transactionManager,
            new TransactionEstimatorHandler<TFunctionMessage>(transactionManager))
        {

        }

        public TransactionSigner(ITransactionManager transactionManager,
            ITransactionEstimatorHandler<TFunctionMessage> contractTransactionEstimatorHandler) : base(transactionManager)
        {
            _contractTransactionEstimatorHandler = contractTransactionEstimatorHandler;
        }



        public async Task<string> SignTransactionAsync(string contractAddress, TFunctionMessage functionMessage = null)
        {
            if(functionMessage == null) functionMessage = new TFunctionMessage();
            SetEncoderContractAddress(contractAddress);
            functionMessage.Gas = await GetOrEstimateMaximumGasAsync(functionMessage, contractAddress).ConfigureAwait(false);
            var transactionInput = FunctionMessageEncodingService.CreateTransactionInput(functionMessage);
            return await TransactionManager.SignTransactionRetrievingNextNonceAsync(transactionInput).ConfigureAwait(false);
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