using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.CQS
{

    public class ContractTransactionHandler<TContractMessage> : ContractHandlerBase<TContractMessage>
        where TContractMessage : ContractMessage
    {
        public TransactionInput CreateTransactionInput(TContractMessage functionMessage,
            string contractAddress)
        {
            ValidateContractMessage(functionMessage);
            var function = GetFunction(contractAddress);
            var transactionInput = function.CreateTransactionInput(
                functionMessage,
                GetDefaultAddressFrom(functionMessage),
                GetMaximumGas(functionMessage),
                GetGasPrice(functionMessage),
                GetValue(functionMessage));
            transactionInput.Nonce = GetNonce(functionMessage);
            return transactionInput;
        }

        public string GetData(TContractMessage functionMessage, string contractAddress)
        {
            ValidateContractMessage(functionMessage);
            var function = GetFunction(contractAddress);
            return function.GetData(functionMessage);
        }

        public TContractMessage DecodeInput(TContractMessage functionMessage, TransactionInput transactionInput, string contractAddress)
        {
            ValidateContractMessage(functionMessage);
            var function = GetFunction(contractAddress);
            return function.DecodeFunctionInput(functionMessage, transactionInput);
        }

#if !DOTNET35

        public async Task<string> SignTransactionAsync(TContractMessage functionMessage,
            string contractAddress, bool estimateGas = true)
        {
            TransactionInput transactionInput = null;
            if (estimateGas)
                transactionInput = await CreateTransactionInputEstimatingGasAsync(functionMessage, contractAddress).ConfigureAwait(false);
            else
                transactionInput = CreateTransactionInput(functionMessage, contractAddress);
            return await this.Eth.TransactionManager.SignTransactionAsync(transactionInput).ConfigureAwait(false);
        }

        public async Task<string> SignTransactionRetrievingNextNonceAsync(TContractMessage functionMessage,
            string contractAddress, bool estimateGas = true)
        {
            TransactionInput transactionInput = null;
            if (estimateGas)
                transactionInput = await CreateTransactionInputEstimatingGasAsync(functionMessage, contractAddress).ConfigureAwait(false);
            else
                transactionInput = CreateTransactionInput(functionMessage, contractAddress);
            return await this.Eth.TransactionManager.SignTransactionRetrievingNextNonceAsync(transactionInput).ConfigureAwait(false);
        }

        public async Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(TContractMessage functionMessage,
            string contractAddress, CancellationTokenSource tokenSource = null)
        {
            ValidateContractMessage(functionMessage);
            var function = GetFunction(contractAddress);

            var transactionInput = await CreateTransactionInputEstimatingGasAsync(functionMessage, contractAddress).ConfigureAwait(false);
            return await function.SendTransactionAndWaitForReceiptAsync(functionMessage, transactionInput, tokenSource).ConfigureAwait(false);
        }

        public async Task<string> SendRequestAsync(TContractMessage functionMessage, string contractAddress)
        {
            ValidateContractMessage(functionMessage);
            var function = GetFunction(contractAddress);

            var transactionInput = await CreateTransactionInputEstimatingGasAsync(functionMessage, contractAddress).ConfigureAwait(false);
            return await function.SendTransactionAsync(functionMessage, transactionInput).ConfigureAwait(false); ;
        }

        public async Task<TransactionInput> CreateTransactionInputEstimatingGasAsync(TContractMessage functionMessage,
            string contractAddress)
        {
            ValidateContractMessage(functionMessage);
            var function = GetFunction(contractAddress);

            var gasEstimate = await GetOrEstimateMaximumGas(functionMessage, function).ConfigureAwait(false);
            functionMessage.Gas = gasEstimate;
            return CreateTransactionInput(functionMessage, contractAddress);
            
        }

        protected virtual async Task<HexBigInteger> GetOrEstimateMaximumGas(TContractMessage functionMessage,
            Function<TContractMessage> function)
        {
            var maxGas = GetMaximumGas(functionMessage) ??
                         await EstimateGasAsync(functionMessage, function).ConfigureAwait(false);
            return maxGas;
        }

        public Task<HexBigInteger> EstimateGasAsync(TContractMessage functionMessage, string contractAddress)
        {
            ValidateContractMessage(functionMessage);
            var contract = Eth.GetContract<TContractMessage>(contractAddress);
            var function = contract.GetFunction<TContractMessage>();
            return EstimateGasAsync(functionMessage, function);
        }

        protected Task<HexBigInteger> EstimateGasAsync(TContractMessage functionMessage,
            Function<TContractMessage> function)
        {
            return function.EstimateGasAsync(functionMessage, GetDefaultAddressFrom(functionMessage), null,
                GetValue(functionMessage));
        }
#endif
    }

}