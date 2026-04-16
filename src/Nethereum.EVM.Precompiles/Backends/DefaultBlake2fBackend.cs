using Nethereum.EVM.Execution.Precompiles.CryptoBackends;

namespace Nethereum.EVM.Precompiles.Backends
{
    /// <summary>
    /// Default BLAKE2F (EIP-152) backend — a managed port of the BLAKE2b
    /// compression function. Lifted byte-for-byte from the previous
    /// inline implementation in <c>Blake2fPrecompile</c>; the Zisk sync
    /// path supplies its own witness-backed variant that calls the
    /// native <c>blake2b_compress_c</c> primitive.
    /// </summary>
    public sealed class DefaultBlake2fBackend : IBlake2fBackend
    {
        public static readonly DefaultBlake2fBackend Instance = new DefaultBlake2fBackend();

        private static readonly ulong[] Iv = new ulong[]
        {
            0x6a09e667f3bcc908UL, 0xbb67ae8584caa73bUL,
            0x3c6ef372fe94f82bUL, 0xa54ff53a5f1d36f1UL,
            0x510e527fade682d1UL, 0x9b05688c2b3e6c1fUL,
            0x1f83d9abfb41bd6bUL, 0x5be0cd19137e2179UL
        };

        private static readonly int[][] Sigma = new int[][]
        {
            new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 },
            new int[] { 14, 10, 4, 8, 9, 15, 13, 6, 1, 12, 0, 2, 11, 7, 5, 3 },
            new int[] { 11, 8, 12, 0, 5, 2, 15, 13, 10, 14, 3, 6, 7, 1, 9, 4 },
            new int[] { 7, 9, 3, 1, 13, 12, 11, 14, 2, 6, 5, 10, 4, 0, 15, 8 },
            new int[] { 9, 0, 5, 7, 2, 4, 10, 15, 14, 1, 11, 12, 6, 8, 3, 13 },
            new int[] { 2, 12, 6, 10, 0, 11, 8, 3, 4, 13, 7, 5, 15, 14, 1, 9 },
            new int[] { 12, 5, 1, 15, 14, 13, 4, 10, 0, 7, 6, 3, 9, 2, 8, 11 },
            new int[] { 13, 11, 7, 14, 12, 1, 3, 9, 5, 0, 15, 4, 8, 6, 2, 10 },
            new int[] { 6, 15, 14, 9, 11, 3, 0, 8, 12, 2, 13, 7, 1, 4, 10, 5 },
            new int[] { 10, 2, 8, 4, 7, 6, 1, 5, 15, 11, 9, 14, 3, 12, 13, 0 }
        };

        public void Compress(uint rounds, ulong[] h, ulong[] m, ulong t0, ulong t1, bool finalBlock)
        {
            var v = new ulong[16];
            for (int i = 0; i < 8; i++)
            {
                v[i] = h[i];
                v[i + 8] = Iv[i];
            }

            v[12] ^= t0;
            v[13] ^= t1;
            if (finalBlock) v[14] = ~v[14];

            for (uint r = 0; r < rounds; r++)
            {
                var s = Sigma[r % 10];

                G(ref v[0], ref v[4], ref v[8],  ref v[12], m[s[0]],  m[s[1]]);
                G(ref v[1], ref v[5], ref v[9],  ref v[13], m[s[2]],  m[s[3]]);
                G(ref v[2], ref v[6], ref v[10], ref v[14], m[s[4]],  m[s[5]]);
                G(ref v[3], ref v[7], ref v[11], ref v[15], m[s[6]],  m[s[7]]);
                G(ref v[0], ref v[5], ref v[10], ref v[15], m[s[8]],  m[s[9]]);
                G(ref v[1], ref v[6], ref v[11], ref v[12], m[s[10]], m[s[11]]);
                G(ref v[2], ref v[7], ref v[8],  ref v[13], m[s[12]], m[s[13]]);
                G(ref v[3], ref v[4], ref v[9],  ref v[14], m[s[14]], m[s[15]]);
            }

            for (int i = 0; i < 8; i++)
                h[i] ^= v[i] ^ v[i + 8];
        }

        private static void G(ref ulong a, ref ulong b, ref ulong c, ref ulong d, ulong x, ulong y)
        {
            a = a + b + x;
            d = RotateRight(d ^ a, 32);
            c = c + d;
            b = RotateRight(b ^ c, 24);
            a = a + b + y;
            d = RotateRight(d ^ a, 16);
            c = c + d;
            b = RotateRight(b ^ c, 63);
        }

        private static ulong RotateRight(ulong value, int bits)
        {
            return (value >> bits) | (value << (64 - bits));
        }
    }
}
