using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthSyncingHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_syncing.ToString();

        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            // The metadata store is reached via the bundle (it is not registered
            // as a standalone service); fall back to a direct registration for
            // compositions that provide one. Everything is read from metadata —
            // during snap-bootstrap the chain node is not yet initialised, so we
            // must not touch context.Node here.
            var metadata = context.GetService<IChainStoreBundle>()?.Metadata
                ?? context.GetService<IChainMetadataStore>();
            var state = metadata?.GetSnapSyncState();

            if (state == null
                || state.Phase == SnapPhase.NotStarted
                || state.Phase == SnapPhase.Complete)
            {
                return Task.FromResult(Success(request.Id, false));
            }

            // currentBlock tracks Phase-1 download progress via the body backfill
            // cursor, so a tip-advance catch-up shows the cursor climbing toward the
            // pivot; once committed blocks exist (post-snap) the executed head leads.
            var executed = (System.Numerics.BigInteger)metadata.GetLastBlock();
            var bodyCursor = (System.Numerics.BigInteger)metadata.GetLastFetchedBody();
            var currentBlock = bodyCursor > executed ? bodyCursor : executed;
            var counters = state.Counters ?? SnapSyncCounters.Zero;

            var output = new EthSyncingSnapOutput
            {
                StartingBlock       = new HexBigInteger(0),
                CurrentBlock        = new HexBigInteger(currentBlock),
                HighestBlock        = new HexBigInteger(state.PivotBlockNumber),
                SyncedAccounts      = new HexBigInteger(counters.AccountsSynced),
                SyncedAccountBytes  = new HexBigInteger(counters.AccountBytes),
                SyncedBytecodes     = new HexBigInteger(counters.BytecodesSynced),
                SyncedBytecodeBytes = new HexBigInteger(counters.BytecodeBytes),
                SyncedStorage       = new HexBigInteger(counters.StorageSlotsSynced),
                SyncedStorageBytes  = new HexBigInteger(counters.StorageBytes),
                HealedTrienodes     = new HexBigInteger(counters.TrieNodesHealed),
                HealedTrienodeBytes = new HexBigInteger(counters.TrieNodeBytesHealed),
                HealedBytecodes     = new HexBigInteger(counters.BytecodesHealed),
                HealingBytecode     = new HexBigInteger(0),
                HealingTrienodes    = new HexBigInteger(0),
            };

            return Task.FromResult(Success(request.Id, output));
        }
    }
}
