using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BlockAccessListRetentionTests
    {
        private static readonly byte[] EmptyRlpList = RLP.RLP.EncodeList();

        [Fact]
        public async Task Given_AnAmsterdamBlock_When_Imported_Then_TheAccessListIsRetainedAsTheBytesTheHeaderCommitsTo()
        {
            var store = new InMemoryBlockAccessListStore();
            var (header, txs) = await AmsterdamBlockPipelineHarness.ProduceValidBlockAsync();

            var result = await AmsterdamBlockPipelineHarness.ImportAsync(header, txs, store);

            Assert.True(result.RootMatches, string.Join(",", result.FailedChecks));
            var retained = await store.GetByBlockHashAsync(result.BlockHash);
            Assert.NotNull(retained);
            Assert.Equal(header.BlockAccessListHash, new Sha3Keccack().CalculateHash(retained));
            Assert.Equal(BlockAccessListRLPEncoder.Current.Encode(result.BlockAccessList), retained);
        }

        [Fact]
        public async Task Given_APreAmsterdamBlock_When_Imported_Then_NothingIsRetained()
        {
            var store = new InMemoryBlockAccessListStore();
            var producer = await BlockPipelineHarness.CreateAsync(HardforkName.Prague);
            var txs = new List<ISignedTransaction> { BlockPipelineHarness.Transfer(0) };
            var produced = await producer.ProduceAsync(txs);

            var follower = await BlockPipelineHarness.CreateAsync(HardforkName.Prague);
            var importer = new BlockImporter(
                follower.Engine, follower.BlockStore, follower.StateStore,
                transactionStore: null, receiptStore: null, logStore: null, uncleStore: null,
                logger: null, nodeCommitBlockContext: null, atomicFlush: null, flushCadence: null,
                blockAccessListStore: store);
            var result = await importer.ImportAsync(
                produced.Header, txs, uncles: null, withdrawals: null, CancellationToken.None);

            Assert.True(result.RootMatches, string.Join(",", result.FailedChecks));
            Assert.Null(produced.Header.BlockAccessListHash);
            Assert.Null(await store.GetByBlockHashAsync(result.BlockHash));
        }

        [Fact]
        public async Task Given_AnAmsterdamBlockWithAWrongAccessListHash_When_Imported_Then_NothingIsRetained()
        {
            var store = new InMemoryBlockAccessListStore();
            var (header, txs) = await AmsterdamBlockPipelineHarness.ProduceValidBlockAsync();
            var tampered = AmsterdamBlockPipelineHarness.CloneHeader(header);
            tampered.BlockAccessListHash = new byte[32];

            var result = await AmsterdamBlockPipelineHarness.ImportAsync(tampered, txs, store);

            Assert.True(result.BlockAccessListHashMismatch);
            Assert.Null(result.BlockHash);
            var amsterdamCodec = Model.Codecs.BlockHeaderCodecs.ForFork(HardforkName.Amsterdam);
            Assert.Null(await store.GetByBlockHashAsync(
                new Sha3Keccack().CalculateHash(amsterdamCodec.Encode(tampered))));
            Assert.Null(await store.GetByBlockHashAsync(
                new Sha3Keccack().CalculateHash(amsterdamCodec.Encode(header))));
        }

        // "we retained a block that changed nothing" and "we retained nothing" are different answers, and
        // EIP-7928 gives them different wire values — 0xc0 against null.
        [Fact]
        public async Task Given_ABlockThatChangedNothing_When_Retained_Then_TheEmptyListIsDistinguishableFromAbsence()
        {
            var store = new InMemoryBlockAccessListStore();
            var blockHash = new byte[32];

            await store.SaveAsync(blockHash, BlockAccessListRLPEncoder.Current.Encode(new List<AccountChanges>()));

            Assert.Equal(EmptyRlpList, await store.GetByBlockHashAsync(blockHash));
            Assert.Single(EmptyRlpList);
            Assert.Equal(0xc0, EmptyRlpList[0]);
            Assert.Null(await store.GetByBlockHashAsync(new byte[32] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                                                                       0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }));
        }

        // with no transactions. The mechanism is the EIP-2935 pre-execution system call, which every
        // built for the thinnest block, and that "retained" stays distinguishable from "never retained".
        [Fact]
        public async Task Given_AnAmsterdamBlockWithNoTransactions_When_Imported_Then_ItsListIsStillRetainedVerbatim()
        {
            var store = new InMemoryBlockAccessListStore();
            var (header, txs) = await AmsterdamBlockPipelineHarness.ProduceValidBlockAsync(
                new List<ISignedTransaction>());

            var result = await AmsterdamBlockPipelineHarness.ImportAsync(header, txs, store);

            Assert.True(result.RootMatches, string.Join(",", result.FailedChecks));
            Assert.NotEmpty(result.BlockAccessList);
            var retained = await store.GetByBlockHashAsync(result.BlockHash);
            Assert.NotNull(retained);
            Assert.Equal(BlockAccessListRLPEncoder.Current.Encode(result.BlockAccessList), retained);
            Assert.Equal(header.BlockAccessListHash, new Sha3Keccack().CalculateHash(retained));
            Assert.Null(await store.GetByBlockHashAsync(new byte[32]));
        }

        [Fact]
        public async Task Given_ARetainedAccessList_When_LookedUpByBlockNumber_Then_TheSameBytesAreReturned()
        {
            var blocks = new InMemoryBlockStore();
            var store = new InMemoryBlockAccessListStore(blocks);
            var (header, txs) = await AmsterdamBlockPipelineHarness.ProduceValidBlockAsync();
            var result = await AmsterdamBlockPipelineHarness.ImportAsync(header, txs, store);
            await blocks.SaveAsync(header, result.BlockHash);

            var byNumber = await store.GetByBlockNumberAsync(header.BlockNumber);
            Assert.NotNull(byNumber);
            Assert.Equal(await store.GetByBlockHashAsync(result.BlockHash), byNumber);
            Assert.Null(await store.GetByBlockNumberAsync(header.BlockNumber + 1));
        }

        [Fact]
        public async Task Given_AnAmsterdamBlockIsProduced_When_AStoreIsWired_Then_TheAuthorRetainsWhatItsHeaderCommitsTo()
        {
            var store = new InMemoryBlockAccessListStore();

            var produced = await AmsterdamBlockPipelineHarness.ProduceBlockAsync(
                new List<ISignedTransaction> { AmsterdamBlockPipelineHarness.Transfer(0) },
                AmsterdamBlockPipelineHarness.DefaultBlockGasLimit,
                store);

            var retained = await store.GetByBlockHashAsync(produced.BlockHash);
            Assert.NotNull(retained);
            Assert.Equal(produced.Header.BlockAccessListHash, new Sha3Keccack().CalculateHash(retained));
        }

        [Fact]
        public async Task Given_APreAmsterdamBlockIsProduced_When_AStoreIsWired_Then_NothingIsRetained()
        {
            var store = new InMemoryBlockAccessListStore();
            var producer = await BlockPipelineHarness.CreateAsync(HardforkName.Prague, blockAccessListStore: store);

            var produced = await producer.ProduceAsync(
                new List<ISignedTransaction> { BlockPipelineHarness.Transfer(0) });

            Assert.Null(produced.Header.BlockAccessListHash);
            Assert.Null(await store.GetByBlockHashAsync(produced.BlockHash));
        }

        [Fact]
        public async Task Given_ARetainedAccessList_When_TheBlockIsOrphaned_Then_TheListIsDeletedWithIt()
        {
            var blocks = new InMemoryBlockStore();
            var store = new InMemoryBlockAccessListStore(blocks);
            var (header, txs) = await AmsterdamBlockPipelineHarness.ProduceValidBlockAsync();
            var result = await AmsterdamBlockPipelineHarness.ImportAsync(header, txs, store);
            await blocks.SaveAsync(header, result.BlockHash);
            Assert.NotNull(await store.GetByBlockNumberAsync(header.BlockNumber));

            await store.DeleteByBlockNumberAsync(header.BlockNumber);

            Assert.Null(await store.GetByBlockNumberAsync(header.BlockNumber));
            Assert.Null(await store.GetByBlockHashAsync(result.BlockHash));
        }

        [Fact]
        public async Task Given_TwoRetainedAccessLists_When_OneBlockIsOrphaned_Then_TheOtherSurvives()
        {
            var store = new InMemoryBlockAccessListStore();
            var orphaned = new byte[32];
            var survivor = new byte[32];
            survivor[0] = 1;
            await store.SaveAsync(orphaned, BlockAccessListRLPEncoder.Current.Encode(new List<AccountChanges>()));
            await store.SaveAsync(survivor, BlockAccessListRLPEncoder.Current.Encode(new List<AccountChanges>()));

            await store.DeleteByBlockHashAsync(orphaned);

            Assert.Null(await store.GetByBlockHashAsync(orphaned));
            Assert.NotNull(await store.GetByBlockHashAsync(survivor));
        }

        [Fact]
        public async Task Given_ARetainedAccessList_When_TheCallerMutatesWhatItWasGiven_Then_TheStoreIsUnchanged()
        {
            var store = new InMemoryBlockAccessListStore();
            var blockHash = new byte[32];
            var saved = BlockAccessListRLPEncoder.Current.Encode(new List<AccountChanges>());
            await store.SaveAsync(blockHash, saved);

            var first = await store.GetByBlockHashAsync(blockHash);
            first[0] = 0xff;
            saved[0] = 0xee;

            Assert.Equal(EmptyRlpList, await store.GetByBlockHashAsync(blockHash));
        }

        [Fact]
        public async Task Given_TheFollowerExecutorStack_When_AnAmsterdamBlockIsImported_Then_TheBundleRetainsItsAccessList()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            await Nethereum.CoreChain.Forks.SystemContractPredeploys
                .ApplyGenesisAllocationAsync(bundle.State, HardforkName.Amsterdam);
            await bundle.State.SaveAccountAsync(AmsterdamBlockPipelineHarness.SenderAddress, new Account
            {
                Balance = 1_000_000_000_000_000,
                Nonce = 0
            });
            var config = new ChainConfig
            {
                ChainId = AmsterdamBlockPipelineHarness.ChainId,
                BlockGasLimit = AmsterdamBlockPipelineHarness.DefaultBlockGasLimit,
                BaseFee = 0,
                Hardfork = "Amsterdam"
            };
            var executor = FollowerExecutorStackFactory.BuildFollowerExecutorStack(
                bundle,
                new Nethereum.CoreChain.Forks.FixedChainActivations(HardforkName.Amsterdam),
                chainConfigFactory: _ => config,
                hardforkConfigFactory: _ => config.GetHardforkConfig(),
                rewardPolicy: NoRewardPolicy.Instance);

            var (header, txs) = await AmsterdamBlockPipelineHarness.ProduceValidBlockAsync();
            var result = await executor.ProcessBlockAsync(header, txs, uncles: null, withdrawals: null, CancellationToken.None);

            Assert.True(result.RootMatches, string.Join(",", result.FailedChecks));
            var retained = await bundle.BlockAccessLists.GetByBlockHashAsync(result.BlockHash);
            Assert.NotNull(retained);
            Assert.Equal(header.BlockAccessListHash, new Sha3Keccack().CalculateHash(retained));
        }
    }
}
