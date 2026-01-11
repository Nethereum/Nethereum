using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Extensions;

namespace Nethereum.DevChain.Rpc.Handlers.Dev
{
    public class EvmSnapshotHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.evm_snapshot.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var devNode = (DevChainNode)context.Node;
            var snapshot = await devNode.TakeSnapshotAsync();
            return Success(request.Id, ToHex((long)snapshot.SnapshotId));
        }
    }
}
