using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas;
using Nethereum.EVM.UnitTests;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class Eip8037EmptyInitCodeNewAccountTests
    {
        private const string SenderAddress = "0x1111111111111111111111111111111111111111";
        private const string FunderAddress = "0x2222222222222222222222222222222222222222";

        private static async Task<(TransactionExecutionContext ctx, TransactionExecutionResult result)> RunAsync(
            HardforkConfig config, TransactionExecutionContext ctx)
        {
            var executor = new TransactionExecutor(config);
            var result = await executor.ExecuteAsync(ctx);
            return (ctx, result);
        }

        private static async Task<EIP7702TestNodeDataService> FundedNodeAsync(string address, BigInteger balance)
        {
            var node = new EIP7702TestNodeDataService();
            await node.SetBalanceAsync(address, balance);
            return node;
        }

        private static TransactionExecutionContext CreationCtx(
            ExecutionStateService executionState, long value) => new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = "",
                Data = new byte[0],
                IsContractCreation = true,
                GasLimit = 500_000,
                Value = value,
                GasPrice = 1,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 0,
                ChainId = 1,
                Coinbase = SenderAddress,
                ExecutionState = executionState
            };

        [Fact]
        public async Task Given_EmptyInitCode_AtAmsterdam_OntoVirginTarget_WithZeroValue_Then_NewAccountChargedOnce()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);
            var ctx = CreationCtx(executionState, value: 0);

            var (_, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS, result.StateGasUsed);
        }

        [Fact]
        public async Task Given_EmptyInitCode_AtAmsterdam_OntoVirginTarget_WithNonZeroValue_Then_NewAccountChargedOnce()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);
            var ctx = CreationCtx(executionState, value: 1);

            var (_, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS, result.StateGasUsed);

            var targetBalance = await executionState.GetTotalBalanceAsync(ctx.ContractAddress);
            Assert.Equal(new EvmUInt256(1), targetBalance);
        }

        [Fact]
        public async Task Given_EmptyInitCode_AtAmsterdam_OntoAlreadyAliveTarget_WithZeroValue_Then_NotCharged()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetBalanceAsync(FunderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);

            var targetAddress = Nethereum.Util.ContractUtils.CalculateContractAddress(SenderAddress, 0);

            var fundingCtx = new TransactionExecutionContext
            {
                Sender = FunderAddress,
                To = targetAddress,
                Data = null,
                IsContractCreation = false,
                GasLimit = 300_000,
                Value = 1,
                GasPrice = 1,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 0,
                ChainId = 1,
                Coinbase = SenderAddress,
                ExecutionState = executionState
            };
            var executor = new TransactionExecutor(config);
            var fundingResult = await executor.ExecuteAsync(fundingCtx);
            Assert.True(fundingResult.Success, fundingResult.Error);

            var ctx = CreationCtx(executionState, value: 0);
            var (_, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(0, result.StateGasUsed);
        }

        [Fact]
        public async Task Given_NonEmptyInitCode_AtAmsterdam_OntoVirginTarget_Then_ChargedExactlyOnce_NotDoubleCharged()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);
            var ctx = CreationCtx(executionState, value: 0);
            ctx.Data = new byte[] { 0x00 };

            var (_, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS, result.StateGasUsed);
        }

        [Fact]
        public async Task Given_EmptyInitCode_AtOsaka_OntoVirginTarget_Then_NotCharged_NoCrossForkLeak()
        {
            var config = HardforkConfig.Osaka.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);
            var ctx = CreationCtx(executionState, value: 0);

            var (_, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(0, result.StateGasUsed);
        }
    }
}
