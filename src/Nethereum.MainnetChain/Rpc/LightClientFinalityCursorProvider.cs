using System;
using System.Numerics;
using Nethereum.Consensus.LightClient;
using Nethereum.CoreChain.Rpc;

namespace Nethereum.MainnetChain.Rpc
{
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
