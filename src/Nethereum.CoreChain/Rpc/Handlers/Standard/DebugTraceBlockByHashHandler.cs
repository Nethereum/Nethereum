using System;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.CoreChain.Tracing;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class DebugTraceBlockByHashHandler : RpcHandlerBase
    {
        public override string MethodName => "debug_traceBlockByHash";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var (blockHash, tracer, config) = ParseClientInput(() => ParseRequest(request));
            if (blockHash == null)
                return Error(request.Id, -32602, "Invalid block hash");

            try
            {
                var header = await context.Node.GetBlockByHashAsync(blockHash);
                if (header != null && header.BlockNumber.IsZero)
                    return Error(request.Id, -32000, "genesis is not traceable");

                switch (tracer)
                {
                    case "callTracer":
                        var callResult = await context.Node.TraceBlockCallTracerByHashAsync(blockHash);
                        return Success(request.Id, callResult);

                    default:
                        var result = await context.Node.TraceBlockByHashAsync(blockHash, config);
                        return Success(request.Id, result);
                }
            }
            catch (InvalidOperationException ex)
            {
                return Error(request.Id, -32000, ex.Message);
            }
        }

        private static (byte[] BlockHash, string Tracer, OpcodeTraceConfig Config) ParseRequest(RpcRequestMessage request)
        {
            var blockHashHex = GetParam<string>(request, 0);
            var blockHash = string.IsNullOrEmpty(blockHashHex) ? null : blockHashHex.HexToByteArray();

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

            return (blockHash, tracer, config);
        }
    }
}
