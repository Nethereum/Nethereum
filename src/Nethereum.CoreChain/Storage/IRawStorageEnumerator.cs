using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.CoreChain.Storage
{
    public interface IRawStorageEnumerator
    {
        System.Collections.Generic.IAsyncEnumerable<KeyValuePair<BigInteger, byte[]>>
            StreamRawStorageAsync(string address);
    }
}
