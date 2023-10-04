using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Signer.Crypto;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Nethereum.Signer.AWSKeyManagement
{
    public class AWSKeyManagementExternalSigner : EthExternalSignerBase
    {
        #region Properties

		protected IAmazonKeyManagementService KeyClient { get; private set; }

		public string KeyId { get; }

		public bool IsPrivateTransaction { get; }

		#endregion

		#region Constructors

		public AWSKeyManagementExternalSigner(IAmazonKeyManagementService keyClient, string keyId, bool isPrivateTransaction)
		{
			KeyId = keyId ?? throw new ArgumentNullException(nameof(keyId));
			IsPrivateTransaction = isPrivateTransaction;
			KeyClient = keyClient;
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

				var signature = EthECDSASignature.FromDER(result.Signature.ToArray());

				byte[] r = signature.R;
				byte[] s = signature.S;

				var secp256k1N = new HexBigInteger("fffffffffffffffffffffffffffffffebaaedce6af48a03bbfd25e8cd0364141");

				if (!signature.IsLowS)
					s = new HexBigInteger(System.Numerics.BigInteger.Subtract(secp256k1N, s.ToBigIntegerFromRLPDecoded()));

				var v = FindRightKey(hashBytes, r, s, await GetAddressAsync());

				if (IsPrivateTransaction)
					v = new HexBigInteger(System.Numerics.BigInteger.Add(new HexBigInteger(v.ToHex(true)), new HexBigInteger(10).Value));

				return ECDSASignatureFactory.FromComponents(r, s, v);
			}
		}

		public override Task SignAsync(LegacyTransaction transaction) => SignHashTransactionAsync(transaction);

		public override Task SignAsync(LegacyTransactionChainId transaction) => SignHashTransactionAsync(transaction);

		public override Task SignAsync(Transaction1559 transaction) => SignHashTransactionAsync(transaction);

		public override bool CalculatesV { get; protected set; } = true;

		public override ExternalSignerTransactionFormat ExternalSignerTransactionFormat { get; protected set; } = ExternalSignerTransactionFormat.Hash;

		public override bool Supported1559 { get; } = true;

		#endregion

		#region Private methods

		private byte[] FindRightKey(byte[] msgHash, byte[] r, byte[] s, string ethereumAddress)
		{
			var possibleValues = new short[] { 27, 28 };
			foreach (var candidate in possibleValues)
			{
				var v = new HexBigInteger(candidate);
				if (RecoverPubKeyFromSig(msgHash, r, s, v) == ethereumAddress)
					return v;
			}

			throw new ArgumentException("There was no v value found for the ethereum address", nameof(ethereumAddress));
		}

		private string RecoverPubKeyFromSig(byte[] msgHash, byte[] r, byte[] s, byte[] v)
		{
			return GetSignature(msgHash, r, s, v).GetPublicAddress();
		}

		private EthECKey GetSignature(byte[] msgHash, byte[] r, byte[] s, byte[] v)
		{
			EthECDSASignature ethECDSASignature = EthECDSASignatureFactory.FromComponents(r, s, v);
			return GetSignature(msgHash, ethECDSASignature);
		}

		private EthECKey GetSignature(byte[] msgHash, EthECDSASignature signature)
		{
			return EthECKey.RecoverFromSignature(signature, msgHash);
		}

		#endregion
    }
}
