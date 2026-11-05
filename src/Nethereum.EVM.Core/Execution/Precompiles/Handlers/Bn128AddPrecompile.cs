using System;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;

namespace Nethereum.EVM.Execution.Precompiles.Handlers
{
    public sealed class Bn128AddPrecompile : PrecompileHandlerBase
    {
        private readonly IBn128Backend _backend;
        private readonly IBn128OutcomeBackend _outcomes;

        public Bn128AddPrecompile(IBn128Backend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _outcomes = backend as IBn128OutcomeBackend;
        }

        public override int AddressNumeric => 6;

        public override byte[] Execute(byte[] input)
        {
            return _backend.Add(input);
        }

        public override bool TryExecute(byte[] input, out byte[] output)
        {
            if (_outcomes != null) return _outcomes.TryAdd(input, out output);
            return TryThroughThrowingOperation(() => _backend.Add(input), out output);
        }
    }
}
