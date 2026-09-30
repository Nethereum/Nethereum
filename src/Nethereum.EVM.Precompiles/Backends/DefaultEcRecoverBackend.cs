using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Nethereum.Signer;

namespace Nethereum.EVM.Precompiles.Backends
{
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
