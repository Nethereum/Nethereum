using System;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;

namespace Nethereum.EVM.Execution.Precompiles.Handlers
{
    /// <summary>
    /// Precompile 0x09 — BLAKE2F (EIP-152). Runs a single round block of
    /// the BLAKE2b compression function with a caller-specified round count.
    /// Added in Istanbul. Gas cost is <c>rounds</c> (1 gas per round) per
    /// the fork's <see cref="PrecompileGasCalculators"/>.
    ///
    /// Input layout (exactly 213 bytes):
    ///   [0..4)     rounds  (big-endian u32)
    ///   [4..68)    h       (8 × u64, little-endian)
    ///   [68..196)  m       (16 × u64, little-endian)
    ///   [196..204) t0      (u64, little-endian)
    ///   [204..212) t1      (u64, little-endian)
    ///   [212]      f       (0 or 1)
    /// Output: 64-byte h state.
    ///
    /// The compression primitive itself is provided by an
    /// <see cref="IBlake2fBackend"/>; production uses the managed port in
    /// <c>Nethereum.EVM</c> and Zisk uses a witness-backed variant.
    /// </summary>
    public sealed class Blake2fPrecompile : PrecompileHandlerBase
    {
        private readonly IBlake2fBackend _backend;

        public Blake2fPrecompile(IBlake2fBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public override int AddressNumeric => 9;

        private const int InputLength = 213;

        public override byte[] Execute(byte[] input)
        {
            RequireInputLength(input, InputLength, "Blake2f");

            var rounds = (uint)((input[0] << 24) | (input[1] << 16) | (input[2] << 8) | input[3]);

            var h = new ulong[8];
            for (int i = 0; i < 8; i++)
                h[i] = BitConverter.ToUInt64(input, 4 + i * 8);

            var m = new ulong[16];
            for (int i = 0; i < 16; i++)
                m[i] = BitConverter.ToUInt64(input, 68 + i * 8);

            var t0 = BitConverter.ToUInt64(input, 196);
            var t1 = BitConverter.ToUInt64(input, 204);

            if (input[212] > 1)
                throw new ArgumentException("Invalid Blake2f final flag");

            var f = input[212] != 0;

            _backend.Compress(rounds, h, m, t0, t1, f);

            var result = new byte[64];
            for (int i = 0; i < 8; i++)
            {
                var bytes = BitConverter.GetBytes(h[i]);
                Array.Copy(bytes, 0, result, i * 8, 8);
            }
            return result;
        }
    }
}
