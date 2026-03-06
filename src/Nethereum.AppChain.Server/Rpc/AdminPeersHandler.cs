using System.Linq;
using System.Threading.Tasks;
using Nethereum.AppChain.Sync;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AppChain.Server.Rpc
{
    public class AdminPeersHandler : RpcHandlerBase
    {
        public override string MethodName => "admin_peers";

        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var peerManager = context.GetService<IPeerManager>();
            if (peerManager == null)
                return Task.FromResult(Error(request.Id, -32601, "admin methods not enabled"));

            var peers = peerManager.Peers.Select(p => new PeerInfoDto
            {
                Url = p.Url,
                BlockNumber = ToHex(p.BlockNumber),
                IsHealthy = p.IsHealthy,
                LastSeen = p.LastSeen.ToString("O"),
                LastError = p.LastError,
                FailureCount = p.FailureCount
            }).ToArray();

            return Task.FromResult(Success(request.Id, peers));
        }
    }

    public class PeerInfoDto
    {
        public string Url { get; set; } = "";
        public string BlockNumber { get; set; } = "0x0";
        public bool IsHealthy { get; set; }
        public string LastSeen { get; set; } = "";
        public string? LastError { get; set; }
        public int FailureCount { get; set; }
    }
}
