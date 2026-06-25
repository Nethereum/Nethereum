using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync;
using Nethereum.Model.P2P.Snap;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Sync
{
    /// <summary>
    /// Server-side soft-cap clamp behaviour for snap/1
    /// (https://github.com/ethereum/devp2p/blob/master/caps/snap.md).
    /// Pins that <see cref="PatriciaSnapRequestHandler.SoftResponseLimit"/>
    /// dominates a peer-supplied <c>responseBytes</c> when the peer asks for
    /// more, while leaving smaller peer budgets untouched. Geth interop
    /// invariant: <c>eth/protocols/snap/handler.go:32</c>.
    /// </summary>
    public class SnapResponseLimitClampTests
    {
        [Fact]
        public async Task Given_PeerRequestsMaxUlongBytes_When_AccountRangeServed_Then_ResponseClampedToSoftLimit()
        {
            var f = SnapHandlerTestFixture.Build(accountCount: 32);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);

            var resp = await handler.GetAccountRangeAsync(new GetAccountRangeMessage
            {
                RequestId = 1,
                RootHash = f.StateTrie.Root.GetHash(),
                StartingHash = new byte[32],
                LimitHash = SnapHandlerTestFixture.FilledHash(0xff),
                ResponseBytes = ulong.MaxValue
            });

            var totalBytes = resp.Accounts.Sum(a => 32 + a.Body.Length);
            Assert.True(totalBytes <= PatriciaSnapRequestHandler.SoftResponseLimit + 256,
                $"totalBytes {totalBytes} must not exceed SoftResponseLimit + one-entry overshoot");
        }

        [Fact]
        public async Task Given_PeerRequestsBudgetBelowSoftLimit_When_AccountRangeServed_Then_BudgetHonoured()
        {
            var f = SnapHandlerTestFixture.Build(accountCount: 64);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);

            var resp = await handler.GetAccountRangeAsync(new GetAccountRangeMessage
            {
                RequestId = 2,
                RootHash = f.StateTrie.Root.GetHash(),
                StartingHash = new byte[32],
                LimitHash = SnapHandlerTestFixture.FilledHash(0xff),
                ResponseBytes = 1024UL
            });

            Assert.InRange(resp.Accounts.Count, 1, f.Accounts.Count - 1);
        }

        [Fact]
        public async Task Given_PeerRequestsZeroBytes_When_AccountRangeServed_Then_ExactlyOneEntryReturned()
        {
            var f = SnapHandlerTestFixture.Build(accountCount: 32);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);

            var resp = await handler.GetAccountRangeAsync(new GetAccountRangeMessage
            {
                RequestId = 3,
                RootHash = f.StateTrie.Root.GetHash(),
                StartingHash = new byte[32],
                LimitHash = SnapHandlerTestFixture.FilledHash(0xff),
                ResponseBytes = 0UL
            });

            Assert.Single(resp.Accounts);
        }

        [Fact]
        public async Task Given_PeerRequestsMaxUlongBytes_When_StorageRangesServed_Then_ResponseClampedToSoftLimit()
        {
            var f = SnapHandlerTestFixture.Build(accountCount: 8);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);

            var resp = await handler.GetStorageRangesAsync(new GetStorageRangesMessage
            {
                RequestId = 4,
                RootHash = f.StateTrie.Root.GetHash(),
                AccountHashes = f.Accounts.Select(a => a.hash).ToList(),
                StartingHash = new byte[32],
                LimitHash = SnapHandlerTestFixture.FilledHash(0xff),
                ResponseBytes = ulong.MaxValue
            });

            var totalSlotBytes = resp.Slots.Sum(slots => slots.Sum(s => 32 + s.Data.Length));
            var hardLimit = (long)(PatriciaSnapRequestHandler.SoftResponseLimit
                                   * (1.0 + PatriciaSnapRequestHandler.StateLookupSlack));
            Assert.True(totalSlotBytes <= hardLimit + 256,
                $"storage totalSlotBytes {totalSlotBytes} must not exceed soft*1.1 + overshoot");
        }

        [Fact]
        public async Task Given_PeerRequestsMaxUlongBytes_When_ByteCodesServed_Then_ResponseClampedToSoftLimit()
        {
            var f = SnapHandlerTestFixture.Build(accountCount: 16, withCode: 16);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);

            var resp = await handler.GetByteCodesAsync(new GetByteCodesMessage
            {
                RequestId = 5,
                Hashes = f.KnownCodeHashes,
                ResponseBytes = ulong.MaxValue
            });

            var totalCodeBytes = resp.Codes.Sum(c => c.Length);
            Assert.True(totalCodeBytes <= PatriciaSnapRequestHandler.SoftResponseLimit,
                $"byte codes totalCodeBytes {totalCodeBytes} must not exceed SoftResponseLimit");
        }

        [Fact]
        public async Task Given_PeerRequestsMaxUlongBytes_When_TrieNodesServed_Then_ResponseClampedToSoftLimit()
        {
            var f = SnapHandlerTestFixture.Build(accountCount: 16);
            var handler = new PatriciaSnapRequestHandler(f.TrieStorage, f.Bytecodes);

            var pathsets = new List<List<byte[]>>();
            for (int i = 0; i < 64; i++)
                pathsets.Add(new List<byte[]> { new byte[] { 0x00 } });

            var resp = await handler.GetTrieNodesAsync(new GetTrieNodesMessage
            {
                RequestId = 6,
                RootHash = f.StateTrie.Root.GetHash(),
                Paths = pathsets,
                ResponseBytes = ulong.MaxValue
            });

            var totalNodeBytes = resp.Nodes.Sum(n => n.Length);
            Assert.True(totalNodeBytes <= PatriciaSnapRequestHandler.SoftResponseLimit + 4096,
                $"trie nodes totalNodeBytes {totalNodeBytes} must not exceed SoftResponseLimit + overshoot");
        }
    }
}
