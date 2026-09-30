using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AppChain.Server.Rpc
{
    public class AdminRemovePeerHandler : RpcHandlerBase
    {
        public override string MethodName => "admin_removePeer";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var peers = context.GetService<IPeerPool>();
            if (peers == null)
                return Error(request.Id, -32601, "admin methods not enabled");

            var enode = GetParam<string>(request, 0);
            if (string.IsNullOrWhiteSpace(enode))
                return Error(request.Id, -32602, "enode required");

            await peers.BanAndDropAsync(enode, "removed by admin_removePeer", default)
                .ConfigureAwait(false);
            return Success(request.Id, true);
        }
    }
}
