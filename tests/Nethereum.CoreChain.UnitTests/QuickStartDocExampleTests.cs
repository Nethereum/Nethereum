using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Documentation;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class QuickStartDocExampleTests
    {
        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "corechain-quick-start",
            "Execute a block and read the validity verdict", Order = 1)]
        public async Task Given_ABlockThisNodeSealedItself_When_ImportedByASecondNode_Then_TheVerdictIsRootMatches()
        {
            var sealer = await OpenNodeAsync();
            var transactions = new List<ISignedTransaction> { SignedTransfer(nonce: 0) };
            var sealed_ = await new BlockProducer(
                    sealer.Engine, sealer.Stores.Blocks, sealer.Stores.Transactions,
                    sealer.Stores.Receipts, sealer.Stores.Logs, sealer.Stores.State,
                    sealer.Stores.TrieNodes, sealer.Roots)
                .ProduceBlockAsync(transactions, ProductionOptions());

            var follower = await OpenNodeAsync();
            var verdict = await new BlockImporter(follower.Engine, follower.Stores.Blocks, follower.Stores.State)
                .ImportAsync(sealed_.Header, transactions, uncles: null, withdrawals: null);

            Assert.True(verdict.RootMatches, verdict.DescribeRejection());
            Assert.Equal("the block passed every validity check", verdict.DescribeRejection());
            Assert.Empty(verdict.FailedChecks);
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "corechain-quick-start",
            "A rejected block names the check it failed", Order = 2)]
        public async Task Given_AHeaderClaimingMoreGasThanItsOwnLimit_When_Imported_Then_TheVerdictNamesGasCapacity()
        {
            var sealer = await OpenNodeAsync();
            var transactions = new List<ISignedTransaction> { SignedTransfer(nonce: 0) };
            var sealed_ = await new BlockProducer(
                    sealer.Engine, sealer.Stores.Blocks, sealer.Stores.Transactions,
                    sealer.Stores.Receipts, sealer.Stores.Logs, sealer.Stores.State,
                    sealer.Stores.TrieNodes, sealer.Roots)
                .ProduceBlockAsync(transactions, ProductionOptions());

            var encoding = RlpBlockEncodingProvider.Instance;
            var tampered = encoding.DecodeBlockHeader(encoding.EncodeBlockHeader(sealed_.Header));
            tampered.GasUsed = tampered.GasLimit + 1;

            var follower = await OpenNodeAsync();
            var verdict = await new BlockImporter(follower.Engine, follower.Stores.Blocks, follower.Stores.State)
                .ImportAsync(tampered, transactions, uncles: null, withdrawals: null);

            Assert.False(verdict.RootMatches);
            Assert.Contains(BlockValidityCheck.GasCapacity, verdict.FailedValidityChecks);
            Assert.Contains("gasCapacity", verdict.FailedChecks);
            Assert.StartsWith("failed checks: gasCapacity", verdict.DescribeRejection());
            Assert.Null(verdict.BlockHash);
        }

        private const string SenderPrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string SenderAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string RecipientAddress = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
        private const int ChainId = 1337;

        private static async Task<(BlockExecutor Engine, InMemoryChainStoreBundle Stores,
            IIncrementalStateRootCalculator Roots)> OpenNodeAsync()
        {
            var stores = InMemoryChainStoreBundle.Open();
            await SystemContractPredeploys.ApplyGenesisAllocationAsync(stores.State, HardforkName.Prague);
            await stores.State.SaveAccountAsync(SenderAddress, new Account { Balance = 1_000_000_000_000_000, Nonce = 0 });

            var config = new ChainConfig { ChainId = ChainId, BlockGasLimit = 30_000_000, BaseFee = 0 };
            var roots = new IncrementalStateRootCalculator(stores.State, stores.TrieNodes);
            var engine = new BlockExecutor(
                stores.State, stores.Blocks,
                new FixedChainActivations(HardforkName.Prague),
                chainConfigFactory: _ => config,
                hardforkConfigFactory: _ => config.GetHardforkConfig(),
                stateRootCalculator: roots,
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: stores.TrieNodes);

            return (engine, stores, roots);
        }

        private static BlockProductionOptions ProductionOptions() => new BlockProductionOptions
        {
            Timestamp = 1_700_000_000,
            Coinbase = SenderAddress,
            BaseFee = 0,
            BlockGasLimit = 30_000_000,
            Difficulty = 1,
            ChainId = ChainId
        };

        private static ISignedTransaction SignedTransfer(int nonce) =>
            TransactionFactory.CreateTransaction(
                new LegacyTransactionSigner().SignTransaction(
                    SenderPrivateKey.HexToByteArray(), ChainId, RecipientAddress,
                    amount: 100, nonce: nonce, gasPrice: 2_000_000_000, gasLimit: 21_000, data: ""));
    }
}
