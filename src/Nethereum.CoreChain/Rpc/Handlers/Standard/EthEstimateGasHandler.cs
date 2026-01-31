using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthEstimateGasHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_estimateGas.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var callInput = GetParam<CallInput>(request, 0);
            var blockTag = GetOptionalParam<string>(request, 1, "latest");

            ValidateBlockParameterIsLatest(blockTag, MethodName);

            BigInteger? value = callInput.Value?.Value;

            // Use a high gas limit for estimation
            var estimationGasLimit = (BigInteger)30_000_000;

            // Check if this is contract creation (no 'to' address)
            var isContractCreation = string.IsNullOrEmpty(callInput.To) || callInput.To == "0x";

            // Calculate intrinsic gas using TransactionProcessor constants
            var intrinsicGas = TransactionProcessor.CalculateIntrinsicGas(
                callInput.Data?.HexToByteArray(),
                isContractCreation);

            BigInteger executionGas = 0;

            if (isContractCreation)
            {
                // For contract creation, execute the init code to estimate gas
                var createResult = await context.Node.EstimateContractCreationGasAsync(
                    callInput.Data?.HexToByteArray(),
                    callInput.From,
                    value,
                    estimationGasLimit - intrinsicGas
                );

                if (!createResult.Success)
                {
                    return Error(request.Id, 3, $"execution reverted: {createResult.RevertReason}", createResult.ReturnData?.ToHex(true));
                }

                executionGas = createResult.GasUsed;
            }
            else
            {
                // For regular calls, use the existing CallAsync
                var result = await context.Node.CallAsync(
                    callInput.To,
                    callInput.Data?.HexToByteArray(),
                    callInput.From,
                    value,
                    estimationGasLimit - intrinsicGas
                );

                if (!result.Success)
                {
                    return Error(request.Id, 3, $"execution reverted: {result.RevertReason}", result.ReturnData?.ToHex(true));
                }

                executionGas = result.GasUsed;
            }

            // Total gas = intrinsic gas + execution gas
            var totalGas = intrinsicGas + executionGas;

            // Add 10% buffer for safety
            var estimate = totalGas * 110 / 100;

            return Success(request.Id, new HexBigInteger(estimate));
        }
    }
}
