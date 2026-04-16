using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Nethereum.Signer.Crypto;

namespace Nethereum.EVM.Precompiles.Backends
{
    /// <summary>
    /// Default alt_bn128 (BN254) backend for precompiles 0x06 / 0x07 /
    /// 0x08 that wraps the managed <see cref="BN128Curve"/>
    /// implementation in <c>Nethereum.Signer.Crypto</c>. Each method
    /// takes the full precompile input and returns the full precompile
    /// output, matching the shape expected by the handlers.
    /// </summary>
    public sealed class DefaultBn128Backend : IBn128Backend
    {
        public static readonly DefaultBn128Backend Instance = new DefaultBn128Backend();

        public byte[] Add(byte[] input) => BN128Curve.Add(input);

        public byte[] Mul(byte[] input) => BN128Curve.Mul(input);

        public byte[] Pairing(byte[] input) => BN128Curve.Pairing(input);
    }
}
