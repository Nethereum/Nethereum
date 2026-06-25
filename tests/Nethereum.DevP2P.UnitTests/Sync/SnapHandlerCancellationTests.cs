using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Sync
{
    /// <summary>
    /// Cancellation contract for snap/1 handlers
    /// (https://github.com/ethereum/devp2p/blob/master/caps/snap.md).
    /// Pre-cancelled <see cref="CancellationToken"/> on entry to any of the
    /// four handlers must propagate <see cref="OperationCanceledException"/>
    /// rather than being swallowed by the narrow outer catch.
    /// </summary>
    public class SnapHandlerCancellationTests
    {
        [Fact]
        public async Task Given_PreCancelledToken_When_AccountRangeInvoked_Then_OperationCanceledExceptionPropagates()
        {
            var f = SnapHandlerTestFixture.Build(accountCount: 8);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                handler.GetAccountRangeAsync(new GetAccountRangeMessage
                {
                    RequestId = 1,
                    RootHash = f.StateTrie.Root.GetHash(),
                    StartingHash = new byte[32],
                    LimitHash = SnapHandlerTestFixture.FilledHash(0xff),
                    ResponseBytes = 1_000_000UL
                }, cts.Token));
        }

        [Fact]
        public async Task Given_PreCancelledToken_When_StorageRangesInvoked_Then_OperationCanceledExceptionPropagates()
        {
            var f = SnapHandlerTestFixture.Build(accountCount: 8);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                handler.GetStorageRangesAsync(new GetStorageRangesMessage
                {
                    RequestId = 2,
                    RootHash = f.StateTrie.Root.GetHash(),
                    AccountHashes = f.Accounts.Select(a => a.hash).ToList(),
                    StartingHash = new byte[32],
                    LimitHash = SnapHandlerTestFixture.FilledHash(0xff),
                    ResponseBytes = 1_000_000UL
                }, cts.Token));
        }

        [Fact]
        public async Task Given_PreCancelledToken_When_ByteCodesInvoked_Then_OperationCanceledExceptionPropagates()
        {
            var f = SnapHandlerTestFixture.Build(accountCount: 4, withCode: 4);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                handler.GetByteCodesAsync(new GetByteCodesMessage
                {
                    RequestId = 3,
                    Hashes = f.KnownCodeHashes,
                    ResponseBytes = 1_000_000UL
                }, cts.Token));
        }

        [Fact]
        public async Task Given_PreCancelledToken_When_TrieNodesInvoked_Then_OperationCanceledExceptionPropagates()
        {
            var f = SnapHandlerTestFixture.Build(accountCount: 8);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                handler.GetTrieNodesAsync(new GetTrieNodesMessage
                {
                    RequestId = 4,
                    RootHash = f.StateTrie.Root.GetHash(),
                    Paths = new List<List<byte[]>> { new List<byte[]> { new byte[] { 0x00 } } },
                    ResponseBytes = 1_000_000UL
                }, cts.Token));
        }
    }
}
