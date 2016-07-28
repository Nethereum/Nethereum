using System;
using System.Numerics;
using NBitcoin.Crypto;
using Xunit;
using Nethereum.ABI.Util.RLP;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Core;
using System.Diagnostics;
using System.Linq;
using Nethereum.Hex.HexTypes;

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
        public void ShouldResolveAddress()
        {
            //data from https://github.com/ethereum/go-ethereum/blob/506c9277911746dfbab0a585aee736bd3095f206/tests/files/TransactionTests/Homestead/ttTransactionTest.json
           var rlp = "0xf87c80018261a894095e7baea6a6c7c4c2dfeb977efac326af552d870a9d00000000000000000000000000010000000000000000000000000000001ba048b55bfa915ac795c431978d8a6a992b628d557da5ff759b307d495a36649353a01fffd310ac743f371de3b9f7f9cb56c0b28ad43601b4ab949f53faa07bd2c804";
           var tx = new Transaction(rlp.HexToByteArray());
            var address = GetPublicEthereumAddress(tx.GetKey());
            Assert.Equal("67719a47cf3e3fe77b89c994d85395ad0f899d86", address);

            rlp =   "0xf85f800182520894095e7baea6a6c7c4c2dfeb977efac326af552d870a801ba048b55bfa915ac795c431978d8a6a992b628d557da5ff759b307d495a36649353a01fffd310ac743f371de3b9f7f9cb56c0b28ad43601b4ab949f53faa07bd2c804";
            tx = new Transaction(rlp.HexToByteArray());
            address = GetPublicEthereumAddress(tx.GetKey());
            Assert.Equal("963f4a0d8a11b758de8d5b99ab4ac898d6438ea6", address);


        }


        public void ShouldCreateASignedTransaction()
        {
            string privateKey = "b5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
            string sendersAddress = "12890d2cce102216644c59daE5baed380d84830c";
            string publicKey = "87977ddf1e8e4c3f0a4619601fc08ac5c1dcf78ee64e826a63818394754cef52457a10a599cb88afb7c5a6473b7534b8b150d38d48a11c9b515dd01434cceb08";
            Debug.WriteLine(new HexBigInteger(10000).HexValue);
            Debug.WriteLine(new HexBigInteger(324).HexValue);
            Debug.WriteLine(new HexBigInteger(10000000000000).HexValue);
            Debug.WriteLine(new HexBigInteger(21000).HexValue);

            var tx = Transaction.Create("0x13f022d72158410433cbd66f5dd8bf6d2d129924", 10000, 324, 10000000000000, 21000);
            tx.Sign(new ECKey(privateKey.HexToByteArray(), true));
            var encoded = tx.GetEncoded();
            Debug.WriteLine(encoded.ToHex());
            var address = GetPublicEthereumAddress(tx.GetKey());
            Assert.Equal(GetPublicEthereumAddress(privateKey), address);

            var rlp = "f8698201448609184e72a0008252089413f022d72158410433cbd66f5dd8bf6d2d129924822710801ca0b1874eb8dab80e9072e57b746f8f0f281890568fd655488b0a1f5556a117775ea06ea87e03a9131cae14b5420cbfeb984bb2641d76fb32327d87cf0c9c0ee8f234";
            var tx3 = new Transaction(rlp.HexToByteArray());
            Assert.Equal(tx.GetData(), tx3.GetData());
            
            Debug.WriteLine(tx.ToJsonHex());

            Transaction tx2 = new Transaction(tx.GetEncoded());
            address = GetPublicEthereumAddress(tx2.GetKey());
            Assert.Equal(GetPublicEthereumAddress(privateKey), address);
            //Assert.Equal("f8513c7b69e17eaa7c4283a0c52843864a4cd95cf108b2cb1010a45116bf163a", tx2.GetSignature().S.ToByteArrayUnsigned().ToHex());

            Assert.Equal(tx.GetGasLimit().ToHex(), tx3.GetGasLimit().ToHex());
            Assert.Equal(tx.GetNonce().ToHex(), tx3.GetNonce().ToHex());
            Assert.Equal(tx.GetGasPrice().ToHex(), tx3.GetGasPrice().ToHex());
            Assert.Equal(tx.GetValue().ToHex(), tx3.GetValue().ToHex());
            Assert.Equal(tx.GetRawHash().ToHex(), tx3.GetRawHash().ToHex());
            Assert.Equal(GetPublicEthereumAddress(tx3.GetKey()), GetPublicEthereumAddress(tx.GetKey()));
           
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
            
            tx.Sign(new ECKey(KEY.HexToByteArray(), true));
            tx.GetKey().Verify(tx.GetRawHash(), tx.GetSignature());
            var address = GetPublicEthereumAddress(tx.GetKey());
            Assert.Equal(GetPublicEthereumAddress(KEY), address);
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
            
            Assert.Equal("eab47c1a49bf2fe5d40e01d313900e19ca485867d462fe06e139e3a536c6d4f4", tx.GetSignature().R.ToByteArrayUnsigned().ToHex());
            
            Assert.Equal("14a569d327dcda4b29f74f93c0e9729d2f49ad726e703f9cd90dbb0fbf6649f1", tx.GetSignature().S.ToByteArrayUnsigned().ToHex());

        }

        [Fact]
        public void ShouldSignEncodeTransactionAndRecoverPublicAddress()
        {
            string privateKey = "b5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
            string sendersAddress = "12890d2cce102216644c59daE5baed380d84830c";
            string publicKey = "87977ddf1e8e4c3f0a4619601fc08ac5c1dcf78ee64e826a63818394754cef52457a10a599cb88afb7c5a6473b7534b8b150d38d48a11c9b515dd01434cceb08";
            
            var key = new ECKey(privateKey.HexToByteArray(), true);
            var pubKeyGen = key.GetPubKey(false);
            Debug.WriteLine(pubKeyGen.ToHex());
            var pubKey = key.GetPublicKeyParameters();
            var hash = "test".ToHexUTF8().HexToByteArray();
            var signature = key.Sign(hash);
            Assert.True(key.Verify(hash, signature));


            var initaddr = new Nethereum.ABI.Util.Sha3Keccack().CalculateHash(key.GetEthereumPubKeyForAddress());
            var addr = new byte[initaddr.Length - 12];
            Array.Copy(initaddr, 12, addr, 0, initaddr.Length - 12);
            var addrHex = addr.ToHex();
            Assert.Equal(sendersAddress.ToLower(), addrHex);
        }

        public string GetPublicEthereumAddress(string privateKey)
        {
            var key = new ECKey(privateKey.HexToByteArray(), true);
            return GetPublicEthereumAddress(key);
        }

        public string GetPublicEthereumAddress(ECKey key)
        {
            var initaddr = new Nethereum.ABI.Util.Sha3Keccack().CalculateHash(key.GetEthereumPubKeyForAddress());
            var addr = new byte[initaddr.Length - 12];
            Array.Copy(initaddr, 12, addr, 0, initaddr.Length - 12);
            return addr.ToHex();
        }
    }
}