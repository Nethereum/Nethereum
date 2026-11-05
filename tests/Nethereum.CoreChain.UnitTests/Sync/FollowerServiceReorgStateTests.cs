using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Sync
{
    public class FollowerServiceReorgStateTests
    {
        private const long FirstBlockTimestamp = 1_700_000_000;

        private sealed class ScriptedTips : ICanonicalStateRootSource
        {
            private readonly Queue<CanonicalTip> _tips;
            public string Name => "scripted";

            private readonly Func<Task> _onPoll;

            public ScriptedTips(IEnumerable<CanonicalTip> tips, Func<Task> onPoll)
            {
                _tips = new Queue<CanonicalTip>(tips);
                _onPoll = onPoll;
            }

            public Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(ulong blockNumber, CancellationToken ct)
                => Task.FromResult<(byte[], byte[])>((null, null));

            public async Task<CanonicalTip> GetLatestAsync(CancellationToken ct)
            {
                await _onPoll();
                return _tips.Count == 0 ? null : _tips.Dequeue();
            }
        }

        private sealed class AfterBlockExecutor : IBlockExecutor
        {
            private readonly IBlockExecutor _inner;
            private readonly Func<ulong, Task> _afterBlock;

            public AfterBlockExecutor(IBlockExecutor inner, Func<ulong, Task> afterBlock)
            {
                _inner = inner;
                _afterBlock = afterBlock;
            }

            public List<ulong> Executed { get; } = new();

            public async Task<BlockImporterResult> ProcessBlockAsync(
                BlockHeader header, IList<ISignedTransaction> transactions, IList<BlockHeader> uncles,
                IList<Withdrawal> withdrawals, CancellationToken ct)
            {
                var result = await _inner.ProcessBlockAsync(header, transactions, uncles, withdrawals, ct);
                Executed.Add((ulong)header.BlockNumber);
                if (result.RootMatches) await _afterBlock((ulong)header.BlockNumber);
                return result;
            }
        }

        private sealed class CapturingLogger : Microsoft.Extensions.Logging.ILogger
        {
            public List<string> Entries { get; } = new();

            public IDisposable BeginScope<TState>(TState state) => null;
            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

            public void Log<TState>(
                Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
                TState state, Exception exception, Func<TState, Exception, string> formatter)
                => Entries.Add(logLevel + " " + formatter(state, exception));
        }

        private sealed class AcceptEverythingPolicy : IValidationPolicy
        {
            public bool ShouldAnchorAt(ulong blockNumber) => false;
            public ValidationAction OnVerdict(DivergenceVerdict verdict, ulong blockNumber) => ValidationAction.Continue;
        }

        private static Task<BlockProductionResult> ProduceAsync(
            BlockPipelineHarness harness, long number, List<ISignedTransaction> transactions) =>
            harness.Producer().ProduceBlockAsync(transactions, new BlockProductionOptions
            {
                Timestamp = FirstBlockTimestamp + 12 * (number - 1),
                Coinbase = BlockPipelineHarness.SenderAddress,
                BaseFee = 0,
                BlockGasLimit = 30_000_000,
                Difficulty = 0,
                ChainId = BlockPipelineHarness.ChainId
            });

        private static ISignedTransaction StorageWritingCreation(int nonce) =>
            TransactionFactory.CreateTransaction(new Transaction1559Signer().SignTransaction(
                BlockPipelineHarness.PrivateKey.HexToByteArray(),
                new Transaction1559(BlockPipelineHarness.ChainId, nonce, 2_000_000_000, 2_000_000_000, 200_000, "", 0, "6012600055", null)));

        private static List<ISignedTransaction> Transfers(params int[] nonces) =>
            nonces.Select(n => BlockPipelineHarness.Transfer(n)).ToList();

        private static async Task SaveBlockAsync(IChainStoreBundle bundle, BlockProductionResult block)
        {
            var number = (ulong)block.Header.BlockNumber;
            await bundle.Blocks.SaveAsync(block.Header, block.BlockHash);
            await bundle.Uncles.SaveAsync(block.BlockHash, new List<BlockHeader>());
            await SaveBodyAsync(bundle, block);
        }

        private static Task SaveBodyAsync(IChainStoreBundle bundle, BlockProductionResult block) =>
            bundle.Transactions.SaveManyAsync(block.BlockHash, block.Header.BlockNumber, block.IncludedTransactions);

        [Fact]
        public Task W2_T4_Given_ARewindOnTheTipPath_When_TheNextBlockOfTheNewBranchExecutes_Then_ItRunsAgainstTheAncestorsStateWithNoPendingCalculatorState()
            => RunReorgAsync(clKeepsHeadersLaid: true);

        [Fact]
        public Task W2_T1_Given_ARealExecutorStackOnTheTipPath_When_TheClRelaysTheLast3Blocks_Then_TheNewBranchExecutesToTheNewTipWithMatchingRoots()
            => RunReorgAsync(clKeepsHeadersLaid: false);

        private static async Task RunReorgAsync(bool clKeepsHeadersLaid)
        {
            var main = await BlockPipelineHarness.CreateAsync();
            var fork = await BlockPipelineHarness.CreateAsync();
            var mainBlocks = new List<BlockProductionResult>();
            var forkBlocks = new List<BlockProductionResult>();
            mainBlocks.Add(await ProduceAsync(main, 1, Transfers(0)));
            mainBlocks.Add(await ProduceAsync(main, 2, Transfers(1)));
            mainBlocks.Add(await ProduceAsync(main, 3, new List<ISignedTransaction> { StorageWritingCreation(2) }));
            mainBlocks.Add(await ProduceAsync(main, 4, Transfers(3)));
            forkBlocks.Add(await ProduceAsync(fork, 1, Transfers(0)));
            forkBlocks.Add(await ProduceAsync(fork, 2, Transfers(1)));
            forkBlocks.Add(await ProduceAsync(fork, 3, new List<ISignedTransaction>()));
            forkBlocks.Add(await ProduceAsync(fork, 4, Transfers(2)));
            forkBlocks.Add(await ProduceAsync(fork, 5, Transfers(3)));
            Assert.Equal(mainBlocks[1].BlockHash, forkBlocks[1].BlockHash);
            Assert.NotEqual(mainBlocks[2].BlockHash, forkBlocks[2].BlockHash);

            var bundle = InMemoryChainStoreBundle.Open(HistoricalStateOptions.FullArchive);
            await SystemContractPredeploys.ApplyGenesisAllocationAsync(bundle.State, HardforkName.Prague);
            await bundle.State.SaveAccountAsync(BlockPipelineHarness.SenderAddress, new Account
            {
                Balance = 1_000_000_000_000_000,
                Nonce = 0
            });
            var genesisHash = await ShapedHeaders.SaveGenesisAsync(bundle.Blocks, HardforkName.Prague, 30_000_000);
            bundle.Metadata.Commit(0, genesisHash);
            foreach (var block in mainBlocks) await SaveBlockAsync(bundle, block);
            bundle.Metadata.SaveCheckpoint(2, mainBlocks[1].Header.StateRoot, mainBlocks[1].BlockHash);

            var config = new ChainConfig { ChainId = BlockPipelineHarness.ChainId, BlockGasLimit = 30_000_000, BaseFee = 0 };
            var real = FollowerExecutorStackFactory.BuildFollowerExecutorStack(
                bundle, new FixedChainActivations(HardforkName.Prague),
                _ => config, _ => config.GetHardforkConfig(), NoRewardPolicy.Instance);
            var relaid = false;
            var executor = new AfterBlockExecutor(real, async n =>
            {
                if (n != 4 || relaid) return;
                relaid = true;
                for (var i = 2; i < forkBlocks.Count; i++) await SaveBlockAsync(bundle, forkBlocks[i]);
            });
            var tips = new ScriptedTips(new[]
            {
                new CanonicalTip { BlockNumber = 4, BlockHash = mainBlocks[3].BlockHash },
                new CanonicalTip { BlockNumber = 5, BlockHash = forkBlocks[4].BlockHash },
                new CanonicalTip { BlockNumber = 5, BlockHash = forkBlocks[4].BlockHash },
                new CanonicalTip { BlockNumber = 5, BlockHash = forkBlocks[4].BlockHash },
            }, async () =>
            {
                if (!relaid || !clKeepsHeadersLaid) return;
                for (var i = 2; i < forkBlocks.Count; i++) await SaveBlockAsync(bundle, forkBlocks[i]);
            });
            BodyRepairDelegate refetch = async (from, to, b, ct) =>
            {
                for (var i = (int)from - 1; i < forkBlocks.Count; i++) await SaveBodyAsync(b, forkBlocks[i]);
                return true;
            };
            BackwardWalkerDelegate neverWalks = (f, h, t, b, ct, n) =>
                throw new InvalidOperationException("the external-header-follow path must not walk");

            var log = new CapturingLogger();
            var result = await new FollowerService(neverWalks, null, refetch).RunAsync(
                new LocalReplayBlockSource(new List<BlockBundle>()),
                () => bundle,
                _ => executor,
                new AcceptEverythingPolicy(),
                tips,
                new FollowerOptions(StartBlock: 0, CheckpointEvery: 0, AnchorEvery: 0, EndBlock: 5,
                    TipPollInterval: TimeSpan.FromMilliseconds(10)) { ExternalHeaderFollow = true },
                new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token,
                log);

            Assert.True(
                result.ExitReason == FollowerExitReason.SourceCompleted,
                result.ExitReason + " " + result.Detail + Environment.NewLine + string.Join(Environment.NewLine, log.Entries));
            Assert.Equal(0UL, result.RootMismatches);
            Assert.Equal(5UL, result.LastExecutedBlock);
            Assert.Equal(forkBlocks[4].BlockHash, bundle.Metadata.GetLastBlockHash());
            Assert.Equal(new ulong[] { 1, 2, 3, 4, 3, 4, 5 }, executor.Executed.ToArray());
        }
    }
}
