using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class EIP2930Tests
    {
        private const string TestPrivateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
        private const string ExpectedSenderAddress = "0x12890D2cce102216644c59daE5baed380d84830c";

        [Theory]
        [InlineData(1, 0, 20000000000, 21000, "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836", 1000000000000000000, "")]
        [InlineData(1, 1, 25000000000, 42000, "0x3535353535353535353535353535353535353535", 0, "0x1234")]
        [InlineData(5, 100, 1000000000, 100000, "0xde0b295669a9fd93d5f28d9ec85e40f4cb697bae", 500000000000000000, "0xabcdef")]
        [InlineData(1337, 0, 0, 21000, null, 0, null)]
        public void ShouldEncodeDecodeSignTransaction2930(ulong chainId, ulong nonce, ulong gasPrice,
            ulong gasLimit, string receiverAddress, ulong amount, string data)
        {
            var transaction = new Transaction2930(
                new BigInteger(chainId),
                new BigInteger(nonce),
                new BigInteger(gasPrice),
                new BigInteger(gasLimit),
                receiverAddress,
                new BigInteger(amount),
                data,
                null);

            var signer = new TypeTransactionSigner<Transaction2930>();
            var signedHex = signer.SignTransaction(TestPrivateKey, transaction);

            var decoded = Transaction2930Encoder.Current.Decode(signedHex.HexToByteArray());

            Assert.Equal(chainId, (ulong)decoded.ChainId);
            Assert.Equal(nonce, (ulong)decoded.Nonce.Value);
            Assert.Equal(gasPrice, (ulong)decoded.GasPrice.Value);
            Assert.Equal(gasLimit, (ulong)decoded.GasLimit.Value);
            Assert.Equal(amount, (ulong)decoded.Amount.Value);

            if (string.IsNullOrEmpty(receiverAddress))
            {
                Assert.Null(decoded.ReceiverAddress);
            }
            else
            {
                Assert.True(receiverAddress.IsTheSameAddress(decoded.ReceiverAddress));
            }

            if (string.IsNullOrEmpty(data))
            {
                Assert.Null(decoded.Data);
            }
            else
            {
                Assert.Equal(data.ToLower(), decoded.Data.ToLower());
            }

            Assert.NotNull(decoded.Signature);
            Assert.NotNull(decoded.Signature.R);
            Assert.NotNull(decoded.Signature.S);
            Assert.NotNull(decoded.Signature.V);
        }

        [Fact]
        public void ShouldEncodeDecodeWithAccessList()
        {
            var accessList = new List<AccessListItem>
            {
                new AccessListItem("0x627306090abaB3A6e1400e9345bC60c78a8BEf57",
                    new List<byte[]>
                    {
                        "0x3fd54831f488a22b28398de0c567a3b064b937f54f81739ae9bd545967f3abab".HexToByteArray(),
                        "0x3fd54831f488a22b28398de0c567a3b064b937f54f81739ae9bd545967f3abac".HexToByteArray()
                    }),
                new AccessListItem("0x427306090abaB3A6e1400e9345bC60c78a8BEf5c",
                    new List<byte[]>
                    {
                        "0x0000000000000000000000000000000000000000000000000000000000000001".HexToByteArray()
                    })
            };

            var transaction = new Transaction2930(
                1,
                0,
                20000000000,
                100000,
                "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836",
                1000000000000000000,
                "0x",
                accessList);

            var signer = new TypeTransactionSigner<Transaction2930>();
            var signedHex = signer.SignTransaction(TestPrivateKey, transaction);

            var decoded = Transaction2930Encoder.Current.Decode(signedHex.HexToByteArray());

            Assert.NotNull(decoded.AccessList);
            Assert.Equal(2, decoded.AccessList.Count);

            Assert.True(accessList[0].Address.IsTheSameAddress(decoded.AccessList[0].Address));
            Assert.Equal(2, decoded.AccessList[0].StorageKeys.Count);
            Assert.Equal(accessList[0].StorageKeys[0].ToHex(), decoded.AccessList[0].StorageKeys[0].ToHex());
            Assert.Equal(accessList[0].StorageKeys[1].ToHex(), decoded.AccessList[0].StorageKeys[1].ToHex());

            Assert.True(accessList[1].Address.IsTheSameAddress(decoded.AccessList[1].Address));
            Assert.Single(decoded.AccessList[1].StorageKeys);
            Assert.Equal(accessList[1].StorageKeys[0].ToHex(), decoded.AccessList[1].StorageKeys[0].ToHex());
        }

        [Fact]
        public void ShouldEncodeDecodeWithEmptyAccessList()
        {
            var accessList = new List<AccessListItem>();

            var transaction = new Transaction2930(
                1,
                5,
                30000000000,
                21000,
                "0x3535353535353535353535353535353535353535",
                100000000000000000,
                null,
                accessList);

            var signer = new TypeTransactionSigner<Transaction2930>();
            var signedHex = signer.SignTransaction(TestPrivateKey, transaction);

            var decoded = Transaction2930Encoder.Current.Decode(signedHex.HexToByteArray());

            Assert.True(decoded.AccessList == null || decoded.AccessList.Count == 0);
        }

        [Fact]
        public void ShouldRecoverSenderAddress()
        {
            var transaction = new Transaction2930(
                1,
                0,
                20000000000,
                21000,
                "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836",
                1000000000000000000,
                null,
                null);

            var signer = new TypeTransactionSigner<Transaction2930>();
            signer.SignTransaction(TestPrivateKey, transaction);

            var decoded = Transaction2930Encoder.Current.Decode(transaction.GetRLPEncoded());

            Assert.True(decoded.VerifyTransaction());
            var senderAddress = decoded.GetSenderAddress();
            Assert.True(ExpectedSenderAddress.IsTheSameAddress(senderAddress));
        }

        [Fact]
        public void ShouldHaveCorrectTransactionType()
        {
            var transaction = new Transaction2930(
                1,
                0,
                20000000000,
                21000,
                "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836",
                1000000000000000000,
                null,
                null);

            Assert.Equal(TransactionType.LegacyEIP2930, transaction.TransactionType);
        }

        [Fact]
        public void ShouldStartWithTypeByte()
        {
            var transaction = new Transaction2930(
                1,
                0,
                20000000000,
                21000,
                "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836",
                1000000000000000000,
                null,
                null);

            var signer = new TypeTransactionSigner<Transaction2930>();
            signer.SignTransaction(TestPrivateKey, transaction);

            var encoded = transaction.GetRLPEncoded();
            Assert.Equal(0x01, encoded[0]);
        }

        [Fact]
        public void ShouldHandleAccessListWithNoStorageKeys()
        {
            var accessList = new List<AccessListItem>
            {
                new AccessListItem("0x627306090abaB3A6e1400e9345bC60c78a8BEf57", new List<byte[]>())
            };

            var transaction = new Transaction2930(
                1,
                0,
                20000000000,
                100000,
                "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836",
                0,
                null,
                accessList);

            var signer = new TypeTransactionSigner<Transaction2930>();
            var signedHex = signer.SignTransaction(TestPrivateKey, transaction);

            var decoded = Transaction2930Encoder.Current.Decode(signedHex.HexToByteArray());

            Assert.Single(decoded.AccessList);
            Assert.True(accessList[0].Address.IsTheSameAddress(decoded.AccessList[0].Address));
            Assert.Empty(decoded.AccessList[0].StorageKeys);
        }

        [Fact]
        public void ShouldRoundTripRLPEncoding()
        {
            var accessList = new List<AccessListItem>
            {
                new AccessListItem("0x627306090abaB3A6e1400e9345bC60c78a8BEf57",
                    new List<byte[]>
                    {
                        "0x0000000000000000000000000000000000000000000000000000000000000000".HexToByteArray()
                    })
            };

            var transaction = new Transaction2930(
                1,
                42,
                50000000000,
                65000,
                "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836",
                2500000000000000000,
                "0xdeadbeef",
                accessList);

            var signer = new TypeTransactionSigner<Transaction2930>();
            signer.SignTransaction(TestPrivateKey, transaction);

            var encoded = transaction.GetRLPEncoded();
            var decoded = Transaction2930Encoder.Current.Decode(encoded);
            var reencoded = decoded.GetRLPEncoded();

            Assert.Equal(encoded.ToHex(), reencoded.ToHex());
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(5)]
        [InlineData(1337)]
        [InlineData(11155111)]
        public void ShouldHandleVariousChainIds(ulong chainId)
        {
            var transaction = new Transaction2930(
                new BigInteger(chainId),
                0,
                20000000000,
                21000,
                "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836",
                1000000000000000000,
                null,
                null);

            var signer = new TypeTransactionSigner<Transaction2930>();
            signer.SignTransaction(TestPrivateKey, transaction);

            var decoded = Transaction2930Encoder.Current.Decode(transaction.GetRLPEncoded());

            Assert.Equal(chainId, (ulong)decoded.ChainId);
            Assert.True(decoded.VerifyTransaction());
        }

        [Fact]
        public void ShouldHandleLargeAccessList()
        {
            var accessList = new List<AccessListItem>();
            for (int i = 0; i < 10; i++)
            {
                var storageKeys = new List<byte[]>();
                for (int j = 0; j < 5; j++)
                {
                    var key = new byte[32];
                    key[0] = (byte)i;
                    key[1] = (byte)j;
                    storageKeys.Add(key);
                }
                accessList.Add(new AccessListItem(
                    $"0x{i:D2}7306090abaB3A6e1400e9345bC60c78a8BEf57",
                    storageKeys));
            }

            var transaction = new Transaction2930(
                1,
                0,
                20000000000,
                500000,
                "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836",
                0,
                null,
                accessList);

            var signer = new TypeTransactionSigner<Transaction2930>();
            signer.SignTransaction(TestPrivateKey, transaction);

            var decoded = Transaction2930Encoder.Current.Decode(transaction.GetRLPEncoded());

            Assert.Equal(10, decoded.AccessList.Count);
            for (int i = 0; i < 10; i++)
            {
                Assert.Equal(5, decoded.AccessList[i].StorageKeys.Count);
            }
        }

        [Fact]
        public void ShouldHandleContractCreation()
        {
            var deploymentBytecode = "0x608060405234801561001057600080fd5b50610150806100206000396000f3";

            var transaction = new Transaction2930(
                1,
                0,
                20000000000,
                500000,
                null,
                0,
                deploymentBytecode,
                null);

            var signer = new TypeTransactionSigner<Transaction2930>();
            signer.SignTransaction(TestPrivateKey, transaction);

            var decoded = Transaction2930Encoder.Current.Decode(transaction.GetRLPEncoded());

            Assert.Null(decoded.ReceiverAddress);
            Assert.Equal(deploymentBytecode.ToLower(), decoded.Data.ToLower());
            Assert.True(decoded.VerifyTransaction());
        }
    }
}
