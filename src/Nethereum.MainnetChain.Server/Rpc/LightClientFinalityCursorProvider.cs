using System;
using System.Numerics;
using Nethereum.Consensus.LightClient;

namespace Nethereum.MainnetChain.Server.Rpc
{
    /// <summary>
    /// Reads the finalized and optimistic execution-payload block numbers from the running
    /// <see cref="LightClientService"/> state per
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
    /// specs/altair/light-client/sync-protocol.md</see> lines 460–478 (finalized header)
    /// and lines 533–547 (optimistic header). Returns null while the light client has not
    /// yet completed its initial bootstrap so the RPC layer falls back to "latest" semantics.
    /// </summary>
    public sealed class LightClientFinalityCursorProvider : IFinalityCursorProvider
    {
        private readonly Func<LightClientState?> _stateAccessor;

        public LightClientFinalityCursorProvider(LightClientService service)
            : this(() => SafeGetState(service))
        {
        }

        public LightClientFinalityCursorProvider(Func<LightClientState?> stateAccessor)
        {
            _stateAccessor = stateAccessor ?? throw new ArgumentNullException(nameof(stateAccessor));
        }

        private static LightClientState? SafeGetState(LightClientService service)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            try { return service.GetState(); }
            catch (InvalidOperationException) { return null; }
        }

        public BigInteger? GetFinalizedBlockNumber()
        {
            var state = _stateAccessor();
            return state?.FinalizedExecutionPayload != null
                ? (BigInteger?)state.FinalizedExecutionPayload.BlockNumber
                : null;
        }

        public BigInteger? GetSafeBlockNumber()
        {
            var state = _stateAccessor();
            return state?.OptimisticExecutionPayload != null
                ? (BigInteger?)state.OptimisticExecutionPayload.BlockNumber
                : null;
        }
    }
}
