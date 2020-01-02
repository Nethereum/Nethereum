using Nethereum.GSN.Models;
using System.Collections.Generic;

namespace Nethereum.GSN
{
    internal class RelayEventComparer : IEqualityComparer<RelayEvent>
    {
        public bool Equals(RelayEvent x, RelayEvent y)
        {
            if (object.ReferenceEquals(x, y)) return true;

            if (object.ReferenceEquals(x, null) || object.ReferenceEquals(y, null))
                return false;

            return x.TxHash == y.TxHash;
        }

        public int GetHashCode(RelayEvent obj)
        {
            return obj.TxHash.GetHashCode();
        }
    }
}
