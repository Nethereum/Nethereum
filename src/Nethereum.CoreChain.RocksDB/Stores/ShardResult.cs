using System.Collections.Generic;
using Nethereum.CoreChain.Storage;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal sealed class ShardResult
    {
        public FlatStateReconcileResult Result;
        public List<(byte[] AccountHash, byte[] StorageRoot)> Unresolvable;
        public List<(byte[] Owner, byte[] Path)> MissingNodes;
        public HashSet<byte[]> MissingCode;
    }
}
