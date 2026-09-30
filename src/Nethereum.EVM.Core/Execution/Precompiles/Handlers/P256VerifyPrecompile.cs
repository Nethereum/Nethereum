using System;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;

namespace Nethereum.EVM.Execution.Precompiles.Handlers
{
    public sealed class P256VerifyPrecompile : PrecompileHandlerBase
    {
        private readonly IP256VerifyBackend _backend;

        public P256VerifyPrecompile(IP256VerifyBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public override int AddressNumeric => 0x100;

        private const int InputLength = 160;

        public override byte[] Execute(byte[] input)
        {
            if (input == null || input.Length != InputLength)
                return new byte[0];

            try
            {
                var hash = new byte[32];
                var r = new byte[32];
                var s = new byte[32];
                var x = new byte[32];
                var y = new byte[32];
                Array.Copy(input, 0, hash, 0, 32);
                Array.Copy(input, 32, r, 0, 32);
                Array.Copy(input, 64, s, 0, 32);
                Array.Copy(input, 96, x, 0, 32);
                Array.Copy(input, 128, y, 0, 32);

                if (_backend.Verify(hash, r, s, x, y))
                {
                    var result = new byte[32];
                    result[31] = 1;
                    return result;
                }

                return new byte[0];
            }
            catch
            {
                return new byte[0];
            }
        }
    }
}
