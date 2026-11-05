using Nethereum.Documentation;
using System;

namespace Nethereum.CoreChain
{
    public interface IFlushCadence
    {
        bool IsBoundary(ulong blockNumber);
    }

    public sealed class EveryBlockFlushCadence : IFlushCadence
    {
        public bool IsBoundary(ulong blockNumber) => true;
    }

    public sealed class FixedIntervalFlushCadence : IFlushCadence
    {
        private readonly ulong _k;

        public ulong K => _k;

        public FixedIntervalFlushCadence(ulong k)
        {
            if (k < 1) throw new ArgumentOutOfRangeException(nameof(k), "K must be >= 1.");
            _k = k;
        }

        public bool IsBoundary(ulong blockNumber) => _k == 1 || blockNumber % _k == 0;
    }

    public static class FlushCadences
    {
        [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "FlushCadences.ForBlocks - the state flush interval a node asks for")]
        public static IFlushCadence? ForBlocks(int blocks, string paramName = "blocks")
        {
            if (blocks < 1)
                throw new ArgumentException($"FlushCadenceBlocks must be >= 1 (got {blocks}).", paramName);

            return blocks > 1 ? new FixedIntervalFlushCadence((ulong)blocks) : null;
        }
    }
}
