using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Snap.CatchUp;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Chain.TestData.UnitTests
{
    public class BalCatchUpTests
    {
        private const string AddressA = "0x2000000000000000000000000000000000000001";
        private const string AddressB = "0x2000000000000000000000000000000000000002";
        private const string AddressC = "0x2000000000000000000000000000000000000003";
        private const string AddressD = "0x2000000000000000000000000000000000000004";
        private const string AddressE = "0x2000000000000000000000000000000000000005";

        private static readonly byte[] ContractCode = { 0x60, 0x01, 0x60, 0x00, 0x55 };

        private static byte[] HashOf(string address) => Sha3Keccack.Current.CalculateHash(address.HexToByteArray());

        private static AccountChanges Balance(string address, ulong balance, ulong? nonce = null)
        {
            var change = new AccountChanges(address);
            change.BalanceChanges.Add(new BalanceChange(0, balance));
            if (nonce.HasValue) change.NonceChanges.Add(new NonceChange(0, nonce.Value));
            return change;
        }

        private static AccountChanges Nonce(string address, ulong nonce)
        {
            var change = new AccountChanges(address);
            change.NonceChanges.Add(new NonceChange(0, nonce));
            return change;
        }

        private static AccountChanges Storage(string address, params (ulong Slot, ulong Value)[] slots)
        {
            var change = new AccountChanges(address);
            foreach (var (slot, value) in slots)
            {
                var slotChanges = new SlotChanges(slot);
                slotChanges.Changes.Add(new StorageChange(0, value));
                change.StorageChanges.Add(slotChanges);
            }
            return change;
        }

        private static List<AccountChanges> Block(params AccountChanges[] changes) => changes.ToList();

        private static BalChainFixture Chain(List<AccountChanges> genesis, params List<AccountChanges>[] blocks)
            => BalChainFixture.Build(genesis, blocks.Cast<IReadOnlyList<AccountChanges>>().ToList());

        private static BalChainFixture FiveBlockChain()
            => Chain(
                Block(Balance(AddressA, 100, 1), Storage(AddressB, (1, 3)), Balance(AddressB, 1)),
                Block(Balance(AddressA, 90)),
                Block(Storage(AddressB, (1, 4), (2, 8))),
                Block(Balance(AddressA, 80, 2), Storage(AddressB, (2, 0))),
                Block(Balance(AddressC, 7)),
                Block(Storage(AddressB, (3, 6)), Balance(AddressA, 70)));

        [Fact]
        public async Task Given_CatchUpPersistedThroughBlockN_When_TheProcessStopsAndRestarts_Then_CatchUpContinuesAtNPlus1AndTheGeneratedRootMatches()
        {
            using var bed = await BalCatchUpBed.OpenAsync(FiveBlockChain(), flatAt: 1);
            var stopping = new CountingApplier(bed.DurableApplier(), throwBeforeApplying: call => call == 3);

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => bed.CatchUp(bed.Pivot(1), bed.Pivot(5), stopping).CatchUpAsync(BalCatchUpBed.EverythingFetched, CancellationToken.None));

            var saved = await bed.SavedPivotAsync();
            Assert.Equal(3UL, (ulong)saved.Header.BlockNumber);
            var requestedBefore = bed.Bals.RequestedBlockHashes.Count;

            var root = await bed.CatchUp(saved, bed.Pivot(5)).CatchUpAsync(BalCatchUpBed.EverythingFetched, CancellationToken.None);

            Assert.Equal(bed.Fixture.HeaderAt(5).StateRoot.ToHex(), root.ToHex());
            var requestedOnRestart = bed.Bals.RequestedBlockHashes.Skip(requestedBefore).Select(h => h.ToHex()).ToList();
            Assert.Equal(new[] { bed.Fixture.HashAt(4).ToHex(), bed.Fixture.HashAt(5).ToHex() }, requestedOnRestart);
            Assert.Equal(5UL, (ulong)(await bed.SavedPivotAsync()).Header.BlockNumber);
            var generated = await bed.GenerateAsync(5);
            Assert.Equal(bed.Fixture.HeaderAt(5).StateRoot.ToHex(), generated.Root.ToHex());
        }

        [Fact]
        public async Task Given_BlockNFlatWritesLandedButNotItsPivot_When_TheProcessRestarts_Then_ReplayingNGivesTheSameGeneratedRoot()
        {
            using var bed = await BalCatchUpBed.OpenAsync(FiveBlockChain(), flatAt: 1);
            var stopping = new CountingApplier(bed.DurableApplier(), throwAfterApplying: call => call == 3);

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => bed.CatchUp(bed.Pivot(1), bed.Pivot(5), stopping).CatchUpAsync(BalCatchUpBed.EverythingFetched, CancellationToken.None));

            var saved = await bed.SavedPivotAsync();
            Assert.Equal(3UL, (ulong)saved.Header.BlockNumber);
            Assert.Equal((EvmUInt256)80, (await bed.Flat.GetAccountByHashAsync(HashOf(AddressA))).Balance);

            await bed.CatchUp(saved, bed.Pivot(5)).CatchUpAsync(BalCatchUpBed.EverythingFetched, CancellationToken.None);

            var generated = await bed.GenerateAsync(5);
            Assert.Equal(bed.Fixture.HeaderAt(5).StateRoot.ToHex(), generated.Root.ToHex());
        }

        [Fact]
        public async Task Given_AnAppliedPivotReorgedOutMidFlight_When_CatchUpRuns_Then_ItThrowsResetRequiredWithoutApplyingAnyBlock()
        {
            using var bed = await BalCatchUpBed.OpenAsync(FiveBlockChain(), flatAt: 2);
            var counting = new CountingApplier(bed.DurableApplier());
            var reorged = new SnapBootstrapper.PivotState(bed.Fixture.HeaderAt(2), BalCatchUpBed.Filled(0xab));

            var ex = await Assert.ThrowsAsync<SnapSyncResetRequiredException>(
                () => bed.CatchUp(reorged, bed.Pivot(4), counting).CatchUpAsync(BalCatchUpBed.EverythingFetched, CancellationToken.None));

            Assert.Equal("pivot_reorged", ex.Reason);
            Assert.Equal(0, counting.Calls);
            Assert.Empty(bed.Bals.RequestedBlockHashes);
        }

        [Fact]
        public async Task Given_ACanonicalAppliedPivot_When_CatchUpRuns_Then_TheGapApplies()
        {
            using var bed = await BalCatchUpBed.OpenAsync(FiveBlockChain(), flatAt: 2);
            var counting = new CountingApplier(bed.DurableApplier());

            var root = await bed.CatchUp(bed.Pivot(2), bed.Pivot(4), counting).CatchUpAsync(BalCatchUpBed.EverythingFetched, CancellationToken.None);

            Assert.Equal(2, counting.Calls);
            Assert.Equal(bed.Fixture.HeaderAt(4).StateRoot.ToHex(), root.ToHex());
            Assert.Equal(bed.Fixture.HeaderAt(4).StateRoot.ToHex(), (await bed.GenerateAsync(4)).Root.ToHex());
        }

        [Fact]
        public async Task Given_AGapOf90001Blocks_When_CatchUpStarts_Then_ResetIsRequiredWithoutFetchingAnyBal()
        {
            using var bed = await BalCatchUpBed.OpenAsync(FiveBlockChain(), flatAt: 0);
            var counting = new CountingApplier(bed.DurableApplier());
            var farTarget = new SnapBootstrapper.PivotState(
                new BlockHeader { BlockNumber = 90_001, StateRoot = new byte[32] }, BalCatchUpBed.Filled(0xcd));

            var ex = await Assert.ThrowsAsync<SnapSyncResetRequiredException>(
                () => bed.CatchUp(bed.Pivot(0), farTarget, counting).CatchUpAsync(BalCatchUpBed.EverythingFetched, CancellationToken.None));

            Assert.Equal("gap_exceeds_bal_retention", ex.Reason);
            Assert.Equal(0, counting.Calls);
            Assert.Empty(bed.Bals.RequestedBlockHashes);
        }

        [Fact]
        public async Task Given_AGapOfExactly90000Blocks_When_CatchUpStarts_Then_ItProceedsPastTheRetentionCheck()
        {
            using var bed = await BalCatchUpBed.OpenAsync(FiveBlockChain(), flatAt: 0);
            var counting = new CountingApplier(bed.DurableApplier());
            var farTarget = new SnapBootstrapper.PivotState(
                new BlockHeader { BlockNumber = 90_000, StateRoot = new byte[32] }, BalCatchUpBed.Filled(0xcd));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => bed.CatchUp(bed.Pivot(0), farTarget, counting).CatchUpAsync(BalCatchUpBed.EverythingFetched, CancellationToken.None));

            Assert.IsNotType<SnapSyncResetRequiredException>(ex);
            Assert.Contains("canonical header", ex.Message);
        }

        [Fact]
        public async Task Given_TwoConsecutiveGapBlocksChangingDifferentFieldsOfOneAccount_When_BalCatchUpRunsOnARocksDbBundle_Then_TheFlatRowHoldsBothPostValues()
        {
            var fixture = Chain(
                Block(Balance(AddressA, 100, 1)),
                Block(Balance(AddressA, 90)),
                Block(Nonce(AddressA, 5)));
            using var bed = await BalCatchUpBed.OpenAsync(fixture, flatAt: 0);

            await bed.CatchUp(bed.Pivot(0), bed.Pivot(2)).CatchUpAsync(BalCatchUpBed.EverythingFetched, CancellationToken.None);

            var account = await bed.Flat.GetAccountByHashAsync(HashOf(AddressA));
            Assert.Equal((EvmUInt256)90, account.Balance);
            Assert.Equal((EvmUInt256)5, account.Nonce);
            Assert.Equal(fixture.HeaderAt(2).StateRoot.ToHex(), (await bed.GenerateAsync(2)).Root.ToHex());
        }

        [Fact]
        public async Task Given_TwoConsecutiveGapBlocksChangingDifferentFieldsOfOneAccount_When_TheApplierIsBuiltOverTheSstSink_Then_ItThrowsNotSupported()
        {
            var fixture = Chain(
                Block(Balance(AddressA, 100, 1)),
                Block(Balance(AddressA, 90)),
                Block(Nonce(AddressA, 5)));
            using var bed = await BalCatchUpBed.OpenAsync(fixture, flatAt: 0);
            using var sst = bed.Bundle.CreateBulkFlatSink();
            var overSst = new BlockAccessListApplier(sst, bed.Bundle.State);

            await Assert.ThrowsAsync<NotSupportedException>(
                () => bed.CatchUp(bed.Pivot(0), bed.Pivot(2), overSst).CatchUpAsync(BalCatchUpBed.EverythingFetched, CancellationToken.None));
        }

        [Fact]
        public async Task Given_AGapHeaderMissingFromTheLocalCanonicalChain_When_CatchingUp_Then_TheAttemptFailsWithoutApplyingAnyBlockOfThatWindow()
        {
            using var bed = await BalCatchUpBed.OpenAsync(FiveBlockChain(), flatAt: 1, missingCanonical: 4);
            var counting = new CountingApplier(bed.DurableApplier());

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => bed.CatchUp(bed.Pivot(1), bed.Pivot(5), counting).CatchUpAsync(BalCatchUpBed.EverythingFetched, CancellationToken.None));

            Assert.IsNotType<SnapSyncResetRequiredException>(ex);
            Assert.Equal(0, counting.Calls);
            Assert.Empty(bed.Bals.RequestedBlockHashes);
            Assert.Equal(1UL, (ulong)(await bed.SavedPivotAsync()).Header.BlockNumber);
        }

        [Fact]
        public async Task Given_EveryGapHeaderOnTheLocalCanonicalChain_When_CatchingUp_Then_TheWindowApplies()
        {
            using var bed = await BalCatchUpBed.OpenAsync(FiveBlockChain(), flatAt: 1);
            var counting = new CountingApplier(bed.DurableApplier());

            await bed.CatchUp(bed.Pivot(1), bed.Pivot(5), counting).CatchUpAsync(BalCatchUpBed.EverythingFetched, CancellationToken.None);

            Assert.Equal(4, counting.Calls);
            Assert.Equal(5UL, (ulong)(await bed.SavedPivotAsync()).Header.BlockNumber);
        }

        [Fact]
        public async Task Given_AGapBlockThatCreatesAContractWithStorageBelowTheFrontier_When_CaughtUpAndGenerated_Then_TheFlatAccountRootIsRewrittenAndTheStateRootMatches()
        {
            var contract = Storage(AddressC, (1, 5), (2, 7));
            contract.CodeChanges.Add(new CodeChange(0, ContractCode));
            contract.NonceChanges.Add(new NonceChange(0, 1));
            var fixture = Chain(Block(Balance(AddressA, 100, 1)), Block(contract));
            using var bed = await BalCatchUpBed.OpenAsync(fixture, flatAt: 0);

            await bed.CatchUp(bed.Pivot(0), bed.Pivot(1)).CatchUpAsync(BalCatchUpBed.EverythingFetched, CancellationToken.None);
            var generated = await bed.GenerateAsync(1);

            Assert.Equal(fixture.HeaderAt(1).StateRoot.ToHex(), generated.Root.ToHex());
            Assert.True(generated.AccountRootsRewritten >= 1);
            var expectedStorageRoot = IndependentStorageRoot((1, 5), (2, 7));
            Assert.Equal(expectedStorageRoot.ToHex(), (await bed.Flat.GetAccountByHashAsync(HashOf(AddressC))).StateRoot.ToHex());
        }

        [Fact]
        public async Task Given_AGapBlockThatEmptiesAnExistingAccountWithStorage_When_CaughtUpAndGenerated_Then_ItsAccountRowAndItsStorageRowsAreGone()
        {
            var fixture = Chain(
                Block(Balance(AddressA, 100, 1), Balance(AddressD, 5), Storage(AddressD, (1, 7), (2, 9))),
                Block(Balance(AddressD, 0)));
            using var bed = await BalCatchUpBed.OpenAsync(fixture, flatAt: 0);
            Assert.NotNull(await bed.Flat.GetAccountByHashAsync(HashOf(AddressD)));

            await bed.CatchUp(bed.Pivot(0), bed.Pivot(1)).CatchUpAsync(BalCatchUpBed.EverythingFetched, CancellationToken.None);
            var generated = await bed.GenerateAsync(1);

            Assert.Equal(fixture.HeaderAt(1).StateRoot.ToHex(), generated.Root.ToHex());
            Assert.Null(await bed.Flat.GetAccountByHashAsync(HashOf(AddressD)));
            Assert.Equal(2, generated.DanglingSlotsDeleted);
            Assert.True(IsAbsent(await bed.Bundle.State.GetStorageAsync(AddressD, new BigInteger(1))));
            Assert.True(IsAbsent(await bed.Bundle.State.GetStorageAsync(AddressD, new BigInteger(2))));
        }

        [Fact]
        public async Task Given_AGapBlockThatLeavesANewAccountEmpty_When_CaughtUp_Then_NoFlatRowIsWritten()
        {
            var fixture = Chain(Block(Balance(AddressA, 100, 1)), Block(Balance(AddressE, 0), Balance(AddressA, 99)));
            using var bed = await BalCatchUpBed.OpenAsync(fixture, flatAt: 0);

            await bed.CatchUp(bed.Pivot(0), bed.Pivot(1)).CatchUpAsync(BalCatchUpBed.EverythingFetched, CancellationToken.None);

            Assert.Null(await bed.Flat.GetAccountByHashAsync(HashOf(AddressE)));
            Assert.Equal(fixture.HeaderAt(1).StateRoot.ToHex(), (await bed.GenerateAsync(1)).Root.ToHex());
        }

        [Fact]
        public async Task Given_ATargetAtOrBelowTheAppliedPivotWithADifferentHash_When_CatchUpRuns_Then_ResetIsRequired()
        {
            using var bed = await BalCatchUpBed.OpenAsync(FiveBlockChain(), flatAt: 3);
            var counting = new CountingApplier(bed.DurableApplier());
            var sameHeight = new SnapBootstrapper.PivotState(bed.Fixture.HeaderAt(3), BalCatchUpBed.Filled(0x3a));
            var below = new SnapBootstrapper.PivotState(bed.Fixture.HeaderAt(2), BalCatchUpBed.Filled(0x2a));

            var atSameHeight = await Assert.ThrowsAsync<SnapSyncResetRequiredException>(
                () => bed.CatchUp(bed.Pivot(3), sameHeight, counting).CatchUpAsync(BalCatchUpBed.EverythingFetched, CancellationToken.None));
            var atLowerHeight = await Assert.ThrowsAsync<SnapSyncResetRequiredException>(
                () => bed.CatchUp(bed.Pivot(3), below, counting).CatchUpAsync(BalCatchUpBed.EverythingFetched, CancellationToken.None));

            Assert.Equal("pivot_not_ahead", atSameHeight.Reason);
            Assert.Equal("pivot_not_ahead", atLowerHeight.Reason);
            Assert.Equal(0, counting.Calls);
            Assert.Empty(bed.Bals.RequestedBlockHashes);
        }

        [Fact]
        public async Task Given_ATargetWithTheAppliedPivotsHash_When_CatchUpRuns_Then_ItOnlyPrunes()
        {
            using var bed = await BalCatchUpBed.OpenAsync(FiveBlockChain(), flatAt: 3);
            var counting = new CountingApplier(bed.DurableApplier());
            var ghost = BalCatchUpBed.Filled(0xc0);
            await bed.Flat.SaveAccountByHashAsync(ghost, new Account
            {
                Nonce = (EvmUInt256)1, Balance = (EvmUInt256)1,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH, CodeHash = DefaultValues.EMPTY_DATA_HASH,
            });
            var frontier = new[] { BalCatchUpBed.Chunk(BalCatchUpBed.Filled(0xbf), BalCatchUpBed.Filled(0xff)) };

            var root = await bed.CatchUp(bed.Pivot(3), bed.Pivot(3), counting).CatchUpAsync(frontier, CancellationToken.None);

            Assert.Equal(bed.Fixture.HeaderAt(3).StateRoot.ToHex(), root.ToHex());
            Assert.Null(await bed.Flat.GetAccountByHashAsync(ghost));
            Assert.Equal(0, counting.Calls);
            Assert.Empty(bed.Bals.RequestedBlockHashes);
        }

        [Fact]
        public async Task Given_NoDurableTasks_When_CatchUpRuns_Then_AppliedBecomesTheTargetAndNoBalIsFetched()
        {
            using var bed = await BalCatchUpBed.OpenAsync(FiveBlockChain(), flatAt: 0);
            var counting = new CountingApplier(bed.DurableApplier());
            var catchUp = bed.CatchUp(bed.Pivot(0), bed.Pivot(4), counting);

            var root = await catchUp.CatchUpAsync(null, CancellationToken.None);

            Assert.Equal(bed.Fixture.HeaderAt(4).StateRoot.ToHex(), root.ToHex());
            Assert.Equal(bed.Fixture.HashAt(4).ToHex(), catchUp.Applied.Hash.ToHex());
            Assert.Equal(0, counting.Calls);
            Assert.Empty(bed.Bals.RequestedBlockHashes);
        }

        [Fact]
        public async Task Given_EveryTaskDoneAtAPivotMove_When_CatchUpRunsWithAnEmptyUnfinishedList_Then_EveryGapBlockIsApplied()
        {
            using var bed = await BalCatchUpBed.OpenAsync(FiveBlockChain(), flatAt: 2);
            var counting = new CountingApplier(bed.DurableApplier());

            await bed.CatchUp(bed.Pivot(2), bed.Pivot(5), counting).CatchUpAsync(Array.Empty<SnapSyncAccountTask>(), CancellationToken.None);

            Assert.Equal(3, counting.Calls);
            Assert.Equal(bed.Fixture.HeaderAt(5).StateRoot.ToHex(), (await bed.GenerateAsync(5)).Root.ToHex());
        }

        private static byte[] IndependentStorageRoot(params (ulong Slot, ulong Value)[] slots)
        {
            var trie = new PatriciaTrie();
            foreach (var (slot, value) in slots)
            {
                var key = Sha3Keccack.Current.CalculateHash(((EvmUInt256)slot).ToBigEndian());
                var raw = ((EvmUInt256)value).ToBigEndian().SkipWhile(b => b == 0).ToArray();
                trie.Put(key, Nethereum.RLP.RLP.EncodeElement(raw));
            }
            return trie.Root.GetHash();
        }

        private static bool IsAbsent(byte[] value) => value == null || value.All(b => b == 0);
    }
}
