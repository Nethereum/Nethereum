using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution.Opcodes;
using Nethereum.EVM.Gas;
using Nethereum.EVM.UnitTests;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    /// <summary>
    /// What the SSTORE resolver reads, and when it stops reading.
    ///
    /// <para>EIP-7928: "Pre-state validation MUST pass before any state access
    /// occurs. If pre-state validation fails, the target resource (address or
    /// storage slot) is never accessed and MUST NOT be included in the BAL."
    /// A slot recorded by a frame that never had the operands to write it is a
    /// consensus difference — <c>block_access_list_hash</c> is built from these
    /// notifications and the collector has no rollback.</para>
    /// </summary>
    public class SstoreSlotResolvingGasCostTests
    {
        private const string ContractAddress = "0x1234567890123456789012345678901234567890";
        private const string CallerAddress = "0xABCDEF0123456789ABCDEF0123456789ABCDEF01";

        private sealed class CountingStateAccessRecorder : IStateAccessRecorder
        {
            public List<string> Reads { get; } = new List<string>();

            public void RecordAccountRead(string address) => Reads.Add("account:" + address);

            public void RecordStorageRead(string address, EvmUInt256 key) =>
                Reads.Add("slot:" + address + ":" + key.ToString());
        }

        private static (Program program, ExecutionStateService stateService, CountingStateAccessRecorder recorder)
            CreateProgramRecordingStateAccess()
        {
            var stateService = new ExecutionStateService(new MockNodeDataService());
            var recorder = new CountingStateAccessRecorder();
            stateService.AccessRecorder = recorder;

            var callInput = new CallInput
            {
                To = ContractAddress,
                From = CallerAddress,
                Gas = new Nethereum.Hex.HexTypes.HexBigInteger(1_000_000),
                Data = "0x"
            };

            var program = new Program("00".HexToByteArray(), new ProgramContext(callInput, stateService));
            program.GasRemaining = 1_000_000;
            return (program, stateService, recorder);
        }

        private static int WarmStorageKeyCount(ExecutionStateService stateService) =>
            stateService.CreateOrGetAccountExecutionState(ContractAddress).WarmStorageKeys.Count;

        private static byte[] Word(byte value)
        {
            var word = new byte[32];
            word[31] = value;
            return word;
        }

        public static IEnumerable<object[]> EveryForkThatPricesAnSstore() => new[]
        {
            new object[] { "Frontier", OpcodeHandlerSets.Frontier },
            new object[] { "TangerineWhistle", OpcodeHandlerSets.TangerineWhistle },
            new object[] { "Constantinople", OpcodeHandlerSets.Constantinople },
            new object[] { "Petersburg", OpcodeHandlerSets.Petersburg },
            new object[] { "Istanbul", OpcodeHandlerSets.Istanbul },
            new object[] { "Berlin", OpcodeHandlerSets.Berlin },
            new object[] { "Osaka", OpcodeHandlerSets.Osaka },
            new object[] { "Amsterdam", OpcodeHandlerSets.Amsterdam }
        };

        [Fact]
        public async Task Given_AnSstoreWithTooFewStackItems_When_Priced_Then_NoStorageReadIsRecorded()
        {
            var (program, stateService, recorder) = CreateProgramRecordingStateAccess();
            program.StackPush(Word(1));

            await Assert.ThrowsAnyAsync<Exception>(
                () => OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.SSTORE, program));

            Assert.Empty(recorder.Reads);
            Assert.Equal(0, WarmStorageKeyCount(stateService));
        }

        [Fact]
        public async Task Given_AnSstoreWithBothOperands_When_Priced_Then_TheSlotIsReadAndMarkedWarm()
        {
            var (program, stateService, recorder) = CreateProgramRecordingStateAccess();
            program.StackPush(Word(9));
            program.StackPush(Word(1));

            var cost = await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.SSTORE, program);

            Assert.Equal(GasConstants.COLD_SLOAD_COST + GasConstants.EIP8038_STORAGE_WRITE, cost);
            Assert.Equal(new[] { "slot:" + program.ProgramContext.AddressContract + ":1" }, recorder.Reads);
            Assert.Equal(1, WarmStorageKeyCount(stateService));
        }

        [Theory]
        [MemberData(nameof(EveryForkThatPricesAnSstore))]
        public async Task Given_AnSstoreWithBothOperands_When_PricedAtAnyFork_Then_ExactlyTheOneSlotIsReadAndMarkedWarm(
            string fork, OpcodeHandlerTable handlers)
        {
            var (program, stateService, recorder) = CreateProgramRecordingStateAccess();
            program.StackPush(Word(9));
            program.StackPush(Word(1));

            await handlers.GetGasCostAsync(Instruction.SSTORE, program);

            Assert.Equal(new[] { "slot:" + program.ProgramContext.AddressContract + ":1" }, recorder.Reads);
            Assert.Equal(1, WarmStorageKeyCount(stateService));
        }

        [Theory]
        [MemberData(nameof(EveryForkThatPricesAnSstore))]
        public async Task Given_AnSstoreWithTooFewStackItems_When_PricedAtAnyFork_Then_NothingIsReadOrMarkedWarm(
            string fork, OpcodeHandlerTable handlers)
        {
            var (program, stateService, recorder) = CreateProgramRecordingStateAccess();
            program.StackPush(Word(1));

            await Assert.ThrowsAnyAsync<Exception>(
                () => handlers.GetGasCostAsync(Instruction.SSTORE, program));

            Assert.Empty(recorder.Reads);
            Assert.Equal(0, WarmStorageKeyCount(stateService));
        }

        /// <summary>
        /// EIP-7928: "If <c>SSTORE</c> fails this check, the storage slot MUST
        /// NOT appear in <c>storage_reads</c> or <c>storage_changes</c>."
        /// </summary>
        [Fact]
        public async Task Given_AFrameAtTheCallStipend_When_IstanbulPricesAnSstore_Then_ItRefusesWithoutReadingTheSlot()
        {
            var (program, stateService, recorder) = CreateProgramRecordingStateAccess();
            program.GasRemaining = GasConstants.CALL_STIPEND;
            program.StackPush(Word(9));
            program.StackPush(Word(1));

            var cost = await OpcodeHandlerSets.Istanbul.GetGasCostAsync(Instruction.SSTORE, program);

            Assert.Equal(GasConstants.OVERFLOW_GAS_COST, cost);
            Assert.Empty(recorder.Reads);
            Assert.Equal(0, WarmStorageKeyCount(stateService));
        }

        [Fact]
        public async Task Given_AFrameJustAboveTheCallStipend_When_IstanbulPricesAnSstore_Then_TheSlotIsRead()
        {
            var (program, stateService, recorder) = CreateProgramRecordingStateAccess();
            program.GasRemaining = GasConstants.CALL_STIPEND + 1;
            program.StackPush(Word(9));
            program.StackPush(Word(1));

            var cost = await OpcodeHandlerSets.Istanbul.GetGasCostAsync(Instruction.SSTORE, program);

            Assert.Equal(GasConstants.SSTORE_SET, cost);
            Assert.Single(recorder.Reads);
            Assert.Equal(1, WarmStorageKeyCount(stateService));
        }

        [Fact]
        public async Task Given_AFrameAtTheCallStipend_When_BerlinPricesAnSstore_Then_ItPricesRatherThanRefusing()
        {
            var (program, _, _) = CreateProgramRecordingStateAccess();
            program.GasRemaining = GasConstants.CALL_STIPEND;
            program.StackPush(Word(9));
            program.StackPush(Word(1));

            var cost = await OpcodeHandlerSets.Berlin.GetGasCostAsync(Instruction.SSTORE, program);

            Assert.Equal(GasConstants.COLD_SLOAD_COST + GasConstants.SSTORE_SET, cost);
        }

        [Theory]
        [InlineData("Frontier", 1, 20000L)]
        [InlineData("TangerineWhistle", 1, 20000L)]
        [InlineData("Constantinople", 1, 20000L)]
        [InlineData("Petersburg", 1, 20000L)]
        [InlineData("Istanbul", 1, 20000L)]
        [InlineData("Berlin", 1, 22100L)]
        [InlineData("Osaka", 1, 22100L)]
        [InlineData("Amsterdam", 1, 12100L)]
        [InlineData("Frontier", 0, 5000L)]
        [InlineData("TangerineWhistle", 0, 5000L)]
        [InlineData("Constantinople", 0, 200L)]
        [InlineData("Petersburg", 0, 5000L)]
        [InlineData("Istanbul", 0, 800L)]
        [InlineData("Berlin", 0, 2200L)]
        [InlineData("Osaka", 0, 2200L)]
        [InlineData("Amsterdam", 0, 2100L)]
        public async Task Given_AnUntouchedZeroSlot_When_PricedAtAFork_Then_TheChargeIsTheOneThatForkCharges(
            string fork, byte newValue, long expected)
        {
            var (program, _, _) = CreateProgramRecordingStateAccess();
            program.StackPush(Word(newValue));
            program.StackPush(Word(1));

            var cost = await ForkTable(fork).GetGasCostAsync(Instruction.SSTORE, program);

            Assert.Equal(expected, cost);
        }

        private static OpcodeHandlerTable ForkTable(string fork)
        {
            foreach (var row in EveryForkThatPricesAnSstore())
            {
                if ((string)row[0] == fork) return (OpcodeHandlerTable)row[1];
            }

            throw new ArgumentOutOfRangeException(nameof(fork), fork, "No handler table for that fork.");
        }
    }
}
