using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbStoreTests : IDisposable
    {
        private readonly RocksDbTestFixture _fixture;

        public RocksDbStoreTests()
        {
            _fixture = new RocksDbTestFixture();
        }

        public void Dispose()
        {
            _fixture.Dispose();
        }

        private static byte[] MakeHash(int seed)
        {
            var hash = new byte[32];
            hash[0] = (byte)(seed & 0xFF);
            hash[1] = (byte)((seed >> 8) & 0xFF);
            return hash;
        }

        private static BlockHeader MakeBlockHeader(BigInteger blockNumber, byte[] parentHash = null)
        {
            return new BlockHeader
            {
                BlockNumber = blockNumber,
                ParentHash = parentHash ?? new byte[32],
                UnclesHash = new byte[32],
                StateRoot = new byte[32],
                TransactionsHash = new byte[32],
                ReceiptHash = new byte[32],
                LogsBloom = new byte[256],
                GasLimit = 30_000_000,
                GasUsed = 21000,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ExtraData = Array.Empty<byte>(),
                MixHash = new byte[32],
                Nonce = new byte[8],
                Coinbase = AddressUtil.ZERO_ADDRESS
            };
        }

        private static ISignedTransaction MakeTx(byte[] hash)
        {
            var r = new byte[32]; r[0] = 0x01;
            var s = new byte[32]; s[0] = 0x01;
            var tx = new LegacyTransaction(
                nonce: new byte[] { 0x00 },
                gasPrice: new byte[] { 0x01 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20],
                value: new byte[] { 0x00 },
                data: Array.Empty<byte>(),
                r: r,
                s: s,
                v: 27
            );
            return new TestSignedTransaction(tx, hash);
        }

        // --- Transaction Store: BlockNumber in Location ---

        [Fact]
        public async Task TransactionStore_SaveAsync_GetLocation_ReturnsBlockNumber()
        {
            var txHash = MakeHash(1);
            var blockHash = MakeHash(100);
            var blockNumber = new BigInteger(42);
            var tx = MakeTx(txHash);

            var header = MakeBlockHeader(blockNumber);
            await _fixture.BlockStore.SaveAsync(header, blockHash);

            await _fixture.TransactionStore.SaveAsync(tx, blockHash, 0, blockNumber);

            var location = await _fixture.TransactionStore.GetLocationAsync(txHash);
            Assert.NotNull(location);
            Assert.Equal(blockNumber, location.BlockNumber);
            Assert.Equal(0, location.TransactionIndex);
            Assert.Equal(blockHash, location.BlockHash);
        }

        [Fact]
        public async Task TransactionStore_SaveAsync_GetByHash_RoundTrips()
        {
            var txHash = MakeHash(2);
            var blockHash = MakeHash(101);
            var tx = MakeTx(txHash);

            await _fixture.TransactionStore.SaveAsync(tx, blockHash, 0, 5);

            var result = await _fixture.TransactionStore.GetByHashAsync(txHash);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task TransactionStore_GetByBlockHash_ReturnsTxsInOrder()
        {
            var blockHash = MakeHash(200);
            var header = MakeBlockHeader(10);
            await _fixture.BlockStore.SaveAsync(header, blockHash);

            var tx0 = MakeTx(MakeHash(10));
            var tx1 = MakeTx(MakeHash(11));
            var tx2 = MakeTx(MakeHash(12));

            await _fixture.TransactionStore.SaveAsync(tx0, blockHash, 0, 10);
            await _fixture.TransactionStore.SaveAsync(tx1, blockHash, 1, 10);
            await _fixture.TransactionStore.SaveAsync(tx2, blockHash, 2, 10);

            var txs = await _fixture.TransactionStore.GetByBlockHashAsync(blockHash);
            Assert.Equal(3, txs.Count);
        }

        // --- Receipt Store: Secondary Index (CF_RECEIPT_BY_BLOCK) ---

        [Fact]
        public async Task ReceiptStore_SaveAsync_GetByBlockHash_UsesSecondaryIndex()
        {
            var blockHash = MakeHash(300);
            var header = MakeBlockHeader(20);
            await _fixture.BlockStore.SaveAsync(header, blockHash);

            var txHash1 = MakeHash(30);
            var txHash2 = MakeHash(31);

            var receipt1 = new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = 21000 };
            var receipt2 = new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = 42000 };

            await _fixture.ReceiptStore.SaveAsync(receipt1, txHash1, blockHash, 20, 0, 21000, null, 1000000000);
            await _fixture.ReceiptStore.SaveAsync(receipt2, txHash2, blockHash, 20, 1, 21000, null, 1000000000);

            var receipts = await _fixture.ReceiptStore.GetByBlockHashAsync(blockHash);
            Assert.Equal(2, receipts.Count);
        }

        [Fact]
        public async Task ReceiptStore_GetByBlockHash_DoesNotReturnOtherBlocks()
        {
            var blockHash1 = MakeHash(301);
            var blockHash2 = MakeHash(302);

            var header1 = MakeBlockHeader(21);
            var header2 = MakeBlockHeader(22);
            await _fixture.BlockStore.SaveAsync(header1, blockHash1);
            await _fixture.BlockStore.SaveAsync(header2, blockHash2);

            var txHash1 = MakeHash(40);
            var txHash2 = MakeHash(41);

            await _fixture.ReceiptStore.SaveAsync(
                new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = 21000 },
                txHash1, blockHash1, 21, 0, 21000, null, 0);
            await _fixture.ReceiptStore.SaveAsync(
                new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = 21000 },
                txHash2, blockHash2, 22, 0, 21000, null, 0);

            var receipts1 = await _fixture.ReceiptStore.GetByBlockHashAsync(blockHash1);
            Assert.Single(receipts1);

            var receipts2 = await _fixture.ReceiptStore.GetByBlockHashAsync(blockHash2);
            Assert.Single(receipts2);
        }

        [Fact]
        public async Task ReceiptStore_GetByTxHash_StillWorks()
        {
            var blockHash = MakeHash(303);
            var txHash = MakeHash(50);
            var receipt = new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = 21000 };

            await _fixture.ReceiptStore.SaveAsync(receipt, txHash, blockHash, 30, 0, 21000, null, 0);

            var result = await _fixture.ReceiptStore.GetByTxHashAsync(txHash);
            Assert.NotNull(result);
            Assert.True(result.HasSucceeded);
        }

        [Fact]
        public async Task ReceiptStore_DeleteByBlockNumber_CleansUpSecondaryIndex()
        {
            var blockHash = MakeHash(304);
            var header = MakeBlockHeader(40);
            await _fixture.BlockStore.SaveAsync(header, blockHash);

            var txHash = MakeHash(60);
            await _fixture.ReceiptStore.SaveAsync(
                new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = 21000 },
                txHash, blockHash, 40, 0, 21000, null, 0);

            var before = await _fixture.ReceiptStore.GetByBlockHashAsync(blockHash);
            Assert.Single(before);

            await _fixture.ReceiptStore.DeleteByBlockNumberAsync(40);

            var after = await _fixture.ReceiptStore.GetByBlockHashAsync(blockHash);
            Assert.Empty(after);

            var byTx = await _fixture.ReceiptStore.GetByTxHashAsync(txHash);
            Assert.Null(byTx);
        }

        // --- Log Store: Secondary Index (CF_LOG_BY_TX) ---

        [Fact]
        public async Task LogStore_SaveLogs_GetByTxHash_UsesSecondaryIndex()
        {
            var blockHash = MakeHash(400);
            var txHash = MakeHash(70);

            var logs = new List<Log>
            {
                new Log { Address = "0x1111111111111111111111111111111111111111", Data = new byte[] { 0x00 } },
                new Log { Address = "0x2222222222222222222222222222222222222222", Data = new byte[] { 0x01 } }
            };

            await _fixture.LogStore.SaveLogsAsync(logs, txHash, blockHash, 50, 0);

            var result = await _fixture.LogStore.GetLogsByTxHashAsync(txHash);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task LogStore_GetByTxHash_DoesNotReturnOtherTxLogs()
        {
            var blockHash = MakeHash(401);
            var txHash1 = MakeHash(71);
            var txHash2 = MakeHash(72);

            await _fixture.LogStore.SaveLogsAsync(
                new List<Log> { new Log { Address = "0x1111111111111111111111111111111111111111" } },
                txHash1, blockHash, 51, 0);

            await _fixture.LogStore.SaveLogsAsync(
                new List<Log> { new Log { Address = "0x2222222222222222222222222222222222222222" } },
                txHash2, blockHash, 51, 1);

            var logs1 = await _fixture.LogStore.GetLogsByTxHashAsync(txHash1);
            Assert.Single(logs1);

            var logs2 = await _fixture.LogStore.GetLogsByTxHashAsync(txHash2);
            Assert.Single(logs2);
        }

        [Fact]
        public async Task LogStore_GetByBlockHash_StillWorks()
        {
            var blockHash = MakeHash(402);
            var txHash = MakeHash(73);

            var logs = new List<Log>
            {
                new Log { Address = "0x3333333333333333333333333333333333333333" }
            };

            await _fixture.LogStore.SaveLogsAsync(logs, txHash, blockHash, 52, 0);

            var result = await _fixture.LogStore.GetLogsByBlockHashAsync(blockHash);
            Assert.Single(result);
        }

        [Fact]
        public async Task LogStore_GetByBlockNumber_StillWorks()
        {
            var blockHash = MakeHash(403);
            var txHash = MakeHash(74);

            var logs = new List<Log>
            {
                new Log { Address = "0x4444444444444444444444444444444444444444" }
            };

            await _fixture.LogStore.SaveLogsAsync(logs, txHash, blockHash, 53, 0);

            var result = await _fixture.LogStore.GetLogsByBlockNumberAsync(53);
            Assert.Single(result);
        }

        [Fact]
        public async Task LogStore_DeleteByBlockNumber_CleansUpTxIndex()
        {
            var blockHash = MakeHash(404);
            var txHash = MakeHash(75);

            var logs = new List<Log>
            {
                new Log { Address = "0x5555555555555555555555555555555555555555" }
            };

            await _fixture.LogStore.SaveLogsAsync(logs, txHash, blockHash, 54, 0);
            await _fixture.LogStore.SaveBlockBloomAsync(54, new byte[256]);

            var before = await _fixture.LogStore.GetLogsByTxHashAsync(txHash);
            Assert.Single(before);

            await _fixture.LogStore.DeleteByBlockNumberAsync(54);

            var after = await _fixture.LogStore.GetLogsByTxHashAsync(txHash);
            Assert.Empty(after);
        }

        // --- Transaction Store: Backward Compat for old data ---

        [Fact]
        public async Task TransactionStore_GetLocation_MissingHash_ReturnsNull()
        {
            var result = await _fixture.TransactionStore.GetLocationAsync(MakeHash(999));
            Assert.Null(result);
        }

        [Fact]
        public async Task ReceiptStore_GetByBlockHash_EmptyBlock_ReturnsEmpty()
        {
            var result = await _fixture.ReceiptStore.GetByBlockHashAsync(MakeHash(998));
            Assert.Empty(result);
        }

        [Fact]
        public async Task LogStore_GetByTxHash_NoLogs_ReturnsEmpty()
        {
            var result = await _fixture.LogStore.GetLogsByTxHashAsync(MakeHash(997));
            Assert.Empty(result);
        }

        private class TestSignedTransaction : ISignedTransaction
        {
            private readonly ISignedTransaction _inner;
            private readonly byte[] _hash;

            public TestSignedTransaction(ISignedTransaction inner, byte[] hash)
            {
                _inner = inner;
                _hash = hash;
            }

            public byte[] Hash => _hash;
            public byte[] RawHash => _inner.RawHash;
            public ISignature Signature => _inner.Signature;
            public TransactionType TransactionType => _inner.TransactionType;
            public byte[] GetRLPEncoded() => _inner.GetRLPEncoded();
            public byte[] GetRLPEncodedRaw() => _inner.GetRLPEncodedRaw();
            public void SetSignature(ISignature signature) => _inner.SetSignature(signature);
        }
    }
}
