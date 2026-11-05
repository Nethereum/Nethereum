using System;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;

namespace Nethereum.EVM.Execution.Precompiles.Handlers
{
    public sealed class Bn128PairingPrecompile : PrecompileHandlerBase
    {
        private readonly IBn128Backend _backend;
        private readonly IBn128OutcomeBackend _outcomes;

        public Bn128PairingPrecompile(IBn128Backend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _outcomes = backend as IBn128OutcomeBackend;
        }

        public override int AddressNumeric => 8;

        public override byte[] Execute(byte[] input)
        {
            return _backend.Pairing(input);
        }

        public override bool TryExecute(byte[] input, out byte[] output)
        {
            if (_outcomes != null) return _outcomes.TryPairing(input, out output);
            return TryThroughThrowingOperation(() => _backend.Pairing(input), out output);
        }
    }
}
