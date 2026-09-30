using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Sync
{
    /// <summary>
    /// EIP-225 calls difficulty "the standalone score of the block to derive the quality of a chain" and
    /// specifies no rule for choosing between chains, so the choice here is ours: the heavier branch from
    /// the common ancestor wins, and an exact tie is broken the same way on every node, because a tie
    /// broken differently on two nodes is what makes a split permanent.
    /// </summary>
    public class DifficultyForkChoiceTests
    {
        private const long InTurn = 2;
        private const long OutOfTurn = 1;

        private sealed class BranchStore : IBlockStore
        {
            private readonly Dictionary<string, BlockHeader> _byHash = new();
            private readonly Dictionary<BigInteger, byte[]> _canonical = new();
            private BigInteger _height = -1;

            private static string Key(byte[] hash) => BitConverter.ToString(hash);

            public void PutCanonical(BlockHeader header, byte[] hash)
            {
                _byHash[Key(hash)] = header;
                _canonical[header.BlockNumber] = hash;
                if ((BigInteger)header.BlockNumber > _height) _height = header.BlockNumber;
            }

            public void PutSideBranch(BlockHeader header, byte[] hash) => _byHash[Key(hash)] = header;

            public Task<BlockHeader> GetByHashAsync(byte[] hash)
                => Task.FromResult(_byHash.TryGetValue(Key(hash), out var h) ? h : null);

            public Task<BlockHeader> GetByNumberAsync(BigInteger number)
                => Task.FromResult(_canonical.TryGetValue(number, out var hash) ? _byHash[Key(hash)] : null);

            public Task<BlockHeader> GetLatestAsync() => GetByNumberAsync(_height);

            public Task<BigInteger> GetHeightAsync() => Task.FromResult(_height);

            public Task<byte[]> GetHashByNumberAsync(BigInteger number)
                => Task.FromResult(_canonical.TryGetValue(number, out var hash) ? hash : null);

            public Task SaveAsync(BlockHeader header, byte[] blockHash)
            {
                PutCanonical(header, blockHash);
                return Task.CompletedTask;
            }

            public Task<bool> ExistsAsync(byte[] hash) => Task.FromResult(_byHash.ContainsKey(Key(hash)));

            public Task UpdateBlockHashAsync(BigInteger blockNumber, byte[] newHash) => Task.CompletedTask;

            public Task DeleteByNumberAsync(BigInteger blockNumber)
            {
                _canonical.Remove(blockNumber);
                return Task.CompletedTask;
            }
        }

        private static byte[] Hash(byte marker)
        {
            var hash = new byte[32];
            hash[0] = marker;
            return hash;
        }

        private static BlockHeader Block(long number, byte[] parentHash, long difficulty) => new BlockHeader
        {
            BlockNumber = number,
            ParentHash = parentHash,
            Difficulty = (EvmUInt256)(BigInteger)difficulty,
            MixHash = new byte[32],
            Nonce = new byte[8],
            ExtraData = Array.Empty<byte>(),
            Timestamp = 1
        };

        private static readonly byte[] GenesisHash = Hash(0x01);

        private static BranchStore StoreWithGenesisAnd(long headDifficulty, byte[] headHash)
        {
            var store = new BranchStore();
            store.PutCanonical(Block(0, null, 1), GenesisHash);
            store.PutCanonical(Block(1, GenesisHash, headDifficulty), headHash);
            return store;
        }

        private static Task<ForkChoiceVerdict> Ask(BranchStore store, BlockHeader incoming, byte[] hash, int depth = 64)
            => new DifficultyForkChoice(store, depth).ShouldAdoptAsync(incoming, hash, CancellationToken.None);

        [Fact]
        public async Task Given_ACompetingBlockOfLowerDifficulty_When_ItIsOffered_Then_TheLocalChainIsKept()
        {
            var store = StoreWithGenesisAnd(InTurn, Hash(0xAA));

            var verdict = await Ask(store, Block(1, GenesisHash, OutOfTurn), Hash(0xBB));

            Assert.Equal(ForkChoiceOutcome.KeepLocal, verdict.Outcome);
        }

        [Fact]
        public async Task Given_ACompetingBlockOfHigherDifficulty_When_ItIsOffered_Then_ItIsAdoptedFromTheCommonAncestor()
        {
            var store = StoreWithGenesisAnd(OutOfTurn, Hash(0xAA));

            var verdict = await Ask(store, Block(1, GenesisHash, InTurn), Hash(0xBB));

            Assert.Equal(ForkChoiceOutcome.AdoptIncoming, verdict.Outcome);
            Assert.Equal(0UL, verdict.CommonAncestor);
        }

        [Fact]
        public async Task Given_TwoBranchesOfEqualWeightAndHeight_When_EachNodeChooses_Then_BothNameTheSameWinner()
        {
            var lower = Hash(0xAA);
            var higher = Hash(0xBB);

            var nodeHoldingLower = StoreWithGenesisAnd(InTurn, lower);
            var nodeHoldingHigher = StoreWithGenesisAnd(InTurn, higher);

            var lowerNodeVerdict = await Ask(nodeHoldingLower, Block(1, GenesisHash, InTurn), higher);
            var higherNodeVerdict = await Ask(nodeHoldingHigher, Block(1, GenesisHash, InTurn), lower);

            Assert.Equal(ForkChoiceOutcome.KeepLocal, lowerNodeVerdict.Outcome);
            Assert.Equal(ForkChoiceOutcome.AdoptIncoming, higherNodeVerdict.Outcome);
        }

        [Fact]
        public async Task Given_AHeavierBranchTwoBlocksLong_When_ItIsOffered_Then_ItIsAdoptedFromTheSharedParent()
        {
            var store = StoreWithGenesisAnd(InTurn, Hash(0xAA));
            var sideOne = Block(1, GenesisHash, OutOfTurn);
            store.PutSideBranch(sideOne, Hash(0xB1));

            var verdict = await Ask(store, Block(2, Hash(0xB1), InTurn), Hash(0xB2));

            Assert.Equal(ForkChoiceOutcome.AdoptIncoming, verdict.Outcome);
            Assert.Equal(0UL, verdict.CommonAncestor);
        }

        [Fact]
        public async Task Given_ABlockAlreadyOnOurCanonicalChain_When_ItIsOffered_Then_NoReorgIsProposed()
        {
            var head = Hash(0xAA);
            var store = StoreWithGenesisAnd(InTurn, head);

            var verdict = await Ask(store, Block(1, GenesisHash, InTurn), head);

            Assert.Equal(ForkChoiceOutcome.KeepLocal, verdict.Outcome);
            Assert.False(verdict.RequiresReorg);
        }

        [Fact]
        public async Task Given_ACompetingBranchWeDoNotHold_When_ItIsOffered_Then_ItIsUndecidableRatherThanKept()
        {
            var store = StoreWithGenesisAnd(InTurn, Hash(0xAA));

            var verdict = await Ask(store, Block(2, Hash(0xCC), InTurn), Hash(0xDD));

            Assert.Equal(ForkChoiceOutcome.Undecidable, verdict.Outcome);
        }

        [Fact]
        public async Task Given_ACompetingBranchDeeperThanTheReorgBound_When_ItIsOffered_Then_ItIsUndecidableRatherThanSilentlyIgnored()
        {
            var store = new BranchStore();
            store.PutCanonical(Block(0, null, 1), GenesisHash);

            var parent = GenesisHash;
            for (long number = 1; number <= 5; number++)
            {
                var hash = Hash((byte)(0x10 + number));
                store.PutCanonical(Block(number, parent, InTurn), hash);
                parent = hash;
            }

            var sideParent = GenesisHash;
            for (long number = 1; number <= 4; number++)
            {
                var hash = Hash((byte)(0x30 + number));
                store.PutSideBranch(Block(number, sideParent, InTurn), hash);
                sideParent = hash;
            }

            var verdict = await Ask(store, Block(5, sideParent, InTurn), Hash(0x3F), depth: 2);

            Assert.Equal(ForkChoiceOutcome.Undecidable, verdict.Outcome);
            Assert.Contains("deeper than", verdict.Reason);
        }
    }
}
