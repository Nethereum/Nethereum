using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.Contracts.CQS
{
#if !DOTNET35
    public abstract class ContractTransactionHandlerBase<TFunctionDTO> : ContractHandler<TFunctionDTO> where TFunctionDTO : ContractMessage
    {
        public async Task<TransactionReceipt> ExecuteAsync(TFunctionDTO functionMessage, CancellationTokenSource tokenSource = null)
        {
            ValidateFunctionDTO(functionMessage);
            var gasEstimate = await GetOrEstimateMaximumGas(functionMessage).ConfigureAwait(false);
            return await ExecuteTransactionAsync(functionMessage, gasEstimate, tokenSource); 
        }

        protected virtual async Task<HexBigInteger> GetOrEstimateMaximumGas(TFunctionDTO functionMessage)
        {
            var maxGas = GetMaximumGas(functionMessage);

            if(maxGas == null)
            {
                maxGas = await EstimateGasAsync(functionMessage).ConfigureAwait(false);
            }

            return maxGas;
        }

        protected abstract Task<HexBigInteger> EstimateGasAsync(TFunctionDTO functionMessage);
        protected abstract Task<TransactionReceipt> ExecuteTransactionAsync(TFunctionDTO functionMessage, HexBigInteger gasEstimate, CancellationTokenSource tokenSource = null);

    }
#endif
}
