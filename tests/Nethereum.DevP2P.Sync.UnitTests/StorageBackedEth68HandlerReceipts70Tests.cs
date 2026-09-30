using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Serving.Strategies;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Util;
using Xunit;
using Nethereum.DevP2P.Sync.Serving.Strategies;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class StorageBackedEth68HandlerReceipts70Tests
    {
        private static byte[] HashOf(int seed)
        {
            var bytes = new byte[32];
            bytes[31] = (byte)(seed & 0xFF);
            bytes[30] = (byte)((seed >> 8) & 0xFF);
            return bytes;
        }

        private static BlockHeader MakeHeader(int blockNumber) => new BlockHeader
        {
            BlockNumber = (EvmUInt256)(ulong)blockNumber,
            ParentHash = blockNumber == 0 ? new byte[32] : HashOf(blockNumber - 1),
            StateRoot = new byte[32],
            TransactionsHash = new byte[32],
            ReceiptHash = new byte[32],
            LogsBloom = new byte[256],
            MixHash = new byte[32],
            ExtraData = new byte[0],
            Nonce = new byte[8],
            Coinbase = "0x0000000000000000000000000000000000000000"
        };

        private static Receipt MakeReceipt(byte statusSeed) => new Receipt
        {
            PostStateOrStatus = new byte[] { statusSeed },
            CumulativeGasUsed = new EvmUInt256(21000UL + statusSeed),
            Bloom = new byte[256],
            Logs = new List<Log>()
        };

        private static async Task<(StorageBackedEth68Handler handler, byte[] hash)> PrimeBlockAsync(
            int blockNumber, IReadOnlyList<Receipt> receipts)
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();

            var hash = HashOf(blockNumber);
            await blockStore.SaveAsync(MakeHeader(blockNumber), hash);
            for (int i = 0; i < receipts.Count; i++)
                await receiptStore.SaveAsync(receipts[i], HashOf(1000 + i), hash, blockNumber, i, 21000, null, 1);

            var handler = new StorageBackedEth68Handler(blockStore, txStore, receiptStore);
            return (handler, hash);
        }

        [Fact]
        public async Task Eth70Server_HonorsFirstBlockReceiptIndex_SkipsEarlierReceipts()
        {
            var receipts = new List<Receipt> { MakeReceipt(1), MakeReceipt(2), MakeReceipt(3) };
            var (handler, hash) = await PrimeBlockAsync(0, receipts);

            var result = await handler.GetReceipts70Async(
                new[] { hash }, firstBlockReceiptIndex: 1, sizeCap: 10_000_000, CancellationToken.None);

            Assert.False(result.LastBlockIncomplete);
            Assert.Equal(1, result.ReceiptsByBlock.Count);
            Assert.Equal(2, result.ReceiptsByBlock[0].Count);
            Assert.Equal(new byte[] { 2 }, result.ReceiptsByBlock[0][0].PostStateOrStatus);
            Assert.Equal(new byte[] { 3 }, result.ReceiptsByBlock[0][1].PostStateOrStatus);
        }

        [Fact]
        public async Task Eth70Server_HonorsFirstBlockReceiptIndex_Twin_IndexZeroReturnsAll()
        {
            var receipts = new List<Receipt> { MakeReceipt(1), MakeReceipt(2), MakeReceipt(3) };
            var (handler, hash) = await PrimeBlockAsync(0, receipts);

            var result = await handler.GetReceipts70Async(
                new[] { hash }, firstBlockReceiptIndex: 0, sizeCap: 10_000_000, CancellationToken.None);

            Assert.Equal(3, result.ReceiptsByBlock[0].Count);
        }

        [Fact]
        public async Task Eth70Server_FirstBlockReceiptIndex_OnlyAppliesToFirstRequestedBlock()
        {
            var block0Receipts = new List<Receipt> { MakeReceipt(1), MakeReceipt(2), MakeReceipt(3) };
            var block1Receipts = new List<Receipt> { MakeReceipt(4), MakeReceipt(5) };

            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var hash0Combined = HashOf(0);
            var hash1Combined = HashOf(1);
            await blockStore.SaveAsync(MakeHeader(0), hash0Combined);
            await blockStore.SaveAsync(MakeHeader(1), hash1Combined);
            for (int i = 0; i < block0Receipts.Count; i++)
                await receiptStore.SaveAsync(block0Receipts[i], HashOf(1000 + i), hash0Combined, 0, i, 21000, null, 1);
            for (int i = 0; i < block1Receipts.Count; i++)
                await receiptStore.SaveAsync(block1Receipts[i], HashOf(2000 + i), hash1Combined, 1, i, 21000, null, 1);
            var handler = new StorageBackedEth68Handler(blockStore, txStore, receiptStore);

            var result = await handler.GetReceipts70Async(
                new[] { hash0Combined, hash1Combined }, firstBlockReceiptIndex: 1, sizeCap: 10_000_000, CancellationToken.None);

            Assert.Equal(2, result.ReceiptsByBlock.Count);
            Assert.Equal(2, result.ReceiptsByBlock[0].Count);
            Assert.Equal(2, result.ReceiptsByBlock[1].Count);
        }

        [Fact]
        public async Task Eth70Server_SetsLastBlockIncomplete_WhenResponseExceedsCap()
        {
            var receipts = new List<Receipt> { MakeReceipt(1), MakeReceipt(2), MakeReceipt(3) };
            var firstReceiptBytes = ReceiptsMessageEth69Encoder.EncodeSingleReceipt(receipts[0]).Length;
            var (handler, hash) = await PrimeBlockAsync(0, receipts);

            var result = await handler.GetReceipts70Async(
                new[] { hash }, firstBlockReceiptIndex: 0, sizeCap: (ulong)firstReceiptBytes, CancellationToken.None);

            Assert.True(result.LastBlockIncomplete);
            Assert.Equal(1, result.ReceiptsByBlock.Count);
            Assert.Equal(1, result.ReceiptsByBlock[0].Count);
            Assert.Equal(new byte[] { 1 }, result.ReceiptsByBlock[0][0].PostStateOrStatus);
        }

        [Fact]
        public async Task Eth70Server_SetsLastBlockIncomplete_Twin_UnderCapStaysComplete()
        {
            var receipts = new List<Receipt> { MakeReceipt(1), MakeReceipt(2), MakeReceipt(3) };
            var (handler, hash) = await PrimeBlockAsync(0, receipts);

            var result = await handler.GetReceipts70Async(
                new[] { hash }, firstBlockReceiptIndex: 0, sizeCap: 10_000_000, CancellationToken.None);

            Assert.False(result.LastBlockIncomplete);
            Assert.Equal(3, result.ReceiptsByBlock[0].Count);
        }

        [Fact]
        public async Task Eth70Server_AtLeastOneReceiptIncluded_EvenIfAloneExceedsCap()
        {
            var receipts = new List<Receipt> { MakeReceipt(1), MakeReceipt(2) };
            var (handler, hash) = await PrimeBlockAsync(0, receipts);

            var result = await handler.GetReceipts70Async(
                new[] { hash }, firstBlockReceiptIndex: 0, sizeCap: 1, CancellationToken.None);

            Assert.True(result.LastBlockIncomplete);
            Assert.Equal(1, result.ReceiptsByBlock.Count);
            Assert.Equal(1, result.ReceiptsByBlock[0].Count);
        }
    }
}
