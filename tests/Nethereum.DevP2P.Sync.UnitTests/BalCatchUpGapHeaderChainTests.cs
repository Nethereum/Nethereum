using System;
using System.Collections.Generic;
using Nethereum.DevP2P.Sync.Snap.CatchUp;
using Nethereum.Model;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class BalCatchUpGapHeaderChainTests
    {
        private static BlockHeader Header(long number, byte[] parentHash) => new BlockHeader
        {
            BlockNumber = number,
            ParentHash = parentHash,
            StateRoot = new byte[32],
            TransactionsHash = new byte[32],
            ReceiptHash = new byte[32],
            UnclesHash = new byte[32],
            ExtraData = Array.Empty<byte>(),
            LogsBloom = new byte[256],
            Coinbase = "0x0000000000000000000000000000000000000000",
            Difficulty = 0,
            GasLimit = 30_000_000,
            GasUsed = 0,
            Timestamp = 1_700_000_000 + number * 12,
            MixHash = new byte[32],
            Nonce = new byte[8],
        };

        private static byte[] Hash(BlockHeader header) => RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(header);

        private static (BlockHeader Old, List<BlockHeader> Gap) Chain(int gapLength)
        {
            var old = Header(100, new byte[32]);
            var gap = new List<BlockHeader>();
            var parent = old;
            for (int i = 1; i <= gapLength; i++)
            {
                var next = Header(100 + i, Hash(parent));
                gap.Add(next);
                parent = next;
            }
            return (old, gap);
        }

        [Fact]
        public void Given_GapHeadersLinkedFromTheOldPivotToTheNewPivot_When_Verified_Then_TheyAreAccepted()
        {
            var (old, gap) = Chain(5);

            BalCatchUp.VerifyGapHeaderChain(old, gap[^1], gap);
        }

        [Fact]
        public void Given_AGapHeaderWhoseParentHashBreaksTheChain_When_Verified_Then_TheGapIsRejected()
        {
            var (old, gap) = Chain(5);
            gap[2] = Header(103, new byte[32]);

            Assert.Throws<InvalidOperationException>(() => BalCatchUp.VerifyGapHeaderChain(old, gap[^1], gap));
        }

        [Fact]
        public void Given_GapHeadersThatDoNotEndAtTheNewPivot_When_Verified_Then_TheGapIsRejected()
        {
            var (old, gap) = Chain(5);
            var (_, otherGap) = Chain(6);

            Assert.Throws<InvalidOperationException>(() => BalCatchUp.VerifyGapHeaderChain(old, otherGap[^1], gap));
        }

        [Fact]
        public void Given_GapHeadersThatDoNotStartAtTheOldPivot_When_Verified_Then_TheGapIsRejected()
        {
            var (_, gap) = Chain(5);
            var foreignOld = Header(100, new byte[] { 1, 2, 3 });

            Assert.Throws<InvalidOperationException>(() => BalCatchUp.VerifyGapHeaderChain(foreignOld, gap[^1], gap));
        }
    }
}
