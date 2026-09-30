using System;
using System.Collections.Generic;
using Nethereum.CoreChain;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Util;
using Xunit;
using Nethereum.DevP2P.Sync.FullSync;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class BlockTaskQueueLackingExpiryTests
    {
        private static readonly byte[] EmptyTrieRoot =
            "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();

        private static readonly byte[] EmptyUnclesHash =
            "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray();

        private static BlockTaskQueue NewQueue(Func<DateTime> utcNow, ulong initialCursor = 0) =>
            new BlockTaskQueue(PatriciaBlockRootsProvider.Instance, initialCursor, activations: null,
                maxInFlightPerPeer: 1, utcNow: utcNow);

        [Fact]
        public void Given_a_reservation_marked_lacking_When_the_TTL_elapses_Then_the_mark_expires_and_the_blocks_are_reassignable()
        {
            var now = DateTime.UtcNow;
            var queue = NewQueue(() => now);
            queue.EnqueueHeader(WithTxRoot(MakeHeader(0), HexBytes("aa", 32)), MakeHash(0));

            var badPeer = Guid.NewGuid();
            var reservation = queue.ReserveBodies(badPeer, capacity: 1);
            Assert.Equal(1, reservation.Count);

            queue.DeliverBodies(reservation, new List<BlockBody>());
            Assert.Equal(0, queue.ReserveBodies(badPeer, capacity: 1).Count);

            now = now.AddMinutes(2).AddSeconds(1);

            var retry = queue.ReserveBodies(badPeer, capacity: 1);
            Assert.Equal(1, retry.Count);
        }

        [Fact]
        public void Given_a_peer_genuinely_missing_a_range_When_re_requested_before_TTL_Then_it_is_not_re_asked()
        {
            var now = DateTime.UtcNow;
            var queue = NewQueue(() => now);
            queue.EnqueueHeader(WithTxRoot(MakeHeader(0), HexBytes("aa", 32)), MakeHash(0));

            var badPeer = Guid.NewGuid();
            var reservation = queue.ReserveBodies(badPeer, capacity: 1);
            Assert.Equal(1, reservation.Count);

            queue.DeliverBodies(reservation, new List<BlockBody>());

            now = now.AddMinutes(1);

            var retry = queue.ReserveBodies(badPeer, capacity: 1);
            Assert.Equal(0, retry.Count);

            var goodPeer = Guid.NewGuid();
            var rescue = queue.ReserveBodies(goodPeer, capacity: 1);
            Assert.Equal(1, rescue.Count);
        }

        [Fact]
        public void Given_a_peer_released_When_it_had_a_lacking_mark_Then_the_mark_is_cleared_immediately()
        {
            var now = DateTime.UtcNow;
            var queue = NewQueue(() => now);
            queue.EnqueueHeader(WithTxRoot(MakeHeader(0), HexBytes("aa", 32)), MakeHash(0));

            var peer = Guid.NewGuid();
            var reservation = queue.ReserveBodies(peer, capacity: 1);
            Assert.Equal(1, reservation.Count);

            queue.DeliverBodies(reservation, new List<BlockBody>());
            Assert.Equal(0, queue.ReserveBodies(peer, capacity: 1).Count);

            queue.ReleasePeer(peer);

            var retry = queue.ReserveBodies(peer, capacity: 1);
            Assert.Equal(1, retry.Count);
        }

        private static BlockHeader MakeHeader(long blockNumber) => new BlockHeader
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
