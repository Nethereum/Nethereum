namespace Nethereum.EVM
{
    public sealed class ForkIdentityThresholds
    {
        public ForkIdentityThresholds(ulong[] blockHeights, ulong[] timestamps)
        {
            BlockHeights = blockHeights;
            Timestamps = timestamps;
        }

        public ulong[] BlockHeights { get; }

        public ulong[] Timestamps { get; }
    }
}
