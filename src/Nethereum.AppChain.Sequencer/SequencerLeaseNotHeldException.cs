using System;

namespace Nethereum.AppChain.Sequencer
{
    public sealed class SequencerLeaseNotHeldException : InvalidOperationException
    {
        public long BlockNumber { get; }

        public SequencerLeaseNotHeldException(long blockNumber)
            : base($"Block {blockNumber} was not sealed: the producer authority does not currently name this node.")
        {
            BlockNumber = blockNumber;
        }
    }
}
