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
}
