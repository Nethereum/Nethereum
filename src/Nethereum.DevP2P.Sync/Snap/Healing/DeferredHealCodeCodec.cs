using System;
using System.Collections.Generic;

namespace Nethereum.DevP2P.Sync.Snap.Healing
{
    [Obsolete("Renamed: use NeedsHealCodeCodec. Contract code that needs heal is fetched by the heal; nothing is deferred.")]
    public static class DeferredHealCodeCodec
    {
        public static byte[] Encode(IReadOnlyList<byte[]> codeHashes)
            => NeedsHealCodeCodec.Encode(codeHashes);

        public static List<byte[]> Decode(byte[] blob)
            => NeedsHealCodeCodec.Decode(blob);

        internal static HashSet<byte[]> DecodeSet(byte[] blob)
            => NeedsHealCodeCodec.DecodeSet(blob);

        internal static byte[] EncodeSet(HashSet<byte[]> set)
            => NeedsHealCodeCodec.EncodeSet(set);
    }
}
