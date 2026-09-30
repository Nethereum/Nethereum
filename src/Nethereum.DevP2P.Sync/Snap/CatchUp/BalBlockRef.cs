namespace Nethereum.DevP2P.Sync.Snap.CatchUp
{
    public sealed class BalBlockRef
    {
        public BalBlockRef(byte[] blockHash, byte[] blockAccessListHash)
        {
            BlockHash = blockHash;
            BlockAccessListHash = blockAccessListHash;
        }

        public byte[] BlockHash { get; }

        public byte[] BlockAccessListHash { get; }
    }
}
