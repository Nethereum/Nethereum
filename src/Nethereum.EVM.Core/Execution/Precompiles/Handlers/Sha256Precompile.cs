using System;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;

namespace Nethereum.EVM.Execution.Precompiles.Handlers
{
    /// <summary>
    /// Precompile 0x02 — SHA256. Standard SHA-256 hash. Available from Frontier.
    /// Gas cost (base + per-word) is defined by the fork's
    /// <see cref="PrecompileGasCalculators"/>; the underlying hash primitive
    /// is provided by an <see cref="ISha256Backend"/>.
    /// </summary>
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
