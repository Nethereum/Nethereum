using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AppChain.Server.Rpc
{
    public class AdminAddPeerHandler : RpcHandlerBase
    {
        public override string MethodName => "admin_addPeer";

        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var peers = context.GetService<IPeerPool>();
            if (peers == null)
                return Task.FromResult(Error(request.Id, -32601, "admin methods not enabled"));

            var enode = GetParam<string>(request, 0);
            if (string.IsNullOrWhiteSpace(enode))
                return Task.FromResult(Error(request.Id, -32602, "enode required"));

            return Task.FromResult(Success(request.Id, peers.EnqueueCandidate(enode)));
        }
    }
}
