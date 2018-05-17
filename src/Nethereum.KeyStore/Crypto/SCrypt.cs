using System;
using System.Diagnostics;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;

namespace Nethereum.KeyStore.Crypto
{
    /* Scrypt and SalsaCore from BouncyCastle
    Changes:
                //The Scrypt original spec says that 
                // N CPU/ Memory cost parameter, must be larger than 1,
                // a power of 2 and less than 2 ^ (128 * r / 8).
                
                // But the Ethereum test vectors use a bigger cost, with an r of only 1, so to pass
                // the test vectors and allow for further cost we allow this.

                //avoiding the: throw new ArgumentException("Cost parameter N must be > 1 and < 65536.");
     
     License:
     Please note this should be read in the same way as the MIT license.

    Please also note this licensing model is made possible through funding from donations and the sale of support contracts.

     LICENSE
    Copyright (c) 2000 - 2018 The Legion of the Bouncy Castle Inc. (https://www.bouncycastle.org)

    Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

     */

    /// <summary>Implementation of the scrypt a password-based key derivation function.</summary>
    /// <remarks>
    /// Scrypt was created by Colin Percival and is specified in
    /// <a href="http://tools.ietf.org/html/draft-josefsson-scrypt-kdf-01">draft-josefsson-scrypt-kd</a>.
    /// </remarks>
    public class SCrypt
    {

        internal static uint R(uint x, int y)
        {
            return (x << y) | (x >> (32 - y));
        }

        internal static void SalsaCore(int rounds, uint[] input, uint[] x)
        {
            if (input.Length != 16)
                throw new ArgumentException();
            if (x.Length != 16)
                throw new ArgumentException();
            if (rounds % 2 != 0)
                throw new ArgumentException("Number of rounds must be even");

            uint x00 = input[0];
            uint x01 = input[1];
            uint x02 = input[2];
            uint x03 = input[3];
            uint x04 = input[4];
            uint x05 = input[5];
            uint x06 = input[6];
            uint x07 = input[7];
            uint x08 = input[8];
            uint x09 = input[9];
            uint x10 = input[10];
            uint x11 = input[11];
            uint x12 = input[12];
            uint x13 = input[13];
            uint x14 = input[14];
            uint x15 = input[15];

            for (int i = rounds; i > 0; i -= 2)
            {
                x04 ^= R((x00 + x12), 7);
                x08 ^= R((x04 + x00), 9);
                x12 ^= R((x08 + x04), 13);
                x00 ^= R((x12 + x08), 18);
                x09 ^= R((x05 + x01), 7);
                x13 ^= R((x09 + x05), 9);
                x01 ^= R((x13 + x09), 13);
                x05 ^= R((x01 + x13), 18);
                x14 ^= R((x10 + x06), 7);
                x02 ^= R((x14 + x10), 9);
                x06 ^= R((x02 + x14), 13);
                x10 ^= R((x06 + x02), 18);
                x03 ^= R((x15 + x11), 7);
                x07 ^= R((x03 + x15), 9);
                x11 ^= R((x07 + x03), 13);
                x15 ^= R((x11 + x07), 18);

                x01 ^= R((x00 + x03), 7);
                x02 ^= R((x01 + x00), 9);
                x03 ^= R((x02 + x01), 13);
                x00 ^= R((x03 + x02), 18);
                x06 ^= R((x05 + x04), 7);
                x07 ^= R((x06 + x05), 9);
                x04 ^= R((x07 + x06), 13);
                x05 ^= R((x04 + x07), 18);
                x11 ^= R((x10 + x09), 7);
                x08 ^= R((x11 + x10), 9);
                x09 ^= R((x08 + x11), 13);
                x10 ^= R((x09 + x08), 18);
                x12 ^= R((x15 + x14), 7);
                x13 ^= R((x12 + x15), 9);
                x14 ^= R((x13 + x12), 13);
                x15 ^= R((x14 + x13), 18);
            }

            x[0] = x00 + input[0];
            x[1] = x01 + input[1];
            x[2] = x02 + input[2];
            x[3] = x03 + input[3];
            x[4] = x04 + input[4];
            x[5] = x05 + input[5];
            x[6] = x06 + input[6];
            x[7] = x07 + input[7];
            x[8] = x08 + input[8];
            x[9] = x09 + input[9];
            x[10] = x10 + input[10];
            x[11] = x11 + input[11];
            x[12] = x12 + input[12];
            x[13] = x13 + input[13];
            x[14] = x14 + input[14];
            x[15] = x15 + input[15];
        }


        internal static byte[] UInt32_To_LE(uint n)
        {
            byte[] bs = new byte[4];
            UInt32_To_LE(n, bs, 0);
            return bs;
        }

        internal static void UInt32_To_LE(uint n, byte[] bs)
        {
            bs[0] = (byte)(n);
            bs[1] = (byte)(n >> 8);
            bs[2] = (byte)(n >> 16);
            bs[3] = (byte)(n >> 24);
        }

        internal static void UInt32_To_LE(uint n, byte[] bs, int off)
        {
            bs[off] = (byte)(n);
            bs[off + 1] = (byte)(n >> 8);
            bs[off + 2] = (byte)(n >> 16);
            bs[off + 3] = (byte)(n >> 24);
        }

        internal static byte[] UInt32_To_LE(uint[] ns)
        {
            byte[] bs = new byte[4 * ns.Length];
            UInt32_To_LE(ns, bs, 0);
            return bs;
        }

        internal static void UInt32_To_LE(uint[] ns, byte[] bs, int off)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                UInt32_To_LE(ns[i], bs, off);
                off += 4;
            }
        }

        internal static uint LE_To_UInt32(byte[] bs)
        {
            return (uint)bs[0]
                   | (uint)bs[1] << 8
                   | (uint)bs[2] << 16
                   | (uint)bs[3] << 24;
        }

        internal static uint LE_To_UInt32(byte[] bs, int off)
        {
            return (uint)bs[off]
                   | (uint)bs[off + 1] << 8
                   | (uint)bs[off + 2] << 16
                   | (uint)bs[off + 3] << 24;
        }

        internal static void LE_To_UInt32(byte[] bs, int off, uint[] ns)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                ns[i] = LE_To_UInt32(bs, off);
                off += 4;
            }
        }

        internal static void LE_To_UInt32(byte[] bs, int bOff, uint[] ns, int nOff, int count)
        {
            for (int i = 0; i < count; ++i)
            {
                ns[nOff + i] = LE_To_UInt32(bs, bOff);
                bOff += 4;
            }
        }

        internal static uint[] LE_To_UInt32(byte[] bs, int off, int count)
        {
            uint[] ns = new uint[count];
            for (int i = 0; i < ns.Length; ++i)
            {
                ns[i] = LE_To_UInt32(bs, off);
                off += 4;
            }
            return ns;
        }

        /// <summary>Generate a key using the scrypt key derivation function.</summary>
        /// <param name="P">the bytes of the pass phrase.</param>
        /// <param name="S">the salt to use for this invocation.</param>
        /// <param name="N">CPU/Memory cost parameter. Must be larger than 1, a power of 2 and less than
        ///     <code>2^(128 * r / 8)</code>.</param>
        /// <param name="r">the block size, must be >= 1.</param>
        /// <param name="p">Parallelization parameter. Must be a positive integer less than or equal to
        ///     <code>Int32.MaxValue / (128 * r * 8)</code>.</param>
        /// <param name="dkLen">the length of the key to generate.</param>
        /// <returns>the generated key.</returns>
        public static byte[] Generate(byte[] P, byte[] S, int N, int r, int p, int dkLen)
        {
            if (P == null)
                throw new ArgumentNullException("Passphrase P must be provided.");
            if (S == null)
                throw new ArgumentNullException("Salt S must be provided.");
            if (N <= 1 || !IsPowerOf2(N))
                throw new ArgumentException("Cost parameter N must be > 1 and a power of 2.");
            // Only value of r that cost (as an int) could be exceeded for is 1
            if (r == 1 && N >= 65536)

                //The spec says that 
                // N CPU/ Memory cost parameter, must be larger than 1,
                // a power of 2 and less than 2 ^ (128 * r / 8).
                
                // But the Ethereum test vectors use a bigger cost, with an r of only 1, so to pass
                // the test vectors and allow for further cost we allow this.

                //throw new ArgumentException("Cost parameter N must be > 1 and < 65536.");
                if (r < 1)
                    throw new ArgumentException("Block size r must be >= 1.");
            int maxParallel = Int32.MaxValue / (128 * r * 8);
            if (p < 1 || p > maxParallel)
            {
                throw new ArgumentException("Parallelisation parameter p must be >= 1 and <= " + maxParallel
                                                                                               + " (based on block size r of " + r + ")");
            }
            if (dkLen < 1)
                throw new ArgumentException("Generated key length dkLen must be >= 1.");

            return MFcrypt(P, S, N, r, p, dkLen);
        }

        private static byte[] MFcrypt(byte[] P, byte[] S, int N, int r, int p, int dkLen)
        {
            int MFLenBytes = r * 128;
            byte[] bytes = SingleIterationPBKDF2(P, S, p * MFLenBytes);

            uint[] B = null;

            try
            {
                int BLen = bytes.Length >> 2;
                B = new uint[BLen];

                LE_To_UInt32(bytes, 0, B);

                int MFLenWords = MFLenBytes >> 2;
                for (int BOff = 0; BOff < BLen; BOff += MFLenWords)
                {
                    // TODO These can be done in parallel threads
                    SMix(B, BOff, N, r);
                }

                UInt32_To_LE(B, bytes, 0);

                return SingleIterationPBKDF2(P, bytes, dkLen);
            }
            finally
            {
                ClearAll(bytes, B);
            }
        }

        private static byte[] SingleIterationPBKDF2(byte[] P, byte[] S, int dkLen)
        {
            PbeParametersGenerator pGen = new Pkcs5S2ParametersGenerator(new Sha256Digest());
            pGen.Init(P, S, 1);
            KeyParameter key = (KeyParameter)pGen.GenerateDerivedMacParameters(dkLen * 8);
            return key.GetKey();
        }

        private static void SMix(uint[] B, int BOff, int N, int r)
        {
            int BCount = r * 32;

            uint[] blockX1 = new uint[16];
            uint[] blockX2 = new uint[16];
            uint[] blockY = new uint[BCount];

            uint[] X = new uint[BCount];
            uint[][] V = new uint[N][];

            try
            {
                Array.Copy(B, BOff, X, 0, BCount);

                for (int i = 0; i < N; ++i)
                {
                    V[i] = (uint[])X.Clone();
                    BlockMix(X, blockX1, blockX2, blockY, r);
                }

                uint mask = (uint)N - 1;
                for (int i = 0; i < N; ++i)
                {
                    uint j = X[BCount - 16] & mask;
                    Xor(X, V[j], 0, X);
                    BlockMix(X, blockX1, blockX2, blockY, r);
                }

                Array.Copy(X, 0, B, BOff, BCount);
            }
            finally
            {
                ClearAll(V);
                ClearAll(X, blockX1, blockX2, blockY);
            }
        }

        private static void BlockMix(uint[] B, uint[] X1, uint[] X2, uint[] Y, int r)
        {
            Array.Copy(B, B.Length - 16, X1, 0, 16);

            int BOff = 0, YOff = 0, halfLen = B.Length >> 1;

            for (int i = 2 * r; i > 0; --i)
            {
                Xor(X1, B, BOff, X2);

                SalsaCore(8, X2, X1);
                Array.Copy(X1, 0, Y, YOff, 16);

                YOff = halfLen + BOff - YOff;
                BOff += 16;
            }

            Array.Copy(Y, 0, B, 0, Y.Length);
        }

        private static void Xor(uint[] a, uint[] b, int bOff, uint[] output)
        {
            for (int i = output.Length - 1; i >= 0; --i)
            {
                output[i] = a[i] ^ b[bOff + i];
            }
        }

        private static void Clear(Array array)
        {
            if (array != null)
            {
                Array.Clear(array, 0, array.Length);
            }
        }

        private static void ClearAll(params Array[] arrays)
        {
            foreach (Array array in arrays)
            {
                Clear(array);
            }
        }

        // note: we know X is non-zero
        private static bool IsPowerOf2(int x)
        {
            Debug.Assert(x != 0);

            return (x & (x - 1)) == 0;
        }
    }
}