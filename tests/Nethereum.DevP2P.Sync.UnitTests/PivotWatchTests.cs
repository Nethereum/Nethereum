using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class PivotWatchTests
    {
        private const string WaitingForPivot = "snap.phase3.waiting_for_pivot";
        private const string Recycle = "snap.phase3.recycle";
        private static readonly TimeSpan StartBound = TimeSpan.FromSeconds(90);
        private static readonly Sha3Keccack Keccak = new();
        private static readonly IHashProvider Hp = new Sha3KeccackHashProvider();

        private static byte[] Fill(byte b)
        {
            var a = new byte[32];
            for (int i = 0; i < 32; i++) a[i] = b;
            return a;
        }

        private static BlockHeader Header(int blockNumber, byte[] stateRoot)
            => new() { BlockNumber = blockNumber, StateRoot = stateRoot };

        private sealed class NullBytecodes : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        private sealed class OrderedLog : Microsoft.Extensions.Logging.ILogger
        {
            private readonly List<string> _messages = new();
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
            public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
                TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                var text = formatter(state, exception);
                lock (_messages) _messages.Add(text);
            }

            public int Count(string fragment)
            {
                lock (_messages) return _messages.Count(m => m.Contains(fragment, StringComparison.Ordinal));
            }

            public string First(params string[] fragments)
            {
                lock (_messages)
                    return _messages.Select(m => fragments.FirstOrDefault(f => m.Contains(f, StringComparison.Ordinal)))
                        .FirstOrDefault(f => f != null);
            }

            public async Task<bool> WaitForAnyAsync(TimeSpan timeout, params string[] fragments)
            {
                var deadline = DateTime.UtcNow + timeout;
                while (DateTime.UtcNow < deadline)
                {
                    if (First(fragments) != null) return true;
                    await Task.Delay(50);
                }
                return First(fragments) != null;
            }
        }

        private sealed class StatelessAfterScheduler : IFetchRequestScheduler
        {
            private readonly PatriciaSnapRequestHandler _handler;
            private readonly int _servedRequests;
            private int _requests;

            public StatelessAfterScheduler(PatriciaSnapRequestHandler handler, int servedRequests)
            {
                _handler = handler;
                _servedRequests = servedRequests;
            }

            public void OnTargetRootChanged() { }

            public async Task<TrieNodesMessage> FetchTrieNodesAsync(
                byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
            {
                await Task.Yield();
                if (_handler != null && Interlocked.Increment(ref _requests) <= _servedRequests)
                    return await _handler.GetTrieNodesAsync(new GetTrieNodesMessage
                    {
                        RequestId = 1, RootHash = stateRoot, Paths = paths, ResponseBytes = responseBytes,
                    });
                var stateless = new TrieNodesMessage { RequestId = 1, Nodes = new List<byte[]>() };
                for (int i = 0; i < paths.Count; i++) stateless.Nodes.Add(Array.Empty<byte>());
                return stateless;
            }

            public Task<AccountRangeMessage> FetchAccountRangeAsync(
                byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new FetchRequestFailedException("account range unavailable at the scripted root", null);
            public Task<StorageRangesMessage> FetchStorageRangesAsync(
                byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new FetchRequestFailedException("storage range unavailable at the scripted root", null);
            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false)
                => throw new NotImplementedException();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid> excludePeers, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
        }

        private static (byte[] Root, PatriciaSnapRequestHandler Handler) ServedAccountTrie()
        {
            var store = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(store, Hp);
            var encoder = new AccountEncoder();
            for (byte i = 1; i <= 8; i++)
                trie.Put(Keccak.CalculateHash(new byte[] { i }), encoder.Encode(new Account
                {
                    Nonce = (EvmUInt256)i,
                    Balance = (EvmUInt256)1000UL,
                    StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                    CodeHash = DefaultValues.EMPTY_DATA_HASH,
                }));
            trie.SaveDirtyNodesToStorage();
            return (trie.Root.GetHash(), new PatriciaSnapRequestHandler(store, new NullBytecodes()));
        }

        private static async Task StopAsync(Task run, CancellationTokenSource cts)
        {
            if (run.IsCompleted) return;
            cts.Cancel();
            try { await run.WaitAsync(TimeSpan.FromSeconds(30)); } catch { }
        }

        private static Task<SnapBootstrapper.PivotState> Heal(
            IFetchRequestScheduler scheduler, SnapBootstrapper.RollingPivot pivot,
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> refresher,
            OrderedLog log, CancellationToken ct)
        {
            var bundle = InMemoryChainStoreBundle.Open();
            return SnapBootstrapper.ReconcileFlatStateAsync(
                bundle, scheduler, pivot, refresher, null, log, ct);
        }

        [Fact]
        public async Task Given_AHealResultWithNoNodesAndAnUnchangedRoot_When_Judged_Then_ItIsStalled()
        {
            var pivot = new SnapBootstrapper.RollingPivot(Header(100, Fill(0xCD)), Fill(0x01));
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> samePivot =
                (_, _) => Task.FromResult<(BlockHeader, byte[])?>((Header(100, Fill(0xCD)), Fill(0x01)));
            var log = new OrderedLog();
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var run = Heal(new StatelessAfterScheduler(null, 0), pivot, samePivot, log, cts.Token);
            try
            {
                Assert.True(await log.WaitForAnyAsync(StartBound, WaitingForPivot, Recycle), "the heal neither waited nor recycled");
                Assert.Equal(WaitingForPivot, log.First(WaitingForPivot, Recycle));
                Assert.Equal(0, log.Count(Recycle));
            }
            finally
            {
                await StopAsync(run, cts);
            }
        }

        [Fact]
        public async Task Given_AHealResultWithNoNodesAndAMovedRoot_When_Judged_Then_ItIsNotStalled()
        {
            var pivot = new SnapBootstrapper.RollingPivot(Header(100, Fill(0xCD)), Fill(0x01));
            var refreshes = 0;
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> movingPivot = (_, _) =>
            {
                var n = Interlocked.Increment(ref refreshes);
                return Task.FromResult<(BlockHeader, byte[])?>((Header(100 + n, Fill((byte)(0x10 + n))), Fill((byte)n)));
            };
            var log = new OrderedLog();
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

            var run = Heal(new StatelessAfterScheduler(null, 0), pivot, movingPivot, log, cts.Token);
            var recycled = await log.WaitForAnyAsync(TimeSpan.FromMinutes(2), Recycle);
            var gaveUp = run.IsCompleted;
            await StopAsync(run, cts);

            Assert.True(recycled, "the heal never re-pivoted while the root kept moving");
            Assert.False(gaveUp, "the heal gave up although the pivot kept moving");
            Assert.Equal(0, log.Count(WaitingForPivot));
        }

        [Fact]
        public async Task Given_AHealResultWithNodesAndAnUnchangedRoot_When_Judged_Then_ItIsNotStalled()
        {
            var (root, handler) = ServedAccountTrie();
            var pivot = new SnapBootstrapper.RollingPivot(Header(100, root), Fill(0x01));
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> samePivot =
                (_, _) => Task.FromResult<(BlockHeader, byte[])?>((Header(100, root), Fill(0x01)));
            var log = new OrderedLog();
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var run = Heal(new StatelessAfterScheduler(handler, 2), pivot, samePivot, log, cts.Token);
            try
            {
                Assert.True(await log.WaitForAnyAsync(StartBound, WaitingForPivot, Recycle), "the heal neither waited nor recycled");
                Assert.Equal(Recycle, log.First(WaitingForPivot, Recycle));
            }
            finally
            {
                await StopAsync(run, cts);
            }
        }
    }
}
