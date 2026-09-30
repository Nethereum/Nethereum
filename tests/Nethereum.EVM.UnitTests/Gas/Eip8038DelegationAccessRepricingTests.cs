using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Execution.CallFrame;
using Nethereum.EVM.Gas;
using Nethereum.EVM.UnitTests;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class Eip8038DelegationAccessRepricingTests
    {
        private const string ContractAddress = "0x1234567890123456789012345678901234567890";
        private const string CallerAddress = "0xABCDEF0123456789ABCDEF0123456789ABCDEF01";
        private const string DelegateAddress = "0x9999999999999999999999999999999999999999";

        private static (Program program, ExecutionStateService stateService) CreateProgram(long gasRemaining = 1_000_000)
        {
            var stateService = new ExecutionStateService(new MockNodeDataService());
            var callInput = new CallInput
            {
                To = ContractAddress,
                From = CallerAddress,
                Gas = new Nethereum.Hex.HexTypes.HexBigInteger(gasRemaining),
                Data = "0x"
            };

            var context = new ProgramContext(callInput, stateService);
            var program = new Program("00".HexToByteArray(), context);
            program.GasRemaining = gasRemaining;
            return (program, stateService);
        }

        private static CallFrameSetupContext CreateDelegationSetupContext(Program program, ExecutionStateService stateService)
        {
            return new CallFrameSetupContext
            {
                Program = program,
                CodeAddress = ContractAddress,
                ByteCode = Eip7702DelegationUtils.CreateDelegationCode(DelegateAddress),
                CallType = CallFrameType.Call,
                ExecutionState = stateService
            };
        }

        [Fact]
        public async Task Given_ADelegatedCall_AtAmsterdam_When_TheDelegateIsCold_Then_ChargesThreeThousand()
        {
            var (program, stateService) = CreateProgram();
            var setupContext = CreateDelegationSetupContext(program, stateService);
            var gasBefore = program.GasRemaining;

            await CallFrameInitRuleSets.Amsterdam.ApplyAsync(setupContext);

            var gasCharged = gasBefore - program.GasRemaining;
            Assert.Equal(GasConstants.EIP8038_COLD_ACCOUNT_ACCESS, gasCharged);
            Assert.Equal(3000, gasCharged);
        }

        [Fact]
        public async Task Given_ADelegatedCall_AtOsaka_When_TheDelegateIsCold_Then_StillChargesTwentySixHundred()
        {
            var (program, stateService) = CreateProgram();
            var setupContext = CreateDelegationSetupContext(program, stateService);
            var gasBefore = program.GasRemaining;

            await CallFrameInitRuleSets.Osaka.ApplyAsync(setupContext);

            var gasCharged = gasBefore - program.GasRemaining;
            Assert.Equal(GasConstants.COLD_ACCOUNT_ACCESS_COST, gasCharged);
            Assert.Equal(2600, gasCharged);
        }

        [Fact]
        public async Task Given_ADelegatedCall_AtAmsterdam_When_TheDelegateIsWarm_Then_ChargesOneHundred()
        {
            var (program, stateService) = CreateProgram();
            stateService.MarkAddressAsWarm(DelegateAddress);
            var setupContext = CreateDelegationSetupContext(program, stateService);
            var gasBefore = program.GasRemaining;

            await CallFrameInitRuleSets.Amsterdam.ApplyAsync(setupContext);

            var gasCharged = gasBefore - program.GasRemaining;
            Assert.Equal(GasConstants.WARM_STORAGE_READ_COST, gasCharged);
            Assert.Equal(100, gasCharged);
        }
    }
}
