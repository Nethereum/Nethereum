using System.Threading.Tasks;
using Nethereum.CoreChain.Engine;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.CoreChain.Rpc.Handlers.Engine
{
    public class EngineGetPayloadV3Handler : RpcHandlerBase
    {
        public override string MethodName => "engine_getPayloadV3";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var payloadId = GetParam<string>(request, 0);
            var engine = context.GetRequiredService<IEngineApiService>();

            try
            {
                var payload = await engine.GetPayloadAsync(payloadId);
                return Success(request.Id, payload);
            }
            catch (PayloadBuildRegistry.UnknownPayloadException ex)
            {
                throw new RpcException(-38001, ex.Message);
            }
        }
    }
}
