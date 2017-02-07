using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using NBitcoin.Crypto;
using Nethereum.ABI.Util;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Signer;
using Nethereum.RLP;
using Xunit;

namespace Nethereum.Web3.Tests
{
   
    public class RLPSignerTests
    {
        private const int NumberOfElementsInTrasaction = 6;

        [Fact]
        public void ShouldResolveAddress()
        {
            //data from https://github.com/ethereum/go-ethereum/blob/506c9277911746dfbab0a585aee736bd3095f206/tests/files/TransactionTests/Homestead/ttTransactionTest.json
            var rlp =
                "0xf87c80018261a894095e7baea6a6c7c4c2dfeb977efac326af552d870a9d00000000000000000000000000010000000000000000000000000000001ba048b55bfa915ac795c431978d8a6a992b628d557da5ff759b307d495a36649353a01fffd310ac743f371de3b9f7f9cb56c0b28ad43601b4ab949f53faa07bd2c804";

            var tx = new RLPSigner(rlp.HexToByteArray(), NumberOfElementsInTrasaction);
            Assert.Equal("67719a47cf3e3fe77b89c994d85395ad0f899d86", tx.Key.GetPublicAddress());
            rlp =
                "0xf85f800182520894095e7baea6a6c7c4c2dfeb977efac326af552d870a801ba048b55bfa915ac795c431978d8a6a992b628d557da5ff759b307d495a36649353a01fffd310ac743f371de3b9f7f9cb56c0b28ad43601b4ab949f53faa07bd2c804";
            tx = new RLPSigner(rlp.HexToByteArray(), NumberOfElementsInTrasaction);
            Assert.Equal("963f4a0d8a11b758de8d5b99ab4ac898d6438ea6", tx.Key.GetPublicAddress());
        }

        [Fact]
        public void ShouldCreateASignedTransaction()
        {
            var privateKey = "b5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
            var sendersAddress = "12890d2cce102216644c59daE5baed380d84830c";
            var publicKey =
                "87977ddf1e8e4c3f0a4619601fc08ac5c1dcf78ee64e826a63818394754cef52457a10a599cb88afb7c5a6473b7534b8b150d38d48a11c9b515dd01434cceb08";

            //data use for other tools for comparison
            Debug.WriteLine(new HexBigInteger(10000).HexValue);
            Debug.WriteLine(new HexBigInteger(324).HexValue);
            Debug.WriteLine(new HexBigInteger(10000000000000).HexValue);
            Debug.WriteLine(new HexBigInteger(21000).HexValue);
            //*****************************
            //order in transaction = nonce, gasPrice, gasLimit, receiveAddress, value, data
            //***************************

            var nonce = 324.ToBytesForRLPEncoding();
            var amount = 10000.ToBytesForRLPEncoding();
            var to = "0x13f022d72158410433cbd66f5dd8bf6d2d129924".HexToByteArray();
            var gasPrice = 10000000000000.ToBytesForRLPEncoding();
            var gasLimit = 21000.ToBytesForRLPEncoding();
            var data = "".HexToByteArray();


            //Create a transaction from scratch
            var tx = new RLPSigner(new byte[][] {nonce, gasPrice, gasLimit, to, amount, data});
            tx.Sign(new EthECKey(privateKey.HexToByteArray(), true));

            var encoded = tx.GetRLPEncoded();
            var rlp =
                "f8698201448609184e72a0008252089413f022d72158410433cbd66f5dd8bf6d2d129924822710801ca0b1874eb8dab80e9072e57b746f8f0f281890568fd655488b0a1f5556a117775ea06ea87e03a9131cae14b5420cbfeb984bb2641d76fb32327d87cf0c9c0ee8f234";

            Assert.Equal(rlp, encoded.ToHex());
            //data used for other tools for comparison
            Debug.WriteLine(encoded.ToHex());

            Assert.Equal(EthECKey.GetPublicAddress(privateKey), tx.Key.GetPublicAddress());

            var tx3 = new RLPSigner(rlp.HexToByteArray(), 6);
            Assert.Equal(tx.Data[5], tx3.Data[5] ?? new byte[] {});



            var tx2 = new Transaction(tx.GetRLPEncoded());
            Assert.Equal(EthECKey.GetPublicAddress(privateKey), tx2.Key.GetPublicAddress());
            //gas limit order 3
            Assert.Equal(tx.Data[2].ToHex(), tx3.Data[2].ToHex());
            //nonce order 1
            Assert.Equal(tx.Data[0].ToHex(), tx3.Data[0].ToHex());
            // gas price order 2
            Assert.Equal(tx.Data[1].ToHex(), tx3.Data[1].ToHex());
            //value order 5
            Assert.Equal(tx.Data[4].ToHex(), tx3.Data[4].ToHex());
            Assert.Equal(tx.RawHash.ToHex(), tx3.RawHash.ToHex());
            Assert.Equal(tx3.Key.GetPublicAddress(), tx.Key.GetPublicAddress());

            Assert.Equal(tx2.RawHash.ToHex(), tx3.RawHash.ToHex());
            Assert.Equal(tx2.Key.GetPublicAddress(), tx.Key.GetPublicAddress());
        }

        [Fact]
        public void ShouldSignAndEncodeAsString()
        {
            var account = "12890d2cce102216644c59daE5baed380d84830c";
            var privateKey = "b5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
            var signedValue = new RLPSigner(new byte[][] { "hello".ToBytesForRLPEncoding() });
            signedValue.Sign(new EthECKey(privateKey.HexToByteArray(), true));
            var encoded = signedValue.GetRLPEncoded();
            var hexEncoded = encoded.ToHex();
            var signedRecovery = new RLPSigner(encoded, 1);
            var value = signedRecovery.Data[0].ToStringFromRLPDecoded();
            Assert.Equal("hello", value);
            var addressSender = signedRecovery.Key.GetPublicAddress();
            Assert.Equal(account.ToLower(), addressSender);
        }

    }
}