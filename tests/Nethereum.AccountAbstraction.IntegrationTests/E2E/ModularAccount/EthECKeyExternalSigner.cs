using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Signer.Crypto;
using Org.BouncyCastle.Math;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E.ModularAccount
{
    public class EthECKeyExternalSigner : EthExternalSignerBase
    {
        private readonly EthECKey _key;

        public EthECKeyExternalSigner(EthECKey key)
        {
            _key = key;
        }

        protected override Task<byte[]> GetPublicKeyAsync() => Task.FromResult(_key.GetPubKey(false));

        protected override Task<ECDSASignature> SignExternallyAsync(byte[] hashBytes)
        {
            var signature = _key.Sign(hashBytes);
            return Task.FromResult(new ECDSASignature(
                new BigInteger(1, signature.R),
                new BigInteger(1, signature.S)));
        }

        public override Task SignAsync(LegacyTransaction transaction) => SignHashTransactionAsync(transaction);
        public override Task SignAsync(LegacyTransactionChainId transaction) => SignHashTransactionAsync(transaction);
        public override Task SignAsync(Transaction1559 transaction) => SignHashTransactionAsync(transaction);
        public override Task SignAsync(Transaction7702 transaction) => SignHashTransactionAsync(transaction);
        public override Task SignAsync(Transaction4844 transaction) => SignHashTransactionAsync(transaction);

        public override bool CalculatesV { get; protected set; } = false;
        public override ExternalSignerTransactionFormat ExternalSignerTransactionFormat { get; protected set; } = ExternalSignerTransactionFormat.Hash;
        public override bool Supported1559 { get; } = true;
    }
}
