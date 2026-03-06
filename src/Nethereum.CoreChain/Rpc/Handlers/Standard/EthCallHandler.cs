using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthCallHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_call.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var callInput = GetParam<CallInput>(request, 0);
            var blockTag = GetOptionalParam<string>(request, 1, "latest");

            var blockNumber = await ResolveBlockNumberAsync(blockTag, context);

            BigInteger? gas = callInput.Gas?.Value;
            BigInteger? value = callInput.Value?.Value;

            var result = await context.Node.CallAsync(
                callInput.To,
                callInput.Data?.HexToByteArray(),
                blockNumber,
                callInput.From,
                value,
                gas
            );

            if (!result.Success)
            {
                var reason = !string.IsNullOrEmpty(result.RevertReason) ? result.RevertReason : "revert";
                return Error(request.Id, 3, $"execution reverted: {reason}", result.ReturnData?.ToHex(true) ?? "0x");
            }

            return Success(request.Id, result.ReturnData?.ToHex(true) ?? "0x");
        }
    }
}
