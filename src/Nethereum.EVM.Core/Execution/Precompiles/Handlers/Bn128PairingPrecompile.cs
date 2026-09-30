using System;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;

namespace Nethereum.EVM.Execution.Precompiles.Handlers
{
    public sealed class Bn128PairingPrecompile : PrecompileHandlerBase
    {
        private readonly IBn128Backend _backend;

        public Bn128PairingPrecompile(IBn128Backend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public override int AddressNumeric => 8;

        public override byte[] Execute(byte[] input)
        {
            return _backend.Pairing(input);
        }
    }
}
