using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
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
                    opcodeConfig = ParseOpcodeConfig(configJson);
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
            if (!element.TryGetProperty("stateOverrides", out var stateOverridesProp))
                return null;

            var overrides = new Dictionary<string, StateOverride>();
            foreach (var prop in stateOverridesProp.EnumerateObject())
            {
                overrides[prop.Name] = ParseStateOverride(prop.Value);
            }
            return overrides;
        }

        private static StateOverride ParseStateOverride(JsonElement element)
        {
            var stateOverride = new StateOverride();

            if (element.TryGetProperty("balance", out var balanceProp))
            {
                var balanceStr = balanceProp.GetString();
                if (!string.IsNullOrEmpty(balanceStr))
                    stateOverride.Balance = new HexBigInteger(balanceStr);
            }

            if (element.TryGetProperty("nonce", out var nonceProp))
                stateOverride.Nonce = nonceProp.GetString();

            if (element.TryGetProperty("code", out var codeProp))
                stateOverride.Code = codeProp.GetString();

            if (element.TryGetProperty("state", out var stateProp))
            {
                stateOverride.State = new Dictionary<string, string>();
                foreach (var prop in stateProp.EnumerateObject())
                {
                    stateOverride.State[prop.Name] = prop.Value.GetString();
                }
            }

            if (element.TryGetProperty("stateDiff", out var stateDiffProp))
            {
                stateOverride.StateDiff = new Dictionary<string, string>();
                foreach (var prop in stateDiffProp.EnumerateObject())
                {
                    stateOverride.StateDiff[prop.Name] = prop.Value.GetString();
                }
            }

            return stateOverride;
        }

        private static OpcodeTraceConfig ParseOpcodeConfig(JsonElement element)
        {
            var config = new OpcodeTraceConfig();

            if (element.TryGetProperty("enableMemory", out var enableMemoryProp))
                config.EnableMemory = enableMemoryProp.GetBoolean();

            if (element.TryGetProperty("disableStack", out var disableStackProp))
                config.DisableStack = disableStackProp.GetBoolean();

            if (element.TryGetProperty("disableStorage", out var disableStorageProp))
                config.DisableStorage = disableStorageProp.GetBoolean();

            if (element.TryGetProperty("enableReturnData", out var enableReturnDataProp))
                config.EnableReturnData = enableReturnDataProp.GetBoolean();

            if (element.TryGetProperty("limit", out var limitProp))
                config.Limit = limitProp.GetInt32();

            if (element.TryGetProperty("tracerConfig", out var tracerConfigProp))
            {
                if (tracerConfigProp.TryGetProperty("enableMemory", out var tcEnableMemory))
                    config.EnableMemory = tcEnableMemory.GetBoolean();
                if (tracerConfigProp.TryGetProperty("disableStack", out var tcDisableStack))
                    config.DisableStack = tcDisableStack.GetBoolean();
                if (tracerConfigProp.TryGetProperty("disableStorage", out var tcDisableStorage))
                    config.DisableStorage = tcDisableStorage.GetBoolean();
                if (tracerConfigProp.TryGetProperty("limit", out var tcLimit))
                    config.Limit = tcLimit.GetInt32();
            }

            return config;
        }
    }
}
