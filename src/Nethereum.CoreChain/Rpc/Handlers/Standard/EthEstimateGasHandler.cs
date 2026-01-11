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

            BigInteger? value = callInput.Value?.Value;

            // Use a high gas limit for estimation
            var estimationGasLimit = (BigInteger)30_000_000;

            var result = await context.Node.CallAsync(
                callInput.To,
                callInput.Data?.HexToByteArray(),
                callInput.From,
                value,
                estimationGasLimit
            );

            if (!result.Success)
            {
                return Error(request.Id, 3, $"execution reverted: {result.RevertReason}");
            }

            // Calculate intrinsic gas (base tx cost + data cost)
            var intrinsicGas = CalculateIntrinsicGas(callInput.Data, callInput.To);

            // Total gas = intrinsic gas + execution gas
            var totalGas = intrinsicGas + result.GasUsed;

            // Add 10% buffer for safety
            var estimate = totalGas * 110 / 100;

            return Success(request.Id, new HexBigInteger(estimate));
        }

        private BigInteger CalculateIntrinsicGas(string data, string to)
        {
            // Base transaction cost
            BigInteger gas = 21000;

            // Contract creation adds 32000 gas
            if (string.IsNullOrEmpty(to))
            {
                gas += 32000;
            }

            // Data cost: 4 per zero byte, 16 per non-zero byte
            if (!string.IsNullOrEmpty(data))
            {
                var dataBytes = data.HexToByteArray();
                foreach (var b in dataBytes)
                {
                    gas += b == 0 ? 4 : 16;
                }
            }

            return gas;
        }
    }
}
