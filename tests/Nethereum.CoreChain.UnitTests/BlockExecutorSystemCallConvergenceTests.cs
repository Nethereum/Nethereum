using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Nethereum.EVM.Execution;
using Xunit;
using static Nethereum.CoreChain.UnitTests.SystemCallBlockHarness;

namespace Nethereum.CoreChain.UnitTests
{
    public class BlockExecutorSystemCallConvergenceTests
    {
        private static readonly EvmUInt256 MarkerSlot = EvmUInt256.Zero;
        private static readonly EvmUInt256 MarkerValue = new EvmUInt256(1);

        private const string BeaconRootsRuntimeCode =
            "3373fffffffffffffffffffffffffffffffffffffffe14604d57602036146024575f5ffd5b5f35801560495" +
            "762001fff810690815414603c575f5ffd5b62001fff01545f5260205ff35b5f5ffd5b62001fff42064281555f3" +
            "59062001fff015500";

        private static AccountChanges FindAccount(List<AccountChanges> bal, string address) =>
            bal?.FirstOrDefault(a => string.Equals(a.Address, address, StringComparison.OrdinalIgnoreCase));

        [Fact]
        public async Task Given_AnAmsterdamBlock_When_RequestSystemCallsRun_Then_AllFourContractsAreCalled()
        {
            var header = Header(blockNumber: 1, timestamp: 1_700_000_000);

            var (_, stateStore) = await ExecuteAcceptedAsync(HardforkName.Amsterdam, header, async store =>
            {
                await DeployMarkerAsync(store, SystemCallContracts.WithdrawalRequests);
                await DeployMarkerAsync(store, SystemCallContracts.ConsolidationRequests);
                await DeployMarkerAsync(store, Nethereum.EVM.Execution.SystemCallContracts.BuilderDeposit);
                await DeployMarkerAsync(store, Nethereum.EVM.Execution.SystemCallContracts.BuilderExit);
            });

            Assert.Equal(MarkerValue, EvmUInt256.FromBigEndian(
                await stateStore.GetStorageAsync(SystemCallContracts.WithdrawalRequests, MarkerSlot)));
            Assert.Equal(MarkerValue, EvmUInt256.FromBigEndian(
                await stateStore.GetStorageAsync(SystemCallContracts.ConsolidationRequests, MarkerSlot)));
            Assert.Equal(MarkerValue, EvmUInt256.FromBigEndian(
                await stateStore.GetStorageAsync(Nethereum.EVM.Execution.SystemCallContracts.BuilderDeposit, MarkerSlot)));
            Assert.Equal(MarkerValue, EvmUInt256.FromBigEndian(
                await stateStore.GetStorageAsync(Nethereum.EVM.Execution.SystemCallContracts.BuilderExit, MarkerSlot)));
        }

        [Fact]
        public async Task Given_APragueBlock_When_RequestSystemCallsRun_Then_OnlyTheTwoPragueContractsAreCalled()
        {
            var header = Header(blockNumber: 1, timestamp: 1_700_000_000);

            var (_, stateStore) = await ExecuteAcceptedAsync(HardforkName.Prague, header, async store =>
            {
                await DeployMarkerAsync(store, SystemCallContracts.WithdrawalRequests);
                await DeployMarkerAsync(store, SystemCallContracts.ConsolidationRequests);
                await DeployMarkerAsync(store, Nethereum.EVM.Execution.SystemCallContracts.BuilderDeposit);
                await DeployMarkerAsync(store, Nethereum.EVM.Execution.SystemCallContracts.BuilderExit);
            });

            Assert.Equal(MarkerValue, EvmUInt256.FromBigEndian(
                await stateStore.GetStorageAsync(SystemCallContracts.WithdrawalRequests, MarkerSlot)));
            Assert.Equal(MarkerValue, EvmUInt256.FromBigEndian(
                await stateStore.GetStorageAsync(SystemCallContracts.ConsolidationRequests, MarkerSlot)));
            Assert.Equal(EvmUInt256.Zero, EvmUInt256.FromBigEndian(
                await stateStore.GetStorageAsync(Nethereum.EVM.Execution.SystemCallContracts.BuilderDeposit, MarkerSlot)));
            Assert.Equal(EvmUInt256.Zero, EvmUInt256.FromBigEndian(
                await stateStore.GetStorageAsync(Nethereum.EVM.Execution.SystemCallContracts.BuilderExit, MarkerSlot)));
        }

        [Fact]
        public async Task Given_AnOsakaBlock_When_RequestSystemCallsRun_Then_OnlyTheTwoPragueContractsAreCalled()
        {
            var header = Header(blockNumber: 1, timestamp: 1_700_000_000);

            var (_, stateStore) = await ExecuteAcceptedAsync(HardforkName.Osaka, header, async store =>
            {
                await DeployMarkerAsync(store, SystemCallContracts.WithdrawalRequests);
                await DeployMarkerAsync(store, SystemCallContracts.ConsolidationRequests);
                await DeployMarkerAsync(store, Nethereum.EVM.Execution.SystemCallContracts.BuilderDeposit);
                await DeployMarkerAsync(store, Nethereum.EVM.Execution.SystemCallContracts.BuilderExit);
            });

            Assert.Equal(MarkerValue, EvmUInt256.FromBigEndian(
                await stateStore.GetStorageAsync(SystemCallContracts.WithdrawalRequests, MarkerSlot)));
            Assert.Equal(MarkerValue, EvmUInt256.FromBigEndian(
                await stateStore.GetStorageAsync(SystemCallContracts.ConsolidationRequests, MarkerSlot)));
            Assert.Equal(EvmUInt256.Zero, EvmUInt256.FromBigEndian(
                await stateStore.GetStorageAsync(Nethereum.EVM.Execution.SystemCallContracts.BuilderDeposit, MarkerSlot)));
            Assert.Equal(EvmUInt256.Zero, EvmUInt256.FromBigEndian(
                await stateStore.GetStorageAsync(Nethereum.EVM.Execution.SystemCallContracts.BuilderExit, MarkerSlot)));
        }

        /// <summary>
        /// AMS-7928-16. EIP-7928 §Scope and Inclusion: "the system caller address, SYSTEM_ADDRESS
        /// (0xfffffffffffffffffffffffffffffffffffffffe), MUST NOT be included unless it experiences
        /// state access itself".
        ///
        /// <para>It must be asserted on a block where a system call ACTUALLY RAN, or it passes for
        /// the wrong reason: an empty access list satisfies "absent" trivially. The beacon-roots
        /// contract being present with its reads is the twin that rules that out.</para>
        /// </summary>
        [Fact]
        public async Task Given_ASystemCallThatRan_When_TheAccessListIsBuilt_Then_TheSystemCallerIsAbsent()
        {
            const long timestamp = 1_700_002_100;
            var beaconRoot = Enumerable.Repeat((byte)0xCD, 32).ToArray();

            var header = Header(blockNumber: 1, timestamp: timestamp, parentBeaconBlockRoot: beaconRoot);

            var (result, _) = await ExecuteAcceptedAsync(HardforkName.Amsterdam, header, async store =>
            {
                var keccak = new Sha3Keccack();
                var code = BeaconRootsRuntimeCode.HexToByteArray();
                var codeHash = keccak.CalculateHash(code);
                await store.SaveCodeAsync(codeHash, code);
                await store.SaveAccountAsync(Eip4788Constants.BeaconRootsAddress,
                    new Account { Balance = EvmUInt256.Zero, Nonce = 1, CodeHash = codeHash });
            });

            Assert.NotNull(result.BlockAccessList);

            var beaconAccount = FindAccount(result.BlockAccessList, Eip4788Constants.BeaconRootsAddress);
            Assert.NotNull(beaconAccount);
            Assert.NotEmpty(beaconAccount.StorageChanges);

            Assert.Null(FindAccount(result.BlockAccessList, Eip7685Constants.SystemAddress));
        }

        [Fact]
        public async Task Given_AnAmsterdamBlock_When_TheBeaconRootWriteIsANoOp_Then_TheSlotIsStillRecordedAsRead()
        {
            const long timestamp = 1_700_001_950;
            var beaconRoot = Enumerable.Repeat((byte)0xAB, 32).ToArray();

            var timestampSlot = EvmUInt256BigIntegerExtensions.FromBigInteger(
                Eip4788Helpers.ComputeTimestampSlot(timestamp, Eip4788Constants.HistoryBufferLength));
            var rootSlot = EvmUInt256BigIntegerExtensions.FromBigInteger(
                Eip4788Helpers.ComputeRootSlot(timestamp, Eip4788Constants.HistoryBufferLength));
            var timestampValue = EvmUInt256BigIntegerExtensions.FromBigInteger(timestamp);
            var rootValue = EvmUInt256.FromBigEndian(beaconRoot);

            var header = Header(blockNumber: 1, timestamp: timestamp, parentBeaconBlockRoot: beaconRoot);

            var (result, _) = await ExecuteAcceptedAsync(HardforkName.Amsterdam, header, async store =>
            {
                var keccak = new Sha3Keccack();
                var code = BeaconRootsRuntimeCode.HexToByteArray();
                var codeHash = keccak.CalculateHash(code);
                await store.SaveCodeAsync(codeHash, code);
                await store.SaveAccountAsync(Eip4788Constants.BeaconRootsAddress,
                    new Account { Balance = EvmUInt256.Zero, Nonce = 1, CodeHash = codeHash });

                await store.SaveStorageAsync(Eip4788Constants.BeaconRootsAddress, timestampSlot, timestampValue.ToBytesForRLPEncoding());
                await store.SaveStorageAsync(Eip4788Constants.BeaconRootsAddress, rootSlot, rootValue.ToBytesForRLPEncoding());
            });

            Assert.NotNull(result.BlockAccessList);
            var beaconAccount = FindAccount(result.BlockAccessList, Eip4788Constants.BeaconRootsAddress);
            Assert.NotNull(beaconAccount);

            Assert.Contains(beaconAccount.StorageReads, s => s.Equals(timestampSlot));
            Assert.Contains(beaconAccount.StorageReads, s => s.Equals(rootSlot));

            Assert.DoesNotContain(beaconAccount.StorageChanges, sc => sc.Slot.Equals(timestampSlot));
            Assert.DoesNotContain(beaconAccount.StorageChanges, sc => sc.Slot.Equals(rootSlot));
        }
    }
}
