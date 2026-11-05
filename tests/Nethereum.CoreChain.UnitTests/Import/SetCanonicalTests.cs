using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Import
{
    public class SetCanonicalTests
    {
        private static readonly BigInteger OneGwei = 1_000_000_000;

        private static async Task<SideChainHarness> NodeHoldingCanonicalAAndSideBAsync(
            List<ProducedBlock> a, List<ProducedBlock> b, int maxReorgDepth = SideChainSupport.DefaultMaxReorgDepth)
        {
            var node = await SideChainHarness.CreateAsync(maxReorgDepth: maxReorgDepth);
            foreach (var block in a) Assert.True((await node.ImportAsync(block)).RootMatches);
            foreach (var block in b)
                Assert.Equal(BlockImportDisposition.StoredAsSide, (await node.ImportAsync(block)).Disposition);
            return node;
        }

        [Fact]
        public async Task Given_AHeavierSideBranchTwoDeep_When_SetCanonical_Then_TheChainRewindsToTheCommonAncestorAndReplaysBothBlocksOnTheOnePath()
        {
            using var producerA = await SideChainHarness.CreateAsync(withSideChain: false);
            using var producerB = await SideChainHarness.CreateAsync(withSideChain: false);
            var a = await producerA.ProduceChainAsync(SideChainHarness.RecipientA, 2);
            var b = await producerB.ProduceChainAsync(SideChainHarness.RecipientB, 2);
            using var node = await NodeHoldingCanonicalAAndSideBAsync(a, b);

            var result = await node.Importer.SetCanonicalAsync(b[1].Hash);

            Assert.Equal(SetCanonicalStatus.Reorganised, result.Status);
            Assert.Equal(0UL, result.Reorganisation.CommonAncestor);
            Assert.Equal(new[] { a[1].Hash, a[0].Hash }, result.Reorganisation.DroppedBlockHashes);
            Assert.Equal(new[] { b[0].Hash, b[1].Hash }, result.Reorganisation.AddedBlockHashes);
            Assert.Equal(b[1].Hash, await node.HeadHashAsync());
            Assert.Equal(b[0].Hash, await node.Node.Blocks.GetHashByNumberAsync(1));
            Assert.Equal(BigInteger.Zero, await node.BalanceOfAsync(SideChainHarness.RecipientA));
            Assert.Equal(2 * OneGwei, await node.BalanceOfAsync(SideChainHarness.RecipientB));
            Assert.Equal(b[1].Header.StateRoot, (await node.Node.Blocks.GetLatestAsync()).StateRoot);
            Assert.False(node.SideChain.SideBlocks.Contains(b[0].Hash));
            Assert.True(node.SideChain.SideBlocks.Contains(a[0].Hash));
            Assert.True(node.SideChain.SideBlocks.Contains(a[1].Hash));
        }

        [Fact]
        public async Task Given_AReplayedBranch_When_TheSecondBlockFails_Then_TheResultNamesItsParentAsLatestValidAndTheHashIsRememberedInvalid()
        {
            using var producerA = await SideChainHarness.CreateAsync(withSideChain: false);
            using var producerB = await SideChainHarness.CreateAsync(withSideChain: false);
            var a = await producerA.ProduceChainAsync(SideChainHarness.RecipientA, 2);
            var b = await producerB.ProduceChainAsync(SideChainHarness.RecipientB, 2);
            using var node = await SideChainHarness.CreateAsync();
            foreach (var block in a) await node.ImportAsync(block);
            await node.ImportAsync(b[0]);
            var forgedHeader = BlockPipelineHarness.CloneHeader(b[1].Header);
            forgedHeader.StateRoot = new byte[32];
            forgedHeader.StateRoot[0] = 9;
            var forged = await node.Importer.ImportAsync(
                forgedHeader, b[1].Transactions, uncles: null, b[1].Withdrawals, CancellationToken.None);
            Assert.Equal(BlockImportDisposition.StoredAsSide, forged.Disposition);

            var result = await node.Importer.SetCanonicalAsync(forged.BlockHash);

            Assert.Equal(SetCanonicalStatus.BranchInvalid, result.Status);
            Assert.Equal(forged.BlockHash, result.Reorganisation.FailedBlockHash);
            Assert.Equal(b[0].Hash, result.Reorganisation.FailedBlockParentHash);
            Assert.Equal(b[0].Hash, await node.HeadHashAsync());
            Assert.True(node.Importer.TryGetInvalidBlock(forged.BlockHash, out var latestValid));
            Assert.Equal(b[0].Hash, latestValid);
        }

        [Fact]
        public async Task Given_AnAdoptedBranch_When_TheOldHeadIsSetCanonicalAgain_Then_TheDroppedBlocksReplayFromTheSideStoreWithoutTheNetwork()
        {
            using var producerA = await SideChainHarness.CreateAsync(withSideChain: false);
            using var producerB = await SideChainHarness.CreateAsync(withSideChain: false);
            var a = await producerA.ProduceChainAsync(SideChainHarness.RecipientA, 2);
            var b = await producerB.ProduceChainAsync(SideChainHarness.RecipientB, 2);
            using var node = await NodeHoldingCanonicalAAndSideBAsync(a, b);
            Assert.Equal(SetCanonicalStatus.Reorganised, (await node.Importer.SetCanonicalAsync(b[1].Hash)).Status);

            var back = await node.Importer.SetCanonicalAsync(a[1].Hash);

            Assert.Equal(SetCanonicalStatus.Reorganised, back.Status);
            Assert.Equal(a[1].Hash, await node.HeadHashAsync());
            Assert.Equal(2 * OneGwei, await node.BalanceOfAsync(SideChainHarness.RecipientA));
            Assert.Equal(BigInteger.Zero, await node.BalanceOfAsync(SideChainHarness.RecipientB));
            Assert.Equal(a[1].Header.StateRoot, (await node.Node.Blocks.GetLatestAsync()).StateRoot);
        }

        [Fact]
        public async Task Given_ABranchDeeperThanMaxReorgDepth_When_SetCanonical_Then_TheOutcomeIsAncestryUnavailableAndNothingRewinds()
        {
            using var producerA = await SideChainHarness.CreateAsync(withSideChain: false);
            using var producerB = await SideChainHarness.CreateAsync(withSideChain: false);
            var a = await producerA.ProduceChainAsync(SideChainHarness.RecipientA, 1);
            var b = await producerB.ProduceChainAsync(SideChainHarness.RecipientB, 3);
            using var node = await NodeHoldingCanonicalAAndSideBAsync(a, b, maxReorgDepth: 2);

            var result = await node.Importer.SetCanonicalAsync(b[2].Hash);

            Assert.Equal(SetCanonicalStatus.AncestryUnavailable, result.Status);
            Assert.Equal(a[0].Hash, await node.HeadHashAsync());
            Assert.Equal(OneGwei, await node.BalanceOfAsync(SideChainHarness.RecipientA));
        }

        [Fact]
        public async Task Given_AJournalShorterThanTheFork_When_SetCanonical_Then_TheOutcomeIsRewindUnavailableAndTheHeadIsUnchanged()
        {
            using var producerA = await SideChainHarness.CreateAsync(withSideChain: false);
            using var producerB = await SideChainHarness.CreateAsync(withSideChain: false);
            var a = await producerA.ProduceChainAsync(SideChainHarness.RecipientA, 3);
            var b = await producerB.ProduceChainAsync(SideChainHarness.RecipientB, 1);
            using var node = await NodeHoldingCanonicalAAndSideBAsync(a, b);
            await node.Bundle.Diffs.DeleteDiffsBelowBlockAsync(2);

            var result = await node.Importer.SetCanonicalAsync(b[0].Hash);

            Assert.Equal(SetCanonicalStatus.RewindUnavailable, result.Status);
            Assert.Equal(a[2].Hash, await node.HeadHashAsync());
            Assert.Equal(3 * OneGwei, await node.BalanceOfAsync(SideChainHarness.RecipientA));
            Assert.Equal(BigInteger.Zero, await node.BalanceOfAsync(SideChainHarness.RecipientB));
        }

        [Fact]
        public async Task Given_AReorg_When_Applied_Then_TheWarmTrieIsDiscardedAndTheNextRootEqualsAFullRecompute()
        {
            using var producerA = await SideChainHarness.CreateAsync(withSideChain: false);
            using var producerB = await SideChainHarness.CreateAsync(withSideChain: false);
            var a = await producerA.ProduceChainAsync(SideChainHarness.RecipientA, 2);
            var b = await producerB.ProduceChainAsync(SideChainHarness.RecipientB, 3);
            using var node = await NodeHoldingCanonicalAAndSideBAsync(a, b.GetRange(0, 2));
            Assert.Equal(SetCanonicalStatus.Reorganised, (await node.Importer.SetCanonicalAsync(b[1].Hash)).Status);

            var next = await node.ImportAsync(b[2]);

            Assert.True(next.RootMatches, next.DescribeRejection());
            Assert.Equal(BlockImportDisposition.ExtendedCanonical, next.Disposition);
            var full = await node.Node.BlockManager.Engine.StateRootCalculator.ComputeFullStateRootAsync();
            Assert.Equal(b[2].Header.StateRoot, full);
        }

        [Fact]
        public async Task Given_ABranchThatWroteContractStorage_When_ADifferentBranchIsSetCanonicalAndThenTheFirstAgain_Then_TheSlotIsClearedAndWrittenAgain()
        {
            var sender = BlockPipelineHarness.SenderAddress;
            var creation = TransactionFactory.CreateTransaction(new Transaction1559Signer().SignTransaction(
                BlockPipelineHarness.PrivateKey.HexToByteArray(),
                new Transaction1559(BlockPipelineHarness.ChainId, 0, 2_000_000_000, 2_000_000_000, 200_000, "", 0, "6012600055", null)));
            var contract = ContractUtils.CalculateContractAddress(sender, 0);
            using var producerA = await SideChainHarness.CreateAsync(withSideChain: false);
            using var producerB = await SideChainHarness.CreateAsync(withSideChain: false);
            var a = await producerA.ProduceAsync(SideChainHarness.RecipientA, transactions: new List<ISignedTransaction> { creation });
            var b = await producerB.ProduceChainAsync(SideChainHarness.RecipientB, 1);
            using var node = await NodeHoldingCanonicalAAndSideBAsync(new List<ProducedBlock> { a }, b);
            Assert.Equal(new BigInteger(0x12), await node.StorageAtAsync(contract, 0));

            Assert.Equal(SetCanonicalStatus.Reorganised, (await node.Importer.SetCanonicalAsync(b[0].Hash)).Status);
            Assert.Equal(BigInteger.Zero, await node.StorageAtAsync(contract, 0));
            Assert.Equal(SetCanonicalStatus.Reorganised, (await node.Importer.SetCanonicalAsync(a.Hash)).Status);

            Assert.Equal(new BigInteger(0x12), await node.StorageAtAsync(contract, 0));
            Assert.Equal(a.Header.StateRoot, (await node.Node.Blocks.GetLatestAsync()).StateRoot);
        }

        [Fact]
        public async Task Given_ACanonicalBranchWithAnEmptyBlockBetweenTwoStateChanges_When_AnotherBranchIsSetCanonical_Then_TheJournalRewindCrossesTheEmptyBlock()
        {
            var preShanghai = new Nethereum.DevChain.DevChainConfig { Hardfork = "paris", AutoMine = false };
            using var producerA = await SideChainHarness.CreateAsync(withSideChain: false, config: preShanghai);
            using var producerB = await SideChainHarness.CreateAsync(withSideChain: false, config: preShanghai);
            var a1 = await producerA.ProduceAsync(
                SideChainHarness.RecipientA, transactions: new List<ISignedTransaction> { BlockPipelineHarness.Transfer(0) }, withWithdrawals: false);
            var a2 = await producerA.ProduceAsync(SideChainHarness.RecipientA, withWithdrawals: false);
            var a3 = await producerA.ProduceAsync(
                SideChainHarness.RecipientA, transactions: new List<ISignedTransaction> { BlockPipelineHarness.Transfer(1) }, withWithdrawals: false);
            var b = await producerB.ProduceChainAsync(SideChainHarness.RecipientB, 1, withWithdrawals: false);
            using var node = await SideChainHarness.CreateAsync(config: preShanghai);
            foreach (var block in new[] { a1, a2, a3 }) Assert.True((await node.ImportAsync(block)).RootMatches);
            Assert.Equal(BlockImportDisposition.StoredAsSide, (await node.ImportAsync(b[0])).Disposition);

            var result = await node.Importer.SetCanonicalAsync(b[0].Hash);

            Assert.Equal(SetCanonicalStatus.Reorganised, result.Status);
            Assert.Equal(b[0].Hash, await node.HeadHashAsync());
            Assert.Equal(BigInteger.Zero, await node.BalanceOfAsync(BlockPipelineHarness.RecipientAddress));
        }

        private const string StoresTheParentHashContract = "6009600c60003960096000f3" + "600143034060005500";

        private static ISignedTransaction Signed(BigInteger nonce, string to, string data) =>
            TransactionFactory.CreateTransaction(new Transaction1559Signer().SignTransaction(
                BlockPipelineHarness.PrivateKey.HexToByteArray(),
                new Transaction1559(BlockPipelineHarness.ChainId, nonce, 2_000_000_000, 2_000_000_000, 200_000, to, 0, data, null)));

        private static async Task<List<ProducedBlock>> DeployThenCallAsync(SideChainHarness producer, string contract, string recipient)
        {
            var deploy = await producer.ProduceAsync(
                recipient, transactions: new List<ISignedTransaction> { Signed(0, "", StoresTheParentHashContract) });
            var call = await producer.ProduceAsync(
                recipient, transactions: new List<ISignedTransaction> { Signed(1, contract, "") });
            return new List<ProducedBlock> { deploy, call };
        }

        [Fact]
        public async Task Given_BlockhashReadsInsideAReplayedBlock_When_Executed_Then_TheyResolveTheBranchAncestorsNotTheDroppedOnes()
        {
            var contract = ContractUtils.CalculateContractAddress(BlockPipelineHarness.SenderAddress, 0);
            using var producerA = await SideChainHarness.CreateAsync(withSideChain: false);
            using var producerB = await SideChainHarness.CreateAsync(withSideChain: false);
            var a = await DeployThenCallAsync(producerA, contract, SideChainHarness.RecipientA);
            var b = await DeployThenCallAsync(producerB, contract, SideChainHarness.RecipientB);
            using var node = await NodeHoldingCanonicalAAndSideBAsync(a, b);
            Assert.Equal(HashAsNumber(a[0].Hash), await node.StorageAtAsync(contract, 0));

            var result = await node.Importer.SetCanonicalAsync(b[1].Hash);

            Assert.Equal(SetCanonicalStatus.Reorganised, result.Status);
            Assert.Equal(HashAsNumber(b[0].Hash), await node.StorageAtAsync(contract, 0));
            Assert.NotEqual(HashAsNumber(a[0].Hash), HashAsNumber(b[0].Hash));
        }

        private static BigInteger HashAsNumber(byte[] hash) => new BigInteger(hash, isUnsigned: true, isBigEndian: true);
    }
}
