using System.Numerics;
using Xunit;
using Nethereum.ABI.Util.RLP;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Core;

namespace SimpleTests
{
    public class TransactionTests
    {
      
        private string RLP_ENCODED_RAW_TX = "e88085e8d4a510008227109413978aee95f38490e9769c39b2773ed763d9cd5f872386f26fc1000080";
        private string RLP_ENCODED_UNSIGNED_TX = "eb8085e8d4a510008227109413978aee95f38490e9769c39b2773ed763d9cd5f872386f26fc1000080808080";
        private string HASH_TX = "328ea6d24659dec48adea1aced9a136e5ebdf40258db30d1b1d97ed2b74be34e";
        private string RLP_ENCODED_SIGNED_TX = "f86b8085e8d4a510008227109413978aee95f38490e9769c39b2773ed763d9cd5f872386f26fc10000801ba0eab47c1a49bf2fe5d40e01d313900e19ca485867d462fe06e139e3a536c6d4f4a014a569d327dcda4b29f74f93c0e9729d2f49ad726e703f9cd90dbb0fbf6649f1";
        private string KEY = "c85ef7d79691fe79573b1a7064c19c1a9819ebdbd1faaab1a8ec92344438aaf4";
        private byte[] testNonce = 0.ToBytesForRLPEncoding();
        private byte[] testGasPrice = new BigInteger(1000000000000L).ToBytesForRLPEncoding();
        private byte[] testGasLimit = new BigInteger(10000).ToBytesForRLPEncoding(); 
        private byte[] testReceiveAddress = "13978aee95f38490e9769c39b2773ed763d9cd5f".HexToByteArray();
        private byte[] testValue = new BigInteger(10000000000000000L).ToBytesForRLPEncoding();
        private byte[] testData = "".ToBytesForRLPEncoding();
        private byte[] testInit = "".ToBytesForRLPEncoding();

        [Fact]
        public void ShouldEncodeATransactionUsingKeccak256()
        { 
            string txRaw = "F89D80809400000000000000000000000000000000000000008609184E72A000822710B3606956330C0D630000003359366000530A0D630000003359602060005301356000533557604060005301600054630000000C5884336069571CA07F6EB94576346488C6253197BDE6A7E59DDC36F2773672C849402AA9C402C3C4A06D254E662BF7450DD8D835160CBB053463FED0B53F2CDD7F3EA8731919C8E8CC";
            byte[] txHashB = new Nethereum.ABI.Util.Sha3Keccack().CalculateHash(txRaw.HexToByteArray());
            string txHash = txHashB.ToHex();
            Assert.Equal("4b7d9670a92bf120d5b43400543b69304a14d767cf836a7f6abff4edde092895", txHash);
        }

        [Fact]
        public void TestTransactionFromUnSignedRLP()
        {
            Transaction tx = new Transaction(RLP_ENCODED_UNSIGNED_TX.HexToByteArray());
                 
            Assert.Equal(RLP_ENCODED_UNSIGNED_TX, tx.GetEncoded().ToHex());
            Assert.Equal(BigInteger.Zero, tx.GetNonce().ToBigIntegerFromRLPDecoded());
            Assert.Equal(testGasPrice.ToBigIntegerFromRLPDecoded(), tx.GetGasPrice().ToBigIntegerFromRLPDecoded());
            Assert.Equal(testGasLimit.ToBigIntegerFromRLPDecoded(), tx.GetGasLimit().ToBigIntegerFromRLPDecoded());
            Assert.Equal(testReceiveAddress.ToHex(), tx.GetReceiveAddress().ToHex());
            Assert.Equal(testValue.ToBigIntegerFromRLPDecoded(), tx.GetValue().ToBigIntegerFromRLPDecoded());
         
            Assert.Equal(HASH_TX, tx.GetRawHash().ToHex());

        }

        [Fact]
        public void TestTransactionFromSignedRLP()
        {
            Transaction tx = new Transaction(RLP_ENCODED_SIGNED_TX.HexToByteArray());

            Assert.Equal(HASH_TX, tx.GetRawHash().ToHex());
            Assert.Equal(RLP_ENCODED_SIGNED_TX, tx.GetEncoded().ToHex());

            Assert.Equal(BigInteger.Zero, tx.GetNonce().ToBigIntegerFromRLPDecoded());
            Assert.Equal(testGasPrice.ToBigIntegerFromRLPDecoded(), tx.GetGasPrice().ToBigIntegerFromRLPDecoded());
            Assert.Equal(testGasLimit.ToBigIntegerFromRLPDecoded(), tx.GetGasLimit().ToBigIntegerFromRLPDecoded());
            Assert.Equal(testReceiveAddress.ToHex(), tx.GetReceiveAddress().ToHex());
            Assert.Equal(testValue.ToBigIntegerFromRLPDecoded(), tx.GetValue().ToBigIntegerFromRLPDecoded());

            Assert.Null(tx.GetData());
            Assert.Equal(27, tx.GetSignature().V);
            
            Assert.Equal("eab47c1a49bf2fe5d40e01d313900e19ca485867d462fe06e139e3a536c6d4f4", new BigInteger(tx.GetSignature().R.ToByteArrayUnsigned()).ToBytesForRLPEncoding().ToHex());
            
            Assert.Equal("14a569d327dcda4b29f74f93c0e9729d2f49ad726e703f9cd90dbb0fbf6649f1", new BigInteger(tx.GetSignature().S.ToByteArrayUnsigned()).ToBytesForRLPEncoding().ToHex());
        }
    }
}