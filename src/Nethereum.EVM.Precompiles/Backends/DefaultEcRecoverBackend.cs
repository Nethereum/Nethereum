using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Nethereum.Signer;

namespace Nethereum.EVM.Precompiles.Backends
{
    /// <summary>
    /// Default ECRECOVER backend for precompile 0x01 that wraps
    /// <see cref="EthECKey.RecoverFromSignature"/> from
    /// <c>Nethereum.Signer</c>. Returns the 20-byte recovered address, or
    /// null on any failure — the handler maps null into the 32-byte
    /// consensus "empty output".
    /// </summary>
    public sealed class DefaultEcRecoverBackend : IEcRecoverBackend
    {
        public static readonly DefaultEcRecoverBackend Instance = new DefaultEcRecoverBackend();

        public byte[] Recover(byte[] hash, byte v, byte[] r, byte[] s)
        {
            return EthECKey
                .RecoverFromSignature(
                    EthECDSASignatureFactory.FromComponents(r, s, new byte[] { v }),
                    hash)
                .GetPublicAddressAsBytes();
        }
    }
}
