using Nethereum.Documentation;
using System;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Nethereum.Signer.Crypto;

namespace Nethereum.EVM.Precompiles.Backends
{
    [NethereumDocExample(DocSection.EvmSimulator, "default-precompile-backends", "The bn128 backend the default composition serves ecAdd and ecMul with")]
    public sealed class ManagedBn254Backend : IBn128Backend, IBn128OutcomeBackend
    {
        public static readonly ManagedBn254Backend Instance = new ManagedBn254Backend(DefaultBn128Backend.Instance);

        private readonly DefaultBn128Backend _pairing;

        public ManagedBn254Backend(DefaultBn128Backend pairing)
        {
            _pairing = pairing ?? throw new ArgumentNullException(nameof(pairing));
        }

        public byte[] Add(byte[] input) =>
            TryAdd(input, out var output)
                ? output
                : throw new ArgumentException("Invalid BN128 ADD input: coordinates not in the field or point not on the curve");

        public byte[] Mul(byte[] input) =>
            TryMul(input, out var output)
                ? output
                : throw new ArgumentException("Invalid BN128 MUL input: coordinates not in the field or point not on the curve");

        public byte[] Pairing(byte[] input) => _pairing.Pairing(input);

        public bool TryPairing(byte[] input, out byte[] output) => _pairing.TryPairing(input, out output);

        public bool TryAdd(byte[] input, out byte[] output) => BN128Curve.TryAdd(input, out output);

        public bool TryMul(byte[] input, out byte[] output) => BN128Curve.TryMul(input, out output);
    }
}
