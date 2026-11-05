using System.Threading.Tasks;
using Nethereum.CoreChain.Engine;
using Nethereum.CoreChain.Rpc;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.MainnetChain.Rpc
{
    public class DebugSetHeadHandler : RpcHandlerBase
    {
        public override string MethodName => "debug_setHead";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var blockNumber = GetParam<string>(request, 0).HexToBigInteger(false);

            await context.GetRequiredService<FollowerEngineApiService>().SetHeadAsync((ulong)blockNumber);

            return Success(request.Id, null);
        }
    }
}
