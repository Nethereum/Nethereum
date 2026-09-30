using System;
using System.Collections.Generic;
using System.Linq;
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
    public class SelfDestructBeneficiaryResolvingGasCostTests
    {
        private const string ContractAddress = "0x1234567890123456789012345678901234567890";
        private const string BeneficiaryAddress = "0x00000000000000000000000000000000000000bb";

        private sealed class CountingStateAccessRecorder : IStateAccessRecorder
        {
            public List<string> Reads { get; } = new List<string>();

            public void RecordAccountRead(string address) => Reads.Add("account:" + address.ToLower());

            public void RecordStorageRead(string address, EvmUInt256 key) =>
                Reads.Add("slot:" + address.ToLower() + ":" + key.ToString());
        }

        private sealed class Fixture
        {
            public Program Program;
            public ExecutionStateService StateService;
            public CountingStateAccessRecorder Recorder;
        }

        private static Fixture CreateProgram(
            long contractBalance = 0,
            long beneficiaryBalance = 0,
            byte[] beneficiaryCode = null,
            long beneficiaryNonce = 0,
            bool beneficiaryHasAPreStateRecord = false,
            bool beneficiaryIsWarm = false,
            long gas = 1_000_000,
            bool stateGasActive = false,
            bool pushBeneficiary = true)
        {
            var accounts = new Dictionary<string, AccountState>();
            if (contractBalance != 0)
                accounts[ContractAddress.ToLower()] = new AccountState { Balance = new EvmUInt256(contractBalance) };
            if (beneficiaryBalance != 0 || beneficiaryCode != null || beneficiaryNonce != 0)
                accounts[BeneficiaryAddress.ToLower()] = new AccountState
                {
                    Balance = new EvmUInt256(beneficiaryBalance),
                    Nonce = new EvmUInt256(beneficiaryNonce),
                    Code = beneficiaryCode ?? new byte[0]
                };

            var stateService = new ExecutionStateService(new MockNodeDataService(new InMemoryStateReader(accounts)));
            if (beneficiaryHasAPreStateRecord)
                stateService.CreateOrGetAccountExecutionState(BeneficiaryAddress).WasInPreState = true;
            if (beneficiaryIsWarm)
                stateService.MarkAddressAsWarm(BeneficiaryAddress);

            var recorder = new CountingStateAccessRecorder();
            stateService.AccessRecorder = recorder;

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
            if (pushBeneficiary) program.StackPush(AddressWord(BeneficiaryAddress));

            return new Fixture { Program = program, StateService = stateService, Recorder = recorder };
        }

        private static byte[] AddressWord(string address)
        {
            var raw = address.HexToByteArray();
            var word = new byte[32];
            Array.Copy(raw, 0, word, 32 - raw.Length, raw.Length);
            return word;
        }

        private static string[] TheBeneficiarysBalanceCodeAndNonce() => new[]
        {
            "account:" + BeneficiaryAddress,
            "account:" + BeneficiaryAddress,
            "account:" + BeneficiaryAddress
        };

        private static OpcodeHandlerTable ForkTable(string fork)
        {
            switch (fork)
            {
                case "Frontier": return OpcodeHandlerSets.Frontier;
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
        [InlineData("Frontier", 7, false, 0L)]
        [InlineData("Frontier", 0, false, 0L)]
        [InlineData("TangerineWhistle", 7, false, 30000L)]
        [InlineData("TangerineWhistle", 0, false, 30000L)]
        [InlineData("TangerineWhistle", 7, true, 30000L)]
        [InlineData("SpuriousDragon", 7, false, 30000L)]
        [InlineData("SpuriousDragon", 0, false, 5000L)]
        [InlineData("SpuriousDragon", 7, true, 30000L)]
        [InlineData("Istanbul", 7, false, 30000L)]
        [InlineData("Istanbul", 0, false, 5000L)]
        [InlineData("Berlin", 7, false, 32600L)]
        [InlineData("Berlin", 0, false, 7600L)]
        [InlineData("Berlin", 7, true, 30000L)]
        [InlineData("Berlin", 0, true, 5000L)]
        [InlineData("Cancun", 7, false, 32600L)]
        [InlineData("Osaka", 7, false, 32600L)]
        [InlineData("Osaka", 0, true, 5000L)]
        [InlineData("Amsterdam", 7, false, 17000L)]
        [InlineData("Amsterdam", 0, false, 8000L)]
        [InlineData("Amsterdam", 7, true, 14000L)]
        [InlineData("Amsterdam", 0, true, 5000L)]
        public async Task Given_AnAbsentBeneficiary_When_SelfDestructPricedAtAFork_Then_TheChargeIsTheOneThatForkCharges(
            string fork, long contractBalance, bool beneficiaryIsWarm, long expected)
        {
            var fixture = CreateProgram(contractBalance: contractBalance, beneficiaryIsWarm: beneficiaryIsWarm);

            var cost = await ForkTable(fork).GetGasCostAsync(Instruction.SELFDESTRUCT, fixture.Program);

            Assert.Equal(expected, cost);
        }

        [Fact]
        public async Task Given_APreEip161Fork_When_TheBeneficiaryIsSwept_Then_ItsBalanceCodeAndNonceAreStillReadAndRecorded()
        {
            var fixture = CreateProgram(contractBalance: 7);

            await OpcodeHandlerSets.TangerineWhistle.GetGasCostAsync(Instruction.SELFDESTRUCT, fixture.Program);

            Assert.Equal(TheBeneficiarysBalanceCodeAndNonce(), fixture.Recorder.Reads);
            Assert.Equal(new[] { BeneficiaryAddress }, fixture.StateService.AccountsState.Keys.Select(k => k.ToHexLower()).ToList());
        }

        [Fact]
        public async Task Given_APreEip161Fork_When_TheBeneficiaryIsSwept_Then_TheContractsBalanceIsNotRead()
        {
            var fixture = CreateProgram(contractBalance: 7);

            await OpcodeHandlerSets.TangerineWhistle.GetGasCostAsync(Instruction.SELFDESTRUCT, fixture.Program);

            Assert.DoesNotContain("account:" + ContractAddress.ToLower(), fixture.Recorder.Reads);
            Assert.DoesNotContain(EvmAddress.FromHex(ContractAddress), fixture.StateService.AccountsState.Keys);
        }

        [Fact]
        public async Task Given_APreEip161ForkAndABeneficiaryWithAnAccountRecord_When_Swept_Then_NoNewAccountIsCharged()
        {
            var fixture = CreateProgram(contractBalance: 7, beneficiaryHasAPreStateRecord: true);

            var cost = await OpcodeHandlerSets.TangerineWhistle.GetGasCostAsync(Instruction.SELFDESTRUCT, fixture.Program);

            Assert.Equal(GasConstants.SELFDESTRUCT_COST, cost);
        }

        [Fact]
        public async Task Given_ABeneficiaryThatIsAlreadyAlive_When_SelfDestructPriced_Then_TheContractsBalanceIsNotRead()
        {
            var fixture = CreateProgram(contractBalance: 7, beneficiaryBalance: 5);

            await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.SELFDESTRUCT, fixture.Program);

            Assert.Equal(TheBeneficiarysBalanceCodeAndNonce(), fixture.Recorder.Reads);
            Assert.Equal(new[] { BeneficiaryAddress }, fixture.StateService.AccountsState.Keys.Select(k => k.ToHexLower()).ToList());
        }

        [Fact]
        public async Task Given_ADeadBeneficiary_When_SelfDestructPriced_Then_TheContractsBalanceIsReadOnce()
        {
            var fixture = CreateProgram(contractBalance: 7);

            await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.SELFDESTRUCT, fixture.Program);

            Assert.Equal(
                new[]
                {
                    "account:" + BeneficiaryAddress,
                    "account:" + BeneficiaryAddress,
                    "account:" + BeneficiaryAddress,
                    "account:" + ContractAddress.ToLower()
                },
                fixture.Recorder.Reads);
        }

        /// <summary>
        /// EIP-7928: "Pre-state validation MUST pass before any state access
        /// occurs. If pre-state validation fails, the target resource […] is
        /// never accessed and MUST NOT be included in the BAL."
        /// </summary>
        [Fact]
        public async Task Given_AFrameThatCannotAffordThePreStateCharge_When_SelfDestructPriced_Then_TheBeneficiaryIsNeverRead()
        {
            var fixture = CreateProgram(contractBalance: 7, gas: GasConstants.SELFDESTRUCT_COST - 1);

            await Assert.ThrowsAnyAsync<Exception>(
                () => OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.SELFDESTRUCT, fixture.Program));

            Assert.Empty(fixture.Recorder.Reads);
            Assert.Empty(fixture.StateService.AccountsState);
        }

        [Fact]
        public async Task Given_ASelfDestructWithNoOperand_When_Priced_Then_NothingIsReadOrMarkedWarm()
        {
            var fixture = CreateProgram(contractBalance: 7, pushBeneficiary: false);

            await Assert.ThrowsAnyAsync<Exception>(
                () => OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.SELFDESTRUCT, fixture.Program));

            Assert.Empty(fixture.Recorder.Reads);
            Assert.Empty(fixture.StateService.AccountsState);
            Assert.False(fixture.StateService.AddressIsWarm(BeneficiaryAddress));
        }

        [Theory]
        [InlineData("TangerineWhistle")]
        [InlineData("SpuriousDragon")]
        [InlineData("Istanbul")]
        public async Task Given_APreBerlinFork_When_AColdBeneficiaryIsSwept_Then_ItDoesNotJoinTheAccessList(string fork)
        {
            var fixture = CreateProgram(contractBalance: 7);

            await ForkTable(fork).GetGasCostAsync(Instruction.SELFDESTRUCT, fixture.Program);

            Assert.False(fixture.StateService.AddressIsWarm(BeneficiaryAddress));
        }

        [Fact]
        public async Task Given_ABerlinFork_When_AColdBeneficiaryIsSwept_Then_ItJoinsTheAccessList()
        {
            var fixture = CreateProgram(contractBalance: 7);

            await OpcodeHandlerSets.Berlin.GetGasCostAsync(Instruction.SELFDESTRUCT, fixture.Program);

            Assert.True(fixture.StateService.AddressIsWarm(BeneficiaryAddress));
        }

        [Fact]
        public async Task Given_AmsterdamAndASweepThatCreatesTheBeneficiary_When_Priced_Then_StateGasIsChargedToo()
        {
            var fixture = CreateProgram(contractBalance: 7, stateGasActive: true);

            var cost = await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.SELFDESTRUCT, fixture.Program);

            Assert.Equal(
                GasConstants.SELFDESTRUCT_COST + GasConstants.EIP8038_COLD_ACCOUNT_ACCESS + GasConstants.EIP8038_ACCOUNT_WRITE,
                cost);
            Assert.Equal(1_000_000 - GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS, fixture.Program.StateGasLeft);
        }

        [Fact]
        public async Task Given_AmsterdamAndASweepThatCreatesNothing_When_Priced_Then_NoStateGasIsTaken()
        {
            var fixture = CreateProgram(contractBalance: 0, stateGasActive: true);

            await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.SELFDESTRUCT, fixture.Program);

            Assert.Equal(1_000_000, fixture.Program.StateGasLeft);
        }

        [Fact]
        public async Task Given_APreAmsterdamFork_When_TheSweepCreatesTheBeneficiary_Then_NoStateGasIsTaken()
        {
            var fixture = CreateProgram(contractBalance: 7, stateGasActive: true);

            await OpcodeHandlerSets.Berlin.GetGasCostAsync(Instruction.SELFDESTRUCT, fixture.Program);

            Assert.Equal(1_000_000, fixture.Program.StateGasLeft);
        }

        [Theory]
        [InlineData("balance")]
        [InlineData("code")]
        [InlineData("nonce")]
        public async Task Given_ABeneficiaryAliveOnOneFieldAlone_When_SelfDestructPriced_Then_NoNewAccountIsCharged(
            string field)
        {
            var fixture = CreateProgram(
                contractBalance: 7,
                beneficiaryBalance: field == "balance" ? 5 : 0,
                beneficiaryCode: field == "code" ? new byte[] { 0x60, 0x00 } : null,
                beneficiaryNonce: field == "nonce" ? 3 : 0);

            var cost = await OpcodeHandlerSets.Berlin.GetGasCostAsync(Instruction.SELFDESTRUCT, fixture.Program);

            Assert.Equal(GasConstants.SELFDESTRUCT_COST + GasConstants.COLD_ACCOUNT_ACCESS_COST, cost);
        }
    }
}
