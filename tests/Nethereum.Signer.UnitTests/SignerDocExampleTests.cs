using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.XUnitEthereumClients;
using System.Collections.Generic;
using System.Numerics;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class SignerDocExampleTests
    {
        private const string KnownPrivateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
        private const string KnownAddress = "0x12890D2cce102216644c59daE5baed380d84830c";

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "ec-key-management", "Generate and use EC keys")]
        public void ShouldGenerateKeyAndDerivePublicKeyAndAddress()
        {
            var ecKey = EthECKey.GenerateKey();

            var privateKeyHex = ecKey.GetPrivateKey();
            var publicKeyBytes = ecKey.GetPubKey();
            var address = ecKey.GetPublicAddress();

            Assert.False(string.IsNullOrEmpty(privateKeyHex));
            Assert.NotEmpty(publicKeyBytes);
            Assert.StartsWith("0x", address);

            var reconstructed = new EthECKey(privateKeyHex);
            Assert.Equal(address, reconstructed.GetPublicAddress());
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "ec-key-management", "Deterministic key from private key")]
        public void ShouldDeriveKnownAddressFromPrivateKey()
        {
            var ecKey = new EthECKey(KnownPrivateKey);
            var address = ecKey.GetPublicAddress();

            Assert.True(KnownAddress.IsTheSameAddress(address));
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "transaction-signing", "Sign legacy transaction")]
        public void ShouldSignLegacyTransaction()
        {
            var signer = new LegacyTransactionSigner();
            var receiverAddress = "0x13f022d72158410433cbd66f5dd8bf6d2d129924";
            var amount = BigInteger.Parse("1000000000000000000");
            BigInteger nonce = 0;
            BigInteger gasPrice = BigInteger.Parse("20000000000");
            BigInteger gasLimit = 21000;

            var signedRlpHex = signer.SignTransaction(
                KnownPrivateKey, receiverAddress, amount, nonce, gasPrice, gasLimit);

            Assert.False(string.IsNullOrEmpty(signedRlpHex));
            Assert.True(signedRlpHex.Length > 0);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "transaction-signing", "Sign EIP-1559 transaction")]
        public void ShouldSignEip1559Transaction()
        {
            var signer = new Transaction1559Signer();
            BigInteger chainId = 1;
            BigInteger nonce = 0;
            BigInteger maxPriorityFeePerGas = BigInteger.Parse("2000000000");
            BigInteger maxFeePerGas = BigInteger.Parse("100000000000");
            BigInteger gasLimit = 21000;
            var receiverAddress = "0x13f022d72158410433cbd66f5dd8bf6d2d129924";
            BigInteger amount = BigInteger.Parse("1000000000000000000");

            var transaction = new Transaction1559(
                chainId, nonce, maxPriorityFeePerGas, maxFeePerGas,
                gasLimit, receiverAddress, amount, null, new List<AccessListItem>());

            var signedRlpHex = signer.SignTransaction(KnownPrivateKey, transaction);

            Assert.False(string.IsNullOrEmpty(signedRlpHex));
            Assert.NotNull(transaction.Signature);
            Assert.NotNull(transaction.Signature.R);
            Assert.NotNull(transaction.Signature.S);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "transaction-signing", "Sign EIP-155 transaction with chain ID")]
        public void ShouldSignEip155TransactionWithChainId()
        {
            var receiverAddress = "0x3535353535353535353535353535353535353535";
            BigInteger chainId = 1;
            BigInteger nonce = 9;
            BigInteger gasPrice = BigInteger.Parse("20000000000");
            BigInteger gasLimit = 21000;
            BigInteger amount = BigInteger.Parse("1000000000000000000");

            var tx = new LegacyTransactionChainId(receiverAddress, amount, nonce, gasPrice, gasLimit, chainId);
            var signer = new LegacyTransactionSigner();
            signer.SignTransaction(KnownPrivateKey.HexToByteArray(), tx);

            var vValue = tx.Signature.V.ToBigIntegerFromRLPDecoded();
            Assert.True(vValue == 37 || vValue == 38);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "message-signing", "Verify AllowOnlyLowS")]
        public void ShouldVerifySignatureWithLowSConstraint()
        {
            var ecKey = new EthECKey(KnownPrivateKey);
            var message = "test message";

            var signer = new EthereumMessageSigner();
            var signature = signer.EncodeUTF8AndSign(message, ecKey);

            var prefixedHash = signer.HashPrefixedMessage(message);
            var ethSignature = MessageSigner.ExtractEcdsaSignature(signature);

            Assert.True(ecKey.VerifyAllowingOnlyLowS(prefixedHash, ethSignature));
        }
    }
}
