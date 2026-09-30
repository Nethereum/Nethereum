using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class LogIndexCrossStoreParityTests : IDisposable
    {
        private readonly RocksDbTestFixture _fixture;
        private readonly HistoryBloomScanLogStore _l1;

        private const int BlockNumber = 500;
        private static readonly string AddrA = "0x" + new string('a', 40);
        private static readonly byte[] TopicValue = Enumerable.Repeat((byte)0x11, 32).ToArray();

        private static readonly (int TxIndex, int LogCount)[] Txs = { (0, 2), (1, 1), (256, 1), (257, 1) };

        public LogIndexCrossStoreParityTests()
        {
            _fixture = new RocksDbTestFixture();
            _l1 = new HistoryBloomScanLogStore(_fixture.Manager);
            SeedAsync().GetAwaiter().GetResult();
        }

        public void Dispose() => _fixture.Dispose();

        private static byte[] BlockHash => Enumerable.Repeat((byte)0xCC, 32).ToArray();

        private static byte[] TxHashOf(int txIndex)
        {
            var h = new byte[32];
            h[0] = (byte)txIndex;
            h[1] = (byte)(txIndex >> 8);
            return h;
        }

        private static Log MakeLog(byte data) => new Log
        {
            Address = AddrA,
            Topics = new List<byte[]> { TopicValue },
            Data = new byte[] { data },
        };

        private async Task SeedAsync()
        {
            var blockHash = BlockHash;
            var bloom = new LogBloomFilter();
            byte data = 0;

            foreach (var (txIndex, count) in Txs)
            {
                var logs = Enumerable.Range(0, count).Select(_ => MakeLog(data++)).ToList();
                foreach (var log in logs) bloom.AddLog(log);

                var txHash = TxHashOf(txIndex);
                var receipt = Receipt.CreateStatusReceipt(true, 0, null, logs);
                await _fixture.ReceiptStore.SaveAsync(receipt, txHash, blockHash, BlockNumber, txIndex, 0, null, 0);
                await _fixture.LogStore.SaveLogsAsync(logs, txHash, blockHash, BlockNumber, txIndex);
            }

            await _fixture.BlockStore.SaveAsync(MakeHeader(bloom.Data), blockHash);
            await _fixture.LogStore.SaveBlockBloomAsync(BlockNumber, bloom.Data);
        }

        private static BlockHeader MakeHeader(byte[] bloom) => new BlockHeader
        {
            ParentHash = new byte[32],
            UnclesHash = new byte[32],
            Coinbase = "0x" + new string('0', 40),
            StateRoot = new byte[32],
            TransactionsHash = new byte[32],
            ReceiptHash = new byte[32],
            LogsBloom = bloom,
            Difficulty = 0,
            BlockNumber = BlockNumber,
            GasLimit = 30_000_000,
            GasUsed = 0,
            Timestamp = 0,
            ExtraData = Array.Empty<byte>(),
            MixHash = new byte[32],
            Nonce = new byte[8],
        };

        private static (int TxIndex, int LogIndex)[] ReceiptPathIndexes()
        {
            var expected = new List<(int, int)>();
            var running = 0;
            foreach (var (txIndex, count) in Txs.OrderBy(t => t.TxIndex))
                for (int i = 0; i < count; i++)
                    expected.Add((txIndex, running++));
            return expected.ToArray();
        }

        [Fact]
        public async Task Given_TxIndexAcross256_When_GetLogsMatchAll_Then_L1EqualsL2EqualsReceiptPath_InEmittedOrder()
        {
            var filter = new LogFilter { FromBlock = BlockNumber, ToBlock = BlockNumber };

            var l1 = await _l1.GetLogsAsync(filter);
            var l2 = await _fixture.LogStore.GetLogsAsync(filter);
            var expected = ReceiptPathIndexes();

            var l1Pairs = l1.Select(l => (l.TransactionIndex, l.LogIndex)).ToArray();
            var l2Pairs = l2.Select(l => (l.TransactionIndex, l.LogIndex)).ToArray();

            Assert.Equal(expected, l1Pairs);
            Assert.Equal(expected, l2Pairs);
        }

        [Fact]
        public async Task Given_TxIndexAcross256_When_GetLogsByBlockHash_Then_L1EqualsL2_BlockWide()
        {
            var l1 = await _l1.GetLogsByBlockHashAsync(BlockHash);
            var l2 = await _fixture.LogStore.GetLogsByBlockHashAsync(BlockHash);
            var expected = ReceiptPathIndexes();

            Assert.Equal(expected, l1.Select(l => (l.TransactionIndex, l.LogIndex)).ToArray());
            Assert.Equal(expected, l2.Select(l => (l.TransactionIndex, l.LogIndex)).ToArray());
        }
    }
}
