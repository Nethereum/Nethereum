using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution.Opcodes;
using Nethereum.EVM.Gas;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class CallTargetResolvingGasCostTests
    {
        private const string ContractAddress = "0x1234567890123456789012345678901234567890";
        private const string TargetAddress = "0x00000000000000000000000000000000000000ff";

        private sealed class CountingStateAccessRecorder : IStateAccessRecorder
        {
            public List<string> Reads { get; } = new List<string>();

            public void RecordAccountRead(string address) => Reads.Add("account:" + address.ToLower());

            public void RecordStorageRead(string address, EvmUInt256 key) =>
                Reads.Add("slot:" + address.ToLower() + ":" + key.ToString());
        }

        private sealed class CountingStateReader : IStateReader
        {
            private readonly InMemoryStateReader _inner;
            public List<string> Reads { get; } = new List<string>();

            public CountingStateReader(InMemoryStateReader inner) { _inner = inner; }

            public Task<EvmUInt256> GetBalanceAsync(byte[] address) => GetBalanceAsync(address.ToHex(true));
            public Task<EvmUInt256> GetBalanceAsync(string address)
            { Reads.Add("balance:" + address.ToLower()); return _inner.GetBalanceAsync(address); }

            public Task<byte[]> GetCodeAsync(byte[] address) => GetCodeAsync(address.ToHex(true));
            public Task<byte[]> GetCodeAsync(string address)
            { Reads.Add("code:" + address.ToLower()); return _inner.GetCodeAsync(address); }

            public Task<EvmUInt256> GetTransactionCountAsync(byte[] address) => GetTransactionCountAsync(address.ToHex(true));
            public Task<EvmUInt256> GetTransactionCountAsync(string address)
            { Reads.Add("nonce:" + address.ToLower()); return _inner.GetTransactionCountAsync(address); }

            public Task<byte[]> GetStorageAtAsync(byte[] address, EvmUInt256 position) => GetStorageAtAsync(address.ToHex(true), position);
            public Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 position)
            { Reads.Add("slot:" + address.ToLower()); return _inner.GetStorageAtAsync(address, position); }

            public Task<bool> AccountExistsAsync(string address)
            { Reads.Add("exists:" + address.ToLower()); return _inner.AccountExistsAsync(address); }

            public Task<byte[]> GetBlockHashAsync(long blockNumber) => _inner.GetBlockHashAsync(blockNumber);
        }

        private sealed class Fixture
        {
            public Program Program;
            public ExecutionStateService StateService;
            public CountingStateAccessRecorder Recorder;
            public CountingStateReader Reader;
        }

        private static Fixture CreateProgram(
            long value = 0,
            long targetBalance = 0,
            byte[] targetCode = null,
            long targetNonce = 0,
            bool targetIsWarm = false,
            long gas = 1_000_000,
            bool stateGasActive = false,
            int operands = 7)
        {
            var accounts = new Dictionary<string, AccountState>
            {
                [ContractAddress.ToLower()] = new AccountState { Balance = new EvmUInt256(100) }
            };
            if (targetBalance != 0 || targetCode != null || targetNonce != 0)
                accounts[TargetAddress.ToLower()] = new AccountState
                {
                    Balance = new EvmUInt256(targetBalance),
                    Nonce = new EvmUInt256(targetNonce),
                    Code = targetCode ?? new byte[0]
                };

            var reader = new CountingStateReader(new InMemoryStateReader(accounts));
            var stateService = new ExecutionStateService(reader);
            if (targetIsWarm) stateService.MarkAddressAsWarm(TargetAddress);

            var recorder = new CountingStateAccessRecorder();
            stateService.AccessRecorder = recorder;
            reader.Reads.Clear();

            var callInput = new CallInput
            {
                To = ContractAddress,
                From = ContractAddress,
                Gas = new Nethereum.Hex.HexTypes.HexBigInteger(gas),
                Data = "0x"
            };

            var context = new ProgramContext(callInput, stateService) { StateGasActive = stateGasActive };
            var program = new Program("00".HexToByteArray(), context);
            program.GasRemaining = gas;
            program.StateGasLeft = stateGasActive ? 1_000_000 : 0;
            program.StateGasBaseline = program.StateGasLeft;

            var stack = new[]
            {
                Word(0), Word(0), Word(0), Word(0), Word(value), AddressWord(TargetAddress), Word(50_000)
            };
            for (var i = stack.Length - operands; i < stack.Length; i++) program.StackPush(stack[i]);

            return new Fixture
            {
                Program = program, StateService = stateService, Recorder = recorder, Reader = reader
            };
        }

        private static byte[] Word(long value)
        {
            var word = new byte[32];
            var raw = BitConverter.GetBytes(value);
            for (var i = 0; i < raw.Length; i++) word[31 - i] = raw[i];
            return word;
        }

        private static byte[] AddressWord(string address)
        {
            var raw = address.HexToByteArray();
            var word = new byte[32];
            Array.Copy(raw, 0, word, 32 - raw.Length, raw.Length);
            return word;
        }

        private static OpcodeHandlerTable ForkTable(string fork)
        {
            switch (fork)
            {
                case "Frontier": return OpcodeHandlerSets.Frontier;
                case "Homestead": return OpcodeHandlerSets.Homestead;
                case "TangerineWhistle": return OpcodeHandlerSets.TangerineWhistle;
                case "SpuriousDragon": return OpcodeHandlerSets.SpuriousDragon;
                case "Istanbul": return OpcodeHandlerSets.Istanbul;
                case "Berlin": return OpcodeHandlerSets.Berlin;
                case "Cancun": return OpcodeHandlerSets.Cancun;
                case "Osaka": return OpcodeHandlerSets.Osaka;
                case "Amsterdam": return OpcodeHandlerSets.Amsterdam;
                default: throw new ArgumentOutOfRangeException(nameof(fork), fork, "No handler table for that fork.");
            }
        }

        [Theory]
        [InlineData("Frontier", 0, false, 25_040L)]
        [InlineData("Frontier", 1, false, 34_040L)]
        [InlineData("Homestead", 0, false, 25_040L)]
        [InlineData("TangerineWhistle", 0, false, 25_700L)]
        [InlineData("TangerineWhistle", 1, false, 34_700L)]
        [InlineData("SpuriousDragon", 0, false, 700L)]
        [InlineData("SpuriousDragon", 1, false, 34_700L)]
        [InlineData("Istanbul", 0, false, 700L)]
        [InlineData("Istanbul", 1, false, 34_700L)]
        [InlineData("Berlin", 0, false, 2_600L)]
        [InlineData("Berlin", 1, false, 36_600L)]
        [InlineData("Berlin", 0, true, 100L)]
        [InlineData("Berlin", 1, true, 34_100L)]
        [InlineData("Cancun", 1, false, 36_600L)]
        [InlineData("Osaka", 1, false, 36_600L)]
        [InlineData("Amsterdam", 0, false, 3_000L)]
        [InlineData("Amsterdam", 1, false, 14_300L)]
        [InlineData("Amsterdam", 1, true, 11_400L)]
        public async Task Given_AnAbsentTarget_When_CallPricedAtAFork_Then_TheChargeIsTheOneThatForkCharges(
            string fork, long value, bool targetIsWarm, long expected)
        {
            var fixture = CreateProgram(value: value, targetIsWarm: targetIsWarm);

            var cost = await ForkTable(fork).GetGasCostAsync(Instruction.CALL, fixture.Program);

            Assert.Equal(expected, cost);
        }

        [Theory]
        [InlineData("SpuriousDragon")]
        [InlineData("Istanbul")]
        [InlineData("Berlin")]
        [InlineData("Cancun")]
        [InlineData("Osaka")]
        [InlineData("Amsterdam")]
        public async Task Given_ACallCarryingNoValue_When_PricedFromEip161Onward_Then_TheTargetIsNeverRead(string fork)
        {
            var fixture = CreateProgram(value: 0);

            await ForkTable(fork).GetGasCostAsync(Instruction.CALL, fixture.Program);

            Assert.Empty(fixture.Reader.Reads);
        }

        [Theory]
        [InlineData("SpuriousDragon")]
        [InlineData("Berlin")]
        [InlineData("Amsterdam")]
        public async Task Given_ACallCarryingValue_When_PricedFromEip161Onward_Then_TheTargetIsRead(string fork)
        {
            var fixture = CreateProgram(value: 1);

            await ForkTable(fork).GetGasCostAsync(Instruction.CALL, fixture.Program);

            Assert.Equal(
                new[]
                {
                    "balance:" + TargetAddress,
                    "nonce:" + TargetAddress,
                    "code:" + TargetAddress
                },
                fixture.Reader.Reads);
        }

        [Fact]
        public async Task Given_ACallCarryingValueIntoAFundedTarget_When_Priced_Then_ItsBalanceAloneIsRead()
        {
            var fixture = CreateProgram(value: 1, targetBalance: 5);

            await OpcodeHandlerSets.Berlin.GetGasCostAsync(Instruction.CALL, fixture.Program);

            Assert.Equal(new[] { "balance:" + TargetAddress }, fixture.Reader.Reads);
        }

        [Theory]
        [InlineData("Frontier", 0)]
        [InlineData("TangerineWhistle", 1)]
        [InlineData("Berlin", 0)]
        [InlineData("Berlin", 1)]
        [InlineData("Amsterdam", 0)]
        [InlineData("Amsterdam", 1)]
        public async Task Given_ACallThatCanAffordItsPreStateGas_When_Priced_Then_TheTargetIsRecordedExactlyOnce(
            string fork, long value)
        {
            var fixture = CreateProgram(value: value);

            await ForkTable(fork).GetGasCostAsync(Instruction.CALL, fixture.Program);

            Assert.Equal(new[] { "account:" + TargetAddress }, fixture.Recorder.Reads);
        }

        /// <summary>
        /// EIP-7928: "If pre-state validation fails, the target resource
        /// (address or storage slot) is never accessed and MUST NOT be
        /// included in the BAL."
        /// </summary>
        [Theory]
        [InlineData("Berlin")]
        [InlineData("Amsterdam")]
        public async Task Given_ACallThatCannotAffordItsPreStateGas_When_Priced_Then_NothingIsRecordedOrRead(string fork)
        {
            var fixture = CreateProgram(value: 1, gas: 10);

            await Assert.ThrowsAnyAsync<Exception>(() =>
                ForkTable(fork).GetGasCostAsync(Instruction.CALL, fixture.Program));

            Assert.Empty(fixture.Recorder.Reads);
            Assert.Empty(fixture.Reader.Reads);
        }

        [Theory]
        [InlineData("Frontier", 0)]
        [InlineData("TangerineWhistle", 1)]
        [InlineData("SpuriousDragon", 1)]
        [InlineData("Berlin", 1)]
        [InlineData("Amsterdam", 1)]
        public async Task Given_ACallToAnAbsentTarget_When_Priced_Then_NoAccountIsMaterialised(string fork, long value)
        {
            var fixture = CreateProgram(value: value);

            await ForkTable(fork).GetGasCostAsync(Instruction.CALL, fixture.Program);

            Assert.Empty(fixture.StateService.AccountsState);
        }

        [Fact]
        public async Task Given_AValueCallIntoADeadTargetAtAmsterdam_When_Priced_Then_TheCreationIsChargedToTheStateGasReservoir()
        {
            var fixture = CreateProgram(value: 1, stateGasActive: true);

            var cost = await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.CALL, fixture.Program);

            Assert.Equal(14_300L, cost);
            Assert.Equal(1_000_000L - GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS, fixture.Program.StateGasLeft);
            Assert.True(fixture.Program.CallNewAccountStateGasCharged);
        }

        [Fact]
        public async Task Given_AValueCallIntoALiveTargetAtAmsterdam_When_Priced_Then_TheReservoirIsUntouched()
        {
            var fixture = CreateProgram(value: 1, targetBalance: 5, stateGasActive: true);

            var cost = await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.CALL, fixture.Program);

            Assert.Equal(14_300L, cost);
            Assert.Equal(1_000_000L, fixture.Program.StateGasLeft);
            Assert.False(fixture.Program.CallNewAccountStateGasCharged);
        }

        [Fact]
        public async Task Given_AValueCallIntoADeadTargetAtBerlin_When_Priced_Then_NoStateGasIsChargedAndNoHandoffIsMade()
        {
            var fixture = CreateProgram(value: 1, stateGasActive: true);

            var cost = await OpcodeHandlerSets.Berlin.GetGasCostAsync(Instruction.CALL, fixture.Program);

            Assert.Equal(36_600L, cost);
            Assert.Equal(1_000_000L, fixture.Program.StateGasLeft);
            Assert.False(fixture.Program.CallNewAccountStateGasCharged);
        }

        [Theory]
        [InlineData("Frontier")]
        [InlineData("TangerineWhistle")]
        [InlineData("SpuriousDragon")]
        [InlineData("Istanbul")]
        public async Task Given_AForkWithoutAnAccessList_When_CallPriced_Then_TheTargetIsNotMarkedWarm(string fork)
        {
            var fixture = CreateProgram(value: 1);

            await ForkTable(fork).GetGasCostAsync(Instruction.CALL, fixture.Program);

            Assert.False(fixture.Program.IsAddressWarm(AddressWord(TargetAddress)));
        }

        [Theory]
        [InlineData("Berlin")]
        [InlineData("Amsterdam")]
        public async Task Given_AForkWithAnAccessList_When_CallPriced_Then_TheTargetIsMarkedWarm(string fork)
        {
            var fixture = CreateProgram(value: 1);

            await ForkTable(fork).GetGasCostAsync(Instruction.CALL, fixture.Program);

            Assert.True(fixture.Program.IsAddressWarm(AddressWord(TargetAddress)));
        }
    }
}
