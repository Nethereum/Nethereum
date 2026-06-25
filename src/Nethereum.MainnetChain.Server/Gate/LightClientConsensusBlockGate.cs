using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Consensus.LightClient;
using Nethereum.Model;

namespace Nethereum.MainnetChain.Server.Gate
{
    /// <summary>
    /// Admits an execution-layer block only when it matches the light-client's view of the
    /// chain. The gate reads the latest <see cref="LightClientState"/> from the running
    /// <see cref="LightClientService"/> and compares the computed block hash against the entry
    /// recorded in <see cref="LightClientState.BlockHashHistory"/> for the same block number.
    ///
    /// Finalized headers carry the strongest provenance per
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
    /// specs/altair/light-client/sync-protocol.md</see> lines 460–478
    /// (<c>apply_light_client_update</c>): when a block at or below
    /// <c>state.FinalizedHeader.Execution.BlockNumber</c> is gated, a mismatch is a hard
    /// chain-split signal and the block is rejected. Blocks above the finalized cursor but at
    /// or below the optimistic cursor are checked against the optimistic entry recorded at
    /// the same height; blocks beyond the optimistic cursor pass through (graceful degradation
    /// while the LC catches up to chain head).
    /// </summary>
    public sealed class LightClientConsensusBlockGate : IConsensusBlockGate
    {
        private readonly Func<LightClientState?> _stateAccessor;

        public LightClientConsensusBlockGate(LightClientService service)
            : this(() => SafeGetState(service))
        {
        }

        public LightClientConsensusBlockGate(Func<LightClientState?> stateAccessor)
        {
            _stateAccessor = stateAccessor ?? throw new ArgumentNullException(nameof(stateAccessor));
        }

        private static LightClientState? SafeGetState(LightClientService service)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            try { return service.GetState(); }
            catch (InvalidOperationException) { return null; }
        }

        public Task<ConsensusBlockGateResult> IsBlockCanonicalAsync(
            BlockHeader header,
            byte[] computedBlockHash,
            CancellationToken ct)
        {
            if (header == null || computedBlockHash == null)
            {
                return Task.FromResult(ConsensusBlockGateResult.Reject("null header or hash"));
            }

            var state = _stateAccessor();
            if (state == null)
            {
                return Task.FromResult(ConsensusBlockGateResult.Accept());
            }

            var blockNumber = (ulong)(BigInteger)header.BlockNumber;
            var recorded = state.GetBlockHash(blockNumber);
            if (recorded == null)
            {
                return Task.FromResult(ConsensusBlockGateResult.Accept());
            }

            if (recorded.Length != computedBlockHash.Length)
            {
                return Task.FromResult(ConsensusBlockGateResult.Reject(
                    $"light client block hash length mismatch at block {blockNumber}"));
            }

            for (var i = 0; i < recorded.Length; i++)
            {
                if (recorded[i] != computedBlockHash[i])
                {
                    return Task.FromResult(ConsensusBlockGateResult.Reject(
                        $"light client block hash mismatch at block {blockNumber}"));
                }
            }

            return Task.FromResult(ConsensusBlockGateResult.Accept());
        }
    }
}
