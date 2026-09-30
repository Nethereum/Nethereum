using System;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.EVM
{
    public sealed class ScheduledChainActivations : IChainActivations
    {
        private readonly HardforkName _genesisFork;
        private readonly ForkActivation[] _newestFirst;

        public ScheduledChainActivations(HardforkName genesisFork, IEnumerable<ForkActivation> activations)
        {
            if (activations == null) throw new ArgumentNullException(nameof(activations));

            _genesisFork = genesisFork;

            _newestFirst = activations.Where(a => a.IsScheduled)
                .Reverse()
                .OrderByDescending(a => a.Timestamp.HasValue)
                .ThenByDescending(a => a.Timestamp ?? 0)
                .ThenByDescending(a => a.Block ?? 0)
                .ToArray();
        }

        public static ScheduledChainActivations RunningOnly(HardforkName fork) =>
            new ScheduledChainActivations(fork, new ForkActivation[0]);

        public IReadOnlyList<ForkActivation> Scheduled => _newestFirst;

        public HardforkName ResolveAt(long blockNumber, ulong timestamp)
        {
            foreach (var activation in _newestFirst)
                if (activation.HasBeenReachedAt(blockNumber, timestamp))
                    return activation.Fork;

            return _genesisFork;
        }
    }
}
