using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Extensions;

namespace Nethereum.DevChain.Rpc.Handlers.Dev
{
    public class EvmIncreaseTimeHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.evm_increaseTime.ToString();

        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var seconds = ParseHexOrDecimalLong(request, 0);
            var devNode = (DevChainNode)context.Node;
            var newOffset = devNode.DevConfig.AddTimeOffset(seconds);
            return Task.FromResult(Success(request.Id, ToHex(newOffset)));
        }
    }
}
