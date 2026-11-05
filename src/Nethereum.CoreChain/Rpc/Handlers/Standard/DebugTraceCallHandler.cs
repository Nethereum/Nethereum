using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Tracing;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class DebugTraceCallHandler : RpcHandlerBase
    {
        public override string MethodName => "debug_traceCall";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var (callInput, blockTag, tracer, opcodeConfig, stateOverrides) = ParseClientInput(() => ParseRequest(request));
            var blockNumber = await ResolveBlockNumberAsync(blockTag, context);

            switch (tracer)
            {
                case "callTracer":
                    var callResult = await context.Node.TraceCallCallTracerAsync(callInput, blockNumber, stateOverrides);
                    return Success(request.Id, callResult);

                case "prestateTracer":
                    var prestateResult = await context.Node.TraceCallPrestateAsync(callInput, blockNumber, stateOverrides);
                    return Success(request.Id, prestateResult);

                default:
                    var result = await context.Node.TraceCallAsync(callInput, blockNumber, opcodeConfig, stateOverrides);
                    return Success(request.Id, result);
            }
        }

        private static (CallInput CallInput, string BlockTag, string Tracer, OpcodeTraceConfig OpcodeConfig, Dictionary<string, StateOverride> StateOverrides)
            ParseRequest(RpcRequestMessage request)
        {
            var callInput = ParseCallInput(RequireObject(GetJsonElement(request, 0), "call object"));
            var blockTag = GetOptionalBlockParam(request, 1, "latest");

            string tracer = null;
            OpcodeTraceConfig opcodeConfig = null;
            Dictionary<string, StateOverride> stateOverrides = null;
            if (GetParamCount(request) >= 3)
            {
                var configJson = RequireObject(GetJsonElement(request, 2), "trace config");
                if (configJson.TryGetProperty("tracer", out var tracerProp))
                    tracer = tracerProp.GetOptionalRpcString("tracer");
                opcodeConfig = DebugTraceConfigParser.ParseOpcodeConfig(configJson);
                stateOverrides = ParseStateOverrides(configJson);
            }

            return (callInput, blockTag, tracer, opcodeConfig, stateOverrides);
        }

        private static CallInput ParseCallInput(JsonElement element)
        {
            var callInput = new CallInput();

            if (element.TryGetProperty("from", out var fromProp))
                callInput.From = fromProp.GetOptionalRpcString("from");

            if (element.TryGetProperty("to", out var toProp))
                callInput.To = toProp.GetOptionalRpcString("to");

            if (element.TryGetProperty("gas", out var gasProp))
            {
                var gasStr = gasProp.GetOptionalRpcString("gas");
                if (!string.IsNullOrEmpty(gasStr))
                    callInput.Gas = new HexBigInteger(gasStr);
            }

            if (element.TryGetProperty("gasPrice", out var gasPriceProp))
            {
                var gasPriceStr = gasPriceProp.GetOptionalRpcString("gasPrice");
                if (!string.IsNullOrEmpty(gasPriceStr))
                    callInput.GasPrice = new HexBigInteger(gasPriceStr);
            }

            if (element.TryGetProperty("value", out var valueProp))
            {
                var valueStr = valueProp.GetOptionalRpcString("value");
                if (!string.IsNullOrEmpty(valueStr))
                    callInput.Value = new HexBigInteger(valueStr);
            }

            if (element.TryGetProperty("data", out var dataProp))
                callInput.Data = dataProp.GetOptionalRpcString("data");

            if (element.TryGetProperty("input", out var inputProp))
                callInput.Data = inputProp.GetOptionalRpcString("input");

            return callInput;
        }

        private static Dictionary<string, StateOverride> ParseStateOverrides(JsonElement element)
        {
            if (!element.TryGetProperty("stateOverrides", out var stateOverridesProp) ||
                stateOverridesProp.ValueKind == JsonValueKind.Null)
                return null;

            if (stateOverridesProp.ValueKind != JsonValueKind.Object)
                throw RpcException.InvalidParams("'stateOverrides' must be an object");

            return StateOverrideJsonParser.ParseStateOverrideSet(stateOverridesProp);
        }

    }
}
