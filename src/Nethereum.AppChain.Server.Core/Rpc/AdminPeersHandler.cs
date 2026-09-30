using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AppChain.Server.Rpc
{
    public class AdminPeersHandler : RpcHandlerBase
    {
        public override string MethodName => "admin_peers";

        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var pool = context.GetService<IPeerPool>();
            if (pool == null)
                return Task.FromResult(Error(request.Id, -32601, "admin methods not enabled"));

            var peers = pool.ActivePeers.Select(p => new PeerInfoDto
            {
                Enode = p.Enode,
                Host = p.Host,
                EthVersion = p.EthVersion,
                BlockNumber = ToHex(p.PeerLatestBlock),
                IsTrusted = p.IsTrusted
            }).ToArray();

            return Task.FromResult(Success(request.Id, peers));
        }
    }

    public class PeerInfoDto
    {
        public string Enode { get; set; } = "";
        public string Host { get; set; } = "";
        public int EthVersion { get; set; }
        public string BlockNumber { get; set; } = "0x0";
        public bool IsTrusted { get; set; }
    }
}
