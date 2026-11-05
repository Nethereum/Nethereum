using System.Collections.Generic;
using Nethereum.Util;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal sealed class OwnerPathComparer : IEqualityComparer<(byte[] Owner, byte[] Path)>
    {
        public static readonly OwnerPathComparer Instance = new OwnerPathComparer();

        public bool Equals((byte[] Owner, byte[] Path) x, (byte[] Owner, byte[] Path) y)
            => ByteUtil.AreEqual(x.Owner, y.Owner) && ByteUtil.AreEqual(x.Path, y.Path);

        public int GetHashCode((byte[] Owner, byte[] Path) node)
            => ByteArrayComparer.Current.GetHashCode(node.Owner) * 31
               + ByteArrayComparer.Current.GetHashCode(node.Path);
    }
}
