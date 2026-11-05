using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.Snap.Storage
{
    internal static class SnapByteCodes
    {
        internal const int MaxRequestCount = 84;

        internal static IEnumerable<List<byte[]>> Chunks(IReadOnlyList<byte[]> hashes)
        {
            for (int offset = 0; offset < hashes.Count; offset += MaxRequestCount)
            {
                var count = Math.Min(MaxRequestCount, hashes.Count - offset);
                var chunk = new List<byte[]>(count);
                for (int i = 0; i < count; i++) chunk.Add(hashes[offset + i]);
                yield return chunk;
            }
        }

        internal static List<byte[]> Requestable(IReadOnlyList<byte[]> hashes)
        {
            var seen = new HashSet<byte[]>(ByteArrayComparer.Current);
            var result = new List<byte[]>(hashes.Count);
            foreach (var h in hashes)
            {
                if (h == null || h.Length == 0) continue;
                if (ByteUtil.AreEqual(h, DefaultValues.EMPTY_DATA_HASH)) continue;
                if (seen.Add(h)) result.Add(h);
            }
            return result;
        }

        internal static IEnumerable<(byte[] Hash, byte[] Code)> Matched(
            IReadOnlyList<byte[]> codes, Func<byte[], bool> wanted)
        {
            var keccak = Sha3Keccack.Current;
            foreach (var code in codes)
            {
                if (code == null || code.Length == 0) continue;
                var hash = keccak.CalculateHash(code);
                if (!wanted(hash)) continue;
                yield return (hash, code);
            }
        }

        internal static async Task<List<byte[]>> AbsentAsync(IStateStore state, IEnumerable<byte[]> hashes)
        {
            var absent = new List<byte[]>();
            foreach (var h in hashes)
            {
                var existing = await state.GetCodeAsync(h).ConfigureAwait(false);
                if (existing == null || existing.Length == 0) absent.Add(h);
            }
            return absent;
        }
    }
}
