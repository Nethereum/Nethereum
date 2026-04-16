using System;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Nethereum.Zisk.Core;

namespace Nethereum.EVM.Zisk.Backends
{
    /// <summary>
    /// Zisk / zkVM alt_bn128 (BN254) backend for precompiles 0x06 / 0x07
    /// / 0x08. Wraps the native
    /// <c>ZiskCrypto.bn254_g1_add_c</c> / <c>bn254_g1_mul_c</c> /
    /// <c>bn254_pairing_check_c</c> primitives. Input/output shapes match
    /// what the handlers pass through — full precompile input in, full
    /// precompile output out.
    /// </summary>
    public sealed class ZiskBn128Backend : IBn128Backend
    {
        public static readonly ZiskBn128Backend Instance = new ZiskBn128Backend();

        public byte[] Add(byte[] input)
        {
            var data = input ?? new byte[0];
            if (data.Length < 128)
            {
                var padded = new byte[128];
                Array.Copy(data, 0, padded, 0, data.Length);
                data = padded;
            }

            var p1 = new byte[64];
            var p2 = new byte[64];
            Array.Copy(data, 0, p1, 0, 64);
            Array.Copy(data, 64, p2, 0, 64);
            var result = new byte[64];

            byte success = ZiskCrypto.bn254_g1_add_c(p1, p2, result);
            if (success != 0) throw new ArgumentException("BN254 G1ADD failed");
            return result;
        }

        public byte[] Mul(byte[] input)
        {
            var data = input ?? new byte[0];
            if (data.Length < 96)
            {
                var padded = new byte[96];
                Array.Copy(data, 0, padded, 0, data.Length);
                data = padded;
            }

            var point = new byte[64];
            var scalar = new byte[32];
            Array.Copy(data, 0, point, 0, 64);
            Array.Copy(data, 64, scalar, 0, 32);
            var result = new byte[64];

            byte success = ZiskCrypto.bn254_g1_mul_c(point, scalar, result);
            if (success != 0) throw new ArgumentException("BN254 G1MUL failed");
            return result;
        }

        public byte[] Pairing(byte[] input)
        {
            var data = input ?? new byte[0];
            if (data.Length == 0) return PairingSuccess();
            if (data.Length % 192 != 0) throw new ArgumentException("Invalid BN254 pairing input length");

            int numPairs = data.Length / 192;
            byte success = ZiskCrypto.bn254_pairing_check_c(data, (nuint)numPairs);
            return success == 0 ? PairingSuccess() : PairingFailure();
        }

        private static byte[] PairingSuccess()
        {
            var result = new byte[32];
            result[31] = 1;
            return result;
        }

        private static byte[] PairingFailure() => new byte[32];
    }
}
