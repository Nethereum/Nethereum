using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution.Opcodes;
using Nethereum.EVM.Gas;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class Eip7954CodeSizeTests
    {
        private const long INIT_CODE_WORD_GAS = 2;

        private const int InitCodeLengthOnlyValidAtAmsterdam = 65_536;

        [Fact]
        public void Given_Amsterdam_When_CodeSizeCapsRead_Then_65536_And_DoubleThatForInitCode()
        {
            Assert.Equal(65_536, HardforkConfig.Amsterdam.MaxCodeSize);
            Assert.Equal(131_072, HardforkConfig.Amsterdam.MaxInitcodeSize);

            // EIP-3860's "init code is twice the code cap" relationship must
            Assert.Equal(HardforkConfig.Amsterdam.MaxCodeSize * 2,
                HardforkConfig.Amsterdam.MaxInitcodeSize);
        }

        [Fact]
        public void Given_Osaka_When_CodeSizeCapsRead_Then_StillEip170And3860_NoCrossForkLeak()
        {
            Assert.Equal(24_576, HardforkConfig.Osaka.MaxCodeSize);
            Assert.Equal(49_152, HardforkConfig.Osaka.MaxInitcodeSize);
        }

        [Theory]
        [InlineData(Instruction.CREATE)]
        [InlineData(Instruction.CREATE2)]
        public async Task Given_InitCodeAboveOldCap_AtAmsterdam_When_CreateCharged_Then_WordGasChargedIdenticallyAtBothForks(Instruction instruction)
        {
            long amsterdam = await CreateGasForAsync(OpcodeHandlerSets.Amsterdam, instruction,
                InitCodeLengthOnlyValidAtAmsterdam);
            long osaka = await CreateGasForAsync(OpcodeHandlerSets.Osaka, instruction,
                InitCodeLengthOnlyValidAtAmsterdam);

            Assert.Equal(GasConstants.EIP8038_CREATE_ACCESS - GasConstants.CREATE_BASE,
                amsterdam - osaka);
        }

        [Theory]
        [InlineData(Instruction.CREATE)]
        [InlineData(Instruction.CREATE2)]
        public async Task Given_InitCodeAboveAmsterdamCap_When_PreCheckGasCostRead_Then_WordGasStillCharged_MatchingInitCodeCost(Instruction instruction)
        {
            const int overCap = 131_073;

            long atCap = await CreateGasForAsync(OpcodeHandlerSets.Amsterdam, instruction, 131_072);
            long overCapGas = await CreateGasForAsync(OpcodeHandlerSets.Amsterdam, instruction, overCap);

            Assert.True(overCapGas > atCap,
                $"expected word gas to still be charged above the cap (matching init_code_cost's unconditional formula); at cap {atCap}, over cap {overCapGas}");
        }

        [Fact]
        public async Task Given_InitCodeAboveAmsterdamCap_AtAmsterdam_When_CreateExecuted_Then_CallingFrameExceptionallyHalts()
        {
            var executionStateService = new ExecutionStateService(new MockNodeDataService());
            const string sender = "0x1111111111111111111111111111111111111111";
            const long gasLimit = 10_000_000;

            var code = new byte[] { 0x62, 0x02, 0x00, 0x01, 0x60, 0x00, 0x60, 0x00, 0xf0 };

            var ctx = new TransactionExecutionContext
            {
                Sender = sender,
                To = "",
                Data = code,
                IsContractCreation = true,
                GasLimit = gasLimit,
                Value = 0,
                GasPrice = 1,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 0,
                ChainId = 1,
                Coinbase = sender,
                ExecutionState = executionStateService
            };
            executionStateService.SetInitialChainBalance(sender, System.Numerics.BigInteger.Parse("1000000000000000000000"));

            var executor = new TransactionExecutor(HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase()));
            var result = await executor.ExecuteAsync(ctx);

            Assert.False(result.Success);
            Assert.Equal(gasLimit, result.GasUsed);
        }

        private static async Task<long> CreateGasForAsync(OpcodeHandlerTable table, Instruction instruction, int initCodeLength)
        {
            var program = NewProgram();

            program.StackPush(initCodeLength);
            program.StackPush(0);
            program.StackPush(0);

            return await table.GetGasCostAsync(instruction, program);
        }

        private static Program NewProgram()
        {
            var executionStateService = new ExecutionStateService(new MockNodeDataService());
            var callInput = new CallInput
            {
                From = "0x1111111111111111111111111111111111111111",
                To = "0x2222222222222222222222222222222222222222",
                Data = "",
                Value = new Nethereum.Hex.HexTypes.HexBigInteger(0),
                Gas = new Nethereum.Hex.HexTypes.HexBigInteger(50_000_000)
            };
            var programContext = new ProgramContext(callInput, executionStateService);
            return new Program(new byte[] { 0x00 }, programContext) { GasRemaining = 50_000_000 };
        }
    }
}
