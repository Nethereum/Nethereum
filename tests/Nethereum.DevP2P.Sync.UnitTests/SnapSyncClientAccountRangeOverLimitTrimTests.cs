using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Serving;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapSyncClientAccountRangeOverLimitTrimTests
    {
        private sealed class InMemoryBytecodeStore : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        private sealed class CountingSink : ISnapSyncSink
        {
            private readonly ISnapSyncSink _inner;
            public readonly ConcurrentDictionary<string, int> Writes = new();

            public CountingSink(ISnapSyncSink inner) { _inner = inner; }

            public ValueTask BeginAsync(byte[] targetRoot, CancellationToken ct) => _inner.BeginAsync(targetRoot, ct);

            public ValueTask WriteAccountAsync(byte[] accountHash, byte[] slimRlp, CancellationToken ct)
            {
                Writes.AddOrUpdate(accountHash.ToHex(), 1, (_, n) => n + 1);
                return _inner.WriteAccountAsync(accountHash, slimRlp, ct);
            }

            public ValueTask<IStorageScope> BeginAccountStorageAsync(byte[] accountHash, byte[] expectedStorageRoot, CancellationToken ct)
                => _inner.BeginAccountStorageAsync(accountHash, expectedStorageRoot, ct);

            public ValueTask WriteBytecodeAsync(byte[] codeHash, byte[] code, CancellationToken ct)
                => _inner.WriteBytecodeAsync(codeHash, code, ct);

            public ValueTask<byte[]> FinaliseRootAsync(CancellationToken ct) => _inner.FinaliseRootAsync(ct);

            public int WriteCount(byte[] accountHash) => Writes.TryGetValue(accountHash.ToHex(), out var n) ? n : 0;
        }

        private static byte[] Hash(byte first, byte last = 0x00)
        {
            var h = new byte[32];
            h[0] = first;
            h[31] = last;
            return h;
        }

        private sealed class Scenario
        {
            public byte[] AccountA;
            public byte[] AccountB;
            public CountingSink Sink;
            public SnapSyncClient.SyncResult Result;
        }

        private static async Task<Scenario> RunAsync()
        {
            var serverStore = new InMemoryContentNodeStore();
            var encoder = new AccountEncoder();

            Account MakeAccount(int balance) => new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)balance,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            };

            var accountA = Hash(0x05, 0x11);
            var accountB = Hash(0x80, 0x00);

            var stateTrie = new PatriciaTrie(serverStore);
            stateTrie.Put(accountA, encoder.Encode(MakeAccount(1)));
            stateTrie.Put(accountB, encoder.Encode(MakeAccount(2)));
            stateTrie.SaveDirtyNodesToStorage();
            var stateRoot = stateTrie.Root.GetHash();

            var handler = new PatriciaSnapRequestHandler(serverStore, new InMemoryBytecodeStore());
            var peer = new InProcessSnapPeer(handler);

            var clientNodeStore = new InMemoryContentNodeStore();
            var clientStateStore = new InMemoryStateStore();
            var realSink = new TrieSnapSyncSink(clientNodeStore, clientStateStore, flatWriter: null);
            var sink = new CountingSink(realSink);

            var client = new SnapSyncClient(peer, sink)
            {
                AccountConcurrency = 2,
            };

            var result = await client.SyncStateWithCheckpointAsync(
                stateRoot, resumeFrom: null, checkpointSink: _ => { });

            return new Scenario
            {
                AccountA = accountA,
                AccountB = accountB,
                Sink = sink,
                Result = result,
            };
        }

        [Fact]
        public async Task OverLimitBoundaryAccount_IsTrimmed_NotWrittenUnderCurrentTask()
        {
            var s = await RunAsync();

            Assert.Equal(1, s.Sink.WriteCount(s.AccountB));

            Assert.True(s.Result.RootMatchesTarget);
        }

        [Fact]
        public async Task InRangeAccount_IsUnaffected_WrittenExactlyOnce()
        {
            var s = await RunAsync();

            Assert.Equal(1, s.Sink.WriteCount(s.AccountA));
            Assert.True(s.Result.RootMatchesTarget);
        }
    }
}
