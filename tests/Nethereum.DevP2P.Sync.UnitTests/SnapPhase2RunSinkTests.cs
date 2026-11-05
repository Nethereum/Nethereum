using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.Model.P2P.Snap;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapPhase2RunSinkTests
    {
        private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

        private sealed class KeepsNothingSink : ISnapSyncSink
        {
            public ValueTask BeginAsync(byte[] targetRoot, CancellationToken ct) => default;
            public ValueTask WriteAccountAsync(byte[] accountHash, byte[] slimRlp, CancellationToken ct) => default;
            public ValueTask<IStorageScope> BeginAccountStorageAsync(byte[] accountHash, byte[] expectedStorageRoot, CancellationToken ct)
                => throw new NotSupportedException();
            public ValueTask WriteBytecodeAsync(byte[] codeHash, byte[] code, CancellationToken ct) => default;
            public ValueTask<byte[]> FinaliseRootAsync(CancellationToken ct) => new(new byte[32]);
        }

        private sealed class CountingBlockingPeer : ISnapPeer
        {
            private int _accountRangeCalls;

            public int AccountRangeCalls => Volatile.Read(ref _accountRangeCalls);

            public async Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
            {
                Interlocked.Increment(ref _accountRangeCalls);
                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                return null;
            }

            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
                => throw new NotSupportedException();
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
                => throw new NotSupportedException();
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => throw new NotSupportedException();
        }

        private static byte[] Root()
        {
            var r = new byte[32];
            r[0] = 0x01;
            return r;
        }

        [Fact]
        public async Task Given_ASinkWithNeitherNodeStoreNorFlatWriter_When_Phase2Starts_Then_ItFailsBeforeTheFirstRequest()
        {
            var peer = new CountingBlockingPeer();
            var client = new SnapSyncClient(peer, new KeepsNothingSink());
            using var cts = new CancellationTokenSource(Bound);

            var ex = await Assert.ThrowsAsync<NotSupportedException>(
                () => client.SyncStateAsync(Root(), resumeFrom: null, checkpointSink: null, ct: cts.Token));

            Assert.Contains(nameof(KeepsNothingSink), ex.Message);
            Assert.Equal(0, peer.AccountRangeCalls);
        }

        [Fact]
        public async Task Given_ASinkThatKeepsATrieNodeStore_When_Phase2Starts_Then_TheFirstRequestIsIssued()
        {
            var peer = new CountingBlockingPeer();
            var client = new SnapSyncClient(peer, new InMemorySnapSyncSink());
            using var cts = new CancellationTokenSource(Bound);

            var run = client.SyncStateAsync(Root(), resumeFrom: null, checkpointSink: null, ct: cts.Token);
            while (peer.AccountRangeCalls == 0 && !run.IsCompleted && !cts.IsCancellationRequested)
                await Task.Delay(10);
            var requested = peer.AccountRangeCalls;
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
            Assert.True(requested > 0);
        }
    }
}
