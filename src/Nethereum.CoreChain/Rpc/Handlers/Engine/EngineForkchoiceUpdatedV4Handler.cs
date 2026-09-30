using System.Threading.Tasks;
using Nethereum.CoreChain.Engine;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.DTOs.Engine;

namespace Nethereum.CoreChain.Rpc.Handlers.Engine
{
    public class EngineForkchoiceUpdatedV4Handler : RpcHandlerBase
    {
        public override string MethodName => "engine_forkchoiceUpdatedV4";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var state = GetParam<ForkchoiceStateV1>(request, 0);
            var attributes = GetOptionalParam<PayloadAttributesV4>(request, 1, null);

            var engine = context.GetRequiredService<IEngineApiService>();
            var response = await engine.ForkchoiceUpdatedV4Async(state, attributes);

            return Success(request.Id, response);
        }
    }
}
