using System;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class DebugTraceBlockByNumberHandler : RpcHandlerBase
    {
        public override string MethodName => "debug_traceBlockByNumber";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            try
            {
                var blockTag = GetParam<string>(request, 0);
                if (string.IsNullOrEmpty(blockTag))
                    return Error(request.Id, -32602, "Invalid block number");

                var blockNumber = await ResolveBlockNumberAsync(blockTag, context);

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
                        var callResult = await context.Node.TraceBlockCallTracerByNumberAsync(blockNumber);
                        return Success(request.Id, callResult);

                    default:
                        var config = optionsElement.ValueKind == JsonValueKind.Object
                            ? DebugTraceConfigParser.ParseOpcodeConfig(optionsElement)
                            : null;
                        var result = await context.Node.TraceBlockByNumberAsync(blockNumber, config);
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
