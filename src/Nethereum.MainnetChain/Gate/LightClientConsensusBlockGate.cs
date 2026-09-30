using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Consensus.LightClient;
using Nethereum.CoreChain.Sync;
using Nethereum.Model;

namespace Nethereum.MainnetChain.Gate
{
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
