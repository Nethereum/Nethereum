using System.Threading.Tasks;
using Nethereum.AppChain.Sync;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AppChain.Server.Rpc
{
    public class AdminAddPeerHandler : RpcHandlerBase
    {
        public override string MethodName => "admin_addPeer";

        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var peerManager = context.GetService<IPeerManager>();
            if (peerManager == null)
                return Task.FromResult(Error(request.Id, -32601, "admin methods not enabled"));

            var peerUrl = GetParam<string>(request, 0);
            if (string.IsNullOrWhiteSpace(peerUrl))
                return Task.FromResult(Error(request.Id, -32602, "peer URL required"));

            var result = peerManager.AddPeer(peerUrl);
            return Task.FromResult(Success(request.Id, result));
        }
    }
}
