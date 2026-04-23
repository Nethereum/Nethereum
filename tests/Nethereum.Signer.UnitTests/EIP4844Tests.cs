using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class EIP4844Tests
    {
        private const string TestPrivateKey = "45a915e4d060149eb4365960e6a7a45f334393093061116b197e3240065ff2d8";
        private const string TestSender = "0xa94f5374fce5edbc8e2a8697c15331677e6ebf0b";

        private const string ExpectedSignedRlpHex =
            "03f9012d01800285012a05f200833d090094095e7baea6a6c7c4c2dfeb977efac326af552d87" +
            "830186a000f85bf85994095e7baea6a6c7c4c2dfeb977efac326af552d87f842a00000000000" +
            "000000000000000000000000000000000000000000000000000000a000000000000000000000" +
            "000000000000000000000000000000000000000000010af863a001a915e4d060149eb4365960" +
            "e6a7a45f334393093061116b197e3240065f0001a001a915e4d060149eb4365960e6a7a45f33" +
            "4393093061116b197e3240065f0002a001a915e4d060149eb4365960e6a7a45f334393093061" +
            "116b197e3240065f000380a0787d491816e6b21187fc25ee71bc1dd2240b41de89852c21f7a5" +
            "18101dc28bc2a05b5dbb0592f2fdc391efe1e9c6181e54f1b817d07d6f5c7767c7267529528e20";

        private static Transaction4844 CreateTestTransaction()
        {
            var accessList = new List<AccessListItem>
            {
                new AccessListItem
                {
                    Address = "0x095e7baea6a6c7c4c2dfeb977efac326af552d87",
                    StorageKeys = new List<byte[]>
                    {
                        new byte[32],
                        "0000000000000000000000000000000000000000000000000000000000000001".HexToByteArray()
                    }
                }
            };

            var blobHashes = new List<byte[]>
            {
                "01a915e4d060149eb4365960e6a7a45f334393093061116b197e3240065f0001".HexToByteArray(),
                "01a915e4d060149eb4365960e6a7a45f334393093061116b197e3240065f0002".HexToByteArray(),
                "01a915e4d060149eb4365960e6a7a45f334393093061116b197e3240065f0003".HexToByteArray()
            };

            return new Transaction4844(
                chainId: (EvmUInt256)1,
                nonce: (EvmUInt256)0,
                maxPriorityFeePerGas: (EvmUInt256)2,
                maxFeePerGas: (EvmUInt256)5000000000,
                gasLimit: (EvmUInt256)4000000,
                receiverAddress: "0x095e7baea6a6c7c4c2dfeb977efac326af552d87",
                amount: (EvmUInt256)100000,
                data: "0x00",
                accessList: accessList,
                maxFeePerBlobGas: (EvmUInt256)10,
                blobVersionedHashes: blobHashes);
        }

        [Fact]
        public void ShouldDecodeSignedTransaction()
        {
            var rlpBytes = ExpectedSignedRlpHex.HexToByteArray();
            var decoded = Transaction4844Encoder.Current.Decode(rlpBytes);

            Assert.Equal((EvmUInt256)1, decoded.ChainId);
            Assert.Equal((EvmUInt256)0, decoded.Nonce);
            Assert.Equal((EvmUInt256)2, decoded.MaxPriorityFeePerGas);
            Assert.Equal((EvmUInt256)5000000000, decoded.MaxFeePerGas);
            Assert.Equal((EvmUInt256)4000000, decoded.GasLimit);
            Assert.Equal("0x095e7baea6a6c7c4c2dfeb977efac326af552d87", decoded.ReceiverAddress);
            Assert.Equal((EvmUInt256)100000, decoded.Amount);
            Assert.Equal((EvmUInt256)10, decoded.MaxFeePerBlobGas);
            Assert.Equal(3, decoded.BlobVersionedHashes.Count);
            Assert.NotNull(decoded.Signature);
        }

        [Fact]
        public void ShouldVerifySignatureAndRecoverSender()
        {
            var rlpBytes = ExpectedSignedRlpHex.HexToByteArray();
            var decoded = Transaction4844Encoder.Current.Decode(rlpBytes);

            Assert.True(decoded.VerifyTransaction());
            Assert.True(decoded.GetSenderAddress().IsTheSameAddress(TestSender));
        }

        [Fact]
        public void ShouldSignAndMatchExpectedRlp()
        {
            var tx = CreateTestTransaction();
            var signer = new Transaction4844Signer();
            signer.SignTransaction(TestPrivateKey.HexToByteArray(), tx);

            var encoded = tx.GetRLPEncoded().ToHex();
            Assert.Equal(ExpectedSignedRlpHex, encoded);
        }

        [Fact]
        public void ShouldRoundTripEncodeDecode()
        {
            var tx = CreateTestTransaction();
            var signer = new Transaction4844Signer();
            signer.SignTransaction(TestPrivateKey.HexToByteArray(), tx);

            var encoded = tx.GetRLPEncoded();
            var decoded = Transaction4844Encoder.Current.Decode(encoded);

            Assert.Equal(tx.ChainId, decoded.ChainId);
            Assert.Equal(tx.Nonce, decoded.Nonce);
            Assert.Equal(tx.MaxPriorityFeePerGas, decoded.MaxPriorityFeePerGas);
            Assert.Equal(tx.MaxFeePerGas, decoded.MaxFeePerGas);
            Assert.Equal(tx.GasLimit, decoded.GasLimit);
            Assert.Equal(tx.ReceiverAddress, decoded.ReceiverAddress);
            Assert.Equal(tx.Amount, decoded.Amount);
            Assert.Equal(tx.MaxFeePerBlobGas, decoded.MaxFeePerBlobGas);
            Assert.Equal(tx.BlobVersionedHashes.Count, decoded.BlobVersionedHashes.Count);
            for (int i = 0; i < tx.BlobVersionedHashes.Count; i++)
                Assert.Equal(tx.BlobVersionedHashes[i], decoded.BlobVersionedHashes[i]);
        }

        [Fact]
        public void ShouldDecodeViaTransactionFactory()
        {
            var rlpBytes = ExpectedSignedRlpHex.HexToByteArray();
            var tx = TransactionFactory.CreateTransaction(rlpBytes);

            Assert.IsType<Transaction4844>(tx);
            var blob = (Transaction4844)tx;
            Assert.Equal((EvmUInt256)1, blob.ChainId);
            Assert.Equal(3, blob.BlobVersionedHashes.Count);
            Assert.Equal((EvmUInt256)10, blob.MaxFeePerBlobGas);
        }

        [Fact]
        public void ShouldHaveCorrectTransactionType()
        {
            var tx = CreateTestTransaction();
            Assert.Equal(TransactionType.Blob, tx.TransactionType);
            Assert.Equal(0x03, tx.TransactionType.AsByte());
        }

        [Theory]
        [InlineData("blobhashListBounds3.json")]
        [InlineData("blobhashListBounds4.json")]
        [InlineData("blobhashListBounds5.json")]
        [InlineData("blobhashListBounds6.json")]
        [InlineData("blobhashListBounds7.json")]
        [InlineData("blobhashListBounds8.json")]
        [InlineData("blobhashListBounds9.json")]
        [InlineData("blobhashListBounds10.json")]
        [InlineData("createBlobhashTx.json")]
        [InlineData("emptyBlobhashList.json")]
        [InlineData("opcodeBlobhashOutOfRange.json")]
        [InlineData("opcodeBlobhBounds.json")]
        [InlineData("wrongBlobhashVersion.json")]
        public void ShouldDecodeAndReencodeSpecVector(string filename)
        {
            var path = System.IO.Path.Combine(FindTestVectorDir(), filename);
            var json = System.IO.File.ReadAllText(path);
            var testObj = Newtonsoft.Json.Linq.JObject.Parse(json);
            var testName = ((Newtonsoft.Json.Linq.JProperty)testObj.First).Name;
            var test = testObj[testName];

            var cancun = test["post"]["Cancun"][0];
            var txbytes = cancun["txbytes"].ToString();
            var rlpBytes = txbytes.HexToByteArray();

            var decoded = Transaction4844Encoder.Current.Decode(rlpBytes);
            Assert.Equal(TransactionType.Blob, decoded.TransactionType);
            Assert.NotNull(decoded.Signature);

            var reencoded = decoded.GetRLPEncoded().ToHex(true);
            Assert.Equal(txbytes.ToLowerInvariant(), reencoded.ToLowerInvariant());
        }

        [Theory]
        [InlineData("blobhashListBounds3.json", 3)]
        [InlineData("blobhashListBounds6.json", 6)]
        [InlineData("emptyBlobhashList.json", 0)]
        [InlineData("opcodeBlobhBounds.json", 2)]
        public void ShouldSignAndMatchSpecVector(string filename, int expectedBlobCount)
        {
            var path = System.IO.Path.Combine(FindTestVectorDir(), filename);
            var json = System.IO.File.ReadAllText(path);
            var testObj = Newtonsoft.Json.Linq.JObject.Parse(json);
            var testName = ((Newtonsoft.Json.Linq.JProperty)testObj.First).Name;
            var test = testObj[testName];

            var txData = test["transaction"];
            var secretKey = txData["secretKey"].ToString().Replace("0x", "");
            var cancun = test["post"]["Cancun"][0];
            var expectedTxBytes = cancun["txbytes"].ToString();

            var chainId = EvmUInt256.One;
            var nonce = ParseHexUint256(txData["nonce"].ToString());
            var maxPriorityFee = ParseHexUint256(txData["maxPriorityFeePerGas"].ToString());
            var maxFee = ParseHexUint256(txData["maxFeePerGas"].ToString());
            var gasLimit = ParseHexUint256(txData["gasLimit"][0].ToString());
            var to = txData["to"]?.ToString();
            if (string.IsNullOrEmpty(to)) to = null;
            var value = ParseHexUint256(txData["value"][0].ToString());
            var data = txData["data"][0].ToString();
            var maxFeeBlobGas = ParseHexUint256(txData["maxFeePerBlobGas"].ToString());

            var accessList = new List<AccessListItem>();
            var accessLists = txData["accessLists"];
            if (accessLists != null && accessLists[0] != null)
            {
                foreach (var entry in accessLists[0])
                {
                    var item = new AccessListItem
                    {
                        Address = entry["address"].ToString(),
                        StorageKeys = new List<byte[]>()
                    };
                    if (entry["storageKeys"] != null)
                    {
                        foreach (var key in entry["storageKeys"])
                            item.StorageKeys.Add(key.ToString().HexToByteArray());
                    }
                    accessList.Add(item);
                }
            }

            var blobHashes = new List<byte[]>();
            if (txData["blobVersionedHashes"] != null)
            {
                foreach (var hash in txData["blobVersionedHashes"])
                    blobHashes.Add(hash.ToString().HexToByteArray());
            }

            Assert.Equal(expectedBlobCount, blobHashes.Count);

            var tx = new Transaction4844(chainId, nonce, maxPriorityFee, maxFee, gasLimit,
                to, value, data, accessList, maxFeeBlobGas, blobHashes);

            var signer = new Transaction4844Signer();
            signer.SignTransaction(secretKey.HexToByteArray(), tx);

            var encoded = tx.GetRLPEncoded().ToHex(true);
            Assert.Equal(expectedTxBytes.ToLowerInvariant(), encoded.ToLowerInvariant());
        }

        private static EvmUInt256 ParseHexUint256(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex == "0x" || hex == "0x0" || hex == "0x00")
                return EvmUInt256.Zero;
            return EvmUInt256.FromBigEndian(hex.HexToByteArray());
        }

        private static string FindTestVectorDir()
        {
            var dir = System.AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null)
            {
                var candidate = System.IO.Path.Combine(dir, "tests", "Nethereum.EVM.UnitTests",
                    "Tests", "GeneralStateTests", "Cancun", "stEIP4844-blobtransactions");
                if (System.IO.Directory.Exists(candidate)) return candidate;
                dir = System.IO.Path.GetDirectoryName(dir);
            }
            throw new System.IO.DirectoryNotFoundException("Cannot find stEIP4844-blobtransactions test vector directory");
        }
    }
}
