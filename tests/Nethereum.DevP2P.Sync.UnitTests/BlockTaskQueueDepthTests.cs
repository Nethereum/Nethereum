using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.CoreChain;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Xunit;
using Nethereum.DevP2P.Sync.FullSync;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class BlockTaskQueueDepthTests
    {
        private static BlockTaskQueue NewQueue(int maxInFlightPerPeer, ulong initialCursor = 0) =>
            new BlockTaskQueue(PatriciaBlockRootsProvider.Instance, initialCursor, activations: null,
                maxInFlightPerPeer: maxInFlightPerPeer);

        [Fact]
        public void ReserveBodies_AllowsUpToDepthConcurrentReservations_ThenRefuses()
        {
            var queue = NewQueue(maxInFlightPerPeer: 3);
            for (ulong n = 0; n < 10; n++)
                queue.EnqueueHeader(MakeHeader((long)n), MakeHash((long)n));

            var peer = Guid.NewGuid();
            var r1 = queue.ReserveBodies(peer, capacity: 2);
            var r2 = queue.ReserveBodies(peer, capacity: 2);
            var r3 = queue.ReserveBodies(peer, capacity: 2);
            Assert.Equal(2, r1.Count);
            Assert.Equal(2, r2.Count);
            Assert.Equal(2, r3.Count);

            var r4 = queue.ReserveBodies(peer, capacity: 2);
            Assert.Equal(0, r4.Count);

            var all = r1.Hashes.Concat(r2.Hashes).Concat(r3.Hashes).Select(h => h.ToHex()).ToList();
            Assert.Equal(all.Count, all.Distinct().Count());
        }

        [Fact]
        public void DeliverBodies_FreesOnlyThatReservationsSlot_OthersRemainInFlight()
        {
            var queue = NewQueue(maxInFlightPerPeer: 2);
            for (ulong n = 0; n < 4; n++)
                queue.EnqueueHeader(MakeHeader((long)n), MakeHash((long)n));

            var peer = Guid.NewGuid();
            var r1 = queue.ReserveBodies(peer, capacity: 2);
            var r2 = queue.ReserveBodies(peer, capacity: 2);
            Assert.Equal(2, r1.Count);
            Assert.Equal(2, r2.Count);
            Assert.Equal(0, queue.ReserveBodies(peer, capacity: 2).Count);

            var result = queue.DeliverBodies(r1, new List<BlockBody> { new BlockBody(), new BlockBody() });
            Assert.Equal(2, result.Matched);

            var afterFree = queue.ReserveBodies(peer, capacity: 2);
            Assert.Equal(0, afterFree.Count);

            var r2Result = queue.DeliverBodies(r2, new List<BlockBody> { new BlockBody(), new BlockBody() });
            Assert.Equal(2, r2Result.Matched);
            Assert.Equal(0, r2Result.Unmatched);
        }

        [Fact]
        public void ReleaseBodyReservation_RevertsOnlyThatReservation_LeavesOthersReserved()
        {
            var queue = NewQueue(maxInFlightPerPeer: 2);
            for (ulong n = 0; n < 4; n++)
                queue.EnqueueHeader(MakeHeader((long)n), MakeHash((long)n));

            var peer = Guid.NewGuid();
            var r1 = queue.ReserveBodies(peer, capacity: 2);
            var r2 = queue.ReserveBodies(peer, capacity: 2);

            queue.ReleaseBodyReservation(r1);

            var other = Guid.NewGuid();
            var rescue = queue.ReserveBodies(other, capacity: 4);
            Assert.Equal(2, rescue.Count);
            var rescued = rescue.Hashes.Select(h => h.ToHex()).ToHashSet();
            var r1Hashes = r1.Hashes.Select(h => h.ToHex()).ToHashSet();
            Assert.True(rescued.SetEquals(r1Hashes));

            var r2Result = queue.DeliverBodies(r2, new List<BlockBody> { new BlockBody(), new BlockBody() });
            Assert.Equal(2, r2Result.Matched);
        }

        [Fact]
        public void ReserveReceipts_AllowsDepthN_AndReleaseReceiptReservationFreesOneSlot()
        {
            var queue = NewQueue(maxInFlightPerPeer: 2);
            for (ulong n = 0; n < 4; n++)
                queue.EnqueueHeader(MakeHeader((long)n), MakeHash((long)n));

            var peer = Guid.NewGuid();
            var r1 = queue.ReserveReceipts(peer, capacity: 2);
            var r2 = queue.ReserveReceipts(peer, capacity: 2);
            Assert.Equal(2, r1.Count);
            Assert.Equal(2, r2.Count);
            Assert.Equal(0, queue.ReserveReceipts(peer, capacity: 2).Count);

            queue.ReleaseReceiptReservation(r1);

            var other = Guid.NewGuid();
            var rescue = queue.ReserveReceipts(other, capacity: 4);
            Assert.Equal(2, rescue.Count);
        }

        [Fact]
        public void ReleasePeer_AtDepth_ClearsEverySlot_PeerCanReReserveToFullDepth()
        {
            var queue = NewQueue(maxInFlightPerPeer: 3);
            for (ulong n = 0; n < 10; n++)
                queue.EnqueueHeader(MakeHeader((long)n), MakeHash((long)n));

            var peer = Guid.NewGuid();
            queue.ReserveBodies(peer, capacity: 2);
            queue.ReserveBodies(peer, capacity: 2);
            queue.ReserveBodies(peer, capacity: 2);
            Assert.Equal(0, queue.ReserveBodies(peer, capacity: 2).Count);

            queue.ReleasePeer(peer);

            Assert.Equal(2, queue.ReserveBodies(peer, capacity: 2).Count);
            Assert.Equal(2, queue.ReserveBodies(peer, capacity: 2).Count);
            Assert.Equal(2, queue.ReserveBodies(peer, capacity: 2).Count);
            Assert.Equal(0, queue.ReserveBodies(peer, capacity: 2).Count);
        }

        [Fact]
        public void DeliverBodies_FreesSlot_PeerReReservesNewlyEnqueuedWork()
        {
            var queue = NewQueue(maxInFlightPerPeer: 1);
            queue.EnqueueHeader(MakeHeader(0), MakeHash(0));
            queue.EnqueueHeader(MakeHeader(1), MakeHash(1));

            var peer = Guid.NewGuid();
            var r1 = queue.ReserveBodies(peer, capacity: 2);
            Assert.Equal(2, r1.Count);
            Assert.Equal(0, queue.ReserveBodies(peer, capacity: 2).Count);

            queue.DeliverBodies(r1, new List<BlockBody> { new BlockBody(), new BlockBody() });

            queue.EnqueueHeader(MakeHeader(2), MakeHash(2));
            Assert.Equal(1, queue.ReserveBodies(peer, capacity: 2).Count);
        }

        [Fact]
        public void ReserveReceipts_ConcurrentReservationsAreDisjoint()
        {
            var queue = NewQueue(maxInFlightPerPeer: 2);
            for (ulong n = 0; n < 6; n++)
                queue.EnqueueHeader(MakeHeader((long)n), MakeHash((long)n));

            var peer = Guid.NewGuid();
            var r1 = queue.ReserveReceipts(peer, capacity: 3);
            var r2 = queue.ReserveReceipts(peer, capacity: 3);
            Assert.Equal(3, r1.Count);
            Assert.Equal(3, r2.Count);
            var a = r1.Hashes.Select(h => h.ToHex()).ToHashSet();
            var b = r2.Hashes.Select(h => h.ToHex()).ToHashSet();
            Assert.Empty(a.Intersect(b));
        }

        [Fact]
        public void EmptyReservation_ConsumesNoSlot()
        {
            var queue = NewQueue(maxInFlightPerPeer: 1);
            var peer = Guid.NewGuid();
            Assert.Equal(0, queue.ReserveBodies(peer, capacity: 2).Count);

            queue.EnqueueHeader(MakeHeader(0), MakeHash(0));
            queue.EnqueueHeader(MakeHeader(1), MakeHash(1));
            Assert.Equal(2, queue.ReserveBodies(peer, capacity: 2).Count);
        }

        [Fact]
        public void DefaultQueue_StillOneInFlight()
        {
            var queue = new BlockTaskQueue(PatriciaBlockRootsProvider.Instance, 0);
            for (ulong n = 0; n < 4; n++)
                queue.EnqueueHeader(MakeHeader((long)n), MakeHash((long)n));

            var peer = Guid.NewGuid();
            Assert.Equal(2, queue.ReserveBodies(peer, capacity: 2).Count);
            Assert.Equal(0, queue.ReserveBodies(peer, capacity: 2).Count);
        }

        private static BlockHeader MakeHeader(long blockNumber) => new BlockHeader
        {
            BlockNumber = blockNumber,
            ParentHash = new byte[32],
            TransactionsHash = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray(),
            UnclesHash = "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray(),
            ReceiptHash = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray(),
            StateRoot = new byte[32],
            LogsBloom = new byte[256],
            Coinbase = "0x0000000000000000000000000000000000000000",
            Difficulty = 0, GasLimit = 0, GasUsed = 0, Timestamp = 0,
            ExtraData = Array.Empty<byte>(), MixHash = new byte[32], Nonce = new byte[8],
        };

        private static byte[] MakeHash(long blockNumber)
        {
            var h = new byte[32];
            h[0] = (byte)(blockNumber & 0xff);
            h[1] = (byte)((blockNumber >> 8) & 0xff);
            return h;
        }
    }
}
