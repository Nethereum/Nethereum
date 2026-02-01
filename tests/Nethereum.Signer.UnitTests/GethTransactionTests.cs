using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.RLP;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class GethTransactionTests
    {
        [Theory]
        [MemberData(nameof(GethTransactionTestVectors.GetSignatureTestCases), MemberType = typeof(GethTransactionTestVectors))]
        public void ShouldRecoverSenderFromRLP_GethTestVectors(GethTransactionTestVectors.TransactionTestCase testCase)
        {
            var txBytes = testCase.TxBytes.HexToByteArray();
            SignedLegacyTransactionBase tx;

            try
            {
                tx = new LegacyTransaction(txBytes);
            }
            catch
            {
                tx = new LegacyTransactionChainId(txBytes);
            }

            var recoveredSender = tx.GetKey().GetPublicAddress().ToLower();

            Assert.Equal(testCase.ExpectedSender.ToLower(), recoveredSender);
        }

        [Theory]
        [MemberData(nameof(GethTransactionTestVectors.GetSignatureTestCases), MemberType = typeof(GethTransactionTestVectors))]
        public void ShouldRoundTripRLPEncoding_GethTestVectors(GethTransactionTestVectors.TransactionTestCase testCase)
        {
            var txBytes = testCase.TxBytes.HexToByteArray();
            SignedLegacyTransactionBase tx;

            try
            {
                tx = new LegacyTransaction(txBytes);
            }
            catch
            {
                tx = new LegacyTransactionChainId(txBytes);
            }

            var reencoded = tx.GetRLPEncoded();

            Assert.Equal(testCase.TxBytes.ToLower(), reencoded.ToHex().EnsureHexPrefix().ToLower());
        }

        [Theory]
        [MemberData(nameof(GethTransactionTestVectors.GetSignatureTestCases), MemberType = typeof(GethTransactionTestVectors))]
        public void ShouldVerifyTransactionHash_GethTestVectors(GethTransactionTestVectors.TransactionTestCase testCase)
        {
            if (string.IsNullOrEmpty(testCase.ExpectedHash))
                return;

            var txBytes = testCase.TxBytes.HexToByteArray();
            SignedLegacyTransactionBase tx;

            try
            {
                tx = new LegacyTransaction(txBytes);
            }
            catch
            {
                tx = new LegacyTransactionChainId(txBytes);
            }

            var hash = tx.Hash.ToHex().EnsureHexPrefix().ToLower();

            Assert.Equal(testCase.ExpectedHash.ToLower(), hash);
        }

        [Fact]
        public void Vitalik_1_DetailedTest()
        {
            var txBytes = "0xf864808504a817c800825208943535353535353535353535353535353535353535808025a0044852b2a670ade5407e78fb2863c51de9fcb96542a07186fe3aeda6bb8a116da0044852b2a670ade5407e78fb2863c51de9fcb96542a07186fe3aeda6bb8a116d".HexToByteArray();
            var tx = new LegacyTransactionChainId(txBytes);

            Assert.Equal(0, tx.Nonce.ToBigIntegerFromRLPDecoded());
            Assert.Equal(new BigInteger(20000000000), tx.GasPrice.ToBigIntegerFromRLPDecoded());
            Assert.Equal(21000, tx.GasLimit.ToBigIntegerFromRLPDecoded());
            Assert.Equal("3535353535353535353535353535353535353535", tx.ReceiveAddress.ToHex());
            Assert.Equal(0, tx.Value.ToBigIntegerFromRLPDecoded());

            Assert.Equal(37, tx.Signature.V.ToBigIntegerFromRLPDecoded());
            Assert.Equal("044852b2a670ade5407e78fb2863c51de9fcb96542a07186fe3aeda6bb8a116d",
                tx.Signature.R.ToHex());
            Assert.Equal("044852b2a670ade5407e78fb2863c51de9fcb96542a07186fe3aeda6bb8a116d",
                tx.Signature.S.ToHex());

            var sender = tx.GetKey().GetPublicAddress();
            Assert.Equal("0xf0f6f18bca1b28cd68e4357452947e021241e9ce", sender.ToLower());
        }

        [Fact]
        public void Vitalik_2_DetailedTest()
        {
            var txBytes = "0xf864018504a817c80182a410943535353535353535353535353535353535353535018025a0489efdaa54c0f20c7adf612882df0950f5a951637e0307cdcb4c672f298b8bcaa0489efdaa54c0f20c7adf612882df0950f5a951637e0307cdcb4c672f298b8bc6".HexToByteArray();
            var tx = new LegacyTransactionChainId(txBytes);

            Assert.Equal(1, tx.Nonce.ToBigIntegerFromRLPDecoded());
            Assert.Equal(new BigInteger(20000000001), tx.GasPrice.ToBigIntegerFromRLPDecoded());
            Assert.Equal(42000, tx.GasLimit.ToBigIntegerFromRLPDecoded());
            Assert.Equal("3535353535353535353535353535353535353535", tx.ReceiveAddress.ToHex());
            Assert.Equal(1, tx.Value.ToBigIntegerFromRLPDecoded());

            var sender = tx.GetKey().GetPublicAddress();
            Assert.Equal("0x23ef145a395ea3fa3deb533b8a9e1b4c6c25d112", sender.ToLower());
        }

        [Fact]
        public void ShouldDecodeTransactionFieldsCorrectly()
        {
            var txBytes = "0xf85f800182520894095e7baea6a6c7c4c2dfeb977efac326af552d870a801ba048b55bfa915ac795c431978d8a6a992b628d557da5ff759b307d495a36649353a01fffd310ac743f371de3b9f7f9cb56c0b28ad43601b4ab949f53faa07bd2c804".HexToByteArray();
            var tx = new LegacyTransaction(txBytes);

            Assert.Equal(0, tx.Nonce.ToBigIntegerFromRLPDecoded());
            Assert.Equal(1, tx.GasPrice.ToBigIntegerFromRLPDecoded());
            Assert.Equal(21000, tx.GasLimit.ToBigIntegerFromRLPDecoded());
            Assert.Equal("095e7baea6a6c7c4c2dfeb977efac326af552d87", tx.ReceiveAddress.ToHex());
            Assert.Equal(10, tx.Value.ToBigIntegerFromRLPDecoded());

            var sender = tx.GetKey().GetPublicAddress();
            Assert.Equal("0x963f4a0d8a11b758de8d5b99ab4ac898d6438ea6", sender.ToLower());
        }
    }
}
