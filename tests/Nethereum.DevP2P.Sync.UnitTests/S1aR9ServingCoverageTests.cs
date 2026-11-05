using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Serving.Strategies;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class S1aR9ServingCoverageTests
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

        private static async Task<StorageBackedEth68Handler> HandlerAsync()
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            for (var n = 0; n <= 20; n++)
                await blockStore.SaveAsync(MakeHeader(n), HashOf(n));
            return new StorageBackedEth68Handler(blockStore, txStore, receiptStore);
        }

        private sealed class SlowNodeStore : Nethereum.Merkle.Patricia.Storage.ITrieNodeStore
        {
            private readonly Nethereum.Merkle.Patricia.Storage.ITrieNodeStore _inner;
            private readonly int _delayMs;
            public SlowNodeStore(Nethereum.Merkle.Patricia.Storage.ITrieNodeStore inner, int delayMs) { _inner = inner; _delayMs = delayMs; }
            public void Commit(Nethereum.Merkle.Patricia.Storage.TrieNodeSet nodes) => _inner.Commit(nodes);
            public byte[] Get(Nethereum.Merkle.Patricia.Nodes.Node reference)
            {
                Thread.Sleep(_delayMs);
                return _inner.Get(reference);
            }
            public bool Contains(Nethereum.Merkle.Patricia.Nodes.Node reference) => _inner.Contains(reference);
            public void Flush() => _inner.Flush();
            public void Clear() => _inner.Clear();
            public bool ContainsKey(byte[] stateRoot) => _inner.ContainsKey(stateRoot);
        }

        [Fact]
        public async Task S1aR9_12_Given_AStorageRangesRequestWhoseAccountLookupsAreSlow_When_Served_Then_TheRequestStopsAtTheTimeBudget()
        {
            var served = new ServedState();
            var store = new SlowNodeStore(served.Store, delayMs: 10);
            var handler = new Nethereum.DevP2P.Sync.Serving.PatriciaSnapRequestHandler(store, served.Bytecodes);
            var accounts = System.Linq.Enumerable.Range(0, 2000).Select(i =>
            {
                var hash = new byte[32];
                hash[30] = (byte)(i >> 8); hash[31] = (byte)i;
                hash[0] = 0x7F;
                return hash;
            }).ToList();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            await handler.GetStorageRangesAsync(new Nethereum.Model.P2P.Snap.GetStorageRangesMessage
            {
                RequestId = 1, RootHash = served.Root, AccountHashes = accounts, ResponseBytes = 1024 * 1024
            }, CancellationToken.None);

            Assert.True(sw.Elapsed < System.TimeSpan.FromSeconds(12), "one GetStorageRanges ran for " + sw.Elapsed);
        }

        [Fact]
        public async Task S1aR9_11_Given_AMissingHashBetweenTwoKnownOnes_When_Eth70ReceiptsAreRequested_Then_ServingStopsAtTheGap()
        {
            var handler = await HandlerAsync();

            var served = await handler.GetReceipts70Async(
                new[] { HashOf(5), HashOf(900), HashOf(6) }, 0, 2 * 1024 * 1024, CancellationToken.None);

            Assert.Single(served.ReceiptsByBlock);
        }
    }
}
