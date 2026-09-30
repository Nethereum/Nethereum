using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Tracing;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Mappers;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthCallHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_call.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var callInput = GetParam<TransactionInput>(request, 0);
            var blockTag = GetOptionalParam<string>(request, 1, "latest");

            var blockNumber = await ResolveBlockNumberAsync(blockTag, context);

            BigInteger? gas = callInput.Gas?.Value;
            BigInteger? value = callInput.Value?.Value;

            Dictionary<string, StateOverride> stateOverrides = null;
            if (GetParamCount(request) >= 3)
            {
                var overridesJson = GetJsonElement(request, 2);
                if (overridesJson.ValueKind == JsonValueKind.Object)
                    stateOverrides = StateOverrideJsonParser.ParseStateOverrideSet(overridesJson);
                else if (overridesJson.ValueKind != JsonValueKind.Null && overridesJson.ValueKind != JsonValueKind.Undefined)
                    throw RpcException.InvalidParams("state override set (parameter 3) must be an object");
            }

            var authorisationList = callInput.AuthorisationList?.ToAuthorisation7720SignedList();

            var data = callInput.Data?.HexToByteArray();
            var result = context.Node is ChainNodeBase node
                ? await node.CallWithFeePolicyAsync(
                    callInput.To, data, blockNumber, callInput.From, value, gas,
                    stateOverrides, authorisationList,
                    callInput.GasPrice?.Value, callInput.MaxFeePerGas?.Value, callInput.MaxPriorityFeePerGas?.Value)
                : await context.Node.CallAsync(
                    callInput.To, data, blockNumber, callInput.From, value, gas,
                    stateOverrides, authorisationList);

            if (!result.Success) return RevertedError(request.Id, result);

            return Success(request.Id, result.ReturnData?.ToHex(true) ?? "0x");
        }
    }
}
