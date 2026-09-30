using System.Collections.Generic;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.Snap.Healing
{
    public static class DeferredHealCodeCodec
    {
        private const int EntrySize = 32;

        public static byte[] Encode(IReadOnlyList<byte[]> codeHashes)
        {
            if (codeHashes == null || codeHashes.Count == 0) return System.Array.Empty<byte>();
            var seen = new HashSet<byte[]>(ByteArrayComparer.Current);
            var valid = new List<byte[]>(codeHashes.Count);
            foreach (var h in codeHashes)
                if (h is { Length: EntrySize } && seen.Add(h)) valid.Add(h);
            if (valid.Count == 0) return System.Array.Empty<byte>();
            var blob = new byte[valid.Count * EntrySize];
            for (int i = 0; i < valid.Count; i++)
                valid[i].CopyTo(blob, i * EntrySize);
            return blob;
        }

        public static List<byte[]> Decode(byte[] blob)
        {
            var list = new List<byte[]>();
            if (blob == null || blob.Length == 0) return list;
            for (int offset = 0; offset + EntrySize <= blob.Length; offset += EntrySize)
            {
                var hash = new byte[EntrySize];
                System.Buffer.BlockCopy(blob, offset, hash, 0, EntrySize);
                list.Add(hash);
            }
            return list;
        }
    }
}
