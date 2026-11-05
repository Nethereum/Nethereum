using System;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.EVM
{
    public sealed class ScheduledChainActivations : IChainActivations, IChainBlobSchedule
    {
        private readonly HardforkName _genesisFork;
        private readonly ForkActivation[] _newestFirst;
        private readonly BlobSchedule _blobSchedule;

        public ScheduledChainActivations(HardforkName genesisFork, IEnumerable<ForkActivation> activations)
            : this(genesisFork, activations, BlobSchedule.Empty)
        {
        }

        public ScheduledChainActivations(
            HardforkName genesisFork, IEnumerable<ForkActivation> activations, BlobSchedule blobSchedule)
        {
            if (activations == null) throw new ArgumentNullException(nameof(activations));

            _genesisFork = genesisFork;

            _newestFirst = activations.Where(a => a.IsScheduled)
                .Reverse()
                .OrderByDescending(a => a.Timestamp.HasValue)
                .ThenByDescending(a => a.Timestamp ?? 0)
                .ThenByDescending(a => a.Block ?? 0)
                .ToArray();

            _blobSchedule = blobSchedule ?? BlobSchedule.Empty;
        }

        public BlobSchedule BlobSchedule => _blobSchedule;

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

        public BlobParameters? BlobParametersAt(long blockNumber, ulong timestamp)
        {
            var fork = ResolveAt(blockNumber, timestamp);
            return _blobSchedule.At(fork, TimestampForkActivatedAt(fork), timestamp);
        }

        private ulong TimestampForkActivatedAt(HardforkName fork)
        {
            foreach (var activation in _newestFirst)
                if (activation.Fork == fork)
                    return activation.Timestamp ?? 0;

            return 0;
        }
    }
}
