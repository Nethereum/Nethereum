using System;

namespace Nethereum.EVM
{
    public sealed class PreviewingChainActivations : IChainActivations
    {
        private readonly IChainActivations _chainSchedule;
        private readonly ForkActivation _preview;

        public PreviewingChainActivations(IChainActivations chainSchedule, ForkActivation preview)
        {
            _chainSchedule = chainSchedule ?? throw new ArgumentNullException(nameof(chainSchedule));
            if (!preview.IsScheduled)
                throw new ArgumentException(
                    "A previewed fork needs a block or a timestamp to take effect at; an unscheduled " +
                    "activation would leave the chain exactly as it was and say nothing.", nameof(preview));

            _preview = preview;
        }

        public static PreviewingChainActivations FromGenesis(IChainActivations chainSchedule, HardforkName fork) =>
            new PreviewingChainActivations(chainSchedule, ForkActivation.AtBlock(fork, 0));

        public HardforkName ResolveAt(long blockNumber, ulong timestamp) =>
            _preview.HasBeenReachedAt(blockNumber, timestamp)
                ? _preview.Fork
                : _chainSchedule.ResolveAt(blockNumber, timestamp);
    }
}
