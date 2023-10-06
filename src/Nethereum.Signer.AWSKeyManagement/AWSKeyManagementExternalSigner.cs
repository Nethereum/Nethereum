using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Nethereum.Model;
using Nethereum.Signer.Crypto;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Nethereum.Signer.AWSKeyManagement
{
    public class AWSKeyManagementExternalSigner : EthExternalSignerBase
    {
        #region Properties

		protected IAmazonKeyManagementService KeyClient { get; private set; }

		public string KeyId { get; }

		#endregion

		#region Constructors

		public AWSKeyManagementExternalSigner(IAmazonKeyManagementService keyClient, string keyId, bool isPrivateTransaction)
		{
			KeyId = keyId ?? throw new ArgumentNullException(nameof(keyId));
			KeyClient = keyClient ?? throw new ArgumentNullException(nameof(keyClient));
            CalculatesV = isPrivateTransaction;
        }

        #endregion

        #region Abstract Implementations

        protected override async Task<byte[]> GetPublicKeyAsync()
		{
			var request = new GetPublicKeyRequest()
			{
				KeyId = this.KeyId
			};

			var pubKey = await KeyClient.GetPublicKeyAsync(request).ConfigureAwait(false);
			var publicKeyEcDsa = System.Security.Cryptography.ECDsa.Create();
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

			using (MemoryStream message = new (hashBytes))
			{
				var request = new SignRequest()
				{
					Message = message,
					KeyId = this.KeyId,
					MessageType = MessageType.DIGEST,
					SigningAlgorithm = SigningAlgorithmSpec.ECDSA_SHA_256
				};

				var result = await KeyClient.SignAsync(request).ConfigureAwait(false);

				var signature = ECDSASignature.FromDER(result.Signature.ToArray());

				if (!CalculatesV)
                    return signature;

                var publicKey = await GetPublicKeyAsync().ConfigureAwait(false);
                var recId = CalculateRecId(signature, hashBytes, publicKey);
                signature.V = new[] { (byte)(recId + 10) };

				return signature;
			}
		}

		protected override int CalculateRecId(ECDSASignature signature, byte[] message, byte[] publicKey)
		{
            var possibleValues = new short[] { 10, 11 };

            foreach (var candidate in possibleValues)
            {
                var rec = ECKey.RecoverFromSignature(candidate, signature, message, false);
				if (rec == null)
					continue;

                var k = rec.GetPubKey(false);
                if (k != null && k.SequenceEqual(publicKey))
					return candidate;
            }

            throw new ArgumentException("Could not construct a recoverable key. This should never happen.");
        }

		public override Task SignAsync(LegacyTransaction transaction) => SignHashTransactionAsync(transaction);

		public override Task SignAsync(LegacyTransactionChainId transaction) => SignHashTransactionAsync(transaction);

		public override Task SignAsync(Transaction1559 transaction) => SignHashTransactionAsync(transaction);

		public override bool CalculatesV { get; protected set; } = true;

		public override ExternalSignerTransactionFormat ExternalSignerTransactionFormat { get; protected set; } = ExternalSignerTransactionFormat.Hash;

		public override bool Supported1559 { get; } = true;

		#endregion
    }
}
