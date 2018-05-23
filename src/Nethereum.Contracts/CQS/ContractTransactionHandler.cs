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
            return function.CreateTransactionInput(
                functionMessage,
                GetDefaultAddressFrom(functionMessage),
                GetMaximumGas(functionMessage),
                GetGasPrice(functionMessage),
                GetValue(functionMessage));
        }

        public string GetData(TContractMessage functionMessage, string contractAddress)
        {
            ValidateContractMessage(functionMessage);
            var function = GetFunction(contractAddress);
            return function.GetData(functionMessage);
        }

#if !DOTNET35
        public async Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(TContractMessage functionMessage,
            string contractAddress, CancellationTokenSource tokenSource = null)
        {
            ValidateContractMessage(functionMessage);
            var function = GetFunction(contractAddress);

            var gasEstimate = await GetOrEstimateMaximumGas(functionMessage, function).ConfigureAwait(false);
            return await ExecuteTransactionAndWaitForReceiptAsync(functionMessage, gasEstimate, function, tokenSource)
                .ConfigureAwait(false);
        }

        public async Task<string> SendRequestAsync(TContractMessage functionMessage, string contractAddress)
        {
            ValidateContractMessage(functionMessage);
            var function = GetFunction(contractAddress);

            var gasEstimate = await GetOrEstimateMaximumGas(functionMessage, function).ConfigureAwait(false);
            return await ExecuteTransactionAsync(functionMessage, gasEstimate, function).ConfigureAwait(false);
        }

        public async Task<TransactionInput> CreateTransactionInputEstimatingGasAsync(TContractMessage functionMessage,
            string contractAddress)
        {
            ValidateContractMessage(functionMessage);
            var function = GetFunction(contractAddress);

            var gasEstimate = await GetOrEstimateMaximumGas(functionMessage, function).ConfigureAwait(false);

            return function.CreateTransactionInput(
                functionMessage,
                GetDefaultAddressFrom(functionMessage),
                gasEstimate,
                GetGasPrice(functionMessage),
                GetValue(functionMessage));
        }

        private Function<TContractMessage> GetFunction(string contractAddress)
        {
            var contract = Eth.GetContract<TContractMessage>(contractAddress);
            var function = contract.GetFunction<TContractMessage>();
            return function;
        }

        protected virtual async Task<HexBigInteger> GetOrEstimateMaximumGas(TContractMessage functionMessage,
            Function<TContractMessage> function)
        {
            var maxGas = GetMaximumGas(functionMessage) ??
                         await EstimateGasAsync(functionMessage, function).ConfigureAwait(false);
            return maxGas;
        }

        protected Task<string> ExecuteTransactionAsync(TContractMessage functionMessage, HexBigInteger gasEstimate,
            Function<TContractMessage> function, CancellationTokenSource tokenSource = null)
        {
            return function.SendTransactionAsync(
                functionMessage,
                GetDefaultAddressFrom(functionMessage),
                gasEstimate,
                GetGasPrice(functionMessage),
                GetValue(functionMessage));
        }

        protected Task<TransactionReceipt> ExecuteTransactionAndWaitForReceiptAsync(TContractMessage functionMessage,
            HexBigInteger gasEstimate, Function<TContractMessage> function, CancellationTokenSource tokenSource = null)
        {
            return function.SendTransactionAndWaitForReceiptAsync(
                functionMessage,
                GetDefaultAddressFrom(functionMessage),
                gasEstimate,
                GetGasPrice(functionMessage),
                GetValue(functionMessage),
                tokenSource);
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