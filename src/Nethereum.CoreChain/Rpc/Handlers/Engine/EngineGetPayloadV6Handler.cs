using System.Threading.Tasks;
using Nethereum.CoreChain.Engine;
using Nethereum.EVM;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.CoreChain.Rpc.Handlers.Engine
{
    public class EngineGetPayloadV6Handler : RpcHandlerBase
    {
        public override string MethodName => "engine_getPayloadV6";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var payloadId = GetParam<string>(request, 0);
            var engine = context.GetRequiredService<IEngineApiService>();

            try
            {
                var response = await engine.GetPayloadV6Async(payloadId);

                EngineForkGuard.RequireAtLeast(
                    context,
                    HardforkName.Prague,
                    (long)response.ExecutionPayload.BlockNumber.Value,
                    (ulong)response.ExecutionPayload.Timestamp.Value);

                return Success(request.Id, response);
            }
            catch (PayloadBuildRegistry.UnknownPayloadException ex)
            {
                throw new RpcException(-38001, ex.Message);
            }
        }
    }
}
