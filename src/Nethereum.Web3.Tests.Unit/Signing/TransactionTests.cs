using System.Diagnostics;
using System.Numerics;
using Nethereum.ABI.Util;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.RLP;
using Xunit;

namespace Nethereum.Web3.Tests.Unit
{
    public class TransactionTests
    {
        private readonly string HASH_TX = "328ea6d24659dec48adea1aced9a136e5ebdf40258db30d1b1d97ed2b74be34e";
        private readonly string KEY = "c85ef7d79691fe79573b1a7064c19c1a9819ebdbd1faaab1a8ec92344438aaf4";

        private readonly string RLP_ENCODED_SIGNED_TX =
            "f86b8085e8d4a510008227109413978aee95f38490e9769c39b2773ed763d9cd5f872386f26fc10000801ba0eab47c1a49bf2fe5d40e01d313900e19ca485867d462fe06e139e3a536c6d4f4a014a569d327dcda4b29f74f93c0e9729d2f49ad726e703f9cd90dbb0fbf6649f1";

        private readonly string RLP_ENCODED_UNSIGNED_TX =
            "eb8085e8d4a510008227109413978aee95f38490e9769c39b2773ed763d9cd5f872386f26fc1000080808080";

        private byte[] testData = "".ToBytesForRLPEncoding();
        private readonly byte[] testGasLimit = new BigInteger(10000).ToBytesForRLPEncoding();
        private readonly byte[] testGasPrice = new BigInteger(1000000000000L).ToBytesForRLPEncoding();
        private byte[] testInit = "".ToBytesForRLPEncoding();
        private byte[] testNonce = 0.ToBytesForRLPEncoding();
        private readonly byte[] testReceiveAddress = "13978aee95f38490e9769c39b2773ed763d9cd5f".HexToByteArray();
        private readonly byte[] testValue = new BigInteger(10000000000000000L).ToBytesForRLPEncoding();

        [Fact]
        public void ShouldEncodeATransactionUsingKeccak256()
        {
            var txRaw =
                "F89D80809400000000000000000000000000000000000000008609184E72A000822710B3606956330C0D630000003359366000530A0D630000003359602060005301356000533557604060005301600054630000000C5884336069571CA07F6EB94576346488C6253197BDE6A7E59DDC36F2773672C849402AA9C402C3C4A06D254E662BF7450DD8D835160CBB053463FED0B53F2CDD7F3EA8731919C8E8CC";
            var txHashB = new Sha3Keccack().CalculateHash(txRaw.HexToByteArray());
            var txHash = txHashB.ToHex();
            Assert.Equal("4b7d9670a92bf120d5b43400543b69304a14d767cf836a7f6abff4edde092895", txHash);
        }

        [Fact]
        public void ShouldResolveAddress()
        {
            //data from https://github.com/ethereum/go-ethereum/blob/506c9277911746dfbab0a585aee736bd3095f206/tests/files/TransactionTests/Homestead/ttTransactionTest.json
            var rlp =
                "0xf87c80018261a894095e7baea6a6c7c4c2dfeb977efac326af552d870a9d00000000000000000000000000010000000000000000000000000000001ba048b55bfa915ac795c431978d8a6a992b628d557da5ff759b307d495a36649353a01fffd310ac743f371de3b9f7f9cb56c0b28ad43601b4ab949f53faa07bd2c804";
            var tx = new Transaction(rlp.HexToByteArray());
            Assert.Equal("67719a47cf3e3fe77b89c994d85395ad0f899d86".EnsureHexPrefix().ToLower(), tx.Key.GetPublicAddress().EnsureHexPrefix().ToLower());
            rlp =
                "0xf85f800182520894095e7baea6a6c7c4c2dfeb977efac326af552d870a801ba048b55bfa915ac795c431978d8a6a992b628d557da5ff759b307d495a36649353a01fffd310ac743f371de3b9f7f9cb56c0b28ad43601b4ab949f53faa07bd2c804";
            tx = new Transaction(rlp.HexToByteArray());
            Assert.Equal("0x963f4a0d8a11b758de8d5b99ab4ac898d6438ea6".EnsureHexPrefix().ToLower(), tx.Key.GetPublicAddress().EnsureHexPrefix().ToLower());
        }

        [Fact]
        public void ShouldCreateASignedTransaction_Legacy()
        {
            var privateKey = "b5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";

            //data use for other tools for comparison
            Debug.WriteLine(new HexBigInteger(10000).HexValue);
            Debug.WriteLine(new HexBigInteger(324).HexValue);
            Debug.WriteLine(new HexBigInteger(10000000000000).HexValue);
            Debug.WriteLine(new HexBigInteger(21000).HexValue);

            //Create a transaction from scratch
            var tx = new Transaction("0x13f022d72158410433cbd66f5dd8bf6d2d129924", 10000, 324, 10000000000000, 21000);
            tx.Sign(new EthECKey(privateKey.HexToByteArray(), true));

            var encoded = tx.GetRLPEncoded();
            var rlp =
                "f8698201448609184e72a0008252089413f022d72158410433cbd66f5dd8bf6d2d129924822710801ca0b1874eb8dab80e9072e57b746f8f0f281890568fd655488b0a1f5556a117775ea06ea87e03a9131cae14b5420cbfeb984bb2641d76fb32327d87cf0c9c0ee8f234";

            Assert.Equal(rlp, encoded.ToHex());
            //data used for other tools for comparison
            Debug.WriteLine(encoded.ToHex());

            Assert.Equal(EthECKey.GetPublicAddress(privateKey), tx.Key.GetPublicAddress());

            var tx3 = new Transaction(rlp.HexToByteArray());
            Assert.Equal(tx.Data, tx3.Data ?? new byte[] { });

            Debug.WriteLine(tx.ToJsonHex());

            var tx2 = new Transaction(tx.GetRLPEncoded());
            Assert.Equal(EthECKey.GetPublicAddress(privateKey), tx2.Key.GetPublicAddress());

            Assert.Equal(tx.GasLimit.ToHex(), tx3.GasLimit.ToHex());
            Assert.Equal(tx.Nonce.ToHex(), tx3.Nonce.ToHex());
            Assert.Equal(tx.GasPrice.ToHex(), tx3.GasPrice.ToHex());
            Assert.Equal(tx.Value.ToHex(), tx3.Value.ToHex());
            Assert.Equal(tx.RawHash.ToHex(), tx3.RawHash.ToHex());
            Assert.Equal(tx3.Key.GetPublicAddress(), tx.Key.GetPublicAddress());
            Assert.Equal(tx2.RawHash.ToHex(), tx3.RawHash.ToHex());
            Assert.Equal(tx2.Key.GetPublicAddress(), tx.Key.GetPublicAddress());
        }

        [Fact]
        public void TestTransactionFromUnSignedRLP_Legacy()
        {
            var tx = new Transaction(RLP_ENCODED_UNSIGNED_TX.HexToByteArray());

            Assert.Equal(RLP_ENCODED_UNSIGNED_TX, tx.GetRLPEncoded().ToHex());
            Assert.Equal(BigInteger.Zero, tx.Nonce.ToBigIntegerFromRLPDecoded());
            Assert.Equal(testGasPrice.ToBigIntegerFromRLPDecoded(), tx.GasPrice.ToBigIntegerFromRLPDecoded());
            Assert.Equal(testGasLimit.ToBigIntegerFromRLPDecoded(), tx.GasLimit.ToBigIntegerFromRLPDecoded());
            Assert.Equal(testReceiveAddress.ToHex(), tx.ReceiveAddress.ToHex());
            Assert.Equal(testValue.ToBigIntegerFromRLPDecoded(), tx.Value.ToBigIntegerFromRLPDecoded());

            Assert.Equal(HASH_TX, tx.RawHash.ToHex());

            tx.Sign(new EthECKey(KEY.HexToByteArray(), true));
            tx.Key.Verify(tx.RawHash, tx.Signature);
            Assert.Equal(EthECKey.GetPublicAddress(KEY), tx.Key.GetPublicAddress());
        }

        [Fact]
        public void TestTransactionFromSignedRLP()
        {
            var tx = new Transaction(RLP_ENCODED_SIGNED_TX.HexToByteArray());

            Assert.Equal(HASH_TX, tx.RawHash.ToHex());
            Assert.Equal(RLP_ENCODED_SIGNED_TX, tx.GetRLPEncoded().ToHex());

            Assert.Equal(BigInteger.Zero, tx.Nonce.ToBigIntegerFromRLPDecoded());
            Assert.Equal(testGasPrice.ToBigIntegerFromRLPDecoded(), tx.GasPrice.ToBigIntegerFromRLPDecoded());
            Assert.Equal(testGasLimit.ToBigIntegerFromRLPDecoded(), tx.GasLimit.ToBigIntegerFromRLPDecoded());
            Assert.Equal(testReceiveAddress.ToHex(), tx.ReceiveAddress.ToHex());
            Assert.Equal(testValue.ToBigIntegerFromRLPDecoded(), tx.Value.ToBigIntegerFromRLPDecoded());

            Assert.Null(tx.Data);
            Assert.Equal(27, tx.Signature.V);

            Assert.Equal("eab47c1a49bf2fe5d40e01d313900e19ca485867d462fe06e139e3a536c6d4f4",
                tx.Signature.R.ToHex());

            Assert.Equal("14a569d327dcda4b29f74f93c0e9729d2f49ad726e703f9cd90dbb0fbf6649f1",
                tx.Signature.S.ToHex());
        }

        [Fact]
        public void ShouldSignEncodeTransactionAndRecoverPublicAddress()
        {
            var privateKey = "b5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
            var sendersAddress = "12890d2cce102216644c59daE5baed380d84830c";
            var publicKey =
                "87977ddf1e8e4c3f0a4619601fc08ac5c1dcf78ee64e826a63818394754cef52457a10a599cb88afb7c5a6473b7534b8b150d38d48a11c9b515dd01434cceb08";

            var key = new EthECKey(privateKey.HexToByteArray(), true);
            var hash = "test".ToHexUTF8().HexToByteArray();
            var signature = key.Sign(hash);
            Assert.True(key.Verify(hash, signature));
            Assert.Equal(key.GetPubKeyNoPrefix().ToHex(), publicKey);
            Assert.Equal(sendersAddress.EnsureHexPrefix().ToLower(), key.GetPublicAddress().EnsureHexPrefix().ToLower());
        }


        [Fact]
        public void ShouldGenerateECKey()
        {
            var ecKey = EthECKey.GenerateKey();
            var key = ecKey.GetPrivateKeyAsBytes();
            var regeneratedKey = new EthECKey(key, true);
            Assert.Equal(key.ToHex(), regeneratedKey.GetPrivateKeyAsBytes().ToHex());
            Assert.Equal(ecKey.GetPublicAddress().EnsureHexPrefix().ToLower(), regeneratedKey.GetPublicAddress().EnsureHexPrefix().ToLower());
        }
    }
}