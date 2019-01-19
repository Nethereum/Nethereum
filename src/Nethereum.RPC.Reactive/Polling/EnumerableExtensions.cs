using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("Nethereum.RPC.Reactive.UnitTests")]

namespace Nethereum.RPC.Reactive.Polling
{
    internal static class EnumerableExtensions
    {
        internal static IEnumerable<BigInteger> Range(BigInteger start, BigInteger count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

            for (var i = 0; i < count; i++)
                yield return start + i;
        }
    }
}