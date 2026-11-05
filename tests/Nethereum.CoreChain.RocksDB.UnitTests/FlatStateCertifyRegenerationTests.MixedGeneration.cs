using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    [Trait("Slice", "Flat")]
    public partial class FlatStateCertifyRegenerationTests
    {
        private const int MixedGenerationAccounts = 6_000;
        private const string CertifyStart = "snap.flat.reconcile starting root=0x";

        private sealed class LineLog : Microsoft.Extensions.Logging.ILogger
        {
            public ConcurrentQueue<string> Lines { get; } = new();
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
            public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
                TState state, Exception exception, Func<TState, Exception, string> formatter)
                => Lines.Enqueue(formatter(state, exception));

            public List<string> CertifiedRoots()
                => Lines.Where(l => l.Contains(CertifyStart, StringComparison.Ordinal))
                    .Select(l => l.Substring(l.IndexOf(CertifyStart, StringComparison.Ordinal) + CertifyStart.Length, 64))
                    .ToList();
        }

        private sealed class Generation
        {
            public Generation(int generation)
            {
                for (int i = 0; i < MixedGenerationAccounts; i++)
                {
                    var changed = generation > 0 && (i * 7 + generation * 13) % 10 < 3;
                    Accounts[HashOf("mixed-generation-eoa", i)] = NewAccount((ulong)i, changed ? (ulong)(1_000 * generation + i) : 1UL, DefaultValues.EMPTY_TRIE_HASH);
                }
                var trie = new StackTrie(null, (path, blob) => Rows[path.ToHex()] = blob);
                foreach (var account in Accounts) trie.Update(account.Key, new AccountEncoder().Encode(account.Value));
                Root = trie.Hash();
            }

            public SortedDictionary<byte[], Account> Accounts { get; } = new(ByteArrayComparer.Current);
            public Dictionary<string, byte[]> Rows { get; } = new();
            public byte[] Root { get; }

            public void WriteTrie(RocksDbManager rocks, Func<string, bool> include = null)
            {
                foreach (var row in Rows.Where(r => include == null || include(r.Key)))
                    rocks.Put(RocksDbManager.CF_STATE_TRIE_ACCOUNT, row.Key.HexToByteArray(), row.Value);
            }

            public async Task WriteFlatAsync(RocksDbManager rocks)
            {
                var flat = new RocksDbStateStore(rocks);
                foreach (var account in Accounts) await flat.SaveAccountByHashAsync(account.Key, account.Value);
            }
        }

        private static int MixInSubtreesAndDropNucLikeHoles(RocksDbManager rocks, Generation a, Generation b, int seed)
        {
            var random = new Random(seed);
            foreach (var subtree in b.Rows.Keys.Where(k => k.Length == 6).Where(_ => random.Next(3) == 0).ToList())
            {
                foreach (var key in a.Rows.Keys.Where(k => k.StartsWith(subtree, StringComparison.Ordinal)).ToList())
                    rocks.Delete(RocksDbManager.CF_STATE_TRIE_ACCOUNT, key.HexToByteArray());
                b.WriteTrie(rocks, k => k.StartsWith(subtree, StringComparison.Ordinal));
            }
            var deleted = 0;
            foreach (var key in a.Rows.Keys.Concat(b.Rows.Keys).Distinct())
            {
                var depth = key.Length / 2;
                var drop = depth <= 1
                           || (depth == 2 && random.NextDouble() < 0.96)
                           || (depth == 3 && random.NextDouble() < 0.93)
                           || (depth == 4 && random.NextDouble() < 0.4);
                if (!drop) continue;
                rocks.Delete(RocksDbManager.CF_STATE_TRIE_ACCOUNT, key.HexToByteArray());
                deleted++;
            }
            return deleted;
        }

        private static void PersistWholeTrieSentinel(RocksDbManager rocks, byte[] root)
            => rocks.Put(RocksDbManager.CF_METADATA, System.Text.Encoding.ASCII.GetBytes("flatrepair:inventory"), new byte[32].Concat(root).ToArray());

        [Theory]
        [Trait("Reproducer", "nuc-2026-10-07-mixed-generation-headless-trie")]
        [InlineData(3)]
        [InlineData(5)]
        [InlineData(11)]
        public async Task Given_AHeadlessMixedGenerationAccountTrieWithNucLikeHolesAndAPersistedWholeTrieSentinel_When_FinalizingFromAStalePivot_Then_ItHealsForwardAndCertifiesAndCommitsTheFreshRootByteIdentical(int seed)
        {
            var a = new Generation(0);
            var b = new Generation(1);
            var c = new Generation(2);
            var server = OpenBundle();
            c.WriteTrie(server.Rocks);
            await c.WriteFlatAsync(server.Rocks);
            var client = OpenBundle();
            a.WriteTrie(client.Rocks);
            var deleted = MixInSubtreesAndDropNucLikeHoles(client.Rocks, a, b, seed);
            await a.WriteFlatAsync(client.Rocks);
            PersistWholeTrieSentinel(client.Rocks, b.Root);
            Assert.Null(client.Rocks.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, Array.Empty<byte>()));
            Assert.True(deleted > 0);
            var scheduler = new ServingScheduler(server);
            var fresh = PivotHeader(500, c.Root);
            var log = new LineLog();
            using var bound = new CancellationTokenSource(TimeSpan.FromMinutes(5));

            var finalPivot = await SnapBootstrapper.ReconcileFlatStateAsync(
                client, scheduler, new SnapBootstrapper.RollingPivot(PivotHeader(100, b.Root), HashOf("pivot", 100)),
                (_, _) => Task.FromResult<(BlockHeader, byte[])?>((fresh, HashOf("pivot", 500))),
                null, log, bound.Token);

            Assert.Equal(c.Root, finalPivot.Header.StateRoot);
            Assert.NotEmpty(log.CertifiedRoots());
            Assert.All(log.CertifiedRoots(), root => Assert.Equal(c.Root.ToHex(), root));
            Assert.All(scheduler.TrieNodeRoots, root => Assert.Equal(c.Root, root));
            foreach (var row in c.Rows)
                Assert.Equal(row.Value, client.Rocks.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, row.Key.HexToByteArray()));
            Assert.Equal(0, (await OracleVerify(client, c.Root)).TotalRepairs);
            Assert.Empty(client.GetPersistedDamage());
        }

        [Fact]
        public async Task Given_AnIntactFreshGenerationTrieOverAnOlderFlatAndAPersistedWholeTrieSentinel_When_Finalizing_Then_ItCertifiesTheStoredRootWithoutAnyPeerRequest()
        {
            var a = new Generation(0);
            var c = new Generation(2);
            var server = OpenBundle();
            c.WriteTrie(server.Rocks);
            var client = OpenBundle();
            c.WriteTrie(client.Rocks);
            await a.WriteFlatAsync(client.Rocks);
            PersistWholeTrieSentinel(client.Rocks, a.Root);
            var scheduler = new ServingScheduler(server);
            var stored = PivotHeader(500, c.Root);
            var log = new LineLog();
            using var bound = new CancellationTokenSource(TimeSpan.FromMinutes(5));

            var finalPivot = await SnapBootstrapper.ReconcileFlatStateAsync(
                client, scheduler, new SnapBootstrapper.RollingPivot(stored, HashOf("pivot", 500)),
                (_, _) => Task.FromResult<(BlockHeader, byte[])?>((PivotHeader(600, HashOf("unserved", 600)), HashOf("pivot", 600))),
                null, log, bound.Token);

            Assert.Equal(c.Root, finalPivot.Header.StateRoot);
            Assert.Equal(new[] { c.Root.ToHex() }, log.CertifiedRoots());
            Assert.Empty(scheduler.TrieNodeRoots);
            Assert.Equal(0, (await OracleVerify(client, c.Root)).TotalRepairs);
        }
    }
}
