using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class TransactionHashDocExampleTests
    {
        private const string KnownPrivateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
        private const string KnownAddress = "0x12890D2cce102216644c59daE5baed380d84830c";

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "transaction-hash", "Sign transaction and calculate hash before sending")]
        public void ShouldCalculateTransactionHashFromSignedTransaction()
        {
            var signer = new LegacyTransactionSigner();
            var receiverAddress = "0x83F861941E940d47C5D53B20141912f4D13DEe64";
            BigInteger amount = 10000;
            BigInteger nonce = 0;
            BigInteger gasPrice = 2000000000;
            BigInteger gasLimit = 150000;

            var signedHex = signer.SignTransaction(
                KnownPrivateKey, receiverAddress, amount, nonce, gasPrice, gasLimit);

            var txnHash = TransactionUtils.CalculateTransactionHash(signedHex);

            Assert.False(string.IsNullOrEmpty(txnHash));
            Assert.Equal(64, txnHash.Length);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "transaction-hash", "Transaction hash is deterministic")]
        public void ShouldProduceSameHashForSameTransaction()
        {
            var signer = new LegacyTransactionSigner();
            var receiverAddress = "0x83F861941E940d47C5D53B20141912f4D13DEe64";
            BigInteger amount = 10000;
            BigInteger nonce = 0;
            BigInteger gasPrice = 2000000000;
            BigInteger gasLimit = 150000;

            var signedHex = signer.SignTransaction(
                KnownPrivateKey, receiverAddress, amount, nonce, gasPrice, gasLimit);

            var hash1 = TransactionUtils.CalculateTransactionHash(signedHex);
            var hash2 = TransactionUtils.CalculateTransactionHash(signedHex);

            Assert.Equal(hash1, hash2);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "transaction-hash", "Recovered sender matches original signer")]
        public void ShouldRecoverOriginalSenderFromHash()
        {
            var signer = new LegacyTransactionSigner();
            var receiverAddress = "0x83F861941E940d47C5D53B20141912f4D13DEe64";
            BigInteger amount = 10000;
            BigInteger nonce = 0;
            BigInteger gasPrice = 2000000000;
            BigInteger gasLimit = 150000;

            var signedHex = signer.SignTransaction(
                KnownPrivateKey, receiverAddress, amount, nonce, gasPrice, gasLimit);

            var recoveredSender = TransactionVerificationAndRecovery.GetSenderAddress(signedHex);
            Assert.True(KnownAddress.IsTheSameAddress(recoveredSender));
        }
    }
}
