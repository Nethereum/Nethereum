using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Merkle.Patricia.Storage;
using Xunit;
using Nethereum.DevP2P.Sync.Snap.Sinks;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class TrieSnapSyncSinkCancellationTests
    {
        private static TrieSnapSyncSink NewSink()
            => new TrieSnapSyncSink(new InMemoryContentNodeStore(), new InMemoryStateStore(), flatWriter: null);

        private static byte[] Hash32(byte fill)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = fill;
            return h;
        }

        [Fact]
        public async Task WriteAccountAsync_AlreadyCancelledToken_ThrowsInsteadOfCommitting()
        {
            var sink = NewSink();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => sink.WriteAccountAsync(Hash32(0x11), null, cts.Token).AsTask());

            Assert.Equal(0, sink.AccountCount);
        }

        [Fact]
        public async Task WriteSlotAsync_AlreadyCancelledToken_ThrowsInsteadOfCommitting()
        {
            var sink = NewSink();
            using var cts = new CancellationTokenSource();
            var scope = await sink.BeginAccountStorageAsync(Hash32(0x22), null, CancellationToken.None);
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => scope.WriteSlotAsync(Hash32(0x33), null, cts.Token).AsTask());

            Assert.Equal(0, sink.SlotCount);
        }

        [Fact]
        public async Task StorageScope_EndAsync_AlreadyCancelledToken_ThrowsBeforeCommittingTrie()
        {
            var sink = NewSink();
            var scope = await sink.BeginAccountStorageAsync(Hash32(0x44), null, CancellationToken.None);
            await scope.WriteSlotAsync(Hash32(0x55), Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x01 }), CancellationToken.None);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => scope.EndAsync(cts.Token).AsTask());
        }
    }
}
