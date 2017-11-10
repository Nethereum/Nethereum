using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.Contracts.CQS
{

#if !DOTNET35
    public class ContractTransactionHandler<TContractMessage> : ContractHandlerBase<TContractMessage> where TContractMessage : ContractMessage
    {
        public async Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(TContractMessage functionMessage, string contractAddress, CancellationTokenSource tokenSource = null)
        {
            ValidateContractMessage(functionMessage);
            var contract = Eth.GetContract<TContractMessage>(contractAddress);
            var function = contract.GetFunction<TContractMessage>();

            var gasEstimate = await GetOrEstimateMaximumGas(functionMessage, function).ConfigureAwait(false);
            return await ExecuteTransactionAsync(functionMessage, gasEstimate, function, tokenSource);
        }

        protected virtual async Task<HexBigInteger> GetOrEstimateMaximumGas(TContractMessage functionMessage, Function<TContractMessage> function)
        {
            var maxGas = GetMaximumGas(functionMessage);

            if (maxGas == null)
            {
                maxGas = await EstimateGasAsync(functionMessage, function).ConfigureAwait(false);
            }

            return maxGas;
        }

        protected Task<TransactionReceipt> ExecuteTransactionAsync(TContractMessage functionMessage, HexBigInteger gasEstimate, Function<TContractMessage> function, CancellationTokenSource tokenSource = null)
        {
            return function.SendTransactionAndWaitForReceiptAsync(
                                            functionMessage,
                                            functionMessage.FromAddress,
                                            gasEstimate,
                                            GetGasPrice(functionMessage),
                                            GetValue(functionMessage),
                                            tokenSource);
        }

        protected Task<HexBigInteger> EstimateGasAsync(TContractMessage functionMessage, Function<TContractMessage> function)
        {
            return function.EstimateGasAsync(functionMessage, functionMessage.FromAddress, null, GetValue(functionMessage));
        }
    }
#endif
}
