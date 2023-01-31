using Amazon;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Amazon.Runtime;
using Nethereum.Model;
using Nethereum.Signer.Crypto;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Nethereum.Signer.AWSKeyManagement
{
    public class AWSKeyManagementExternalSigner : EthExternalSignerBase
    {
        public AmazonKeyManagementServiceClient KeyClient { get;}
        public string KeyId { get; }

        public AWSKeyManagementExternalSigner(string keyId, string accessKeyId, string accessKey, RegionEndpoint region)
        {
            KeyId = keyId ?? throw new ArgumentNullException(nameof(keyId));
            KeyClient = new AmazonKeyManagementServiceClient(accessKeyId, accessKey, region);
        }
        public AWSKeyManagementExternalSigner(string keyId, AWSCredentials credentials)
        {
            KeyId = keyId ?? throw new ArgumentNullException(nameof(keyId));
            KeyClient = new AmazonKeyManagementServiceClient(credentials);
        }

        public AWSKeyManagementExternalSigner(string keyId, RegionEndpoint region)
        {
            KeyId = keyId ?? throw new ArgumentNullException(nameof(keyId));
            KeyClient = new AmazonKeyManagementServiceClient(region);
        }

        protected override async Task<byte[]> GetPublicKeyAsync()
        {
            var request = new GetPublicKeyRequest()
            {
                KeyId = this.KeyId
            };

            var pubKey = await KeyClient.GetPublicKeyAsync(request).ConfigureAwait(false);
            var publicKeyEcDsa = ECDsa.Create();
            publicKeyEcDsa.ImportSubjectPublicKeyInfo(pubKey.PublicKey.ToArray(), out _);

            var publicKeyParameters = publicKeyEcDsa.ExportExplicitParameters(false);
            var x = publicKeyParameters.Q.X;
            var y = publicKeyParameters.Q.Y;
            var xLen = x.Length;
            var yLen = y.Length;

            var publicKey = new byte[1 + xLen + yLen];
            publicKey[0] = 0x04;
            var offset = 1;
            Buffer.BlockCopy(x, 0, publicKey, offset, xLen);
            offset = offset + xLen;
            Buffer.BlockCopy(y, 0, publicKey, offset, yLen);
            return publicKey;
            
        }


        protected override async Task<ECDSASignature> SignExternallyAsync(byte[] hashBytes)
        {
            if (hashBytes == null)
                throw new ArgumentNullException(nameof(hashBytes));
            var request = new SignRequest()
            {
                Message = new MemoryStream(hashBytes),
                KeyId = this.KeyId,
                MessageType = MessageType.DIGEST,
                SigningAlgorithm = SigningAlgorithmSpec.ECDSA_SHA_256
            };
            var result = await KeyClient.SignAsync(request).ConfigureAwait(false);
             
            return ECDSASignature.FromDER(result.Signature.ToArray());
           
        }

        public override Task SignAsync(LegacyTransaction transaction) => SignHashTransactionAsync(transaction);

        public override Task SignAsync(LegacyTransactionChainId transaction) => SignHashTransactionAsync(transaction);

        public override Task SignAsync(Transaction1559 transaction) => SignHashTransactionAsync(transaction);

        public override bool CalculatesV { get; protected set; } = false;

        public override ExternalSignerTransactionFormat ExternalSignerTransactionFormat { get; protected set; } = ExternalSignerTransactionFormat.Hash;

        public override bool Supported1559 { get; } = true;
    }
}
