using System;
using System.Numerics;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;

namespace Nethereum.EVM.Precompiles.Backends
{
    /// <summary>
    /// Default MODEXP (EIP-198) backend using
    /// <see cref="BigInteger.ModPow"/>. Lives in the production EVM
    /// assembly so <see cref="BigInteger"/> does not leak onto the
    /// <c>Nethereum.EVM.Core</c> / Zisk hot path; Zisk ships its own
    /// witness-backed <c>IModExpBackend</c> that calls a native
    /// <c>modexp_bytes_c</c> primitive.
    /// </summary>
    public sealed class DefaultModExpBackend : IModExpBackend
    {
        public static readonly DefaultModExpBackend Instance = new DefaultModExpBackend();

        public byte[] ModExp(byte[] baseBytes, byte[] expBytes, byte[] modulus)
        {
            var baseVal = BigIntegerFromUnsignedBigEndian(baseBytes);
            var expVal = BigIntegerFromUnsignedBigEndian(expBytes);
            var modVal = BigIntegerFromUnsignedBigEndian(modulus);

            var result = BigInteger.ModPow(baseVal, expVal, modVal);
            var resultBytes = result.ToByteArray();

            // Strip the leading zero sign byte that BigInteger.ToByteArray
            // adds for positive values whose high bit is set.
            if (resultBytes.Length > 1 && resultBytes[resultBytes.Length - 1] == 0)
            {
                var trimmed = new byte[resultBytes.Length - 1];
                Array.Copy(resultBytes, trimmed, trimmed.Length);
                resultBytes = trimmed;
            }

            Array.Reverse(resultBytes);

            var modLen = modulus.Length;
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

        private static BigInteger BigIntegerFromUnsignedBigEndian(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return BigInteger.Zero;
            // +1 sign byte to force unsigned interpretation in little-endian BigInteger.
            var buf = new byte[bytes.Length + 1];
            Array.Copy(bytes, buf, bytes.Length);
            Array.Reverse(buf, 0, bytes.Length);
            return new BigInteger(buf);
        }
    }
}
