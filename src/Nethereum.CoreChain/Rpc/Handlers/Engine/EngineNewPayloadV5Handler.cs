using System.Threading.Tasks;
using Nethereum.CoreChain.Engine;
using Nethereum.EVM;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.DTOs.Engine;

namespace Nethereum.CoreChain.Rpc.Handlers.Engine
{
    public class EngineNewPayloadV5Handler : RpcHandlerBase
    {
        public override string MethodName => "engine_newPayloadV5";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var payload = GetParam<ExecutionPayloadV4>(request, 0);
            var parentBeaconBlockRoot = GetParam<string>(request, 2);
            var executionRequests = GetParam<string[]>(request, 3);

            EngineForkGuard.RequireAtLeast(
                context, HardforkName.Prague, (long)payload.BlockNumber.Value, (ulong)payload.Timestamp.Value);

            var engine = context.GetRequiredService<IEngineApiService>();
            var status = await engine.NewPayloadV5Async(payload, executionRequests, parentBeaconBlockRoot);

            return Success(request.Id, status);
        }
    }
}
