using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Serving.Strategies;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class S1aR8ServingReviewTests
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

        private static async Task<StorageBackedEth68Handler> HandlerAsync(int upToBlock = 20)
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            for (var n = 0; n <= upToBlock; n++)
                await blockStore.SaveAsync(MakeHeader(n), HashOf(n));
            return new StorageBackedEth68Handler(blockStore, txStore, receiptStore);
        }

        [Theory]
        [InlineData(ulong.MaxValue, false)]
        [InlineData(ulong.MaxValue, true)]
        [InlineData(ulong.MaxValue - 1, false)]
        [InlineData(ulong.MaxValue - 1, true)]
        public async Task S1aR8_06_Given_ASkipThatOverflowsWhenOneIsAdded_When_HeadersAreRequested_Then_OnlyTheOriginIsServed(ulong skip, bool reverse)
        {
            var handler = await HandlerAsync();

            var headers = await handler.GetHeadersAsync(new GetBlockHeadersMessage
            {
                RequestId = 1, StartBlock = 10, Limit = 8, Skip = skip, Reverse = reverse
            }, CancellationToken.None);

            Assert.Single(headers);
            Assert.Equal(10UL, (ulong)headers[0].BlockNumber);
        }

        [Theory]
        [InlineData(0UL)]
        [InlineData(3UL)]
        public async Task S1aR8_06_Given_ARequestForZeroHeaders_When_HeadersAreRequested_Then_NothingIsServed(ulong skip)
        {
            var handler = await HandlerAsync();

            var headers = await handler.GetHeadersAsync(new GetBlockHeadersMessage
            {
                RequestId = 1, StartBlock = 10, Limit = 0, Skip = skip, Reverse = false
            }, CancellationToken.None);

            Assert.Empty(headers);
        }

        [Fact]
        public async Task S1aR8_06_Given_AnOriginAboveTheSignedSixtyFourBitRange_When_HeadersAreRequested_Then_NothingIsServed()
        {
            var handler = await HandlerAsync();

            var headers = await handler.GetHeadersAsync(new GetBlockHeadersMessage
            {
                RequestId = 1, StartBlock = ulong.MaxValue, Limit = 8, Skip = 0, Reverse = false
            }, CancellationToken.None);

            Assert.Empty(headers);
        }

        private sealed class CountingNodeStore : Nethereum.Merkle.Patricia.Storage.ITrieNodeStore
        {
            private readonly Nethereum.Merkle.Patricia.Storage.ITrieNodeStore _inner;
            public int Reads;
            public CountingNodeStore(Nethereum.Merkle.Patricia.Storage.ITrieNodeStore inner) => _inner = inner;
            public void Commit(Nethereum.Merkle.Patricia.Storage.TrieNodeSet nodes) => _inner.Commit(nodes);
            public byte[] Get(Nethereum.Merkle.Patricia.Nodes.Node reference)
            {
                System.Threading.Interlocked.Increment(ref Reads);
                return _inner.Get(reference);
            }
            public bool Contains(Nethereum.Merkle.Patricia.Nodes.Node reference) => _inner.Contains(reference);
            public void Flush() => _inner.Flush();
            public void Clear() => _inner.Clear();
            public bool ContainsKey(byte[] stateRoot) => _inner.ContainsKey(stateRoot);
        }

        [Fact]
        public async Task S1aR8_08_Given_AStorageRangesRequestNamingTwentyThousandAbsentAccounts_When_Served_Then_TheTrieIsNotWalkedPerAccountWithoutBound()
        {
            var served = new ServedState();
            var store = new CountingNodeStore(served.Store);
            var handler = new Nethereum.DevP2P.Sync.Serving.PatriciaSnapRequestHandler(store, served.Bytecodes);
            var accounts = Enumerable.Range(0, 20000).Select(i =>
            {
                var hash = new byte[32];
                hash[28] = (byte)(i >> 24); hash[29] = (byte)(i >> 16); hash[30] = (byte)(i >> 8); hash[31] = (byte)i;
                hash[0] = 0x7F;
                return hash;
            }).ToList();

            var response = await handler.GetStorageRangesAsync(new GetStorageRangesMessage
            {
                RequestId = 1, RootHash = served.Root, AccountHashes = accounts, ResponseBytes = 1024 * 1024
            }, CancellationToken.None);

            Assert.True(store.Reads <= 4096, "node reads for one request: " + store.Reads + ", slot lists returned: " + response.Slots.Count);
        }

        [Fact]
        public async Task S1aR8_07_Given_AMissingHashBetweenTwoKnownOnes_When_BodiesAreRequested_Then_ServingStopsAtTheGap()
        {
            var handler = await HandlerAsync();

            var bodies = await handler.GetBodiesAsync(new[] { HashOf(5), HashOf(900), HashOf(6) }, CancellationToken.None);

            Assert.Single(bodies);
        }

        [Fact]
        public async Task S1aR8_07_Given_AMissingHashBetweenTwoKnownOnes_When_ReceiptsAreRequested_Then_ServingStopsAtTheGap()
        {
            var handler = await HandlerAsync();

            var receipts = await handler.GetReceiptsAsync(new[] { HashOf(5), HashOf(900), HashOf(6) }, CancellationToken.None);

            Assert.Single(receipts);
        }
    }
}
