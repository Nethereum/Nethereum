using System;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.CoreChain.Tracing;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class DebugTraceTransactionHandler : RpcHandlerBase
    {
        public override string MethodName => "debug_traceTransaction";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            try
            {
                var txHash = GetParam<string>(request, 0);

                if (string.IsNullOrEmpty(txHash))
                    return Error(request.Id, -32602, "Invalid transaction hash");

                string tracer = null;
                JsonElement optionsElement = default;
                if (GetParamCount(request) >= 2)
                {
                    optionsElement = GetJsonElement(request, 1);
                    if (optionsElement.TryGetProperty("tracer", out var tracerProp))
                        tracer = tracerProp.GetString();
                }

                switch (tracer)
                {
                    case "callTracer":
                        var callResult = await context.Node.TraceTransactionCallTracerAsync(txHash);
                        return Success(request.Id, callResult);

                    case "prestateTracer":
                        var prestateResult = await context.Node.TraceTransactionPrestateAsync(txHash);
                        return Success(request.Id, prestateResult);

                    default:
                        var config = optionsElement.ValueKind != JsonValueKind.Undefined
                            ? DebugTraceConfigParser.ParseOpcodeConfig(optionsElement)
                            : null;
                        var opcodeResult = await context.Node.TraceTransactionAsync(txHash, config);
                        return Success(request.Id, opcodeResult);
                }
            }
            catch (InvalidOperationException ex)
            {
                return Error(request.Id, -32000, ex.Message);
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
    }
}
