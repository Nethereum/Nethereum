using System.Threading.Tasks;
using Nethereum.CoreChain.Engine;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.DTOs.Engine;

namespace Nethereum.CoreChain.Rpc.Handlers.Engine
{
    public class EngineNewPayloadV3Handler : RpcHandlerBase
    {
        public override string MethodName => "engine_newPayloadV3";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var payload = GetParam<ExecutionPayloadV3>(request, 0);
            var parentBeaconBlockRoot = GetParam<string>(request, 2);

            var engine = context.GetRequiredService<IEngineApiService>();
            var status = await engine.NewPayloadAsync(payload, parentBeaconBlockRoot);

            return Success(request.Id, status);
        }
    }
}
