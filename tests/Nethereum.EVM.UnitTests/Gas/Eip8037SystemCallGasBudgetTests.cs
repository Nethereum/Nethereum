using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Gas;
using Nethereum.EVM.UnitTests;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class Eip8037SystemCallGasBudgetTests
    {
        private const string PredeployAddress = "0x0000bff46984e3725691fa540a8c7589300d8282";

        private static HardforkConfig Amsterdam()
            => HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());

        private static HardforkConfig Prague()
            => HardforkConfig.Prague.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());

        private static string StorageSets(int count)
        {
            var code = new System.Text.StringBuilder();
            for (var slot = 0; slot < count; slot++)
                code.Append("6001").Append("60").Append(slot.ToString("x2")).Append("55");
            return code.Append("00").ToString();
        }

        private static string MemoryExpansionTo(int offset)
            => "6000" + "63" + offset.ToString("x8") + "52" + "00";

        private static async Task<EIP7702TestNodeDataService> PredeployedAsync(string code)
        {
            var node = new EIP7702TestNodeDataService();
            await node.SetCodeAsync(PredeployAddress, code.HexToByteArray());
            return node;
        }

        private static TransactionExecutionContext SystemCall(ExecutionStateService executionState, byte[] callData = null)
            => new TransactionExecutionContext
            {
                Mode = ExecutionMode.SystemCall,
                Sender = SystemCallContracts.SystemCaller,
                To = PredeployAddress,
                Data = callData ?? new byte[0],
                IsContractCreation = false,
                Value = 0,
                GasPrice = 0,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 0,
                BlockGasLimit = 36_000_000,
                ChainId = 1,
                Coinbase = PredeployAddress,
                ExecutionState = executionState
            };

        private static async Task<(TransactionExecutionContext ctx, TransactionExecutionResult result)> RunAsync(
            HardforkConfig config, TransactionExecutionContext ctx)
        {
            var result = await new TransactionExecutor(config).ExecuteAsync(ctx);
            return (ctx, result);
        }

        [Fact]
        public void Given_TheEip8037Formula_When_Evaluated_Then_TheAmsterdamBudgetIsThirtyMillionPlusOneAndAHalfMillion()
        {
            Assert.Equal(16, GasConstants.EIP8037_SYSTEM_MAX_SSTORES_PER_CALL);
            Assert.Equal(30_000_000, SystemCallGasBudget.Eip8037.ExecutionGas);
            Assert.Equal(1_566_720, SystemCallGasBudget.Eip8037.StateGasReservoir);
            Assert.Equal(30_000_000, SystemCallGasBudget.ExecutionGasOnly.ExecutionGas);
            Assert.Equal(0, SystemCallGasBudget.ExecutionGasOnly.StateGasReservoir);
        }

        [Fact]
        public void Given_TheForkSpecs_When_TheirSystemCallBudgetIsRead_Then_OnlyAmsterdamCarriesAReservoir()
        {
            Assert.Same(SystemCallGasBudget.Eip8037, HardforkConfig.Amsterdam.SystemCallGas);
            Assert.Same(SystemCallGasBudget.ExecutionGasOnly, HardforkConfig.Osaka.SystemCallGas);
            Assert.Same(SystemCallGasBudget.ExecutionGasOnly, HardforkConfig.Prague.SystemCallGas);
            Assert.Same(SystemCallGasBudget.ExecutionGasOnly, HardforkConfig.Cancun.SystemCallGas);
        }

        [Fact]
        public async Task Given_AnAmsterdamSystemCall_When_ItExhaustsExecutionGasWithReservoirRemaining_Then_ItRunsOutOfGas()
        {
            var config = Amsterdam();
            var node = await PredeployedAsync(MemoryExpansionTo(3_992_864));
            var ctx = SystemCall(new ExecutionStateService(node));

            var (resultCtx, result) = await RunAsync(config, ctx);

            Assert.False(result.Success);
            Assert.Equal(SystemCallGasBudget.Eip8037.ExecutionGas, resultCtx.ExecutionGasGrant);
            Assert.Equal(SystemCallGasBudget.Eip8037.StateGasReservoir, resultCtx.StateGasReservoir);
            Assert.Equal(resultCtx.StateGasReservoir, resultCtx.StateGas.ReservoirRemaining);
        }

        [Fact]
        public async Task Given_AnAmsterdamSystemCall_When_ItConsumesTheWholeBudget_Then_ItSucceeds()
        {
            var config = Amsterdam();
            var node = await PredeployedAsync(StorageSets((int)GasConstants.EIP8037_SYSTEM_MAX_SSTORES_PER_CALL));
            var ctx = SystemCall(new ExecutionStateService(node));

            var (resultCtx, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(0, resultCtx.StateGas.ReservoirRemaining);
            Assert.Equal(SystemCallGasBudget.Eip8037.StateGasReservoir, resultCtx.StateGas.FromReservoir);
            Assert.Equal(0, resultCtx.StateGas.SpilledIntoExecution);
        }

        [Fact]
        public async Task Given_AnAmsterdamSystemCall_When_Metered_Then_TheTwoToThe24TransactionCapIsNotApplied()
        {
            var config = Amsterdam();
            var node = await PredeployedAsync(MemoryExpansionTo(3_477_248));
            var ctx = SystemCall(new ExecutionStateService(node));

            var (resultCtx, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.True(resultCtx.ExecutionGasGrant > GasConstants.EIP8037_TX_MAX_GAS_LIMIT);
        }

        [Fact]
        public async Task Given_AnAmsterdamSystemCall_When_Metered_Then_NoIntrinsicGasIsCharged()
        {
            var config = Amsterdam();
            var callData = new byte[32];
            for (var i = 0; i < callData.Length; i++) callData[i] = 0xff;

            var node = await PredeployedAsync(StorageSets(1));
            var ctx = SystemCall(new ExecutionStateService(node), callData);

            var (resultCtx, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(0, resultCtx.IntrinsicExecutionGas);
            Assert.Equal(SystemCallGasBudget.Eip8037.ExecutionGas, resultCtx.ExecutionGasGrant);
            Assert.True(config.IntrinsicGasRules.CalculateIntrinsicGas(
                callData, isContractCreation: false, accessList: null, isSelfTransfer: false, hasValue: false) > 0);
        }

        [Fact]
        public async Task Given_APragueSystemCall_When_Metered_Then_TheBudgetIsThirtyMillionWithNoStateReservoir()
        {
            var config = Prague();
            var node = await PredeployedAsync(StorageSets(1));
            var ctx = SystemCall(new ExecutionStateService(node));

            var (resultCtx, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(0, resultCtx.IntrinsicExecutionGas);
            Assert.Equal(GasConstants.SYSTEM_CALL_EXECUTION_GAS, resultCtx.ExecutionGasGrant);
            Assert.Equal(0, resultCtx.StateGasReservoir);
        }


        private static Task<Program> BuildGuestProgramAsync(HardforkConfig config)
        {
            var block = new BlockWitnessData
            {
                BlockNumber = 1,
                Timestamp = 1704067200,
                BlockGasLimit = 36_000_000,
                ChainId = 1,
                Coinbase = PredeployAddress
            };
            var executionState = new ExecutionStateService(new EIP7702TestNodeDataService());
            return SystemCallExecution.BuildProgramAsync(
                block, executionState, config, PredeployAddress, new byte[0], StorageSets(1).HexToByteArray());
        }

        [Fact]
        public async Task Given_ASystemCallAtAForkThatEnforcesTheStipendSentry_When_TheGuestBuildsIt_Then_TheSentryIsEnforced()
        {
            var program = await BuildGuestProgramAsync(Amsterdam());

            Assert.True(program.ProgramContext.EnforceSstoreGasStipend);
        }

        [Fact]
        public async Task Given_ASystemCallAtAForkBeforeTheSentry_When_TheGuestBuildsIt_Then_ItIsNotEnforced()
        {
            var beforeTheSentry = HardforkConfig.Byzantium
                .WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());

            var program = await BuildGuestProgramAsync(beforeTheSentry);

            Assert.False(program.ProgramContext.EnforceSstoreGasStipend);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Given_AnyFork_When_TheGuestBuildsASystemCall_Then_TheSentryMatchesTheForkConfig(bool sentryActive)
        {
            var config = sentryActive ? Amsterdam() : HardforkConfig.Byzantium
                .WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());

            var program = await BuildGuestProgramAsync(config);

            Assert.Equal(config.EnforceSstoreGasStipend, program.ProgramContext.EnforceSstoreGasStipend);
        }

        [Fact]
        public async Task Given_AnAmsterdamSystemCallProgram_When_Built_Then_ItCarriesTheExecutionGrantAndTheStateReservoir()
        {
            var program = await BuildGuestProgramAsync(Amsterdam());

            Assert.Equal(SystemCallGasBudget.Eip8037.ExecutionGas, program.ProgramContext.Gas);
            Assert.True(program.ProgramContext.StateGasActive);
            Assert.Equal(SystemCallGasBudget.Eip8037.StateGasReservoir, program.StateGasLeft);
            Assert.Equal(SystemCallGasBudget.Eip8037.StateGasReservoir, program.StateGasBaseline);
        }

        [Fact]
        public async Task Given_APragueSystemCallProgram_When_Built_Then_ItCarriesNoStateReservoir()
        {
            var program = await BuildGuestProgramAsync(Prague());

            Assert.Equal(GasConstants.SYSTEM_CALL_EXECUTION_GAS, program.ProgramContext.Gas);
            Assert.False(program.ProgramContext.StateGasActive);
            Assert.Equal(0, program.StateGasLeft);
            Assert.Equal(0, program.StateGasBaseline);
        }
    }
}
