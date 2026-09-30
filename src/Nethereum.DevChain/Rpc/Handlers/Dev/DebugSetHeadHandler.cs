using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.DevChain.Rpc.Handlers.Dev
{
    public class DebugSetHeadHandler : RpcHandlerBase
    {
        public override string MethodName => "debug_setHead";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var devNode = (DevChainNode)context.Node;
            var blockNumberHex = GetParam<string>(request, 0);
            var blockNumber = blockNumberHex.HexToBigInteger(false);

            await devNode.SetHeadAsync(blockNumber);

            return Success(request.Id, null);
        }
    }
}
