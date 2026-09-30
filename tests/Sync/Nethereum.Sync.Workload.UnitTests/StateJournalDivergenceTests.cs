using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Chain.TestData.Vectors;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Chain.TestData.UnitTests
{
    public class StateJournalDivergenceTests
    {
        private readonly ITestOutputHelper _output;

        public StateJournalDivergenceTests(ITestOutputHelper output) => _output = output;

        private static async Task<InProcessSequencerDriver> WorkloadAsync()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 50);
            await new WorkloadV1().BuildAsync(sequencer);
            return sequencer;
        }


        private static string LastDiagnosis;

        private static async Task<int> ReplayAsync(
            InProcessSequencerDriver sequencer, bool fullArchive, bool commitBracket,
            Nethereum.CoreChain.Storage.HistoricalStateOptions journal = null, bool withSigner = false)
        {
            var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "probe-" + Guid.NewGuid().ToString("N"));

            var journalOptions = journal ?? (fullArchive
                ? Nethereum.CoreChain.Storage.HistoricalStateOptions.FullArchive
                : null);

            using var bundle = withSigner
                ? Nethereum.CoreChain.RocksDB.RocksDbChainStoreBundle.Open(
                    dir, journalOptions, bulkSync: false, storageOptions: null,
                    signer: new Nethereum.Signer.TransactionVerificationAndRecoveryImp())
                : Nethereum.CoreChain.RocksDB.RocksDbChainStoreBundle.Open(dir, journalOptions);

            var config = Nethereum.AppChain.AppChainConfig.CreateWithName(
                InProcessSequencerDriver.ChainName, sequencer.ChainId);
            config.SequencerAddress = sequencer.SequencerAddress;
            var appChain = new Nethereum.AppChain.AppChain(
                config, bundle.Blocks, bundle.Transactions, bundle.Receipts, bundle.Logs, bundle.State, bundle.TrieNodes);
            await appChain.InitializeAsync(sequencer.Genesis);

            var chainConfig = new Nethereum.CoreChain.ChainConfig
            {
                ChainId = sequencer.ChainId,
                BaseFee = System.Numerics.BigInteger.Zero,
                Coinbase = sequencer.SequencerAddress
            };
            var activations = new Nethereum.CoreChain.Forks.FixedChainActivations(
                Nethereum.EVM.HardforkNames.Parse("prague"));
            var hardforkConfig = chainConfig.GetHardforkConfig();
            var calc = new Nethereum.CoreChain.IncrementalStateRootCalculator(bundle.State, bundle.TrieNodes);
            var engine = new Nethereum.CoreChain.BlockExecutor(
                bundle.State, bundle.Blocks, activations,
                chainConfigFactory: _ => chainConfig, hardforkConfigFactory: _ => hardforkConfig,
                stateRootCalculator: calc, rewardPolicy: Nethereum.CoreChain.NoRewardPolicy.Instance,
                trieNodeStore: bundle.TrieNodes);

            var importer = commitBracket
                ? new Nethereum.CoreChain.BlockImporter(
                    engine, bundle.Blocks, bundle.State, bundle.Transactions, bundle.Receipts, bundle.Logs,
                    uncleStore: bundle.Uncles,
                    nodeCommitBlockContext: bundle.NodeCommitBlockSource,
                    atomicFlush: bundle as Nethereum.CoreChain.Storage.IAtomicBlockFlush)
                : new Nethereum.CoreChain.BlockImporter(
                    engine, bundle.Blocks, bundle.State, bundle.Transactions, bundle.Receipts, bundle.Logs,
                    uncleStore: bundle.Uncles);

            foreach (var (header, txs) in sequencer.ProducedBlockData)
            {
                var w = await sequencer.Withdrawals.GetByBlockNumberAsync(header.BlockNumber);
                var r = await importer.ImportAsync(header, txs, null, w);
                if (!r.RootMatches)
                {
                    LastDiagnosis =
                        $"block {header.BlockNumber}: anyFailed={r.FailedChecks.Count > 0} " +
                        $"failed=[{string.Join(",", r.FailedChecks)}] " +
                        $"computed={(r.ComputedStateRoot == null ? "null" : r.ComputedStateRoot.ToHex())} " +
                        $"expected={(r.ExpectedStateRoot == null ? "null" : r.ExpectedStateRoot.ToHex())} " +
                        $"error={r.ErrorMessage}";
                    return (int)header.BlockNumber;
                }
                bundle.Metadata.Commit((ulong)header.BlockNumber, r.BlockHash);
            }

            return -1;
        }

        [Fact]
        public async Task Given_TheSameWorkload_When_ReplayedWithAPruningJournalAndASigner_Then_EveryBlockRootMatches()
        {
            var journal = new Nethereum.CoreChain.Storage.HistoricalStateOptions
            {
                MaxHistoryBlocks = 128,
                EnablePruning = true,
                PruningIntervalBlocks = 64
            };

            var at = await ReplayAsync(await WorkloadAsync(), false, true, journal, withSigner: true);
            _output.WriteLine($"pruning journal + bracket + SIGNER -> diverged at block {at} (-1 means none)");
            Assert.Equal(-1, at);
        }

        [Fact]
        public async Task Given_TheSameWorkload_When_ReplayedWithAPruningJournal_Then_EveryBlockRootMatches()
        {
            var journal = new Nethereum.CoreChain.Storage.HistoricalStateOptions
            {
                MaxHistoryBlocks = 128,
                EnablePruning = true,
                PruningIntervalBlocks = 64
            };

            var at = await ReplayAsync(await WorkloadAsync(), false, true, journal);
            _output.WriteLine($"pruning journal + bracket -> block {at}; {LastDiagnosis}");
            Assert.Equal(-1, at);
        }

        [Fact]
        public async Task Given_TheSameWorkload_When_ReplayedWithAnUnprunedJournalOfTheSameDepth_Then_EveryBlockRootMatches()
        {
            var journal = new Nethereum.CoreChain.Storage.HistoricalStateOptions
            {
                MaxHistoryBlocks = 128,
                EnablePruning = false,
                PruningIntervalBlocks = 0
            };

            var at = await ReplayAsync(await WorkloadAsync(), false, true, journal);
            _output.WriteLine($"journal 128, pruning OFF + bracket -> diverged at block {at} (-1 means none)");
            Assert.Equal(-1, at);
        }

        [Fact]
        public async Task Given_TheSameWorkload_When_ReplayedWithTheCommitBracketButNoFullArchive_Then_EveryBlockRootMatches()
        {
            var at = await ReplayAsync(await WorkloadAsync(), fullArchive: false, commitBracket: true);
            _output.WriteLine($"bracket, no full archive -> diverged at block {at} (-1 means none)");
            Assert.Equal(-1, at);
        }

        [Fact]
        public async Task Given_TheSameWorkload_When_ReplayedWithFullArchiveButNoCommitBracket_Then_EveryBlockRootMatches()
        {
            var at = await ReplayAsync(await WorkloadAsync(), fullArchive: true, commitBracket: false);
            _output.WriteLine($"full archive, no bracket -> diverged at block {at} (-1 means none)");
            Assert.Equal(-1, at);
        }

        [Fact]
        public async Task Given_TheSameWorkload_When_ReplayedWithBoth_Then_EveryBlockRootMatches()
        {
            var at = await ReplayAsync(await WorkloadAsync(), fullArchive: true, commitBracket: true);
            _output.WriteLine($"full archive + bracket -> diverged at block {at} (-1 means none)");
            Assert.Equal(-1, at);
        }

        [Fact]
        public async Task Given_TheSameWorkload_When_ReplayedHashKeyed_Then_EveryBlockRootMatches()
        {
            var sequencer = await WorkloadAsync();

            using var server = await PathKeyedWorkloadServer.CreateAsync(sequencer, pathKeyed: false);

            _output.WriteLine($"hash-keyed replay completed, height={await server.Bundle.Blocks.GetHeightAsync()}");
        }

        [Fact]
        public async Task Given_TheSameWorkload_When_ReplayedPathKeyed_Then_EveryBlockRootMatches()
        {
            var sequencer = await WorkloadAsync();

            using var server = await PathKeyedWorkloadServer.CreateAsync(
                sequencer, pathKeyed: true, trieNodeHistoryBlocks: 32, historyIndex: true);

            _output.WriteLine($"path-keyed replay completed, height={await server.Bundle.Blocks.GetHeightAsync()}");
        }

        [Fact]
        public async Task Given_TheSameWorkload_When_ReplayedByAPlainImporterOnRocksDb_Then_EveryBlockRootMatches()
        {
            var sequencer = await WorkloadAsync();
            var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "probe-" + Guid.NewGuid().ToString("N"));

            using var bundle = Nethereum.CoreChain.RocksDB.RocksDbChainStoreBundle.Open(dir);

            var config = Nethereum.AppChain.AppChainConfig.CreateWithName(
                InProcessSequencerDriver.ChainName, sequencer.ChainId);
            config.SequencerAddress = sequencer.SequencerAddress;
            var appChain = new Nethereum.AppChain.AppChain(
                config, bundle.Blocks, bundle.Transactions, bundle.Receipts, bundle.Logs, bundle.State, bundle.TrieNodes);
            await appChain.InitializeAsync(sequencer.Genesis);

            var chainConfig = new Nethereum.CoreChain.ChainConfig
            {
                ChainId = sequencer.ChainId,
                BaseFee = System.Numerics.BigInteger.Zero,
                Coinbase = sequencer.SequencerAddress
            };
            var activations = new Nethereum.CoreChain.Forks.FixedChainActivations(
                Nethereum.EVM.HardforkNames.Parse("prague"));
            var hardforkConfig = chainConfig.GetHardforkConfig();
            var calc = new Nethereum.CoreChain.IncrementalStateRootCalculator(bundle.State, bundle.TrieNodes);
            var engine = new Nethereum.CoreChain.BlockExecutor(
                bundle.State, bundle.Blocks, activations,
                chainConfigFactory: _ => chainConfig, hardforkConfigFactory: _ => hardforkConfig,
                stateRootCalculator: calc, rewardPolicy: Nethereum.CoreChain.NoRewardPolicy.Instance,
                trieNodeStore: bundle.TrieNodes);
            var importer = new Nethereum.CoreChain.BlockImporter(
                engine, bundle.Blocks, bundle.State, bundle.Transactions, bundle.Receipts, bundle.Logs,
                uncleStore: bundle.Uncles);

            foreach (var (header, txs) in sequencer.ProducedBlockData)
            {
                var w = await sequencer.Withdrawals.GetByBlockNumberAsync(header.BlockNumber);
                var r = await importer.ImportAsync(header, txs, null, w);
                Assert.True(r.RootMatches, $"plain importer on RocksDB diverged at block {header.BlockNumber}");
            }

            _output.WriteLine("plain importer on RocksDB replayed every block");
        }

        [Fact]
        public async Task Given_TheSameWorkload_When_ReplayedPathKeyedWithoutNodeHistory_Then_EveryBlockRootMatches()
        {
            var sequencer = await WorkloadAsync();

            using var server = await PathKeyedWorkloadServer.CreateAsync(
                sequencer, pathKeyed: true, trieNodeHistoryBlocks: -1, historyIndex: false);

            _output.WriteLine($"path-keyed, no node history, completed, height={await server.Bundle.Blocks.GetHeightAsync()}");
        }
    }
}
