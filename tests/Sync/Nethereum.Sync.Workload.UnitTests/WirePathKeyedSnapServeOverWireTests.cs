using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.State;
using Nethereum.DevP2P.Sync;
using Nethereum.EVM;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model.P2P.Snap;
using Xunit;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.Chain.TestData.UnitTests
{
    public class WirePathKeyedSnapServeOverWireTests : IClassFixture<WirePathKeyedSnapServeOverWireTests.WireFixture>
    {
        private readonly WireFixture _fx;
        public WirePathKeyedSnapServeOverWireTests(WireFixture fx) => _fx = fx;

        private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return true;
                await Task.Delay(100);
            }
            return condition();
        }

        [Fact]
        public async Task Follower_SnapSyncsLatest_FromPathKeyedServer_OverTheWire_AndMatchesEveryAccount()
        {
            var server = _fx.Server;
            var pivotHeader = await server.Bundle.Blocks.GetByNumberAsync(server.Head);
            var pivotHash = await server.Bundle.Blocks.GetHashByNumberAsync(server.Head);

            await using var pool = new PeerPoolManager(
                new WorkloadHandshakeWorker(server.GenesisHash, server.NetworkId),
                new PeerPoolOptions(TargetPeerCount: 1, MinPeerLatestBlock: 0));
            await pool.StartAsync(CancellationToken.None);
            pool.EnqueueCandidate(server.Enode);
            Assert.True(
                await WaitUntilAsync(() => pool.ActivePeers.OfType<SyncPeerSession>().Any(p => p.SupportsSnap), TimeSpan.FromSeconds(20)),
                "follower did not establish a snap-capable peer with the path-keyed server");

            var scheduler = new FetchRequestScheduler(pool, new PeerRequestWorker(), new FetchRequestSchedulerOptions());
            var snapPeer = new SchedulerSnapPeer(scheduler);

            var dbPath = Path.Combine(Path.GetTempPath(), "wire-pathkeyed-snap-" + Guid.NewGuid().ToString("N"));
            try
            {
                using var bundle = RocksDbChainStoreBundle.Open(dbPath);
                await bundle.Blocks.SaveAsync(pivotHeader, pivotHash);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                var activations = new FixedChainActivations(HardforkNames.Parse("prague"));
                var result = await SnapBootstrapper.RunAsync(
                    bundle, snapPeer, pivotHeader, pivotHash, NullLogger.Instance,
                    new SnapRunOptions { Scheduler = scheduler, RunBackfill = false, Activations = activations, Pool = pool },
                    cts.Token);

                Assert.True(result.Ran, result.SkipReason);
                await result.StateCompaction.ConfigureAwait(false);
                Assert.True(result.AccountCount > 50, $"only {result.AccountCount} accounts synced from the path-keyed server");

                var recovered = new TrieFallbackStateStore(bundle.State, (INodeBlobStore)bundle.TrieNodes, () => pivotHeader.StateRoot);
                foreach (var kv in await server.Bundle.State.GetAllAccountsAsync())
                {
                    var synced = await recovered.GetAccountAsync(kv.Key);
                    Assert.NotNull(synced);
                    Assert.Equal(kv.Value.Balance, synced.Balance);
                    Assert.Equal(kv.Value.Nonce, synced.Nonce);
                    Assert.Equal(kv.Value.CodeHash, synced.CodeHash);
                    Assert.Equal(kv.Value.StateRoot, synced.StateRoot);
                }
            }
            finally
            {
                try { if (Directory.Exists(dbPath)) Directory.Delete(dbPath, true); } catch { }
            }
        }

        [Fact]
        public async Task Follower_SnapGetAccountRange_AtHeadRoot_OverTheWire_ProofVerifies()
        {
            var server = _fx.Server;

            await using var pool = new PeerPoolManager(
                new WorkloadHandshakeWorker(server.GenesisHash, server.NetworkId),
                new PeerPoolOptions(TargetPeerCount: 1, MinPeerLatestBlock: 0));
            await pool.StartAsync(CancellationToken.None);
            pool.EnqueueCandidate(server.Enode);
            Assert.True(
                await WaitUntilAsync(() => pool.ActivePeers.OfType<SyncPeerSession>().Any(p => p.SupportsSnap), TimeSpan.FromSeconds(20)),
                "follower did not establish a snap-capable peer");

            var scheduler = new FetchRequestScheduler(pool, new PeerRequestWorker(), new FetchRequestSchedulerOptions());
            var snapPeer = new SchedulerSnapPeer(scheduler);

            var zero = new byte[32];
            var full = new byte[32];
            for (int i = 0; i < 32; i++) full[i] = 0xff;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var headRoot = server.Roots[server.Head];
            var respHead = await snapPeer.GetAccountRangeAsync(new GetAccountRangeMessage
            {
                RootHash = headRoot, StartingHash = zero, LimitHash = full, ResponseBytes = 4UL * 1024 * 1024,
            }, cts.Token);
            Assert.NotEmpty(respHead.Accounts);
            Assert.True(VerifyAccountRange(headRoot, zero, respHead), "head account range over the wire must verify vs head root");
        }

        private static bool VerifyAccountRange(byte[] stateRoot, byte[] origin, AccountRangeMessage resp)
        {
            var keys = new List<byte[]>(resp.Accounts.Count);
            var values = new List<byte[]>(resp.Accounts.Count);
            foreach (var entry in resp.Accounts)
            {
                keys.Add(entry.Hash);
                values.Add(SlimAccountEncoder.FromSlim(entry.Body));
            }
            var proof = (IList<byte[]>)(resp.Proof ?? new List<byte[]>());
            return ProofVerification.Current.Range.Verify(stateRoot, origin, keys, values, proof).Valid;
        }

        [Fact]
        public async Task Follower_SnapSyncs_ContractStorage_FromPathKeyedServer_OverTheWire()
        {
            var seq = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 40);
            var roster = seq.Accounts.All;
            var deployer = roster[0];
            var contract = seq.QueueDeploy(deployer, StorageInitCode(24));
            seq.QueueTransfer(deployer, roster[1].Address, System.Numerics.BigInteger.Pow(10, 18));
            await seq.ProduceBlockAsync();
            for (int i = 0; i < 40; i++)
            {
                var from = roster[i % roster.Count];
                seq.QueueTransfer(from, roster[(i + 1) % roster.Count].Address, System.Numerics.BigInteger.Pow(10, 18));
                seq.QueueTransfer(from, "0x" + (0x3000 + i).ToString("x").PadLeft(40, '0'), System.Numerics.BigInteger.Pow(10, 18));
                await seq.ProduceBlockAsync();
            }

            using var server = await PathKeyedWorkloadServer.CreateAsync(
                seq, pathKeyed: true, trieNodeHistoryBlocks: 32, historyIndex: true);
            await server.StartWire();

            var pivotHeader = await server.Bundle.Blocks.GetByNumberAsync(server.Head);
            var pivotHash = await server.Bundle.Blocks.GetHashByNumberAsync(server.Head);

            await using var pool = new PeerPoolManager(
                new WorkloadHandshakeWorker(server.GenesisHash, server.NetworkId),
                new PeerPoolOptions(TargetPeerCount: 1, MinPeerLatestBlock: 0));
            await pool.StartAsync(CancellationToken.None);
            pool.EnqueueCandidate(server.Enode);
            Assert.True(
                await WaitUntilAsync(() => pool.ActivePeers.OfType<SyncPeerSession>().Any(p => p.SupportsSnap), TimeSpan.FromSeconds(20)),
                "follower did not establish a snap-capable peer");

            var scheduler = new FetchRequestScheduler(pool, new PeerRequestWorker(), new FetchRequestSchedulerOptions());
            var snapPeer = new SchedulerSnapPeer(scheduler);

            var dbPath = Path.Combine(Path.GetTempPath(), "wire-pathkeyed-storage-" + Guid.NewGuid().ToString("N"));
            try
            {
                using var bundle = RocksDbChainStoreBundle.Open(dbPath);
                await bundle.Blocks.SaveAsync(pivotHeader, pivotHash);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
                var activations = new FixedChainActivations(HardforkNames.Parse("prague"));
                var result = await SnapBootstrapper.RunAsync(
                    bundle, snapPeer, pivotHeader, pivotHash, NullLogger.Instance,
                    new SnapRunOptions { Scheduler = scheduler, RunBackfill = false, Activations = activations, Pool = pool },
                    cts.Token);
                Assert.True(result.Ran, result.SkipReason);
                await result.StateCompaction.ConfigureAwait(false);

                var recovered = new TrieFallbackStateStore(bundle.State, (Nethereum.Merkle.Patricia.Storage.INodeBlobStore)bundle.TrieNodes, () => pivotHeader.StateRoot);
                var acct = await recovered.GetAccountAsync(contract);
                Assert.NotNull(acct);
                Assert.False(Nethereum.Util.ByteUtil.AreEqual(acct.StateRoot, Nethereum.Model.DefaultValues.EMPTY_TRIE_HASH),
                    "deployed contract must have a non-empty storage root");

                var storageTrie = Nethereum.Merkle.Patricia.PatriciaTrie.LoadFromStorage(
                    acct.StateRoot, (Nethereum.Merkle.Patricia.Storage.ITrieNodeStore)bundle.TrieNodes);
                var slot0Key = new Nethereum.Util.Sha3Keccack().CalculateHash(new byte[32]);
                var slot0 = storageTrie.Get(slot0Key);
                Assert.NotNull(slot0);
            }
            finally
            {
                try { if (Directory.Exists(dbPath)) Directory.Delete(dbPath, true); } catch { }
            }
        }

        private static byte[] StorageInitCode(int slots)
        {
            var b = new List<byte>();
            for (int i = 0; i < slots; i++)
            {
                b.Add(0x60); b.Add((byte)(i + 1));
                b.Add(0x60); b.Add((byte)i);
                b.Add(0x55);
            }
            b.AddRange(new byte[] { 0x60, 0x00, 0x60, 0x00, 0xf3 });
            return b.ToArray();
        }

        public sealed class WireFixture : IDisposable
        {
            public const int Window = 32;
            public const int Blocks = 74;

            public PathKeyedWorkloadServer Server { get; }

            private static System.Numerics.BigInteger Eth(long v)
                => new System.Numerics.BigInteger(v) * System.Numerics.BigInteger.Pow(10, 18);

            public WireFixture()
            {
                var seq = InProcessSequencerDriver.CreateAsync(generatedAccounts: 50).GetAwaiter().GetResult();
                var roster = seq.Accounts.All;
                for (var i = 0; i < Blocks; i++)
                {
                    var from = roster[i % roster.Count];
                    seq.QueueTransfer(from, roster[(i + 1) % roster.Count].Address, Eth(1));
                    var freshEoa = "0x" + (0x2000 + i).ToString("x").PadLeft(40, '0');
                    seq.QueueTransfer(from, freshEoa, Eth(1));
                    seq.ProduceBlockAsync().GetAwaiter().GetResult();
                }
                Server = PathKeyedWorkloadServer.CreateAsync(
                    seq, pathKeyed: true, trieNodeHistoryBlocks: Window, historyIndex: true).GetAwaiter().GetResult();
                Server.StartWire().GetAwaiter().GetResult();
            }

            public void Dispose() => Server.Dispose();
        }
    }
}
