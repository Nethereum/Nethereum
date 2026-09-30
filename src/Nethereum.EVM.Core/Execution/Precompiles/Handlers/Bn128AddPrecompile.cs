using System;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;

namespace Nethereum.EVM.Execution.Precompiles.Handlers
{
    public sealed class Bn128AddPrecompile : PrecompileHandlerBase
    {
        private readonly IBn128Backend _backend;

        public Bn128AddPrecompile(IBn128Backend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public override int AddressNumeric => 6;

        public override byte[] Execute(byte[] input)
        {
            return _backend.Add(input);
        }
    }
}
