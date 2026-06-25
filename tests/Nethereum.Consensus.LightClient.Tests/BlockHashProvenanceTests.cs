using System;
using System.Linq;
using Nethereum.Consensus.LightClient;
using Xunit;

namespace Nethereum.Consensus.LightClient.Tests
{
    /// <summary>
    /// Validates <see cref="LightClientState.SetBlockHash"/> provenance semantics per
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
    /// specs/altair/light-client/sync-protocol.md</see> lines 513–515 (optimistic
    /// header update condition) and lines 542–548 (finalized header update gate):
    /// optimistic entries may be upgraded to finalized; a finalized entry retains
    /// authority over conflicting optimistic overwrites and throws on a conflicting
    /// finalized overwrite.
    /// </summary>
    public class BlockHashProvenanceTests
    {
        private static readonly byte[] HashX = Enumerable.Repeat((byte)0xAA, 32).ToArray();
        private static readonly byte[] HashY = Enumerable.Repeat((byte)0xBB, 32).ToArray();

        [Fact]
        public void Given_OptimisticEntry_When_Finalized_Then_UpgradesProvenance()
        {
            var state = new LightClientState();

            state.SetBlockHash(100, HashX, BlockHashFinality.Optimistic);
            Assert.Equal(HashX, state.GetBlockHash(100));
            Assert.Null(state.GetFinalizedBlockHash(100));

            state.SetBlockHash(100, HashX, BlockHashFinality.Finalized);
            Assert.Equal(HashX, state.GetFinalizedBlockHash(100));
            Assert.Equal(BlockHashFinality.Finalized, state.BlockHashHistory[100].Finality);
        }

        [Fact]
        public void Given_FinalizedEntry_When_OptimisticConflictingOverwriteAttempted_Then_FinalizedRetained()
        {
            var state = new LightClientState();

            state.SetBlockHash(100, HashX, BlockHashFinality.Finalized);
            state.SetBlockHash(100, HashY, BlockHashFinality.Optimistic);

            Assert.Equal(HashX, state.GetBlockHash(100));
            Assert.Equal(BlockHashFinality.Finalized, state.BlockHashHistory[100].Finality);
        }

        [Fact]
        public void Given_FinalizedEntry_When_FinalizedIdempotentReWrite_Then_NoThrow()
        {
            var state = new LightClientState();

            state.SetBlockHash(100, HashX, BlockHashFinality.Finalized);
            state.SetBlockHash(100, HashX, BlockHashFinality.Finalized);

            Assert.Equal(HashX, state.GetFinalizedBlockHash(100));
        }

        [Fact]
        public void Given_FinalizedEntry_When_FinalizedConflictingOverwrite_Then_Throws()
        {
            var state = new LightClientState();

            state.SetBlockHash(100, HashX, BlockHashFinality.Finalized);

            Assert.Throws<InvalidOperationException>(
                () => state.SetBlockHash(100, HashY, BlockHashFinality.Finalized));
        }

        [Fact]
        public void Given_BlockHashHistoryExceedsMax_When_SetBlockHash_Then_OldestPruned()
        {
            var state = new LightClientState();

            for (ulong i = 0; i < (ulong)LightClientState.MaxBlockHashHistorySize + 5; i++)
            {
                state.SetBlockHash(i, HashX, BlockHashFinality.Finalized);
            }

            Assert.Equal(LightClientState.MaxBlockHashHistorySize, state.BlockHashHistory.Count);
            Assert.Null(state.GetBlockHash(0));
            Assert.Null(state.GetBlockHash(4));
            Assert.NotNull(state.GetBlockHash((ulong)LightClientState.MaxBlockHashHistorySize + 4));
        }

        [Fact]
        public void Given_NullOrWrongLengthHash_When_SetBlockHash_Then_SilentlyNoOps()
        {
            var state = new LightClientState();

            state.SetBlockHash(100, null, BlockHashFinality.Finalized);
            state.SetBlockHash(100, new byte[16], BlockHashFinality.Finalized);

            Assert.Empty(state.BlockHashHistory);
        }
    }
}
