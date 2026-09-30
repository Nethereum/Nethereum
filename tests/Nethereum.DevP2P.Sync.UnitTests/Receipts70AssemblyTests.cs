using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.Model;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    // Exercises SyncPeerSession.AssembleReceipts70Async, the pure eth/70+ receipt-continuation
    // assembler lifted out of the wire path. eth/70 (EIP-7642's successor) truncates a Receipts
    // response along TWO independent axes, and both must be reassembled or blocks are silently lost:
    //   (a) per-BLOCK   — the final block's receipts overflow one message (lastBlockIncomplete=true),
    //                     resumed next round from FirstBlockReceiptIndex;
    //   (b) per-BATCH   — the whole ~10MB message is capped at a block BOUNDARY, so fewer blocks than
    //                     requested return with lastBlockIncomplete=false and the rest must be re-fetched.
    // Axis (b) was the live defect: the assembler returned on lastBlockIncomplete=false, abandoning the
    // un-served remainder of every size-capped batch (~60% of a 96-block receipt request on mainnet).
    public class Receipts70AssemblyTests
    {
        private const int NoRoundCap = 1000;

        private sealed class SizeCappedReceiptPeer
        {
            private readonly IReadOnlyList<List<Receipt>> _blocks;
            private readonly int _maxBlocksPerResponse;
            private readonly int _maxReceiptsPerResponse;

            public int RoundCount { get; private set; }

            public SizeCappedReceiptPeer(IReadOnlyList<List<Receipt>> blocks, int maxBlocksPerResponse, int maxReceiptsPerResponse)
            {
                _blocks = blocks;
                _maxBlocksPerResponse = maxBlocksPerResponse;
                _maxReceiptsPerResponse = maxReceiptsPerResponse;
            }

            public Task<(IReadOnlyList<List<Receipt>> entries, bool lastBlockIncomplete)> FetchAsync(int pos, ulong firstBlockReceiptIndex)
            {
                RoundCount++;
                var entries = new List<List<Receipt>>();
                int receiptsServed = 0;
                bool lastBlockIncomplete = false;

                for (int b = pos; b < _blocks.Count; b++)
                {
                    if (entries.Count >= _maxBlocksPerResponse) break;
                    if (receiptsServed >= _maxReceiptsPerResponse) break;

                    var source = _blocks[b];
                    var served = new List<Receipt>();
                    int start = (b == pos) ? (int)firstBlockReceiptIndex : 0;
                    for (int i = start; i < source.Count; i++)
                    {
                        if (receiptsServed >= _maxReceiptsPerResponse) { lastBlockIncomplete = true; break; }
                        served.Add(source[i]);
                        receiptsServed++;
                    }
                    entries.Add(served);
                    if (lastBlockIncomplete) break;
                }

                return Task.FromResult(((IReadOnlyList<List<Receipt>>)entries, lastBlockIncomplete));
            }
        }

        private static List<Receipt> BlockOf(int receiptCount)
        {
            var list = new List<Receipt>(receiptCount);
            for (int r = 0; r < receiptCount; r++) list.Add(new Receipt());
            return list;
        }

        private static void AssertReconstructs(IReadOnlyList<List<Receipt>> expected, IReadOnlyList<List<Receipt>> actual)
        {
            Assert.Equal(expected.Count, actual.Count);
            for (int b = 0; b < expected.Count; b++)
            {
                Assert.Equal(expected[b].Count, actual[b].Count);
                for (int r = 0; r < expected[b].Count; r++)
                    Assert.Same(expected[b][r], actual[b][r]);
            }
        }

        [Fact]
        public async Task Given_ResponseSizeCappedAtABlockBoundary_When_MoreBlocksRemainUnserved_Then_TheRemainderIsReFetchedAndAllBlocksAssembled()
        {
            var blocks = new List<List<Receipt>> { BlockOf(3), BlockOf(3), BlockOf(3), BlockOf(3) };
            var peer = new SizeCappedReceiptPeer(blocks, maxBlocksPerResponse: 2, maxReceiptsPerResponse: int.MaxValue);

            var assembled = await SyncPeerSession.AssembleReceipts70Async(blocks.Count, peer.FetchAsync, NoRoundCap);

            AssertReconstructs(blocks, assembled);
            Assert.Equal(2, peer.RoundCount);
        }

        [Fact]
        public async Task Given_FinalBlockReceiptsOverflowOneMessage_When_LastBlockIncomplete_Then_TheBlockIsResumedAndCompleted()
        {
            var blocks = new List<List<Receipt>> { BlockOf(5) };
            var peer = new SizeCappedReceiptPeer(blocks, maxBlocksPerResponse: int.MaxValue, maxReceiptsPerResponse: 2);

            var assembled = await SyncPeerSession.AssembleReceipts70Async(blocks.Count, peer.FetchAsync, NoRoundCap);

            AssertReconstructs(blocks, assembled);
            Assert.Equal(3, peer.RoundCount);
        }

        [Fact]
        public async Task Given_BothBatchAndPerBlockCapsBind_When_ResponsesTruncateOnEitherAxis_Then_EveryBlockIsFullyAssembledInOrder()
        {
            var blocks = new List<List<Receipt>> { BlockOf(4), BlockOf(1), BlockOf(5), BlockOf(2), BlockOf(3) };
            var peer = new SizeCappedReceiptPeer(blocks, maxBlocksPerResponse: 2, maxReceiptsPerResponse: 3);

            var assembled = await SyncPeerSession.AssembleReceipts70Async(blocks.Count, peer.FetchAsync, NoRoundCap);

            AssertReconstructs(blocks, assembled);
        }

        [Fact]
        public async Task Given_BlocksWithZeroReceipts_When_ServedUnderABatchCap_Then_EmptyBlocksAreAssembledAsDistinctEntriesInOrder()
        {
            var blocks = new List<List<Receipt>> { BlockOf(0), BlockOf(2), BlockOf(0), BlockOf(0), BlockOf(3) };
            var peer = new SizeCappedReceiptPeer(blocks, maxBlocksPerResponse: 2, maxReceiptsPerResponse: int.MaxValue);

            var assembled = await SyncPeerSession.AssembleReceipts70Async(blocks.Count, peer.FetchAsync, NoRoundCap);

            AssertReconstructs(blocks, assembled);
            Assert.Empty(assembled[0]);
            Assert.Empty(assembled[2]);
            Assert.Empty(assembled[3]);
        }

        [Fact]
        public async Task Given_APeerThatStopsServing_When_AnEmptyResponseArrives_Then_AssemblyStopsWithoutLooping()
        {
            var blocks = new List<List<Receipt>> { BlockOf(2), BlockOf(2) };
            int calls = 0;
            Func<int, ulong, Task<(IReadOnlyList<List<Receipt>> entries, bool lastBlockIncomplete)>> emptyFetch =
                (pos, firstIndex) =>
                {
                    calls++;
                    return Task.FromResult(((IReadOnlyList<List<Receipt>>)new List<List<Receipt>>(), false));
                };

            var assembled = await SyncPeerSession.AssembleReceipts70Async(blocks.Count, emptyFetch, maxRounds: 2);

            Assert.Empty(assembled);
            Assert.Equal(1, calls);
        }
    }
}
