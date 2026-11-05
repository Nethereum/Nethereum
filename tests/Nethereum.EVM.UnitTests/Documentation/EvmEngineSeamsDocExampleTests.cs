using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Documentation;
using Nethereum.EVM;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.EVM.Execution.TransferLogs.Rules;
using Nethereum.EVM.Precompiles;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests.Documentation
{
    public class EvmEngineSeamsDocExampleTests
    {
        private sealed class CountingObserver : IPrecompileCallObserver
        {
            public readonly List<int> Calls = new List<int>();

            public void OnCall(int address) => Calls.Add(address);
        }

        [NethereumDocExample(DocSection.EvmSimulator, "blob-schedule", "State blob parameters per fork and per timestamp, then resolve them", Order = 1)]
        [Fact]
        public void ABlobScheduleResolvesTheParametersActiveAtATimestamp()
        {
            var osaka = new BlobParameters(target: 6, max: 9, baseFeeUpdateFraction: 5_007_716);
            var bpo1 = new BlobParameters(target: 10, max: 15, baseFeeUpdateFraction: 8_346_193);

            var schedule = new BlobSchedule(new[]
            {
                BlobScheduleEntry.ForFork(HardforkName.Osaka, osaka),
                BlobScheduleEntry.AtTimestamp("bpo1", activationTimestamp: 2_000, bpo1)
            });

            Assert.Equal(osaka, schedule.At(HardforkName.Osaka, protocolForkActivatedAt: 1_000, timestamp: 1_999));
            Assert.Equal(bpo1, schedule.At(HardforkName.Osaka, protocolForkActivatedAt: 1_000, timestamp: 2_000));
            Assert.Null(schedule.At(HardforkName.Prague, protocolForkActivatedAt: 0, timestamp: 1_999));
        }

        [NethereumDocExample(DocSection.EvmSimulator, "blob-schedule", "Mainnet reaches the BPO parameters while ResolveAt stays on Osaka", Order = 2)]
        [Fact]
        public void MainnetResolvesBpoParametersWhileTheForkStaysOsaka()
        {
            var activations = MainnetChainActivations.Instance;

            Assert.Equal(HardforkName.Osaka, activations.ResolveAt(30_000_000, MainnetBlobSchedule.Bpo2Timestamp));
            Assert.Equal(MainnetBlobSchedule.Bpo2, activations.BlobParametersAt(30_000_000, MainnetBlobSchedule.Bpo2Timestamp));
            Assert.Equal(MainnetBlobSchedule.Osaka, activations.BlobParametersAt(30_000_000, MainnetBlobSchedule.Bpo1Timestamp - 1));
        }

        [NethereumDocExample(DocSection.EvmSimulator, "blob-schedule", "Apply the scheduled blob parameters to a block's HardforkConfig", Order = 3)]
        [Fact]
        public void AtBlockAppliesTheScheduledBlobParametersToTheConfig()
        {
            var config = DefaultHardforkConfigs.Osaka.AtBlock(
                MainnetChainActivations.Instance, blockNumber: 30_000_000, timestamp: MainnetBlobSchedule.Bpo1Timestamp);

            Assert.Equal(MainnetBlobSchedule.Bpo1.Target, config.TargetBlobsPerBlock);
            Assert.Equal(MainnetBlobSchedule.Bpo1.Max, config.MaxBlobsPerBlock);
            Assert.NotEqual(DefaultHardforkConfigs.Osaka.MaxBlobsPerBlock, config.MaxBlobsPerBlock);
        }

        [NethereumDocExample(DocSection.EvmSimulator, "exceptional-halt", "Read why a frame halted from Program.HaltReason", Order = 1)]
        [Fact]
        public async Task AFrameThatHaltsReportsItsReason()
        {
            var simulator = new EVMSimulator(DefaultHardforkConfigs.Cancun);
            var program = new Program("50".HexToByteArray());

            await simulator.ExecuteWithCallStackAsync(program, traceEnabled: false);

            Assert.True(program.IsExceptionalHalt);
            Assert.Equal(EvmHaltReason.StackUnderflow, program.HaltReason);
        }

        [NethereumDocExample(DocSection.EvmSimulator, "precompile-outcome", "Run a precompile as a value and count the calls with an observer", Order = 1)]
        [Fact]
        public void APrecompileFailsAsAValueAndAnObserverCountsTheCalls()
        {
            var observer = new CountingObserver();
            var registry = DefaultPrecompileRegistries.CancunBase().WithCallObserver(observer);

            Assert.True(registry.TryExecute(0x04, new byte[] { 1, 2, 3 }, out var echoed));
            Assert.Equal(new byte[] { 1, 2, 3 }, echoed);

            Assert.False(registry.TryExecute(0x08, new byte[] { 1 }, out var rejected));
            Assert.Null(rejected);

            Assert.Equal(new[] { 0x04, 0x08 }, observer.Calls);
        }

        [NethereumDocExample(DocSection.EvmSimulator, "log-index", "Number the ETH transfer logs of a block with a shared LogIndexCounter", Order = 1)]
        [Fact]
        public void AnIndexingTransferLogRuleNumbersLogsFromASharedCounter()
        {
            var counter = new LogIndexCounter();
            var rule = new IndexingEthTransferLogRule(Eip7708EthTransferLogRule.Instance, counter);
            var logs = new List<FilterLog>();

            rule.Emit(logs, "0x1111111111111111111111111111111111111111", "0x2222222222222222222222222222222222222222", new EvmUInt256(5));
            rule.Emit(logs, "0x2222222222222222222222222222222222222222", "0x3333333333333333333333333333333333333333", new EvmUInt256(7));

            Assert.Equal(2, logs.Count);
            Assert.Equal(0, (long)logs[0].LogIndex.Value);
            Assert.Equal(1, (long)logs[1].LogIndex.Value);
            Assert.Equal(2, counter.Next());
        }

        [NethereumDocExample(DocSection.EvmSimulator, "witness", "Bind anchor words into a witness and commit to them", Order = 1)]
        [Fact]
        public void AnchorBindingEncodesWordsAndCommitsKeccak256OfThem()
        {
            var encoded = Nethereum.EVM.Witness.AnchorBinding.Encode(new[] { new EvmUInt256(1), new EvmUInt256(2) });

            Assert.Equal(2 * Nethereum.EVM.Witness.AnchorBinding.WordSize, encoded.Length);
            Assert.Equal(new Sha3Keccack().CalculateHash(encoded), Nethereum.EVM.Witness.AnchorBinding.Commitment(encoded));
        }
    }
}
