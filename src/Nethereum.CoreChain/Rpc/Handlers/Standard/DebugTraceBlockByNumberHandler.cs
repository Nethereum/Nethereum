using System;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.CoreChain.Tracing;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class DebugTraceBlockByNumberHandler : RpcHandlerBase
    {
        public override string MethodName => "debug_traceBlockByNumber";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var (blockTag, tracer, config) = ParseClientInput(() => ParseRequest(request));
            if (string.IsNullOrEmpty(blockTag))
                return Error(request.Id, -32602, "Invalid block number");

            try
            {
                var blockNumber = await ResolveBlockNumberAsync(blockTag, context);

                if (blockNumber.IsZero)
                    return Error(request.Id, -32000, "genesis is not traceable");

                switch (tracer)
                {
                    case "callTracer":
                        var callResult = await context.Node.TraceBlockCallTracerByNumberAsync(blockNumber);
                        return Success(request.Id, callResult);

                    default:
                        var result = await context.Node.TraceBlockByNumberAsync(blockNumber, config);
                        return Success(request.Id, result);
                }
            }
            catch (InvalidOperationException ex)
            {
                return Error(request.Id, -32000, ex.Message);
            }
        }

        private static (string BlockTag, string Tracer, OpcodeTraceConfig Config) ParseRequest(RpcRequestMessage request)
        {
            var blockTag = GetParam<string>(request, 0);

            string tracer = null;
            OpcodeTraceConfig config = null;
            if (GetParamCount(request) >= 2)
            {
                var optionsElement = GetJsonElement(request, 1);
                if (optionsElement.ValueKind == JsonValueKind.Object)
                {
                    if (optionsElement.TryGetProperty("tracer", out var tracerProp))
                        tracer = tracerProp.GetOptionalRpcString("tracer");
                    if (tracer != "callTracer" && tracer != "prestateTracer")
                        config = DebugTraceConfigParser.ParseOpcodeConfig(optionsElement);
                }
            }

            return (blockTag, tracer, config);
        }
    }
}
