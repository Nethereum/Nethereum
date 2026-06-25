using System;
using System.Linq;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// EIP-2124 ForkID computation:
    ///   forkHash = CRC32-IEEE(genesisHash ++ uint64BE(fork_block_1) ++ ... ++ uint64BE(fork_timestamp_1) ++ ...)
    /// Fork blocks/timestamps at 0 are skipped (those forks are part of
    /// genesis itself). Consecutive duplicates are dropped. Fork timestamps
    /// come AFTER all fork blocks in the digest.
    ///
    /// Spec: https://eips.ethereum.org/EIPS/eip-2124
    /// The CRC32 polynomial is IEEE (0xEDB88320, same as PKZIP/Ethernet).
    /// </summary>
    public static class Eip2124ForkIdCalculator
    {
        public static uint ComputeForkHash(byte[] genesisHash, ulong[] forkBlocks, ulong[] forkTimestamps)
        {
            if (genesisHash == null || genesisHash.Length != 32)
                throw new ArgumentException("genesisHash must be 32 bytes", nameof(genesisHash));

            uint crc = 0xffffffff;
            crc = Crc32IeeeUpdate(crc, genesisHash);

            ulong last = 0;
            foreach (var fb in (forkBlocks ?? Array.Empty<ulong>()).OrderBy(b => b))
            {
                if (fb == 0 || fb == last) continue;
                crc = Crc32IeeeUpdate(crc, Uint64Be(fb));
                last = fb;
            }
            last = 0;
            foreach (var ts in (forkTimestamps ?? Array.Empty<ulong>()).OrderBy(t => t))
            {
                if (ts == 0 || ts == last) continue;
                crc = Crc32IeeeUpdate(crc, Uint64Be(ts));
                last = ts;
            }
            return crc ^ 0xffffffff;
        }

        private static readonly uint[] _crcTable = BuildTable();

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                table[i] = c;
            }
            return table;
        }

        private static uint Crc32IeeeUpdate(uint crc, byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
                crc = _crcTable[(crc ^ data[i]) & 0xff] ^ (crc >> 8);
            return crc;
        }

        private static byte[] Uint64Be(ulong v)
        {
            var b = new byte[8];
            for (int i = 7; i >= 0; i--) { b[i] = (byte)(v & 0xff); v >>= 8; }
            return b;
        }
    }
}
