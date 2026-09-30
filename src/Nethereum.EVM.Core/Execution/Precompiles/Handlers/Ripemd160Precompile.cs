using System;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Nethereum.Util;

namespace Nethereum.EVM.Execution.Precompiles.Handlers
{
    public sealed class Ripemd160Precompile : PrecompileHandlerBase
    {
        private readonly IRipemd160Backend _backend;

        public Ripemd160Precompile(IRipemd160Backend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public override int AddressNumeric => 3;

        public override byte[] Execute(byte[] input)
        {
            var digest = _backend.Hash(input ?? new byte[0]);
            return digest.PadTo32Bytes();
        }
    }
}
