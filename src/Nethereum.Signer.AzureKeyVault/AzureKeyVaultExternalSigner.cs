using System;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Nethereum.Signer.Crypto;

namespace Nethereum.Signer.AzureKeyVault
{
    public class AzureKeyVaultExternalSigner : EthExternalSignerBase
    {
        public CryptographyClient CryptoClient { get; }
        public KeyClient KeyClient { get; }
        public string KeyName { get; }

        public AzureKeyVaultExternalSigner(string keyName, string vaultUri, TokenCredential credential)
        : this(keyName, new KeyClient(new Uri(vaultUri), credential), credential)
        {
        }

        public AzureKeyVaultExternalSigner(string keyName, KeyClient keyClient, TokenCredential credential)
        {
            KeyName = keyName ?? throw new ArgumentNullException(nameof(keyName));
            KeyClient = keyClient ?? throw new ArgumentNullException(nameof(keyClient));

            var keyId = new UriBuilder(keyClient.VaultUri) { Path = $"keys/{KeyName}" }.Uri;

            CryptoClient = new CryptographyClient(keyId, credential ?? throw new ArgumentNullException(nameof(credential)));
        }

        protected override async Task<byte[]> GetPublicKeyAsync()
        {
            var response = await KeyClient.GetKeyAsync(KeyName).ConfigureAwait(false);
            var jwk = response.Value.Key;
            var publicKey = new byte[1 + jwk.X.Length + jwk.Y.Length];

            publicKey[0] = 0x04;

            Buffer.BlockCopy(jwk.X, 0, publicKey, 1, jwk.X.Length);
            Buffer.BlockCopy(jwk.Y, 0, publicKey, 1 + jwk.X.Length, jwk.Y.Length);

            return publicKey;
        }

        protected override async Task<ECDSASignature> SignExternallyAsync(byte[] hash)
        {
            if (hash == null)
                throw new ArgumentNullException(nameof(hash));

            var result = await CryptoClient.SignAsync(SignatureAlgorithm.ES256K, hash).ConfigureAwait(false);

            return ECDSASignatureFactory.FromComponents(result.Signature).MakeCanonical();
        }

        public override Task SignAsync(LegacyTransaction transaction) => SignHashTransactionAsync(transaction);

        public override Task SignAsync(LegacyTransactionChainId transaction) => SignHashTransactionAsync(transaction);

        public override Task SignAsync(Transaction1559 transaction) => SignHashTransactionAsync(transaction);

        public override bool CalculatesV { get; protected set; } = false;

        public override ExternalSignerTransactionFormat ExternalSignerTransactionFormat { get; protected set; } = ExternalSignerTransactionFormat.Hash;
        
        public override bool Supported1559 { get; } = true;
    }
}
