using System.Runtime.CompilerServices;
using Nethereum.Util;

namespace Nethereum.EVM.Gas
{
    internal static class WordCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long Of(EvmUInt256 length)
        {
            return length.FitsInInt
                ? (length.ToInt() + 31L) >> 5
                : ((length + new EvmUInt256(31)) / new EvmUInt256(32)).ToLongSafe();
        }
    }
}
