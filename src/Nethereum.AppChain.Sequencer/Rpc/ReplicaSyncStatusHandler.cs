using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.AppChain.Sync;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AppChain.Sequencer.Rpc
{
    public class ReplicaSyncStatusHandler : RpcHandlerBase
    {
        public override string MethodName => "replica_syncStatus";

        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var node = context.Node;

            if (node is AppChainReplicaNode replicaNode)
            {
                var syncService = replicaNode.SyncService;
                var status = new ReplicaSyncStatus
                {
                    IsReplica = true,
                    Syncing = replicaNode.IsSyncing,
                    SyncMode = replicaNode.SyncMode.ToString(),
                    FinalizedBlock = syncService?.FinalizedTip.ToString() ?? "0",
                    SoftBlock = syncService?.SoftTip.ToString() ?? "0",
                    AnchoredBlock = syncService?.AnchoredTip.ToString() ?? "0"
                };

                return Task.FromResult(Success(request.Id, status));
            }

            var notReplicaStatus = new ReplicaSyncStatus
            {
                IsReplica = false,
                Syncing = false,
                SyncMode = "none",
                FinalizedBlock = "0",
                SoftBlock = "0",
                AnchoredBlock = "0"
            };

            return Task.FromResult(Success(request.Id, notReplicaStatus));
        }
    }

    public class ReplicaSyncStatus
    {
        public bool IsReplica { get; set; }
        public bool Syncing { get; set; }
        public string SyncMode { get; set; } = "";
        public string FinalizedBlock { get; set; } = "0";
        public string SoftBlock { get; set; } = "0";
        public string AnchoredBlock { get; set; } = "0";
    }
}
