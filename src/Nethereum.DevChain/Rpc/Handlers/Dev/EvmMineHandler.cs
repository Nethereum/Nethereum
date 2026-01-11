using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Extensions;

namespace Nethereum.DevChain.Rpc.Handlers.Dev
{
    public class EvmMineHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.evm_mine.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var devNode = (DevChainNode)context.Node;
            var blockHash = await devNode.MineBlockAsync();
            return Success(request.Id, ToHex(blockHash));
        }
    }
}
