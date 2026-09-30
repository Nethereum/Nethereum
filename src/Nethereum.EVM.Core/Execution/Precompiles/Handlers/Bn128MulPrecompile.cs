using System;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;

namespace Nethereum.EVM.Execution.Precompiles.Handlers
{
    public sealed class Bn128MulPrecompile : PrecompileHandlerBase
    {
        private readonly IBn128Backend _backend;

        public Bn128MulPrecompile(IBn128Backend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public override int AddressNumeric => 7;

        public override byte[] Execute(byte[] input)
        {
            return _backend.Mul(input);
        }
    }
}
