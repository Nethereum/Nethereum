using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Strategies;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    /// <summary>
    /// Validates that <see cref="StorageBackedEth68Handler"/> answers requests
    /// for blocks above its sync cursor with a SILENT-SKIP partial response
    /// rather than padding with empty entries or surfacing an error. This
    /// mirrors geth's behaviour in
    /// <c>eth/protocols/eth/handlers.go</c>:
    /// - GetBlockBodies skips unknown hashes (line 243: <c>if data := chain.GetBodyRLP(hash); len(data) != 0</c>)
    /// - GetReceipts skips unknown hashes (line 286-289)
    /// - GetBlockHeaders breaks the loop on the first missing header (line 92)
    /// - GetPooledTransactions skips unknown hashes (line 477)
    /// </summary>
    public class StorageBackedEth68HandlerCursorTests
    {
        private static byte[] HashOf(int seed)
        {
            var bytes = new byte[32];
            bytes[31] = (byte)(seed & 0xFF);
            bytes[30] = (byte)((seed >> 8) & 0xFF);
            return bytes;
        }

        private static BlockHeader MakeHeader(int blockNumber)
        {
            return new BlockHeader
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
        }

        private static async Task<(IBlockStore, ITransactionStore, IReceiptStore)> PrimeChainAsync(int upToBlock)
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();

            for (int n = 0; n <= upToBlock; n++)
            {
                await blockStore.SaveAsync(MakeHeader(n), HashOf(n));
            }
            return (blockStore, txStore, receiptStore);
        }

        [Fact]
        public async Task Given_CursorAt100_When_HeadersRequestedFrom50To200_Then_ReturnsHeaders50To100()
        {
            // GIVEN: local cursor at block 100; peer asks for [50..50+150] step 1.
            // Geth (eth/protocols/eth/handlers.go:91-93) breaks the loop on the
            // first missing header — so the partial result is [50..100] (51 headers).
            var (blockStore, txStore, receiptStore) = await PrimeChainAsync(upToBlock: 100);
            var handler = new StorageBackedEth68Handler(blockStore, txStore, receiptStore);

            var headers = await handler.GetHeadersAsync(new GetBlockHeadersMessage
            {
                RequestId = 1,
                StartBlock = 50,
                Limit = 150,
                Skip = 0,
                Reverse = false
            }, CancellationToken.None);

            Assert.Equal(51, headers.Count);
            Assert.Equal(50UL, (ulong)headers[0].BlockNumber);
            Assert.Equal(100UL, (ulong)headers[50].BlockNumber);
        }

        [Fact]
        public async Task Given_CursorAt100_When_HeadersRequestedFromUnknownHash_Then_ReturnsEmpty()
        {
            var (blockStore, txStore, receiptStore) = await PrimeChainAsync(upToBlock: 100);
            var handler = new StorageBackedEth68Handler(blockStore, txStore, receiptStore);

            var unknownHash = HashOf(999);
            var headers = await handler.GetHeadersAsync(new GetBlockHeadersMessage
            {
                RequestId = 1,
                StartBlockHash = unknownHash,
                Limit = 10,
                Skip = 0,
                Reverse = false
            }, CancellationToken.None);

            Assert.Empty(headers);
        }

        [Fact]
        public async Task Given_FreshNode_When_HeadersRequested_Then_ReturnsEmpty()
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var handler = new StorageBackedEth68Handler(blockStore, txStore, receiptStore);

            var headers = await handler.GetHeadersAsync(new GetBlockHeadersMessage
            {
                RequestId = 1,
                StartBlock = 0,
                Limit = 192,
                Skip = 0,
                Reverse = false
            }, CancellationToken.None);

            Assert.Empty(headers);
        }

        [Fact]
        public async Task Given_CursorAt100_When_HeadersRequested50To99_Then_ReturnsAll50Headers()
        {
            var (blockStore, txStore, receiptStore) = await PrimeChainAsync(upToBlock: 100);
            var handler = new StorageBackedEth68Handler(blockStore, txStore, receiptStore);

            var headers = await handler.GetHeadersAsync(new GetBlockHeadersMessage
            {
                RequestId = 1,
                StartBlock = 50,
                Limit = 50,
                Skip = 0,
                Reverse = false
            }, CancellationToken.None);

            Assert.Equal(50, headers.Count);
            Assert.Equal(50UL, (ulong)headers[0].BlockNumber);
            Assert.Equal(99UL, (ulong)headers[49].BlockNumber);
        }

        [Fact]
        public async Task Given_CursorAt100_When_BodiesRequestedForMixOfKnownAndUnknown_Then_OmitsUnknownHashes()
        {
            // GIVEN: cursor at 100. Peer requests bodies for blocks [98, 200, 99, 300].
            // Geth (eth/protocols/eth/handlers.go:243) silently skips unknown
            // hashes — response carries entries for [98, 99] only, in order.
            var (blockStore, txStore, receiptStore) = await PrimeChainAsync(upToBlock: 100);
            var handler = new StorageBackedEth68Handler(blockStore, txStore, receiptStore);

            byte[][] requested = {
                HashOf(98),
                HashOf(200),
                HashOf(99),
                HashOf(300)
            };
            var bodies = await handler.GetBodiesAsync(requested, CancellationToken.None);

            Assert.Equal(2, bodies.Count);
        }

        [Fact]
        public async Task Given_FreshNode_When_BodiesRequested_Then_ReturnsEmpty()
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var handler = new StorageBackedEth68Handler(blockStore, txStore, receiptStore);

            byte[][] requested = { HashOf(1), HashOf(2), HashOf(3) };
            var bodies = await handler.GetBodiesAsync(requested, CancellationToken.None);

            Assert.Empty(bodies);
        }

        [Fact]
        public async Task Given_CursorAt100_When_BodiesRequestedAllAboveCursor_Then_ReturnsEmpty()
        {
            var (blockStore, txStore, receiptStore) = await PrimeChainAsync(upToBlock: 100);
            var handler = new StorageBackedEth68Handler(blockStore, txStore, receiptStore);

            byte[][] requested = { HashOf(200), HashOf(300), HashOf(400) };
            var bodies = await handler.GetBodiesAsync(requested, CancellationToken.None);

            Assert.Empty(bodies);
        }

        [Fact]
        public async Task Given_CursorAt100_When_ReceiptsRequestedForMixOfKnownAndUnknown_Then_OmitsUnknownHashes()
        {
            var (blockStore, txStore, receiptStore) = await PrimeChainAsync(upToBlock: 100);
            var handler = new StorageBackedEth68Handler(blockStore, txStore, receiptStore);

            byte[][] requested = {
                HashOf(50),
                HashOf(150),
                HashOf(75)
            };
            var receipts = await handler.GetReceiptsAsync(requested, CancellationToken.None);

            Assert.Equal(2, receipts.Count);
        }

        [Fact]
        public async Task Given_FreshNode_When_ReceiptsRequested_Then_ReturnsEmpty()
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var handler = new StorageBackedEth68Handler(blockStore, txStore, receiptStore);

            byte[][] requested = { HashOf(1), HashOf(2) };
            var receipts = await handler.GetReceiptsAsync(requested, CancellationToken.None);

            Assert.Empty(receipts);
        }

        [Fact]
        public async Task Given_NullTxPool_When_PooledTransactionsRequested_Then_ReturnsEmpty()
        {
            var (blockStore, txStore, receiptStore) = await PrimeChainAsync(upToBlock: 100);
            var handler = new StorageBackedEth68Handler(blockStore, txStore, receiptStore, txPool: null);

            byte[][] requested = { HashOf(1), HashOf(2) };
            var txs = await handler.GetPooledTransactionsAsync(requested, CancellationToken.None);

            Assert.Empty(txs);
        }

        [Fact]
        public async Task Given_NullHashesInRequest_When_BodiesRequested_Then_NullsAreSkipped()
        {
            var (blockStore, txStore, receiptStore) = await PrimeChainAsync(upToBlock: 100);
            var handler = new StorageBackedEth68Handler(blockStore, txStore, receiptStore);

            byte[][] requested = { null!, HashOf(50), null!, HashOf(60) };
            var bodies = await handler.GetBodiesAsync(requested, CancellationToken.None);

            Assert.Equal(2, bodies.Count);
        }
    }
}
