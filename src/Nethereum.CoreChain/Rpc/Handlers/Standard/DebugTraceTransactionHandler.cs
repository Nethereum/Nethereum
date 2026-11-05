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
            var (txHash, tracer, config) = ParseClientInput(() => ParseRequest(request));
            if (string.IsNullOrEmpty(txHash))
                return Error(request.Id, -32602, "Invalid transaction hash");

            try
            {
                switch (tracer)
                {
                    case "callTracer":
                        var callResult = await context.Node.TraceTransactionCallTracerAsync(txHash);
                        return Success(request.Id, callResult);

                    case "prestateTracer":
                        var prestateResult = await context.Node.TraceTransactionPrestateAsync(txHash);
                        return Success(request.Id, prestateResult);

                    default:
                        var opcodeResult = await context.Node.TraceTransactionAsync(txHash, config);
                        return Success(request.Id, opcodeResult);
                }
            }
            catch (InvalidOperationException ex)
            {
                return Error(request.Id, -32000, ex.Message);
            }
        }

        private static (string TxHash, string Tracer, OpcodeTraceConfig Config) ParseRequest(RpcRequestMessage request)
        {
            var txHash = GetParam<string>(request, 0);

            string tracer = null;
            OpcodeTraceConfig config = null;
            if (GetParamCount(request) >= 2)
            {
                var optionsElement = RequireObject(GetJsonElement(request, 1), "trace config");
                if (optionsElement.TryGetProperty("tracer", out var tracerProp))
                    tracer = tracerProp.GetOptionalRpcString("tracer");
                if (tracer != "callTracer" && tracer != "prestateTracer")
                    config = DebugTraceConfigParser.ParseOpcodeConfig(optionsElement);
            }

            return (txHash, tracer, config);
        }
    }
}
