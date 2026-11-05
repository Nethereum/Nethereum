using System;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;

namespace Nethereum.EVM.Execution.Precompiles.Handlers
{
    public sealed class Blake2fPrecompile : PrecompileHandlerBase
    {
        private readonly IBlake2fBackend _backend;

        public Blake2fPrecompile(IBlake2fBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public override int AddressNumeric => 9;

        private const int InputLength = 213;
        private const int FinalFlagIndex = 212;

        public override byte[] Execute(byte[] input)
        {
            RequireInputLength(input, InputLength, "Blake2f");
            if (input[FinalFlagIndex] > 1)
                throw new ArgumentException("Invalid Blake2f final flag");
            return Compress(input);
        }

        public override bool TryExecute(byte[] input, out byte[] output)
        {
            if (!HasInputLength(input, InputLength) || input[FinalFlagIndex] > 1)
            {
                output = null;
                return false;
            }
            output = Compress(input);
            return true;
        }

        private byte[] Compress(byte[] input)
        {
            var rounds = (uint)((input[0] << 24) | (input[1] << 16) | (input[2] << 8) | input[3]);

            var h = new ulong[8];
            for (int i = 0; i < 8; i++)
                h[i] = BitConverter.ToUInt64(input, 4 + i * 8);

            var m = new ulong[16];
            for (int i = 0; i < 16; i++)
                m[i] = BitConverter.ToUInt64(input, 68 + i * 8);

            var t0 = BitConverter.ToUInt64(input, 196);
            var t1 = BitConverter.ToUInt64(input, 204);
            var f = input[FinalFlagIndex] != 0;

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
