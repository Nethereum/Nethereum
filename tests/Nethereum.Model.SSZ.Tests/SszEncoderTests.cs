using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.SSZ;
using Nethereum.Ssz;
using Xunit;

namespace Nethereum.Model.SSZ.Tests
{
    public class SszLogEncoderTests
    {
        [Fact]
        public void EmptyLog_Deterministic()
        {
            var log = new Log { Address = "0x0000000000000000000000000000000000000000" };
            var r1 = SszLogEncoder.Current.HashTreeRoot(log);
            var r2 = SszLogEncoder.Current.HashTreeRoot(log);
            Assert.Equal(r1, r2);
            Assert.Equal(32, r1.Length);
        }

        [Fact]
        public void DifferentAddress_DifferentRoot()
        {
            var log1 = new Log { Address = "0xdead000000000000000000000000000000000001" };
            var log2 = new Log { Address = "0xbeef000000000000000000000000000000000002" };
            Assert.NotEqual(
                SszLogEncoder.Current.HashTreeRoot(log1),
                SszLogEncoder.Current.HashTreeRoot(log2));
        }

        [Fact]
        public void WithTopics_DifferentFromEmpty()
        {
            var log1 = new Log { Address = "0xdead000000000000000000000000000000000001" };
            var log2 = new Log
            {
                Address = "0xdead000000000000000000000000000000000001",
                Topics = new List<byte[]>
                {
                    "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef".HexToByteArray()
                }
            };
            Assert.NotEqual(
                SszLogEncoder.Current.HashTreeRoot(log1),
                SszLogEncoder.Current.HashTreeRoot(log2));
        }

        [Fact]
        public void WithData_DifferentFromEmpty()
        {
            var log1 = new Log { Address = "0xdead000000000000000000000000000000000001" };
            var log2 = new Log
            {
                Address = "0xdead000000000000000000000000000000000001",
                Data = "0x0000000000000000000000000000000000000000000000000de0b6b3a7640000".HexToByteArray()
            };
            Assert.NotEqual(
                SszLogEncoder.Current.HashTreeRoot(log1),
                SszLogEncoder.Current.HashTreeRoot(log2));
        }
    }

    public class SszAccessListEncoderTests
    {
        [Fact]
        public void EmptyAccessList_Deterministic()
        {
            var r1 = SszAccessListEncoder.Current.HashTreeRootAccessList(new List<AccessListItem>());
            var r2 = SszAccessListEncoder.Current.HashTreeRootAccessList(new List<AccessListItem>());
            Assert.Equal(r1, r2);
        }

        [Fact]
        public void WithStorageKeys_DifferentFromEmpty()
        {
            var empty = SszAccessListEncoder.Current.HashTreeRootAccessList(new List<AccessListItem>());
            var withItem = SszAccessListEncoder.Current.HashTreeRootAccessList(new List<AccessListItem>
            {
                new AccessListItem("0xdead000000000000000000000000000000000001",
                    new List<byte[]>
                    {
                        "0x0000000000000000000000000000000000000000000000000000000000000001".HexToByteArray()
                    })
            });
            Assert.NotEqual(empty, withItem);
        }
    }

    public class SszReceiptEncoderTests
    {
        private const string TestFrom = "0xdead000000000000000000000000000000000001";
        private const string TestContract = "0x1234567890abcdef1234567890abcdef12345678";

        [Fact]
        public void BasicReceipt_Deterministic()
        {
            var r1 = SszReceiptEncoder.Current.HashTreeRootBasicReceipt(TestFrom, 21000, new List<Log>(), true);
            var r2 = SszReceiptEncoder.Current.HashTreeRootBasicReceipt(TestFrom, 21000, new List<Log>(), true);
            Assert.Equal(r1, r2);
        }

        [Fact]
        public void BasicReceipt_StatusAffectsRoot()
        {
            var success = SszReceiptEncoder.Current.HashTreeRootBasicReceipt(TestFrom, 21000, new List<Log>(), true);
            var failure = SszReceiptEncoder.Current.HashTreeRootBasicReceipt(TestFrom, 21000, new List<Log>(), false);
            Assert.NotEqual(success, failure);
        }

        [Fact]
        public void BasicReceipt_GasUsedAffectsRoot()
        {
            var r1 = SszReceiptEncoder.Current.HashTreeRootBasicReceipt(TestFrom, 21000, new List<Log>(), true);
            var r2 = SszReceiptEncoder.Current.HashTreeRootBasicReceipt(TestFrom, 42000, new List<Log>(), true);
            Assert.NotEqual(r1, r2);
        }

        [Fact]
        public void BasicReceipt_WithLogs_DifferentFromEmpty()
        {
            var log = Log.Create(new byte[32], TestFrom,
                "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef".HexToByteArray());

            var empty = SszReceiptEncoder.Current.HashTreeRootBasicReceipt(TestFrom, 21000, new List<Log>(), true);
            var withLog = SszReceiptEncoder.Current.HashTreeRootBasicReceipt(TestFrom, 21000, new List<Log> { log }, true);
            Assert.NotEqual(empty, withLog);
        }

        [Fact]
        public void CreateReceipt_DiffersFromBasic()
        {
            var basic = SszReceiptEncoder.Current.HashTreeRootBasicReceipt(TestFrom, 21000, new List<Log>(), true);
            var create = SszReceiptEncoder.Current.HashTreeRootCreateReceipt(TestFrom, 21000, TestContract, new List<Log>(), true);
            Assert.NotEqual(basic, create);
        }

        [Fact]
        public void CreateReceipt_ContractAddressAffectsRoot()
        {
            var r1 = SszReceiptEncoder.Current.HashTreeRootCreateReceipt(TestFrom, 100000, TestContract, new List<Log>(), true);
            var r2 = SszReceiptEncoder.Current.HashTreeRootCreateReceipt(TestFrom, 100000, TestFrom, new List<Log>(), true);
            Assert.NotEqual(r1, r2);
        }

        [Fact]
        public void SetCodeReceipt_WithAuthorities_DifferentFromEmpty()
        {
            var empty = SszReceiptEncoder.Current.HashTreeRootSetCodeReceipt(TestFrom, 50000, new List<Log>(), true, new List<string>());
            var withAuth = SszReceiptEncoder.Current.HashTreeRootSetCodeReceipt(TestFrom, 50000, new List<Log>(), true, new List<string> { TestContract });
            Assert.NotEqual(empty, withAuth);
        }

        [Fact]
        public void AllReceiptTypes_UniqueRoots()
        {
            var basic = SszReceiptEncoder.Current.HashTreeRootBasicReceipt(TestFrom, 21000, new List<Log>(), true);
            var create = SszReceiptEncoder.Current.HashTreeRootCreateReceipt(TestFrom, 21000, TestContract, new List<Log>(), true);
            var setCode = SszReceiptEncoder.Current.HashTreeRootSetCodeReceipt(TestFrom, 21000, new List<Log>(), true, new List<string>());

            Assert.NotEqual(basic, create);
            Assert.NotEqual(basic, setCode);
            Assert.NotEqual(create, setCode);
        }

        [Fact]
        public void Receipt_UnionWrapper_DifferentSelectors_DifferentRoots()
        {
            var dataRoot = new byte[32];
            dataRoot[0] = 0x42;

            var r1 = SszReceiptEncoder.Current.HashTreeRootReceipt(SszReceiptEncoder.SelectorBasicReceipt, dataRoot);
            var r2 = SszReceiptEncoder.Current.HashTreeRootReceipt(SszReceiptEncoder.SelectorCreateReceipt, dataRoot);
            Assert.NotEqual(r1, r2);
        }
    }

    public class SszTransactionEncoderTests
    {
        private static Transaction1559 MakeEip1559Tx(string to = "0xbeef000000000000000000000000000000000001")
        {
            return new Transaction1559(
                chainId: 1,
                nonce: 42,
                maxPriorityFeePerGas: 2000000000,
                maxFeePerGas: 30000000000,
                gasLimit: 21000,
                receiverAddress: to,
                amount: 1000000000000000000,
                data: "",
                accessList: new List<AccessListItem>());
        }

        [Fact]
        public void Transaction1559_Deterministic()
        {
            var tx = MakeEip1559Tx();
            var r1 = SszTransactionEncoder.Current.HashTreeRootTransaction1559(tx);
            var r2 = SszTransactionEncoder.Current.HashTreeRootTransaction1559(tx);
            Assert.Equal(r1, r2);
            Assert.Equal(32, r1.Length);
        }

        [Fact]
        public void Transaction1559_DifferentNonce_DifferentRoot()
        {
            var tx1 = new Transaction1559(1, 0, 2000000000, 30000000000, 21000,
                "0xbeef000000000000000000000000000000000001", 0, "", new List<AccessListItem>());
            var tx2 = new Transaction1559(1, 1, 2000000000, 30000000000, 21000,
                "0xbeef000000000000000000000000000000000001", 0, "", new List<AccessListItem>());
            Assert.NotEqual(
                SszTransactionEncoder.Current.HashTreeRootTransaction1559(tx1),
                SszTransactionEncoder.Current.HashTreeRootTransaction1559(tx2));
        }

        [Fact]
        public void Transaction1559_CreateVsBasic_DifferentActiveFields()
        {
            var basic = MakeEip1559Tx("0xbeef000000000000000000000000000000000001");
            var create = MakeEip1559Tx(null); // no receiver = contract creation
            Assert.NotEqual(
                SszTransactionEncoder.Current.HashTreeRootTransaction1559(basic),
                SszTransactionEncoder.Current.HashTreeRootTransaction1559(create));
        }

        [Fact]
        public void Transaction1559_WithAccessList_DifferentRoot()
        {
            var tx1 = MakeEip1559Tx();
            var tx2 = new Transaction1559(1, 42, 2000000000, 30000000000, 21000,
                "0xbeef000000000000000000000000000000000001", 1000000000000000000, "",
                new List<AccessListItem>
                {
                    new AccessListItem("0xdead000000000000000000000000000000000001",
                        new List<byte[]> { new byte[32] })
                });
            Assert.NotEqual(
                SszTransactionEncoder.Current.HashTreeRootTransaction1559(tx1),
                SszTransactionEncoder.Current.HashTreeRootTransaction1559(tx2));
        }

        [Fact]
        public void Transaction7702_Deterministic()
        {
            var tx = new Transaction7702(1, 1, 2000000000, 30000000000, 50000,
                "0xbeef000000000000000000000000000000000001", 0, "",
                new List<AccessListItem>(), new List<Authorisation7702Signed>());
            var r1 = SszTransactionEncoder.Current.HashTreeRootTransaction7702(tx);
            var r2 = SszTransactionEncoder.Current.HashTreeRootTransaction7702(tx);
            Assert.Equal(r1, r2);
        }

        [Fact]
        public void Transaction7702_WithAuthorisations_DifferentRoot()
        {
            var tx1 = new Transaction7702(1, 1, 2000000000, 30000000000, 50000,
                "0xbeef000000000000000000000000000000000001", 0, "",
                new List<AccessListItem>(), new List<Authorisation7702Signed>());
            var tx2 = new Transaction7702(1, 1, 2000000000, 30000000000, 50000,
                "0xbeef000000000000000000000000000000000001", 0, "",
                new List<AccessListItem>(), new List<Authorisation7702Signed>
                {
                    new Authorisation7702Signed(1, "0xdead000000000000000000000000000000000001",
                        0, new byte[32], new byte[32], new byte[] { 0 })
                });
            Assert.NotEqual(
                SszTransactionEncoder.Current.HashTreeRootTransaction7702(tx1),
                SszTransactionEncoder.Current.HashTreeRootTransaction7702(tx2));
        }

        [Fact]
        public void TransactionsRoot_OrderMatters()
        {
            var tx1Root = SszTransactionEncoder.Current.HashTreeRootTransaction1559(
                new Transaction1559(1, 0, 2000000000, 30000000000, 21000,
                    "0xbeef000000000000000000000000000000000001", 0, "", new List<AccessListItem>()));
            var tx2Root = SszTransactionEncoder.Current.HashTreeRootTransaction1559(
                new Transaction1559(1, 1, 2000000000, 30000000000, 21000,
                    "0xbeef000000000000000000000000000000000001", 0, "", new List<AccessListItem>()));

            var forward = SszTransactionEncoder.Current.HashTreeRootTransactionsRoot(new List<byte[]> { tx1Root, tx2Root });
            var reverse = SszTransactionEncoder.Current.HashTreeRootTransactionsRoot(new List<byte[]> { tx2Root, tx1Root });
            Assert.NotEqual(forward, reverse);
        }
    }

    public class SszBlockHeaderEncoderTests
    {
        [Fact]
        public void EmptyHeader_Deterministic()
        {
            var header = new BlockHeader
            {
                ParentHash = new byte[32],
                StateRoot = new byte[32],
                TransactionsHash = new byte[32],
                ReceiptHash = new byte[32],
                MixHash = new byte[32],
                Coinbase = "0x0000000000000000000000000000000000000000"
            };
            var r1 = SszBlockHeaderEncoder.Current.HashTreeRoot(header);
            var r2 = SszBlockHeaderEncoder.Current.HashTreeRoot(header);
            Assert.Equal(r1, r2);
            Assert.Equal(32, r1.Length);
        }

        [Fact]
        public void DifferentBlockNumber_DifferentRoot()
        {
            var h1 = MakeHeader(100);
            var h2 = MakeHeader(101);
            Assert.NotEqual(
                SszBlockHeaderEncoder.Current.HashTreeRoot(h1),
                SszBlockHeaderEncoder.Current.HashTreeRoot(h2));
        }

        [Fact]
        public void BlockHash_EqualsHashTreeRoot()
        {
            var header = MakeHeader(12345);
            Assert.Equal(
                SszBlockHeaderEncoder.Current.HashTreeRoot(header),
                SszBlockHeaderEncoder.Current.BlockHash(header));
        }

        [Fact]
        public void DifferentTimestamp_DifferentRoot()
        {
            var h1 = MakeHeader();
            h1.Timestamp = 1700000000;
            var h2 = MakeHeader();
            h2.Timestamp = 1700000012;
            Assert.NotEqual(
                SszBlockHeaderEncoder.Current.HashTreeRoot(h1),
                SszBlockHeaderEncoder.Current.HashTreeRoot(h2));
        }

        [Fact]
        public void FullBlock_EndToEnd()
        {
            var tx = new Transaction1559(1, 42, 2000000000, 30000000000, 21000,
                "0xbeef000000000000000000000000000000000001", 1000000000000000000, "",
                new List<AccessListItem>());

            var txRoot = SszTransactionEncoder.Current.HashTreeRootTransaction1559(tx);
            var txsRoot = SszTransactionEncoder.Current.HashTreeRootTransactionsRoot(new List<byte[]> { txRoot });

            var receiptRoot = SszReceiptEncoder.Current.HashTreeRootBasicReceipt(
                "0xdead000000000000000000000000000000000001", 21000, new List<Log>(), true);
            var receiptsRoot = SszReceiptEncoder.Current.HashTreeRootReceiptsRoot(
                new List<byte[]> { SszReceiptEncoder.Current.HashTreeRootReceipt(SszReceiptEncoder.SelectorBasicReceipt, receiptRoot) });

            var header = MakeHeader(12345678);
            header.TransactionsHash = txsRoot;
            header.ReceiptHash = receiptsRoot;
            header.Coinbase = "0xdead000000000000000000000000000000000001";
            header.BaseFee = 30000000000;
            header.GasLimit = 30000000;
            header.GasUsed = 21000;
            header.Timestamp = 1700000000;

            var blockHash = SszBlockHeaderEncoder.Current.BlockHash(header);
            Assert.Equal(32, blockHash.Length);
            Assert.Equal(blockHash, SszBlockHeaderEncoder.Current.BlockHash(header));
        }

        private static BlockHeader MakeHeader(long number = 0)
        {
            return new BlockHeader
            {
                ParentHash = new byte[32],
                StateRoot = new byte[32],
                TransactionsHash = new byte[32],
                ReceiptHash = new byte[32],
                MixHash = new byte[32],
                Coinbase = "0x0000000000000000000000000000000000000000",
                BlockNumber = number
            };
        }
    }
}
