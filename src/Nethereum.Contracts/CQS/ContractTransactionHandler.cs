using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.Contracts.CQS
{

#if !DOTNET35
    public abstract class ContractTransactionHandler<TFunctionDTO> : ContractTransactionHandlerBase<TFunctionDTO> where TFunctionDTO : ContractMessage
    {
        protected override Task<TransactionReceipt> ExecuteTransactionAsync(TFunctionDTO functionMessage, HexBigInteger gasEstimate, CancellationTokenSource tokenSource = null)
        {
            var function = GetFunction();
            return function.SendTransactionAndWaitForReceiptAsync(
                                            functionMessage,
                                            functionMessage.FromAddress,
                                            gasEstimate,
                                            GetGasPrice(functionMessage),
                                            GetValue(functionMessage),
                                            tokenSource);
        }

        protected override Task<HexBigInteger> EstimateGasAsync(TFunctionDTO functionMessage)
        {
            var function = GetFunction();
            return function.EstimateGasAsync(functionMessage, functionMessage.FromAddress, null, GetValue(functionMessage));
        }
    }
#endif
}
