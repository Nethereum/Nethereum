using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.CoreChain;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class BlockTaskQueueWithdrawalsTests
    {
        private static readonly byte[] EmptyUnclesHash =
            "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray();
        private static readonly byte[] EmptyTrieRoot =
            "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();

        private static BlockTaskQueue NewQueue() =>
            new BlockTaskQueue(PatriciaBlockRootsProvider.Instance, 0);

        [Fact]
        public void Given_AShanghaiBlockWhoseBodyCarriesAnotherBlocksWithdrawals_When_TheBodyIsDelivered_Then_ItIsUnmatchedAndTheBlockStaysPending()
        {
            var queue = NewQueue();
            queue.EnqueueHeader(ShanghaiHeader(0, Withdrawals(1)), Hash(0));
            var reservation = queue.ReserveBodies(Guid.NewGuid(), capacity: 1);

            var result = queue.DeliverBodies(reservation, new List<BlockBody> { Body(Withdrawals(2)) });

            Assert.Equal(0, result.Matched);
            Assert.Equal(1, result.Unmatched);
            var another = queue.ReserveBodies(Guid.NewGuid(), capacity: 1);
            Assert.Equal(1, another.Count);
        }

        [Fact]
        public void Given_AShanghaiBlockWhoseBodyOmitsWithdrawals_When_TheBodyIsDelivered_Then_ItIsUnmatched()
        {
            var queue = NewQueue();
            queue.EnqueueHeader(ShanghaiHeader(0, Withdrawals(1)), Hash(0));
            var reservation = queue.ReserveBodies(Guid.NewGuid(), capacity: 1);

            var result = queue.DeliverBodies(reservation, new List<BlockBody> { Body(null) });

            Assert.Equal(0, result.Matched);
            Assert.Equal(1, result.Unmatched);
        }

        [Fact]
        public void Given_AShanghaiBlockWhoseBodyCarriesItsOwnWithdrawals_When_TheBodyIsDelivered_Then_ItIsMatchedAndPersistedWithThoseWithdrawals()
        {
            var queue = NewQueue();
            var own = Withdrawals(1);
            queue.EnqueueHeader(ShanghaiHeader(0, own), Hash(0));
            var reservation = queue.ReserveBodies(Guid.NewGuid(), capacity: 1);

            var result = queue.DeliverBodies(reservation, new List<BlockBody> { Body(own) });
            DeliverEmptyReceipts(queue, 1);

            Assert.Equal(1, result.Matched);
            Assert.Equal(0, result.Unmatched);
            var persisted = queue.DequeuePersistable(1);
            Assert.Single(persisted);
            Assert.Same(own, persisted[0].Body.Withdrawals);
        }

        [Fact]
        public void Given_TwoShanghaiBlocksWithSwappedBodies_When_Delivered_Then_EachBodyIsMatchedToItsOwnWithdrawalsRoot()
        {
            var queue = NewQueue();
            var first = Withdrawals(1);
            var second = Withdrawals(2);
            queue.EnqueueHeader(ShanghaiHeader(0, first), Hash(0));
            queue.EnqueueHeader(ShanghaiHeader(1, second), Hash(1));
            var reservation = queue.ReserveBodies(Guid.NewGuid(), capacity: 2);

            var result = queue.DeliverBodies(reservation, new List<BlockBody> { Body(second), Body(first) });
            DeliverEmptyReceipts(queue, 2);

            Assert.Equal(2, result.Matched);
            var persisted = queue.DequeuePersistable(2);
            Assert.Equal(2, persisted.Count);
            Assert.Same(first, persisted[0].Body.Withdrawals);
            Assert.Same(second, persisted[1].Body.Withdrawals);
        }

        [Fact]
        public void Given_APreShanghaiBlockWhoseBodyCarriesWithdrawals_When_TheBodyIsDelivered_Then_ItIsUnmatched()
        {
            var queue = NewQueue();
            queue.EnqueueHeader(Header(0), Hash(0));
            var reservation = queue.ReserveBodies(Guid.NewGuid(), capacity: 1);

            var result = queue.DeliverBodies(reservation, new List<BlockBody> { Body(Withdrawals(1)) });

            Assert.Equal(0, result.Matched);
            Assert.Equal(1, result.Unmatched);
        }

        private static void DeliverEmptyReceipts(BlockTaskQueue queue, int count)
        {
            var reservation = queue.ReserveReceipts(Guid.NewGuid(), capacity: count);
            var lists = Enumerable.Range(0, count).Select(_ => new List<Receipt>()).ToList();
            queue.DeliverReceipts(reservation, lists);
        }

        private static BlockBody Body(List<Withdrawal> withdrawals) => new BlockBody
        {
            Transactions = new List<ISignedTransaction>(),
            Uncles = new List<BlockHeader>(),
            Withdrawals = withdrawals,
        };

        private static List<Withdrawal> Withdrawals(ulong seed) => new List<Withdrawal>
        {
            new Withdrawal { Index = seed, ValidatorIndex = seed + 10, Address = new byte[20], AmountInGwei = seed * 100 },
        };

        private static BlockHeader ShanghaiHeader(long number, List<Withdrawal> withdrawals)
        {
            var header = Header(number);
            header.WithdrawalsRoot = PatriciaBlockRootsProvider.Instance.CalculateWithdrawalsRoot(withdrawals);
            return header;
        }

        private static BlockHeader Header(long number) => new BlockHeader
        {
            BlockNumber = new EvmUInt256((ulong)number),
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

        private static byte[] Hash(long number)
        {
            var hash = new byte[32];
            BitConverter.GetBytes(number).CopyTo(hash, 0);
            return hash;
        }
    }
}
