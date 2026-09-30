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
            try
            {
                var callInputJson = GetJsonElement(request, 0);
                var callInput = ParseCallInput(callInputJson);

                var blockTag = GetOptionalParam<string>(request, 1, "latest");
                var blockNumber = await ResolveBlockNumberAsync(blockTag, context);

                string tracer = null;
                OpcodeTraceConfig opcodeConfig = null;
                Dictionary<string, StateOverride> stateOverrides = null;

                var paramCount = GetParamCount(request);
                if (paramCount >= 3)
                {
                    var configJson = GetJsonElement(request, 2);
                    if (configJson.TryGetProperty("tracer", out var tracerProp))
                        tracer = tracerProp.GetString();
                    opcodeConfig = DebugTraceConfigParser.ParseOpcodeConfig(configJson);
                    stateOverrides = ParseStateOverrides(configJson);
                }

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
            catch (RpcException ex)
            {
                return Error(request.Id, ex.Code, ex.Message);
            }
            catch (Exception ex)
            {
                return Error(request.Id, -32603, ex.Message);
            }
        }

        private static CallInput ParseCallInput(JsonElement element)
        {
            var callInput = new CallInput();

            if (element.TryGetProperty("from", out var fromProp))
                callInput.From = fromProp.GetString();

            if (element.TryGetProperty("to", out var toProp))
                callInput.To = toProp.GetString();

            if (element.TryGetProperty("gas", out var gasProp))
            {
                var gasStr = gasProp.GetString();
                if (!string.IsNullOrEmpty(gasStr))
                    callInput.Gas = new HexBigInteger(gasStr);
            }

            if (element.TryGetProperty("gasPrice", out var gasPriceProp))
            {
                var gasPriceStr = gasPriceProp.GetString();
                if (!string.IsNullOrEmpty(gasPriceStr))
                    callInput.GasPrice = new HexBigInteger(gasPriceStr);
            }

            if (element.TryGetProperty("value", out var valueProp))
            {
                var valueStr = valueProp.GetString();
                if (!string.IsNullOrEmpty(valueStr))
                    callInput.Value = new HexBigInteger(valueStr);
            }

            if (element.TryGetProperty("data", out var dataProp))
                callInput.Data = dataProp.GetString();

            if (element.TryGetProperty("input", out var inputProp))
                callInput.Data = inputProp.GetString();

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
