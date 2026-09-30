using System.Collections.Generic;
using System.Linq;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Types;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    /// <summary>
    /// EIP-8037 §Gas accounting for halts and reverts: <i>"The step applies to successful children
    /// only. This requires that a frame that reverts or halts exceptionally returns no state-gas to
    /// its parent; the restore to the frame's baseline above provides that."</i>
    ///
    /// <para><see cref="Eip8037ChildReturnReconcileTests"/> drives
    /// <see cref="StateGasMeter.ReturnOutstandingStateGasToGasLeft"/> directly, so it proves the
    /// arithmetic and nothing about where the engine runs it. These run real parent and child
    /// frames through the simulator and read the parent's two pools once the child has been merged
    /// in, so they fail when either merge site loses the step, and when the child's restore to
    /// baseline stops covering the halting paths.</para>
    ///
    /// <para>A frame only ever holds an outstanding <c>state_gas_from_gas_left</c> while its
    /// reservoir is empty - a spilling charge drains the reservoir first, and a refill repays the
    /// spill first. Both pools are non-empty only in the instant between absorbing a child and
    /// reconciling, which is exactly why the EIP puts the step there. Every scenario below
    /// therefore reaches that instant the way the EIP describes: the parent borrows from
    /// <c>gas_left</c>, and a child refills.</para>
    /// </summary>
    public class Eip8037ChildReturnPlacementTests
    {
        private const string Caller = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string ParentContract = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string ChildContract = "0xcccccccccccccccccccccccccccccccccccccccc";
        private const string SlotHolder = "0xdddddddddddddddddddddddddddddddddddddddd";

        private const long StorageSet = GasConstants.EIP8037_STORAGE_SET_STATE_GAS;
        private const long NewAccount = GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS;

        private static readonly byte[] Stop = { 0x00 };
        private static readonly byte[] Revert = { 0x60, 0x00, 0x60, 0x00, 0xfd };
        private static readonly byte[] InvalidOpcode = { 0xfe };

        private static byte[] Push1(byte value) => new byte[] { 0x60, value };

        private static byte[] Push20(string address) =>
            new byte[] { 0x73 }.Concat(address.HexToByteArray()).ToArray();

        private static byte[] Concat(params byte[][] pieces) =>
            pieces.SelectMany(piece => piece).ToArray();

        private static byte[] CallWithCalldataOfLength(byte length, string target) => Concat(
            Push1(0), Push1(0), Push1(length), Push1(0), Push1(0),
            Push20(target), new byte[] { 0x5a, 0xf1, 0x50 });

        private static byte[] DelegateCall(string target) => Concat(
            Push1(0), Push1(0), Push1(0), Push1(0),
            Push20(target), new byte[] { 0x5a, 0xf4, 0x50 });

        private static byte[] SetSlot(byte slot) => Concat(Push1(1), Push1(slot), new byte[] { 0x55 });

        private static byte[] ClearSlot(byte slot) => Concat(Push1(0), Push1(slot), new byte[] { 0x55 });

        /// <summary>
        /// Storing the calldata size makes one contract both the setter and the clearer of its own
        /// slot 0, which is what lets a later frame refill against a write an earlier frame made:
        /// EIP-8037 nets a <c>STORAGE_SET</c> refill only when the slot was zero in the pre-state
        /// and is being returned to zero.
        /// </summary>
        private static byte[] SlotHolderThatStoresItsCalldataSizeInSlotZero() =>
            new byte[] { 0x36, 0x60, 0x00, 0x55, 0x00 };

        private static byte[] ChildThatRefillsTheParentsReservoirThen(byte[] ending) =>
            Concat(ClearSlot(0), ending);

        private static byte[] ParentThatBorrowsThenDelegateCalls(params byte[] slotsToSet) => Concat(
            Concat(slotsToSet.Select(SetSlot).ToArray()),
            DelegateCall(ChildContract),
            Stop);

        private static byte[] InitCodeThatRefillsThroughTheSlotHolder(byte[] ending) => Concat(
            CallWithCalldataOfLength(0, SlotHolder), ending);

        private const byte InitCodeOffsetInParentCode = 50;

        private static byte[] ParentThatBorrowsThenCreates(byte[] initCodeEnding)
        {
            var initCode = InitCodeThatRefillsThroughTheSlotHolder(initCodeEnding);
            var size = (byte)initCode.Length;

            var prologue = Concat(
                CallWithCalldataOfLength(1, SlotHolder),
                Push1(size), Push1(InitCodeOffsetInParentCode), Push1(0), new byte[] { 0x39 },
                Push1(size), Push1(0), Push1(0), new byte[] { 0xf0, 0x50, 0x00 });

            Assert.Equal(InitCodeOffsetInParentCode, prologue.Length);
            return prologue.Concat(initCode).ToArray();
        }

        private static Program RunParent(long reservoir, byte[] parentCode, byte[] childCode)
        {
            var accounts = new Dictionary<string, AccountState>
            {
                [Caller] = new AccountState { Balance = new EvmUInt256(1_000_000_000_000) },
                [ParentContract] = new AccountState { Code = parentCode },
                [ChildContract] = new AccountState { Code = childCode },
                [SlotHolder] = new AccountState { Code = SlotHolderThatStoresItsCalldataSizeInSlotZero() }
            };

            var executionState = new ExecutionStateService(new InMemoryStateReader(accounts));
            var config = DefaultMainnetHardforkRegistry.Instance.Get(HardforkName.Amsterdam);

            var callContext = new EvmCallContext
            {
                From = Caller,
                To = ParentContract,
                Data = new byte[0],
                Value = EvmUInt256.Zero,
                Gas = 5_000_000,
                ChainId = EvmUInt256.One
            };

            var programContext = new ProgramContext(callContext, executionState,
                blockNumber: new EvmUInt256(1), timestamp: new EvmUInt256(1_000))
            {
                StateGasActive = true,
                EnforceSstoreGasStipend = config.EnforceSstoreGasStipend,
                BlockHashRule = config.BlockHashRule,
                SstoreClearsSchedule = config.SstoreClearsSchedule,
                SstoreSetRefund = config.SstoreSetRefund,
                SstoreResetRefund = config.SstoreResetRefund,
                SstoreRefundRule = config.SstoreRefundRule,
                EthTransferLogRule = config.EthTransferLogRule
            };

            var program = new Program(parentCode, programContext)
            {
                StateGasLeft = reservoir,
                StateGasBaseline = reservoir
            };

            return new EVMSimulator(config).ExecuteWithCallStack(program);
        }

        private static Program RunParentThatBorrowedAndDelegateCalled(byte[] childEnding) =>
            RunParent(reservoir: 0,
                ParentThatBorrowsThenDelegateCalls(0x00),
                ChildThatRefillsTheParentsReservoirThen(childEnding));

        private static Program RunParentThatBorrowedAndCreated(byte[] initCodeEnding) =>
            RunParent(reservoir: 0, ParentThatBorrowsThenCreates(initCodeEnding), Stop);

        [Fact]
        [Trait("Category", "EIP8037")]
        public void Given_AParentThatBorrowedFromGasLeft_When_TheCalledChildSucceeds_Then_TheEngineReturnsTheOutstandingStateGas()
        {
            var parent = RunParentThatBorrowedAndDelegateCalled(Stop);

            Assert.Equal(0, parent.StateGasSpilled);
            Assert.Equal(0, parent.StateGasLeft);
        }

        /// <summary>
        /// The pools alone cannot tell "returned to gas_left" from "silently dropped", so this twin
        /// runs the same work with the write funded from the reservoir instead of from
        /// <c>gas_left</c>. EIP-8037: <i>"Note: this undoes no state creation, so
        /// <c>evm_state_gas_used</c> is unchanged. It only moves gas between the two pools."</i> -
        /// the two routes must therefore leave the frame with the same <c>gas_left</c>.
        /// </summary>
        [Fact]
        [Trait("Category", "EIP8037")]
        public void Given_TheSameWorkFundedFromTheReservoir_When_BothFramesFinish_Then_TheBorrowingRouteEndsWithTheSameGasLeft()
        {
            var parentCode = ParentThatBorrowsThenDelegateCalls(0x00);
            var childCode = ChildThatRefillsTheParentsReservoirThen(Stop);

            var borrowed = RunParent(reservoir: 0, parentCode, childCode);
            var fundedFromReservoir = RunParent(reservoir: StorageSet, parentCode, childCode);

            Assert.Equal(fundedFromReservoir.GasRemaining, borrowed.GasRemaining);
        }

        /// <summary>
        /// EIP-8037: <c>d = min(state_gas_reservoir, state_gas_from_gas_left)</c>. The parent
        /// borrows for two storage sets and the child refills one, so a merge that returned the
        /// whole outstanding amount would invent state-gas the reservoir cannot cover.
        /// </summary>
        [Fact]
        [Trait("Category", "EIP8037")]
        public void Given_ACalledChildRefillingLessThanTheParentBorrowed_When_ItSucceeds_Then_OnlyTheReservoirIsReturned()
        {
            var parent = RunParent(reservoir: 0,
                ParentThatBorrowsThenDelegateCalls(0x00, 0x01),
                ChildThatRefillsTheParentsReservoirThen(Stop));

            Assert.Equal(StorageSet, parent.StateGasSpilled);
            Assert.Equal(0, parent.StateGasLeft);
        }

        /// <summary>
        /// EIP-8037: <i>"a frame that reverts or halts exceptionally returns no state-gas to its
        /// parent; the restore to the frame's baseline above provides that."</i> The child's
        /// baseline is empty, so the restore leaves it with nothing to hand back and the parent's
        /// borrowing stays outstanding.
        /// </summary>
        [Fact]
        [Trait("Category", "EIP8037")]
        public void Given_ACalledChildThatRefilledTheReservoir_When_ItReverts_Then_TheParentGetsNoStateGasBack()
        {
            var parent = RunParentThatBorrowedAndDelegateCalled(Revert);

            Assert.Equal(0, parent.StateGasLeft);
            Assert.Equal(StorageSet, parent.StateGasSpilled);
        }

        [Fact]
        [Trait("Category", "EIP8037")]
        public void Given_ACalledChildThatRefilledTheReservoir_When_ItHaltsOnAnInvalidOpcode_Then_TheParentGetsNoStateGasBack()
        {
            var parent = RunParentThatBorrowedAndDelegateCalled(InvalidOpcode);

            Assert.Equal(0, parent.StateGasLeft);
            Assert.Equal(StorageSet, parent.StateGasSpilled);
        }

        /// <summary>
        /// The created frame is merged by a different routine than the called one, so it needs its
        /// own scenario. The parent's outstanding amount here is one storage set plus the
        /// account-creation charge EIP-8037 applies before entering the frame; the refill covers
        /// the storage set, so that is what the merge returns.
        /// </summary>
        [Fact]
        [Trait("Category", "EIP8037")]
        public void Given_AParentThatBorrowedFromGasLeft_When_TheCreatedChildSucceeds_Then_TheEngineReturnsTheOutstandingStateGas()
        {
            var parent = RunParentThatBorrowedAndCreated(Stop);

            Assert.Equal(0, parent.StateGasLeft);
            Assert.Equal(NewAccount, parent.StateGasSpilled);
        }

        /// <summary>
        /// EIP-8037 §Gas accounting for new accounts: <i>"if the child frame reverts or halts exceptionally, the
        /// charged state-gas is refilled in LIFO order"</i> - so a reverted creation gives back its
        /// account-creation charge but, being unsuccessful, returns no reservoir, leaving the
        /// storage set the parent borrowed for still outstanding.
        /// </summary>
        [Fact]
        [Trait("Category", "EIP8037")]
        public void Given_ACreatedChildThatRefilledTheReservoir_When_ItReverts_Then_TheParentGetsNoStateGasBack()
        {
            var parent = RunParentThatBorrowedAndCreated(Revert);

            Assert.Equal(0, parent.StateGasLeft);
            Assert.Equal(StorageSet, parent.StateGasSpilled);
        }
    }
}
