using System;
using System.Numerics;
using System.Security.Cryptography;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Nethereum.Signer;
using Nethereum.Signer.Crypto;
using Nethereum.EVM.Gas;
using Org.BouncyCastle.Crypto.Digests;

namespace Nethereum.EVM.Execution
{
    public class EvmPreCompiledContractsExecution
    {
        public virtual BigInteger GetPrecompileGasCost(string address, byte[] data)
        {
            int dataLen = data?.Length ?? 0;
            int wordCount = (dataLen + 31) / 32;

            switch (address.ToHexCompact())
            {
                case "1": // ECRECOVER
                    return GasConstants.ECRECOVER_GAS;
                case "2": // SHA256
                    return GasConstants.SHA256_BASE_GAS + GasConstants.SHA256_PER_WORD_GAS * wordCount;
                case "3": // RIPEMD160
                    return GasConstants.RIPEMD160_BASE_GAS + GasConstants.RIPEMD160_PER_WORD_GAS * wordCount;
                case "4": // IDENTITY (datacopy)
                    return GasConstants.IDENTITY_BASE_GAS + GasConstants.IDENTITY_PER_WORD_GAS * wordCount;
                case "5": // MODEXP - dynamic gas cost
                    return GetModExpGas(data);
                case "6": // BN128_ADD
                    return 150; // EIP-1108
                case "7": // BN128_MUL
                    return 6000; // EIP-1108
                case "8": // BN128_PAIRING
                    return GetBn128PairingGas(dataLen);
                case "9": // BLAKE2F
                    return GetBlake2fGas(data);
            }
            return 0;
        }

        private BigInteger GetModExpGas(byte[] data)
        {
            data = data ?? new byte[0];

            int offset = 0;
            var baseLen = (int)ReadBigInteger(data, offset, 32);
            offset += 32;
            var expLen = (int)ReadBigInteger(data, offset, 32);
            offset += 32;
            var modLen = (int)ReadBigInteger(data, offset, 32);
            offset += 32;

            // Calculate exponent head (first 32 bytes of exponent, or all if shorter)
            // Per EIP-2565: we only use the bit length of the first 32 bytes
            BigInteger expHead = 0;
            if (expLen > 0)
            {
                int headLen = Math.Min(32, expLen);
                for (int i = 0; i < headLen && (offset + baseLen + i) < data.Length; i++)
                {
                    expHead = (expHead << 8) | data[offset + baseLen + i];
                }
            }

            // Bit length of exponent head
            int expBitLen = 0;
            if (expHead > 0)
            {
                var temp = expHead;
                while (temp > 0)
                {
                    expBitLen++;
                    temp >>= 1;
                }
            }

            // Calculate iteration count per EIP-2565
            // iteration_count = 8 * (expLen - 32) + ((expBitLen - 1) if expHead > 0 else 0)
            BigInteger iterationCount;
            if (expLen <= 32 && expHead == 0)
            {
                iterationCount = 0;
            }
            else if (expLen <= 32)
            {
                iterationCount = expBitLen - 1;
            }
            else
            {
                // When expLen > 32, add (expBitLen - 1) only if expHead > 0
                iterationCount = 8 * (expLen - 32) + (expHead > 0 ? expBitLen - 1 : 0);
            }
            if (iterationCount < 1) iterationCount = 1;

            // Calculate multiplication complexity (use BigInteger to avoid overflow)
            BigInteger maxLen = Math.Max(baseLen, modLen);
            BigInteger mulComplexity;
            if (maxLen <= 64)
            {
                mulComplexity = maxLen * maxLen;
            }
            else if (maxLen <= 1024)
            {
                mulComplexity = maxLen * maxLen / 4 + 96 * maxLen - 3072;
            }
            else
            {
                mulComplexity = maxLen * maxLen / 16 + 480 * maxLen - 199680;
            }

            // EIP-2565: gas = max(200, floor(mulComplexity * iterationCount / 3))
            var gas = mulComplexity * iterationCount / 3;
            return gas < 200 ? 200 : gas;
        }

        private BigInteger GetBn128PairingGas(int dataLen)
        {
            // EIP-1108: 34000 * k + 45000 where k = number of pairs
            int k = dataLen / 192;
            return 34000 * k + 45000;
        }

        private BigInteger GetBlake2fGas(byte[] data)
        {
            if (data == null || data.Length < 4)
                return 0;
            // Gas = rounds (first 4 bytes as big-endian uint32)
            return (uint)((data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3]);
        }

        public virtual bool IsPrecompiledAdress(string address)
        {
            switch (address.ToHexCompact())
            {
                case "1":
                case "2":
                case "3":
                case "4":
                case "5":
                case "6":
                case "7":
                case "8":
                case "9":
                    return true;
            }

            return false;
        }
        public virtual byte[] ExecutePreCompile(string address, byte[] data)
        {
            switch (address.ToHexCompact())
            {
                case "1":
                    return EcRecover(data);
                case "2":
                    return Sha256Hash(data);
                case "3":
                    return Ripemd160Hash(data);
                case "4":
                    return DataCopy(data);
                case "5":
                    return ModExp(data);
                case "6":
                    return BN128Curve.Add(data);
                case "7":
                    return BN128Curve.Mul(data);
                case "8":
                    return BN128Curve.Pairing(data);
                case "9":
                    return Blake2f(data);
            }

            return null;
        }

        public byte[] EcRecover(byte[] data)
        {
            data = data.PadTo128Bytes();
            var hash = data.Slice(0, 32);
            var v = data[63];
            var r = data.Slice(64, 96);
            var s = data.Slice(96, 128);

            byte[] recoveredAddress;
            try
            {
                recoveredAddress = EthECKey.RecoverFromSignature(EthECDSASignatureFactory.FromComponents(r, s, new byte[] { v }), hash).GetPublicAddressAsBytes();
            }
            catch
            {
                return new byte[0]; // ECRECOVER returns empty on failure (per Yellow Paper)
            }

            return recoveredAddress.PadTo32Bytes();
        }

        public byte[] DataCopy(byte[] data)
        {
            return data;
        }

        public byte[] Sha256Hash(byte[] data)
        {
            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(data ?? new byte[0]);
            }
        }

        public byte[] Ripemd160Hash(byte[] data)
        {
            var digest = new RipeMD160Digest();
            var input = data ?? new byte[0];
            digest.BlockUpdate(input, 0, input.Length);
            var result = new byte[20];
            digest.DoFinal(result, 0);
            return result.PadTo32Bytes();
        }

        public byte[] ModExp(byte[] data)
        {
            data = data ?? new byte[0];

            int offset = 0;
            var baseLen = (int)ReadBigInteger(data, offset, 32);
            offset += 32;
            var expLen = (int)ReadBigInteger(data, offset, 32);
            offset += 32;
            var modLen = (int)ReadBigInteger(data, offset, 32);
            offset += 32;

            if (modLen == 0) return new byte[0];

            var baseVal = ReadBigIntegerUnsigned(data, offset, baseLen);
            offset += baseLen;
            var expVal = ReadBigIntegerUnsigned(data, offset, expLen);
            offset += expLen;
            var modVal = ReadBigIntegerUnsigned(data, offset, modLen);

            if (modVal == 0) return new byte[modLen];

            var result = BigInteger.ModPow(baseVal, expVal, modVal);
            var resultBytes = result.ToByteArray();

            if (resultBytes.Length > 1 && resultBytes[resultBytes.Length - 1] == 0)
            {
                var trimmed = new byte[resultBytes.Length - 1];
                Array.Copy(resultBytes, trimmed, trimmed.Length);
                resultBytes = trimmed;
            }

            Array.Reverse(resultBytes);

            if (resultBytes.Length == modLen) return resultBytes;
            if (resultBytes.Length > modLen)
            {
                var truncated = new byte[modLen];
                Array.Copy(resultBytes, resultBytes.Length - modLen, truncated, 0, modLen);
                return truncated;
            }

            var padded = new byte[modLen];
            Array.Copy(resultBytes, 0, padded, modLen - resultBytes.Length, resultBytes.Length);
            return padded;
        }

        private BigInteger ReadBigInteger(byte[] data, int offset, int length)
        {
            if (offset >= data.Length) return 0;
            var actualLength = Math.Min(length, data.Length - offset);
            var bytes = new byte[actualLength];
            Array.Copy(data, offset, bytes, 0, actualLength);
            Array.Reverse(bytes);
            var result = new BigInteger(bytes);
            return result < 0 ? -result : result;
        }

        private BigInteger ReadBigIntegerUnsigned(byte[] data, int offset, int length)
        {
            if (length == 0) return 0;
            if (offset >= data.Length) return 0;
            var actualLength = Math.Min(length, data.Length - offset);
            var bytes = new byte[actualLength + 1];
            Array.Copy(data, offset, bytes, 0, actualLength);
            Array.Reverse(bytes, 0, actualLength);
            return new BigInteger(bytes);
        }

        public byte[] Blake2f(byte[] data)
        {
            if (data == null || data.Length != 213)
                throw new ArgumentException("Invalid Blake2f input: expected 213 bytes");

            var rounds = (uint)((data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3]);
            if (rounds > 0xFFFFFFFF)
                throw new ArgumentException("Invalid Blake2f rounds");

            var h = new ulong[8];
            for (int i = 0; i < 8; i++)
            {
                h[i] = BitConverter.ToUInt64(data, 4 + i * 8);
            }

            var m = new ulong[16];
            for (int i = 0; i < 16; i++)
            {
                m[i] = BitConverter.ToUInt64(data, 68 + i * 8);
            }

            var t0 = BitConverter.ToUInt64(data, 196);
            var t1 = BitConverter.ToUInt64(data, 204);
            var f = data[212] != 0;

            if (data[212] > 1)
                throw new ArgumentException("Invalid Blake2f final flag");

            Blake2bCompress(h, m, t0, t1, f, rounds);

            var result = new byte[64];
            for (int i = 0; i < 8; i++)
            {
                var bytes = BitConverter.GetBytes(h[i]);
                Array.Copy(bytes, 0, result, i * 8, 8);
            }

            return result;
        }

        private static readonly ulong[] BLAKE2B_IV = new ulong[]
        {
            0x6a09e667f3bcc908UL, 0xbb67ae8584caa73bUL,
            0x3c6ef372fe94f82bUL, 0xa54ff53a5f1d36f1UL,
            0x510e527fade682d1UL, 0x9b05688c2b3e6c1fUL,
            0x1f83d9abfb41bd6bUL, 0x5be0cd19137e2179UL
        };

        private static readonly int[][] BLAKE2B_SIGMA = new int[][]
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

        private void Blake2bCompress(ulong[] h, ulong[] m, ulong t0, ulong t1, bool f, uint rounds)
        {
            var v = new ulong[16];
            for (int i = 0; i < 8; i++)
            {
                v[i] = h[i];
                v[i + 8] = BLAKE2B_IV[i];
            }

            v[12] ^= t0;
            v[13] ^= t1;

            if (f) v[14] = ~v[14];

            for (uint r = 0; r < rounds; r++)
            {
                var s = BLAKE2B_SIGMA[r % 10];

                G(ref v[0], ref v[4], ref v[8], ref v[12], m[s[0]], m[s[1]]);
                G(ref v[1], ref v[5], ref v[9], ref v[13], m[s[2]], m[s[3]]);
                G(ref v[2], ref v[6], ref v[10], ref v[14], m[s[4]], m[s[5]]);
                G(ref v[3], ref v[7], ref v[11], ref v[15], m[s[6]], m[s[7]]);
                G(ref v[0], ref v[5], ref v[10], ref v[15], m[s[8]], m[s[9]]);
                G(ref v[1], ref v[6], ref v[11], ref v[12], m[s[10]], m[s[11]]);
                G(ref v[2], ref v[7], ref v[8], ref v[13], m[s[12]], m[s[13]]);
                G(ref v[3], ref v[4], ref v[9], ref v[14], m[s[14]], m[s[15]]);
            }

            for (int i = 0; i < 8; i++)
            {
                h[i] ^= v[i] ^ v[i + 8];
            }
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
