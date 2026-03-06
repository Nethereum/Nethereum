using System;
using System.Numerics;

namespace Nethereum.CoreChain.Storage
{
    public class HistoricalStateNotAvailableException : InvalidOperationException
    {
        public BigInteger RequestedBlock { get; }
        public BigInteger OldestAvailableBlock { get; }

        public HistoricalStateNotAvailableException(BigInteger requestedBlock, BigInteger oldestAvailableBlock)
            : base($"historical state not available for block {requestedBlock}, oldest available block is {oldestAvailableBlock}")
        {
            RequestedBlock = requestedBlock;
            OldestAvailableBlock = oldestAvailableBlock;
        }
    }
}
