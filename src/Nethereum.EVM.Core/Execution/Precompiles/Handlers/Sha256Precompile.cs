using System;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;

namespace Nethereum.EVM.Execution.Precompiles.Handlers
{
    public sealed class Sha256Precompile : PrecompileHandlerBase
    {
        private readonly ISha256Backend _backend;

        public Sha256Precompile(ISha256Backend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public override int AddressNumeric => 2;

        public override byte[] Execute(byte[] input)
        {
            return _backend.Hash(input ?? new byte[0]);
        }
    }
}
