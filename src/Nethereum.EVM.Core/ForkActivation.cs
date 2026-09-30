namespace Nethereum.EVM
{
    public readonly struct ForkActivation
    {
        private ForkActivation(HardforkName fork, long? block, ulong? timestamp, bool countsTowardForkIdentity)
        {
            Fork = fork;
            Block = block;
            Timestamp = timestamp;
            CountsTowardForkIdentity = countsTowardForkIdentity;
        }

        public HardforkName Fork { get; }

        public long? Block { get; }

        public ulong? Timestamp { get; }

        public bool IsScheduled => Block.HasValue || Timestamp.HasValue;

        /// <summary>
        /// EIP-2124 checksums a chain's fork boundaries to identify it to a peer. Not every
        /// activation is one: Paris arrived on total difficulty rather than a block, and geth's
        /// mainnet configuration carries no MergeNetsplitBlock for it, so a client that
        /// checksummed it would fail to handshake with every client that did not.
        /// </summary>
        public bool CountsTowardForkIdentity { get; }

        public static ForkActivation AtBlock(HardforkName fork, long block, bool countsTowardForkIdentity = true) =>
            new ForkActivation(fork, block, null, countsTowardForkIdentity);

        public static ForkActivation AtTimestamp(HardforkName fork, ulong timestamp, bool countsTowardForkIdentity = true) =>
            new ForkActivation(fork, null, timestamp, countsTowardForkIdentity);

        public static ForkActivation Unscheduled(HardforkName fork) =>
            new ForkActivation(fork, null, null, false);

        public bool HasBeenReachedAt(long blockNumber, ulong timestamp)
        {
            if (Timestamp.HasValue) return timestamp >= Timestamp.Value;
            if (Block.HasValue) return blockNumber >= Block.Value;

            return false;
        }
    }
}
