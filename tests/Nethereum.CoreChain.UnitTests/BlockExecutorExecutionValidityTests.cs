using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BlockExecutorExecutionValidityTests
    {
        private const string PrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string SenderAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string RecipientAddress = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
        private static readonly BigInteger ChainId = 1337;
        private static readonly LegacyTransactionSigner Signer = new();

        private static ISignedTransaction CreateSignedTransaction(BigInteger nonce)
        {
            var signedTxHex = Signer.SignTransaction(
                PrivateKey.HexToByteArray(), ChainId, RecipientAddress, 100, nonce, 2_000_000_000, 21_000, "");
            return TransactionFactory.CreateTransaction(signedTxHex);
        }

        private static ChainConfig BuildChainConfig() =>
            new ChainConfig { ChainId = ChainId, BlockGasLimit = 30_000_000, BaseFee = 0 };

        private static async Task SeedSenderAsync(InMemoryStateStore stateStore) =>
            await stateStore.SaveAccountAsync(SenderAddress, new Account
            {
                Balance = 1_000_000_000_000_000,
                Nonce = 0
            });

        private static BlockExecutor BuildEngine(
            InMemoryStateStore stateStore, InMemoryBlockStore blockStore, ChainConfig config,
            ITrieNodeStore trieNodeStore, IIncrementalStateRootCalculator stateRootCalculator) =>
            new BlockExecutor(
                stateStore,
                blockStore,
                new FixedChainActivations(Nethereum.EVM.HardforkName.Prague),
                chainConfigFactory: _ => config,
                hardforkConfigFactory: _ => config.GetHardforkConfig(),
                stateRootCalculator: stateRootCalculator,
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: trieNodeStore);

        private static async Task<(BlockHeader Header, List<ISignedTransaction> Transactions)> BuildValidBlockAsync()
        {
            var stateStore = new InMemoryStateStore();
            await Nethereum.CoreChain.Forks.SystemContractPredeploys
                .ApplyGenesisAllocationAsync(stateStore, Nethereum.EVM.HardforkName.Prague);
            await SeedSenderAsync(stateStore);
            var blockStore = new InMemoryBlockStore();
            var config = BuildChainConfig();
            var trieNodeStore = new InMemoryContentNodeStore();
            var stateRootCalculator = new IncrementalStateRootCalculator(stateStore, trieNodeStore);
            var engine = BuildEngine(stateStore, blockStore, config, trieNodeStore, stateRootCalculator);

            var producer = new BlockProducer(
                engine, blockStore,
                new InMemoryTransactionStore(blockStore), new InMemoryReceiptStore(), new InMemoryLogStore(),
                stateStore, trieNodeStore, stateRootCalculator);

            var txs = new List<ISignedTransaction> { CreateSignedTransaction(nonce: 0) };
            var result = await producer.ProduceBlockAsync(txs, new BlockProductionOptions
            {
                Timestamp = 1_700_000_000,
                Coinbase = SenderAddress,
                BaseFee = 0,
                BlockGasLimit = 30_000_000,
                Difficulty = 1,
                ChainId = ChainId
            });

            return (result.Header, txs);
        }

        private static async Task<BlockImporter> BuildFollowerImporterAsync()
        {
            var stateStore = new InMemoryStateStore();
            await Nethereum.CoreChain.Forks.SystemContractPredeploys
                .ApplyGenesisAllocationAsync(stateStore, Nethereum.EVM.HardforkName.Prague);
            await SeedSenderAsync(stateStore);
            var blockStore = new InMemoryBlockStore();
            var config = BuildChainConfig();
            var trieNodeStore = new InMemoryContentNodeStore();
            var stateRootCalculator = new IncrementalStateRootCalculator(stateStore, trieNodeStore);
            var engine = BuildEngine(stateStore, blockStore, config, trieNodeStore, stateRootCalculator);
            return new BlockImporter(engine, blockStore, stateStore);
        }

        private static BlockHeader CloneHeader(BlockHeader header)
        {
            var provider = RlpBlockEncodingProvider.Instance;
            return provider.DecodeBlockHeader(provider.EncodeBlockHeader(header));
        }

        [Fact]
        public async Task ValidBlock_AllChecksPass_Commits()
        {
            var (header, txs) = await BuildValidBlockAsync();
            var importer = await BuildFollowerImporterAsync();

            var result = await importer.ImportAsync(header, txs, uncles: null, withdrawals: null, CancellationToken.None);

            Assert.True(result.RootMatches, string.Join(",", result.FailedChecks));
            Assert.False(result.StateRootMismatch);
            Assert.False(result.ReceiptsRootMismatch);
            Assert.False(result.LogsBloomMismatch);
            Assert.False(result.GasUsedMismatch);
            Assert.NotNull(result.BlockHash);
        }

        [Fact]
        public async Task CorrectStateRoot_WrongReceiptsRoot_FailsGate_DoesNotCommit()
        {
            var (header, txs) = await BuildValidBlockAsync();
            var tampered = CloneHeader(header);
            tampered.ReceiptHash = new byte[32];
            var importer = await BuildFollowerImporterAsync();

            var result = await importer.ImportAsync(tampered, txs, uncles: null, withdrawals: null, CancellationToken.None);

            Assert.False(result.StateRootMismatch);
            Assert.True(result.ReceiptsRootMismatch);
            Assert.False(result.RootMatches);
            Assert.Contains("receiptsRoot", result.FailedChecks);
            Assert.Null(result.BlockHash);
        }

        [Fact]
        public async Task CorrectStateRoot_WrongLogsBloom_FailsGate_DoesNotCommit()
        {
            var (header, txs) = await BuildValidBlockAsync();
            var tampered = CloneHeader(header);
            var bloom = new byte[256];
            bloom[0] = 0xFF;
            tampered.LogsBloom = bloom;
            var importer = await BuildFollowerImporterAsync();

            var result = await importer.ImportAsync(tampered, txs, uncles: null, withdrawals: null, CancellationToken.None);

            Assert.False(result.StateRootMismatch);
            Assert.True(result.LogsBloomMismatch);
            Assert.False(result.RootMatches);
            Assert.Contains("logsBloom", result.FailedChecks);
            Assert.Null(result.BlockHash);
        }

        [Fact]
        public async Task CorrectStateRoot_WrongGasUsed_FailsGate_DoesNotCommit()
        {
            var (header, txs) = await BuildValidBlockAsync();
            var tampered = CloneHeader(header);
            tampered.GasUsed = tampered.GasUsed + 1;
            var importer = await BuildFollowerImporterAsync();

            var result = await importer.ImportAsync(tampered, txs, uncles: null, withdrawals: null, CancellationToken.None);

            Assert.False(result.StateRootMismatch);
            Assert.True(result.GasUsedMismatch);
            Assert.False(result.RootMatches);
            Assert.Contains("gasUsed", result.FailedChecks);
            Assert.Null(result.BlockHash);
        }
    }
}
