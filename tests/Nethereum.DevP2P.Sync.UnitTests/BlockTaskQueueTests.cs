using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.CoreChain;
using Nethereum.DevP2P.Sync;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class BlockTaskQueueTests
    {
        private static readonly byte[] EmptyUnclesHash =
            "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray();
        private static readonly byte[] EmptyTrieRoot =
            "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();

        private static BlockTaskQueue NewQueue(ulong initialCursor = 0) =>
            new BlockTaskQueue(PatriciaBlockRootsProvider.Instance, initialCursor);

        [Fact]
        public void EnqueueHeader_AdvancesPendingCount()
        {
            var queue = NewQueue();
            queue.EnqueueHeader(MakeHeader(0), MakeHash(0));
            queue.EnqueueHeader(MakeHeader(1), MakeHash(1));
            queue.EnqueueHeader(MakeHeader(2), MakeHash(2));
            Assert.Equal(3, queue.Pending);
        }

        [Fact]
        public void EnqueueHeader_IsIdempotentOnBlockNumber()
        {
            var queue = NewQueue();
            queue.EnqueueHeader(MakeHeader(0), MakeHash(0));
            queue.EnqueueHeader(MakeHeader(0), MakeHash(0));
            Assert.Equal(1, queue.Pending);
        }

        [Fact]
        public void ReserveBodies_HonoursOneInFlightInvariantPerPeer()
        {
            var queue = NewQueue();
            for (ulong n = 0; n < 5; n++)
                queue.EnqueueHeader(MakeHeader((long)n), MakeHash((long)n));

            var peer = Guid.NewGuid();
            var first = queue.ReserveBodies(peer, capacity: 2);
            Assert.Equal(2, first.Count);

            // While the first reservation is still open, a second attempt from
            // the SAME peer must return empty — the queue enforces one open
            // reservation per (peer, stage).
            var second = queue.ReserveBodies(peer, capacity: 2);
            Assert.Equal(0, second.Count);
        }

        [Fact]
        public void ReserveBodies_DifferentPeersCanReserveDifferentBatches()
        {
            var queue = NewQueue();
            for (ulong n = 0; n < 6; n++)
                queue.EnqueueHeader(MakeHeader((long)n), MakeHash((long)n));

            var peerA = Guid.NewGuid();
            var peerB = Guid.NewGuid();
            var a = queue.ReserveBodies(peerA, capacity: 3);
            var b = queue.ReserveBodies(peerB, capacity: 3);

            Assert.Equal(3, a.Count);
            Assert.Equal(3, b.Count);
            // Reservations must not overlap — peer B gets the next 3 blocks.
            var aHashes = a.Hashes.Select(h => h.ToHex()).ToHashSet();
            var bHashes = b.Hashes.Select(h => h.ToHex()).ToHashSet();
            Assert.Empty(aHashes.Intersect(bHashes));
        }

        [Fact]
        public void ReserveBodies_WalksOldestBlockFirst()
        {
            var queue = NewQueue();
            for (ulong n = 0; n < 4; n++)
                queue.EnqueueHeader(MakeHeader((long)n), MakeHash((long)n));

            var reservation = queue.ReserveBodies(Guid.NewGuid(), capacity: 4);
            var numbers = reservation.Headers
                .Select(h => (long)h.BlockNumber.ToBigInteger())
                .ToArray();
            Assert.Equal(new long[] { 0, 1, 2, 3 }, numbers);
        }

        [Fact]
        public void DeliverBodies_MatchesByContentRegardlessOfReturnOrder()
        {
            var queue = NewQueue();
            // Headers that all have the empty TxRoot + UnclesHash — content
            // matching pairs them with empty bodies via (root, unclesHash).
            for (ulong n = 0; n < 3; n++)
                queue.EnqueueHeader(MakeHeader((long)n), MakeHash((long)n));

            var peer = Guid.NewGuid();
            var reservation = queue.ReserveBodies(peer, capacity: 3);

            // Deliver three empty bodies in REVERSE order of the reservation —
            // content-addressed matching must still resolve each header.
            var bodies = new List<BlockBody>
            {
                new BlockBody(),
                new BlockBody(),
                new BlockBody()
            };
            var result = queue.DeliverBodies(reservation, bodies);

            Assert.Equal(3, result.Matched);
            Assert.Equal(0, result.Unmatched);
        }

        [Fact]
        public void DeliverBodies_PartialResponseRevertsMissingHeaders()
        {
            var queue = NewQueue();
            queue.EnqueueHeader(MakeHeader(0), MakeHash(0));
            queue.EnqueueHeader(WithTxRoot(MakeHeader(1), HexBytes("aa", 32)), MakeHash(1));
            queue.EnqueueHeader(MakeHeader(2), MakeHash(2));

            var peer = Guid.NewGuid();
            var reservation = queue.ReserveBodies(peer, capacity: 3);
            // Only deliver the two empty-root bodies. Block 1 wants a body
            // whose TxRoot is 0xaa…, which the peer didn't return.
            var bodies = new List<BlockBody>
            {
                new BlockBody(),
                new BlockBody()
            };
            var result = queue.DeliverBodies(reservation, bodies);

            // Empty-root bodies fan out into one bucket → both block 0 and
            // block 2 can pull from it. Block 1's distinct root means it
            // gets no match and reverts.
            Assert.Equal(2, result.Matched);
            Assert.Equal(1, result.Unmatched);
        }

        [Fact]
        public void DeliverBodies_LackingPeerIsSkippedOnNextReservation()
        {
            var queue = NewQueue();
            queue.EnqueueHeader(WithTxRoot(MakeHeader(0), HexBytes("aa", 32)), MakeHash(0));

            var badPeer = Guid.NewGuid();
            var reservation = queue.ReserveBodies(badPeer, capacity: 1);
            Assert.Equal(1, reservation.Count);

            // Peer returns no body — block 0 reverts, peer joins lacking set.
            queue.DeliverBodies(reservation, new List<BlockBody>());

            // Same peer tries again — must be denied the block it already
            // failed on, even though it's the only block in the queue.
            var retry = queue.ReserveBodies(badPeer, capacity: 1);
            Assert.Equal(0, retry.Count);

            // A different peer is allowed to pick it up.
            var goodPeer = Guid.NewGuid();
            var rescue = queue.ReserveBodies(goodPeer, capacity: 1);
            Assert.Equal(1, rescue.Count);
        }

        [Fact]
        public void DequeuePersistable_AdvancesCursorOverContiguousReadyPrefixOnly()
        {
            var queue = NewQueue();
            for (ulong n = 0; n < 4; n++)
                queue.EnqueueHeader(MakeHeader((long)n), MakeHash((long)n));

            // Walk oldest-first with capacity=1 so each peer gets exactly
            // one block. Block 2 is parked (Reserved-but-undelivered) so
            // the queue treats it as not ready while 0, 1, and 3 are
            // fully delivered out of cursor order.
            DeliverSingle(queue, expectedBlock: 0);
            DeliverSingle(queue, expectedBlock: 1);
            var parkBody = queue.ReserveBodies(Guid.NewGuid(), capacity: 1);
            var parkReceipt = queue.ReserveReceipts(Guid.NewGuid(), capacity: 1);
            Assert.Equal(1, parkBody.Count);
            Assert.Equal(2UL, (ulong)parkBody.Headers[0].BlockNumber.ToBigInteger());
            DeliverSingle(queue, expectedBlock: 3);

            // The drain must stop at the cursor's first non-ready slot.
            var drained = queue.DequeuePersistable(maxCount: 10);
            Assert.Equal(2, drained.Count);
            Assert.Equal(0UL, drained[0].BlockNumber);
            Assert.Equal(1UL, drained[1].BlockNumber);
            Assert.Equal(2UL, queue.PersistCursor);

            // Block 3 sits in storage but the drain refuses to skip 2.
            // Releasing the parking peer reverts block 2 to Pending so a
            // real worker can pick it up; deliver it and the rest flushes.
            queue.ReleasePeer(parkBody.PeerId);
            queue.ReleasePeer(parkReceipt.PeerId);
            DeliverSingle(queue, expectedBlock: 2);

            var drained2 = queue.DequeuePersistable(maxCount: 10);
            Assert.Equal(2, drained2.Count);
            Assert.Equal(2UL, drained2[0].BlockNumber);
            Assert.Equal(3UL, drained2[1].BlockNumber);
            Assert.Equal(4UL, queue.PersistCursor);
        }

        [Fact]
        public void DequeuePersistable_RespectsInitialCursor()
        {
            // Resume case: start at block 1000, never persist below it.
            var queue = NewQueue(initialCursor: 1000);
            queue.EnqueueHeader(MakeHeader(1000), MakeHash(1000));
            DeliverSingle(queue, expectedBlock: 1000);

            var drained = queue.DequeuePersistable(maxCount: 10);
            Assert.Single(drained);
            Assert.Equal(1000UL, drained[0].BlockNumber);
            Assert.Equal(1001UL, queue.PersistCursor);
        }

        [Fact]
        public void ReleasePeer_RevertsAllItsReservationsToPending()
        {
            var queue = NewQueue();
            for (ulong n = 0; n < 4; n++)
                queue.EnqueueHeader(MakeHeader((long)n), MakeHash((long)n));

            var peer = Guid.NewGuid();
            var bodies = queue.ReserveBodies(peer, capacity: 4);
            var receipts = queue.ReserveReceipts(peer, capacity: 4);
            Assert.Equal(4, bodies.Count);
            Assert.Equal(4, receipts.Count);

            queue.ReleasePeer(peer);

            // Another peer should now see all 4 tasks pending again.
            var other = Guid.NewGuid();
            var rescuedBodies = queue.ReserveBodies(other, capacity: 4);
            var rescuedReceipts = queue.ReserveReceipts(other, capacity: 4);
            Assert.Equal(4, rescuedBodies.Count);
            Assert.Equal(4, rescuedReceipts.Count);
        }

        [Fact]
        public void DeliverReceipts_PostByzantium_MatchesByReceiptRoot()
        {
            var queue = NewQueue();
            queue.EnqueueHeader(MakeHeader(0), MakeHash(0));

            var peer = Guid.NewGuid();
            var reservation = queue.ReserveReceipts(peer, capacity: 1);
            var result = queue.DeliverReceipts(reservation, new List<List<Receipt>>
            {
                new List<Receipt>()  // empty list → root = EmptyTrieRoot, matches header.ReceiptHash
            });

            Assert.Equal(1, result.Matched);
            Assert.Equal(0, result.Unmatched);
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        // Reserves a single block via capacity:1 (oldest pending) and
        // delivers an empty body + empty receipt list to it. Asserts the
        // reservation picked up the block the caller expected — keeps
        // the test brittle to ordering bugs in ReserveBodies/ReserveReceipts.
        private static void DeliverSingle(BlockTaskQueue queue, ulong expectedBlock)
        {
            var bodyPeer = Guid.NewGuid();
            var bodyRes = queue.ReserveBodies(bodyPeer, capacity: 1);
            Assert.Equal(1, bodyRes.Count);
            Assert.Equal(expectedBlock, (ulong)bodyRes.Headers[0].BlockNumber.ToBigInteger());
            queue.DeliverBodies(bodyRes, new List<BlockBody> { new BlockBody() });

            var receiptPeer = Guid.NewGuid();
            var receiptRes = queue.ReserveReceipts(receiptPeer, capacity: 1);
            Assert.Equal(1, receiptRes.Count);
            Assert.Equal(expectedBlock, (ulong)receiptRes.Headers[0].BlockNumber.ToBigInteger());
            queue.DeliverReceipts(receiptRes, new List<List<Receipt>> { new List<Receipt>() });
        }

        private static BlockHeader MakeHeader(long blockNumber)
        {
            return new BlockHeader
            {
                BlockNumber = new EvmUInt256((ulong)blockNumber),
                ParentHash = new byte[32],
                TransactionsHash = (byte[])EmptyTrieRoot.Clone(),
                UnclesHash = (byte[])EmptyUnclesHash.Clone(),
                ReceiptHash = (byte[])EmptyTrieRoot.Clone(),
                StateRoot = new byte[32],
                Difficulty = new EvmUInt256(1UL),
                GasLimit = 1,
                Timestamp = 1,
                ExtraData = Array.Empty<byte>(),
                MixHash = new byte[32],
                Nonce = new byte[8],
                LogsBloom = new byte[256],
                Coinbase = "0x0000000000000000000000000000000000000000",
            };
        }

        private static BlockHeader WithTxRoot(BlockHeader h, byte[] root)
        {
            h.TransactionsHash = root;
            return h;
        }

        private static byte[] MakeHash(long blockNumber)
        {
            var hash = new byte[32];
            BitConverter.GetBytes(blockNumber).CopyTo(hash, 0);
            return hash;
        }

        private static byte[] HexBytes(string fillHex, int length)
        {
            var b = new byte[length];
            var fill = (byte)Convert.ToInt32(fillHex, 16);
            for (int i = 0; i < length; i++) b[i] = fill;
            return b;
        }
    }
}
