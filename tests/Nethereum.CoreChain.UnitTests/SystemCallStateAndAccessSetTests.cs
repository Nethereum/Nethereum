using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class SystemCallStateAndAccessSetTests
    {
        public enum Engine
        {
            Host,
            Guest
        }

        private const string Coinbase = "0x0000000000000000000000000000000000c0ffee";
        private const string Precompile = "0x0000000000000000000000000000000000000001";
        private const string NeverWarmed = "0x000000000000000000000000000000000000dead";
        private const string Delegate = "0x000000000000000000000000000000000000beef";

        private static readonly EvmUInt256 MarkerValue = new EvmUInt256(1);

        private static readonly byte[] WritesTheMarker = "600160005500".HexToByteArray();

        private static readonly byte[] WritesItsEntryGas = "5A60005500".HexToByteArray();

        private static readonly byte[] WritesTheMarkerThenReverts = "600160005560006000fd".HexToByteArray();

        private static byte[] MeasuresTheAccessCostOf(params string[] addresses)
        {
            var code = new List<byte>();
            for (var slot = 0; slot < addresses.Length; slot++)
            {
                code.Add(0x5A);
                code.Add(0x73);
                code.AddRange(addresses[slot].HexToByteArray());
                code.Add(0x31);
                code.Add(0x50);
                code.Add(0x5A);
                code.Add(0x90);
                code.Add(0x03);
                code.Add(0x60);
                code.Add((byte)slot);
                code.Add(0x55);
            }
            code.Add(0x00);
            return code.ToArray();
        }

        private static byte[] DelegatesTo(string address) =>
            ("ef0100" + address.Substring(2)).HexToByteArray();

        private static Task<EvmUInt256[]> RunBeaconRootsCallAsync(
            Engine engine, byte[] predeployCode, int slots,
            HardforkName fork = HardforkName.Cancun, byte[] delegateCode = null) =>
            engine == Engine.Host
                ? RunOnTheHostAsync(predeployCode, slots, fork, delegateCode)
                : RunOnTheGuestAsync(predeployCode, slots, fork, delegateCode);

        private static async Task<EvmUInt256[]> RunOnTheHostAsync(
            byte[] predeployCode, int slots, HardforkName fork, byte[] delegateCode)
        {
            var header = SystemCallBlockHarness.Header(
                blockNumber: 1, timestamp: 1_700_000_000, parentBeaconBlockRoot: new byte[32]);
            header.Coinbase = Coinbase;

            var (result, stateStore) = await SystemCallBlockHarness.ExecuteAcceptedAsync(
                fork, header,
                async store =>
                {
                    await SystemCallBlockHarness.DeployAsync(store, SystemCallContracts.BeaconRoots, predeployCode);
                    if (delegateCode != null)
                        await SystemCallBlockHarness.DeployAsync(store, Delegate, delegateCode);
                });

            Assert.NotNull(result);

            var values = new EvmUInt256[slots];
            for (var slot = 0; slot < slots; slot++)
            {
                var raw = await stateStore.GetStorageAsync(SystemCallContracts.BeaconRoots, slot);
                values[slot] = raw == null ? EvmUInt256.Zero : EvmUInt256.FromBigEndian(raw);
            }
            return values;
        }

        private static async Task<EvmUInt256[]> RunOnTheGuestAsync(
            byte[] predeployCode, int slots, HardforkName fork, byte[] delegateCode)
        {
            var accounts = SystemCallBlockHarness.WitnessCarryingEveryActivatedPredeploy(
                fork, SystemCallContracts.BeaconRoots, predeployCode);

            if (delegateCode != null)
                accounts.Add(new WitnessAccount
                {
                    Address = Delegate,
                    Balance = EvmUInt256.Zero,
                    Nonce = 1,
                    Code = delegateCode,
                    Storage = new List<WitnessStorageSlot>()
                });

            var block = new BlockWitnessData
            {
                BlockNumber = 1,
                Timestamp = 1_700_000_000,
                BaseFee = 0,
                BlockGasLimit = 30_000_000,
                ChainId = 1,
                Coinbase = Coinbase,
                Difficulty = new byte[32],
                ParentHash = new byte[32],
                ParentBeaconBlockRoot = new byte[32],
                ExtraData = new byte[0],
                MixHash = new byte[32],
                Nonce = new byte[8],
                Features = new BlockFeatureConfig { Fork = fork },
                Transactions = new List<BlockWitnessTransaction>(),
                Accounts = accounts
            };

            var result = await Nethereum.EVM.Execution.BlockExecutor.ExecuteAsync(
                block, RlpBlockEncodingProvider.Instance, DefaultMainnetHardforkRegistry.Instance);

            var values = new EvmUInt256[slots];
            for (var slot = 0; slot < slots; slot++)
            {
                var raw = await result.StateReader.GetStorageAtAsync(
                    SystemCallContracts.BeaconRoots, new EvmUInt256((ulong)slot));
                values[slot] = raw == null ? EvmUInt256.Zero : EvmUInt256.FromBigEndian(raw);
            }
            return values;
        }

        [Theory]
        [Trait("Category", "EIP4788")]
        [InlineData(Engine.Host)]
        [InlineData(Engine.Guest)]
        public async Task Given_ASystemCallThatReverts_When_TheBlockIsExecuted_Then_NoStateIsCommitted(Engine engine)
        {
            var slots = await RunBeaconRootsCallAsync(engine, WritesTheMarkerThenReverts, slots: 1);

            Assert.True(slots[0].IsZero,
                $"{engine}: the reverted write survived — slot 0 is {slots[0]}");
        }

        [Theory]
        [Trait("Category", "EIP4788")]
        [InlineData(Engine.Host)]
        [InlineData(Engine.Guest)]
        public async Task Given_ASystemCallThatSucceeds_When_TheBlockIsExecuted_Then_ItsWritesArePersisted(Engine engine)
        {
            var slots = await RunBeaconRootsCallAsync(engine, WritesTheMarker, slots: 1);

            Assert.Equal(MarkerValue, slots[0]);
        }

        [Fact]
        [Trait("Category", "EIP3651")]
        public async Task Given_TheSameSystemCall_When_RunOnBothEngines_Then_TheWarmSetGasAndPostStateAreIdentical()
        {
            var probe = MeasuresTheAccessCostOf(
                Coinbase, Precompile, SystemCallContracts.SystemCaller, NeverWarmed);

            var host = await RunBeaconRootsCallAsync(Engine.Host, probe, slots: 4);
            var guest = await RunBeaconRootsCallAsync(Engine.Guest, probe, slots: 4);

            Assert.Equal(host[0], guest[0]);
            Assert.Equal(host[1], guest[1]);
            Assert.Equal(host[2], guest[2]);
            Assert.Equal(host[3], guest[3]);

            AssertTheProbeCanTellWarmFromCold(host, nameof(Engine.Host));
            AssertTheProbeCanTellWarmFromCold(guest, nameof(Engine.Guest));
        }

        [Theory]
        [Trait("Category", "EIP7702")]
        [InlineData(Engine.Host, HardforkName.Prague)]
        [InlineData(Engine.Guest, HardforkName.Prague)]
        [InlineData(Engine.Host, HardforkName.Amsterdam)]
        [InlineData(Engine.Guest, HardforkName.Amsterdam)]
        public async Task Given_ASystemCallToADelegatedTarget_When_TheBlockIsExecuted_Then_TheDelegatesCodeRuns(
            Engine engine, HardforkName fork)
        {
            var slots = await RunBeaconRootsCallAsync(
                engine, DelegatesTo(Delegate), slots: 1, fork: fork, delegateCode: WritesTheMarker);

            Assert.Equal(MarkerValue, slots[0]);
        }

        [Fact]
        [Trait("Category", "EIP7702")]
        public async Task Given_ADelegatedSystemCall_When_RunOnBothEngines_Then_TheGasItIsDispatchedWithIsIdentical()
        {
            var host = await RunBeaconRootsCallAsync(
                Engine.Host, DelegatesTo(Delegate), slots: 1,
                fork: HardforkName.Amsterdam, delegateCode: WritesItsEntryGas);
            var guest = await RunBeaconRootsCallAsync(
                Engine.Guest, DelegatesTo(Delegate), slots: 1,
                fork: HardforkName.Amsterdam, delegateCode: WritesItsEntryGas);

            Assert.False(host[0].IsZero, "the delegate never ran on the host");
            Assert.Equal(host[0], guest[0]);
        }

        private static void AssertTheProbeCanTellWarmFromCold(EvmUInt256[] measured, string engine)
        {
            Assert.True(measured[3] > measured[0], $"{engine}: the coinbase was not warm");
            Assert.True(measured[3] > measured[1], $"{engine}: the precompiles were not warm");
            Assert.True(measured[3] > measured[2], $"{engine}: the origin was not warm");
        }
    }
}
