using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.AppChain.Sync;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Strategies;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests
{
    [Collection(DevP2PGethFixture.COLLECTION_NAME)]
    public class StrategyBasedFollowerTests
    {
        private static readonly (string Address, string BalanceHex)[] GenesisAlloc = new[]
        {
            ("0x12890d2cce102216644c59daE5baed380d84830c", "0x900000000000000000000"),
            ("0x27Ef5cDBe01777D62438AfFeb695e33fC2335979", "0x9000000000000000000000000000000"),
            ("0xE65B318b9dECf504d1cb6Ea5C367Ca657a070Db1", "0x1000000000000000000000000000000")
        };

        private readonly DevP2PGethFixture _fixture;
        private readonly ITestOutputHelper _output;

        public StrategyBasedFollowerTests(
            DevP2PGethFixture fixture,
            ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task MainnetArchiveTarget_FullReExecution_StateRootMatches()
        {
            var (targetBlock, _) = await DriveTrafficAsync(txCount: 3);

            var follower = BuildFollower(out var stateStore, executionMode: ExecutionMode.FullReExecution);
            await using var disposable = follower.Source;

            byte[]? lastRoot = null;
            follower.Service.BlockImported += (_, e) => lastRoot = e.Execution.ComputedStateRoot;

            var result = await follower.Service.SyncRangeAsync(BigInteger.One, targetBlock);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal((int)targetBlock, result.BlocksImported);
            Assert.NotNull(lastRoot);

            var web3 = _fixture.GetWeb3();
            var gethHeader = await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(new RPC.Eth.DTOs.BlockParameter(targetBlock));
            Assert.Equal(gethHeader.StateRoot, lastRoot!.ToHex(true));

            _output.WriteLine($"PASS: archive target replayed {targetBlock} blocks, final root matches Geth");
        }

        [Fact]
        public async Task IndexerTarget_HeaderOnlyExecution_CapturesAllLogs()
        {
            var (targetBlock, _) = await DriveTrafficAsync(txCount: 3);

            var indexer = new CapturingIndexer();
            var follower = BuildFollower(
                out _,
                executionMode: ExecutionMode.HeaderOnly,
                indexing: indexer);
            await using var disposable = follower.Source;

            var result = await follower.Service.SyncRangeAsync(BigInteger.One, targetBlock);
            Assert.True(result.Success, result.ErrorMessage);

            Assert.Equal((int)targetBlock, indexer.BlocksIndexed);
            Assert.True(indexer.ReceiptsCaptured >= 3, $"Expected >=3 receipts, got {indexer.ReceiptsCaptured}");

            _output.WriteLine(
                $"PASS: indexer captured {indexer.BlocksIndexed} blocks, {indexer.ReceiptsCaptured} receipts " +
                $"without executing EVM");
        }

        private async Task<(ulong targetBlock, string lastRecipient)> DriveTrafficAsync(int txCount)
        {
            var recipient = "0x27Ef5cDBe01777D62438AfFeb695e33fC2335979";
            for (int i = 0; i < txCount; i++)
            {
                var r = await _fixture.SendEtherFromSealerAsync(
                    recipient, BigInteger.Parse("20000000000000000") + i);
                _output.WriteLine($"Tx #{i + 1} -> block #{r.BlockNumber.Value}");
            }

            var web3 = _fixture.GetWeb3();
            var tip = (ulong)(await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()).Value;
            return (tip, recipient);
        }

        private enum ExecutionMode { FullReExecution, HeaderOnly }

        private FollowerBundle BuildFollower(
            out InMemoryStateStore stateStore,
            ExecutionMode executionMode,
            IIndexingStrategy? indexing = null)
        {
            stateStore = SeedGenesisAlloc();

            var chainConfig = new ChainConfig
            {
                ChainId = DevP2PGethFixture.ChainId,
                BaseFee = BigInteger.Zero,
                Coinbase = DevP2PGethFixture.SealerAddress,
                Hardfork = "london"
            };

            IExecutionStrategy execution = executionMode == ExecutionMode.FullReExecution
                ? new FullReExecutionStrategy(BuildReExecutor(stateStore, chainConfig))
                : new HeaderOnlyExecutionStrategy();

            var devP2PConfig = new DevP2PConfig
            {
                NetworkId = _fixture.NetworkId,
                ConnectTimeoutMs = 5000,
                HandshakeTimeoutMs = 5000,
                RequestTimeoutMs = 10000
            };
            var source = new DevP2PSequencerRpcClient(_fixture.Enode, devP2PConfig, _fixture.GenesisHash);

            var service = new DevP2PFollowerService(source, execution, indexing ?? new NoIndexingStrategy());
            return new FollowerBundle(service, source);
        }

        private IBlockExecutor BuildReExecutor(InMemoryStateStore stateStore, ChainConfig chainConfig)
        {
            var blockStore = new InMemoryBlockStore();
            var trieNodeStore = new InMemoryTrieNodeStore();
            var stateRootCalculator = new IncrementalStateRootCalculator(stateStore, trieNodeStore);
            var activations = new FixedChainActivations(HardforkName.London);
            var engine = new BlockExecutor(
                stateStore,
                blockStore,
                activations,
                chainConfigFactory: _ => chainConfig,
                hardforkConfigFactory: _ => chainConfig.GetHardforkConfig(),
                stateRootCalculator: stateRootCalculator,
                rewardPolicy: EthereumProofOfWorkRewardPolicy.Instance,
                trieNodeStore: trieNodeStore);
            return new BlockImporter(engine, blockStore, stateStore);
        }

        private InMemoryStateStore SeedGenesisAlloc()
        {
            var stateStore = new InMemoryStateStore();
            foreach (var (address, balanceHex) in GenesisAlloc)
            {
                var balance = new HexBigInteger(balanceHex).Value;
                stateStore.SaveAccountAsync(address, new Account
                {
                    Nonce = EvmUInt256.Zero,
                    Balance = EvmUInt256.FromBigEndian(balance.ToByteArray(isUnsigned: true, isBigEndian: true)),
                    CodeHash = DefaultValues.EMPTY_DATA_HASH
                }).GetAwaiter().GetResult();
            }
            return stateStore;
        }

        private sealed class FollowerBundle
        {
            public DevP2PFollowerService Service { get; }
            public DevP2PSequencerRpcClient Source { get; }
            public FollowerBundle(DevP2PFollowerService service, DevP2PSequencerRpcClient source)
            {
                Service = service;
                Source = source;
            }
        }

        private sealed class CapturingIndexer : IIndexingStrategy
        {
            public int BlocksIndexed { get; private set; }
            public int ReceiptsCaptured { get; private set; }

            public Task IndexAsync(LiveBlockData block, ExecutionStrategyResult execution, CancellationToken ct = default)
            {
                BlocksIndexed++;
                ReceiptsCaptured += block.Receipts.Count;
                return Task.CompletedTask;
            }
        }
    }
}
