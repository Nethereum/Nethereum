using System;
using Nethereum.CoreChain.Freezer;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class ImmutabilityDepthFinalitySource : IFinalitySource
    {
        private readonly Func<long> _tipHeight;
        private readonly long _immutabilityThreshold;

        public ImmutabilityDepthFinalitySource(Func<long> tipHeight, long immutabilityThreshold)
        {
            _tipHeight = tipHeight ?? throw new ArgumentNullException(nameof(tipHeight));
            _immutabilityThreshold = immutabilityThreshold;
        }

        public long FinalizedBlockNumber => _tipHeight() - _immutabilityThreshold;
    }
}
