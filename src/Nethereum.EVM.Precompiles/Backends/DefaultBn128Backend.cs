using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Nethereum.Signer.Crypto;

namespace Nethereum.EVM.Precompiles.Backends
{
    public sealed class DefaultBn128Backend : IBn128Backend, IBn128OutcomeBackend
    {
        public static readonly DefaultBn128Backend Instance = new DefaultBn128Backend();

        public byte[] Add(byte[] input) => BN128Curve.Add(input);

        public byte[] Mul(byte[] input) => BN128Curve.Mul(input);

        public byte[] Pairing(byte[] input) => BN128Curve.Pairing(input);

        public bool TryAdd(byte[] input, out byte[] output) => BN128Curve.TryAdd(input, out output);

        public bool TryMul(byte[] input, out byte[] output) => BN128Curve.TryMul(input, out output);

        public bool TryPairing(byte[] input, out byte[] output) => BN128Curve.TryPairing(input, out output);
    }
}
