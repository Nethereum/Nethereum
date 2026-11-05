using System;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Nethereum.Zisk.Core;

namespace Nethereum.EVM.Zisk.Backends
{
    public sealed class ZiskBn128Backend : IBn128Backend, IBn128OutcomeBackend
    {
        public static readonly ZiskBn128Backend Instance = new ZiskBn128Backend();

        public byte[] Add(byte[] input)
            => TryAdd(input, out var output) ? output : throw new ArgumentException("BN254 G1ADD: invalid input");

        public byte[] Mul(byte[] input)
            => TryMul(input, out var output) ? output : throw new ArgumentException("BN254 G1MUL: invalid input");

        public byte[] Pairing(byte[] input)
            => TryPairing(input, out var output) ? output : throw new ArgumentException("BN254 PAIRING: invalid input");

        public bool TryAdd(byte[] input, out byte[] output)
        {
            var data = PadTo(input, 128);
            var p1 = new byte[64];
            var p2 = new byte[64];
            Array.Copy(data, 0, p1, 0, 64);
            Array.Copy(data, 64, p2, 0, 64);
            var result = new byte[64];

            if (ZiskCrypto.zkvm_bn254_g1_add(p1, p2, result) != 0)
            {
                output = null;
                return false;
            }
            output = result;
            return true;
        }

        public bool TryMul(byte[] input, out byte[] output)
        {
            var data = PadTo(input, 96);
            var point = new byte[64];
            var scalar = new byte[32];
            Array.Copy(data, 0, point, 0, 64);
            Array.Copy(data, 64, scalar, 0, 32);
            var result = new byte[64];

            if (ZiskCrypto.zkvm_bn254_g1_mul(point, scalar, result) != 0)
            {
                output = null;
                return false;
            }
            output = result;
            return true;
        }

        public bool TryPairing(byte[] input, out byte[] output)
        {
            output = null;
            var data = input ?? new byte[0];
            if (data.Length == 0)
            {
                output = PairingSuccess();
                return true;
            }
            if (data.Length % 192 != 0) return false;

            int numPairs = data.Length / 192;
            bool verified = false;
            uint status;
            unsafe { status = ZiskCrypto.zkvm_bn254_pairing(data, (nuint)numPairs, &verified); }
            if (status != 0) return false;
            output = verified ? PairingSuccess() : PairingFailure();
            return true;
        }

        private static byte[] PadTo(byte[] input, int length)
        {
            var data = input ?? new byte[0];
            if (data.Length >= length) return data;
            var padded = new byte[length];
            Array.Copy(data, 0, padded, 0, data.Length);
            return padded;
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
