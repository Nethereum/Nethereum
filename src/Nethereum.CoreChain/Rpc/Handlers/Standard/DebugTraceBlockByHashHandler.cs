using System;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class DebugTraceBlockByHashHandler : RpcHandlerBase
    {
        public override string MethodName => "debug_traceBlockByHash";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            try
            {
                var blockHashHex = GetParam<string>(request, 0);
                if (string.IsNullOrEmpty(blockHashHex))
                    return Error(request.Id, -32602, "Invalid block hash");

                var blockHash = blockHashHex.HexToByteArray();

                string tracer = null;
                JsonElement optionsElement = default;
                if (GetParamCount(request) >= 2)
                {
                    optionsElement = GetJsonElement(request, 1);
                    if (optionsElement.ValueKind == JsonValueKind.Object &&
                        optionsElement.TryGetProperty("tracer", out var tracerProp))
                        tracer = tracerProp.GetString();
                }

                switch (tracer)
                {
                    case "callTracer":
                        var callResult = await context.Node.TraceBlockCallTracerByHashAsync(blockHash);
                        return Success(request.Id, callResult);

                    default:
                        var config = optionsElement.ValueKind == JsonValueKind.Object
                            ? DebugTraceConfigParser.ParseOpcodeConfig(optionsElement)
                            : null;
                        var result = await context.Node.TraceBlockByHashAsync(blockHash, config);
                        return Success(request.Id, result);
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
