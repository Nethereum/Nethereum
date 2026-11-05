using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;


namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapPhase2WatchdogOwnerTests
    {
        private static CancellationTokenSource Within(int seconds) => new(TimeSpan.FromSeconds(seconds));

        private static byte[] Fill(byte b) => Enumerable.Repeat(b, 32).ToArray();

        [Fact]
        public async Task Given_EveryPeerStateless_When_Phase2RunsPastTheStallTimeout_Then_ExactlyOneTaskSetStalledSourceFiresTheWatchdog()
        {
            var store = new InMemoryContentNodeStore();
            var stateRoot = BuildWhaleState(store, Fill(0x70), count: 30);
            var handler = new PatriciaSnapRequestHandler(store, new ServedState.CodeBook());
            var peer = new HangingAfterFirstStoragePeer(new InProcessSnapPeer(handler));
            var sink = new TrieSnapSyncSink(new InMemoryContentNodeStore(), new InMemoryStateStore(), flatWriter: null);
            var log = new RecordingLogger<SnapSyncClient>();
            var client = new SnapSyncClient(peer, sink, logger: log, responseBytesBudget: 300UL)
            {
                AccountConcurrency = 3,
                LargeContractConcurrency = 1,
                TaskSetStallTimeout = TimeSpan.FromSeconds(12),
                ActiveLeaseStallTimeout = TimeSpan.FromMinutes(5),
            };
            using var cts = Within(60);

            var sync = client.SyncStateAsync(stateRoot, null, null, cts.Token);
            await peer.Hung.Task.WaitAsync(cts.Token);
            await Task.Delay(TimeSpan.FromMilliseconds(1500));
            client.TaskSetStallTimeout = TimeSpan.FromMilliseconds(300);
            var stalled = await Assert.ThrowsAsync<SnapSyncClient.SnapTaskSetStalledException>(() => sync);

            Assert.Equal(-1, stalled.Consumer);
            Assert.Equal(0, log.Count("snap.taskset.stalled consumer="));
            Assert.Equal(1, log.Count("snap.phase2.supervisor.taskset_stalled"));
        }

        private static byte[] BuildWhaleState(InMemoryContentNodeStore store, byte[] owner, int count)
        {
            var keccak = new Sha3Keccack();
            var storageTrie = new PatriciaTrie(store, owner);
            for (int i = 0; i < count; i++)
                storageTrie.Put(keccak.CalculateHash(new[] { (byte)i }), new byte[] { (byte)(i & 0xff), 0xCD });
            storageTrie.SaveDirtyNodesToStorage();
            var stateTrie = new PatriciaTrie(store);
            stateTrie.Put(owner, new AccountEncoder().Encode(new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)1,
                StateRoot = storageTrie.Root.GetHash(),
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            }));
            stateTrie.SaveDirtyNodesToStorage();
            return stateTrie.Root.GetHash();
        }

        private sealed class HangingAfterFirstStoragePeer : ISnapPeer
        {
            private readonly ISnapPeer _inner;
            private int _storageCalls;
            public TaskCompletionSource Hung { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public HangingAfterFirstStoragePeer(ISnapPeer inner) => _inner = inner;

            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
                => _inner.GetAccountRangeAsync(r, ct);

            public async Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
            {
                if (Interlocked.Increment(ref _storageCalls) > 1)
                {
                    Hung.TrySetResult();
                    await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                }
                return await _inner.GetStorageRangesAsync(r, ct).ConfigureAwait(false);
            }

            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
                => _inner.GetByteCodesAsync(r, ct);

            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => _inner.GetTrieNodesAsync(r, ct);
        }
    }
}
