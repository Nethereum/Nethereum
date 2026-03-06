using System.Threading.Tasks;
using Nethereum.AppChain;
using Nethereum.AppChain.Sync;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Storage;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AppChain.Server.Rpc
{
    public class AdminNodeInfoHandler : RpcHandlerBase
    {
        public override string MethodName => "admin_nodeInfo";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var blockStore = context.GetRequiredService<IBlockStore>();
            var chainConfig = context.GetService<AppChainConfig>();
            var peerManager = context.GetService<IPeerManager>();

            var blockNumber = await blockStore.GetHeightAsync();
            var latestHash = await blockStore.GetHashByNumberAsync(blockNumber);

            var nodeInfo = new NodeInfoDto
            {
                BlockNumber = ToHex(blockNumber),
                BlockHash = latestHash != null ? ToHex(latestHash) : null,
                ChainId = chainConfig?.ChainId.ToString() ?? "0",
                NodeName = chainConfig?.AppChainName ?? "AppChain",
                PeerCount = peerManager?.Peers.Count ?? 0,
                IsSyncing = false
            };

            return Success(request.Id, nodeInfo);
        }
    }

    public class NodeInfoDto
    {
        public string BlockNumber { get; set; } = "0x0";
        public string? BlockHash { get; set; }
        public string ChainId { get; set; } = "0";
        public string NodeName { get; set; } = "";
        public int PeerCount { get; set; }
        public bool IsSyncing { get; set; }
    }
}
