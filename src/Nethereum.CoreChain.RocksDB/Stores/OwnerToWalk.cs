

namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal sealed class OwnerToWalk
    {
        public byte[] Owner;
        public byte[] ExpectedStorageRoot;
        public byte[] ComputedRoot;
        public bool Malformed;
    }
}
