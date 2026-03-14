using Nethereum.Model;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Signer;
using Nethereum.XUnitEthereumClients;
using Nethereum.Documentation;
using System.Numerics;
using System.Collections.Generic;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class ModelDocExampleTests
    {
        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "transaction-models", "Create a legacy transaction")]
        public void LegacyTransaction_CreateWithProperties()
        {
            var to = "0x13978aee95f38490e9769C39B2773Ed763d9cd5F";
            var amount = BigInteger.Parse("10000000000000000");
            var nonce = BigInteger.Zero;
            var gasPrice = BigInteger.Parse("1000000000000");
            var gasLimit = BigInteger.Parse("10000");

            var tx = new LegacyTransaction(to, amount, nonce, gasPrice, gasLimit);

            Assert.Equal(to.HexToByteArray(), tx.ReceiveAddress);
            Assert.Equal(amount.ToBytesForRLPEncoding(), tx.Value);
            Assert.Equal(nonce.ToBytesForRLPEncoding(), tx.Nonce);
            Assert.Equal(gasPrice.ToBytesForRLPEncoding(), tx.GasPrice);
            Assert.Equal(gasLimit.ToBytesForRLPEncoding(), tx.GasLimit);
            Assert.Equal(TransactionType.LegacyTransaction, tx.TransactionType);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "transaction-models", "Decode signed transaction from RLP")]
        public void LegacyTransaction_DecodeFromRLP()
        {
            var rlpHex = "f86b8085e8d4a510008227109413978aee95f38490e9769c39b2773ed763d9cd5f872386f26fc10000801ba0eab47c1a49bf2fe5d40e01d313900e19ca485867d462fe06e139e3a536c6d4f4a014a569d327dcda4b29f74f93c0e9729d2f49ad726e703f9cd90dbb0fbf6649f1";
            var rlpBytes = rlpHex.HexToByteArray();

            var tx = new LegacyTransaction(rlpBytes);

            Assert.Equal(new byte[] { 0x00 }, tx.Nonce);
            Assert.Equal(BigInteger.Parse("1000000000000").ToBytesForRLPEncoding(), tx.GasPrice);
            Assert.Equal(BigInteger.Parse("10000000000000000").ToBytesForRLPEncoding(), tx.Value);
            Assert.Equal("0x13978aee95f38490e9769c39b2773ed763d9cd5f".HexToByteArray(), tx.ReceiveAddress);
            Assert.NotNull(tx.Signature);
            Assert.Equal(new byte[] { 0x1b }, tx.Signature.V);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "transaction-models", "TransactionFactory detects type")]
        public void TransactionFactory_DetectsLegacyType()
        {
            var rlpHex = "f86b8085e8d4a510008227109413978aee95f38490e9769c39b2773ed763d9cd5f872386f26fc10000801ba0eab47c1a49bf2fe5d40e01d313900e19ca485867d462fe06e139e3a536c6d4f4a014a569d327dcda4b29f74f93c0e9729d2f49ad726e703f9cd90dbb0fbf6649f1";
            var encoded = rlpHex.HexToByteArray();

            var decoded = TransactionFactory.CreateTransaction(encoded);

            Assert.Equal(TransactionType.LegacyTransaction, decoded.TransactionType);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "transaction-models", "Create EIP-1559 transaction")]
        public void Transaction1559_CreateAndEncode()
        {
            var chainId = new BigInteger(1);
            var nonce = new BigInteger(0);
            var maxPriorityFeePerGas = new BigInteger(2000000000);
            var maxFeePerGas = new BigInteger(100000000000);
            var gasLimit = new BigInteger(21000);
            var receiverAddress = "0x13978aee95f38490e9769C39B2773Ed763d9cd5F";
            var amount = new BigInteger(10000000000000000);
            var data = "";

            var tx = new Transaction1559(chainId, nonce, maxPriorityFeePerGas, maxFeePerGas, gasLimit, receiverAddress, amount, data, null);

            Assert.Equal(TransactionType.EIP1559, tx.TransactionType);
            Assert.Equal(chainId, tx.ChainId);
            Assert.Equal(nonce, tx.Nonce);
            Assert.Equal(maxPriorityFeePerGas, tx.MaxPriorityFeePerGas);
            Assert.Equal(maxFeePerGas, tx.MaxFeePerGas);
            Assert.Equal(gasLimit, tx.GasLimit);
            Assert.Equal(amount, tx.Amount);

            var rawEncoded = tx.GetRLPEncodedRaw();
            Assert.NotNull(rawEncoded);
            Assert.True(rawEncoded.Length > 0);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "transaction-models", "Chain enum values")]
        public void Chain_EnumValues()
        {
            Assert.Equal(1, (int)Chain.MainNet);
            Assert.Equal(11155111, (int)Chain.Sepolia);
            Assert.Equal(137, (int)Chain.Polygon);
            Assert.Equal(8453, (int)Chain.Base);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "transaction-models", "Account state model")]
        public void Account_CreateWithProperties()
        {
            var account = new Account
            {
                Nonce = 5,
                Balance = BigInteger.Parse("1000000000000000000"),
                StateRoot = new byte[32],
                CodeHash = new byte[32]
            };

            Assert.Equal(new BigInteger(5), account.Nonce);
            Assert.Equal(BigInteger.Parse("1000000000000000000"), account.Balance);
            Assert.Equal(32, account.StateRoot.Length);
            Assert.Equal(32, account.CodeHash.Length);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "transaction-models", "Log.Create event log")]
        public void Log_CreateWithDataAndTopics()
        {
            var data = new byte[] { 0x01, 0x02, 0x03 };
            var address = "0x13978aee95f38490e9769C39B2773Ed763d9cd5F";
            var topic1 = new byte[32];
            topic1[31] = 0x01;
            var topic2 = new byte[32];
            topic2[31] = 0x02;

            var log = Log.Create(data, address, topic1, topic2);

            Assert.Equal(data, log.Data);
            Assert.Equal(address, log.Address);
            Assert.Equal(2, log.Topics.Count);
            Assert.Equal(topic1, log.Topics[0]);
            Assert.Equal(topic2, log.Topics[1]);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "transaction-models", "Block header properties")]
        public void BlockHeader_SetAndVerifyProperties()
        {
            var blockHeader = new BlockHeader
            {
                BlockNumber = 1000,
                Difficulty = 131072,
                GasLimit = 8000000,
                GasUsed = 21000,
                Timestamp = 1638300000,
                Coinbase = "0x0000000000000000000000000000000000000000",
                ParentHash = new byte[32],
                StateRoot = new byte[32],
                BaseFee = 1000000000
            };

            Assert.Equal(new BigInteger(1000), blockHeader.BlockNumber);
            Assert.Equal(new BigInteger(131072), blockHeader.Difficulty);
            Assert.Equal(8000000, blockHeader.GasLimit);
            Assert.Equal(21000, blockHeader.GasUsed);
            Assert.Equal(1638300000, blockHeader.Timestamp);
            Assert.Equal("0x0000000000000000000000000000000000000000", blockHeader.Coinbase);
            Assert.Equal(32, blockHeader.ParentHash.Length);
            Assert.Equal(32, blockHeader.StateRoot.Length);
            Assert.Equal(new BigInteger(1000000000), blockHeader.BaseFee);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "transaction-models", "AccessListItem creation")]
        public void AccessListItem_CreateWithAddressAndStorageKeys()
        {
            var address = "0x13978aee95f38490e9769C39B2773Ed763d9cd5F";
            var storageKey1 = new byte[32];
            storageKey1[31] = 0x01;
            var storageKey2 = new byte[32];
            storageKey2[31] = 0x02;

            var accessListItem = new AccessListItem(address, new List<byte[]> { storageKey1, storageKey2 });

            Assert.Equal(address, accessListItem.Address);
            Assert.Equal(2, accessListItem.StorageKeys.Count);
            Assert.Equal(storageKey1, accessListItem.StorageKeys[0]);
            Assert.Equal(storageKey2, accessListItem.StorageKeys[1]);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "transaction-models", "Create EIP-2930 access list transaction")]
        public void Transaction2930_CreateWithAccessList()
        {
            var chainId = new BigInteger(1);
            var nonce = new BigInteger(0);
            var gasPrice = new BigInteger(20000000000);
            var gasLimit = new BigInteger(21000);
            var receiverAddress = "0x13978aee95f38490e9769C39B2773Ed763d9cd5F";
            var amount = new BigInteger(10000000000000000);

            var storageKey = new byte[32];
            storageKey[31] = 0x01;
            var accessList = new List<AccessListItem>
            {
                new AccessListItem(receiverAddress, new List<byte[]> { storageKey })
            };

            var tx = new Transaction2930(chainId, nonce, gasPrice, gasLimit, receiverAddress, amount, null, accessList);

            Assert.Equal(TransactionType.LegacyEIP2930, tx.TransactionType);
            Assert.Equal(chainId, tx.ChainId);
            Assert.Equal(nonce, tx.Nonce);
            Assert.Equal(gasPrice, tx.GasPrice);
            Assert.Equal(gasLimit, tx.GasLimit);
            Assert.Equal(amount, tx.Amount);
            Assert.Single(tx.AccessList);
            Assert.Equal(receiverAddress, tx.AccessList[0].Address);

            var rawEncoded = tx.GetRLPEncodedRaw();
            Assert.NotNull(rawEncoded);
            Assert.True(rawEncoded.Length > 0);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "transaction-models", "Create EIP-7702 authorization transaction")]
        public void Transaction7702_CreateWithAuthorisation()
        {
            var chainId = new BigInteger(1);
            var nonce = new BigInteger(0);
            var maxPriorityFeePerGas = new BigInteger(2000000000);
            var maxFeePerGas = new BigInteger(100000000000);
            var gasLimit = new BigInteger(100000);
            var receiverAddress = "0x13978aee95f38490e9769C39B2773Ed763d9cd5F";
            var amount = BigInteger.Zero;

            var authChainId = new BigInteger(1);
            var authAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var authNonce = new BigInteger(0);

            var authorisation = new Authorisation7702 { ChainId = authChainId, Address = authAddress, Nonce = authNonce };

            var ecKey = new EthECKey("0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7");
            var authSigner = new Authorisation7702Signer();
            var signedAuth = authSigner.SignAuthorisation(ecKey, authorisation);

            var tx = new Transaction7702(
                chainId, nonce, maxPriorityFeePerGas, maxFeePerGas,
                gasLimit, receiverAddress, amount, null,
                new List<AccessListItem>(), new List<Authorisation7702Signed> { signedAuth });

            Assert.Equal(TransactionType.EIP7702, tx.TransactionType);
            Assert.Equal(chainId, tx.ChainId);
            Assert.Single(tx.AuthorisationList);
            Assert.Equal(authAddress, tx.AuthorisationList[0].Address);
            Assert.NotNull(tx.AuthorisationList[0].R);
            Assert.NotNull(tx.AuthorisationList[0].S);

            var rawEncoded = tx.GetRLPEncodedRaw();
            Assert.NotNull(rawEncoded);
            Assert.True(rawEncoded.Length > 0);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "transaction-models", "TransactionFactory detects EIP-1559 and EIP-2930")]
        public void TransactionFactory_Detects1559And2930()
        {
            var chainId = new BigInteger(1);
            var nonce = new BigInteger(0);

            var tx1559 = new Transaction1559(chainId, nonce, 2000000000, 100000000000, 21000,
                "0x13978aee95f38490e9769C39B2773Ed763d9cd5F", 10000000000000000, null, null);

            var signer1559 = new Transaction1559Signer();
            var signedHex1559 = signer1559.SignTransaction(
                "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7", tx1559);
            var decoded1559 = TransactionFactory.CreateTransaction(signedHex1559);
            Assert.Equal(TransactionType.EIP1559, decoded1559.TransactionType);

            var tx2930 = new Transaction2930(chainId, nonce, 20000000000, 21000,
                "0x13978aee95f38490e9769C39B2773Ed763d9cd5F", 10000000000000000, null, new List<AccessListItem>());

            var signer2930 = new TypeTransactionSigner<Transaction2930>();
            var signedHex2930 = signer2930.SignTransaction(
                "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7", tx2930);
            var decoded2930 = TransactionFactory.CreateTransaction(signedHex2930);
            Assert.Equal(TransactionType.LegacyEIP2930, decoded2930.TransactionType);
        }
    }
}
