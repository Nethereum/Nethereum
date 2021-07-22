using System;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Nethereum.Signer.Crypto;

namespace Nethereum.Signer.AzureKeyVault
{
    public class AzureKeyVaultExternalSigner : EthExternalSignerBase
    {
        public override ExternalSignerTransactionFormat ExternalSignerTransactionFormat { get; protected set; } = ExternalSignerTransactionFormat.Hash;
        public override bool CalculatesV { get; protected set; } = false;
        public KeyVaultClient KeyVaultClient { get; private set; }
        public string VaultUrl { get; }

        public AzureKeyVaultExternalSigner(KeyVaultClient keyVaultClient, string vaultUrl)
        {
            KeyVaultClient = keyVaultClient;
            VaultUrl = vaultUrl;
        }

        protected override async Task<byte[]> GetPublicKeyAsync()
        {
            var keyBundle = await KeyVaultClient.GetKeyAsync(VaultUrl).ConfigureAwait(false);
            var xLen = keyBundle.Key.X.Length;
            var yLen = keyBundle.Key.Y.Length;
            var publicKey = new byte[1 + xLen + yLen];
            publicKey[0] = 0x04;
            var offset = 1;
            Buffer.BlockCopy(keyBundle.Key.X, 0, publicKey, offset, xLen);
            offset = offset + xLen;
            Buffer.BlockCopy(keyBundle.Key.Y, 0, publicKey, offset, yLen);
            return publicKey;
        }

        protected override async Task<ECDSASignature> SignExternallyAsync(byte[] hash)
        {
            var keyOperationResult = await KeyVaultClient.SignAsync(VaultUrl, "ECDSA256", hash).ConfigureAwait(false);
            var signature = keyOperationResult.Result;
            return ECDSASignatureFactory.FromComponents(signature).MakeCanonical();
        }

        public override Task SignAsync(LegacyTransaction transaction)
        {
            return SignHashTransactionAsync(transaction);
        }

        public override Task SignAsync(LegacyTransactionChainId transaction)
        {
            return SignHashTransactionAsync(transaction);
        }

        public override Task SignAsync(Transaction1559 transaction)
        {
            return SignHashTransactionAsync(transaction);
        }

        public override bool Supported1559 { get; } = true;
    }
}
