using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas;
using Nethereum.EVM.UnitTests;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class Eip8037ValueTransferToNonAliveAccountTests
    {
        private const string SenderAddress = "0x1111111111111111111111111111111111111111";
        private const string FreshTargetAddress = "0x3333333333333333333333333333333333333333";

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

        private static TransactionExecutionContext TransferCtx(
            ExecutionStateService executionState, string to, long value, long gasLimit) => new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = to,
                Data = null,
                IsContractCreation = false,
                GasLimit = gasLimit,
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
        public async Task Given_ValueTransfer_AtAmsterdam_ToFreshAddress_WithLargeGasLimit_Then_NewAccountChargedFromReservoir()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);
            var ctx = TransferCtx(executionState, FreshTargetAddress, value: 1000, gasLimit: 100_000_000);

            var (resultCtx, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS, result.StateGasUsed);
            Assert.Equal(GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS, resultCtx.StateGas.FromReservoir);
            Assert.Equal(0, resultCtx.StateGas.SpilledIntoExecution);

            var targetBalance = await executionState.GetTotalBalanceAsync(FreshTargetAddress);
            Assert.Equal(new Nethereum.Util.EvmUInt256(1000), targetBalance);
        }

        [Fact]
        public async Task Given_ZeroValueCall_AtAmsterdam_ToFreshAddress_Then_NotCharged()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);
            var ctx = TransferCtx(executionState, FreshTargetAddress, value: 0, gasLimit: 100_000_000);

            var (_, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(0, result.StateGasUsed);
        }

        [Fact]
        public async Task Given_ValueTransfer_AtAmsterdam_ToAlreadyAliveAddress_Then_NotCharged()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            const string aliveTarget = "0x4444444444444444444444444444444444444444";
            await node.SetBalanceAsync(aliveTarget, 1);
            var executionState = new ExecutionStateService(node);
            var ctx = TransferCtx(executionState, aliveTarget, value: 1000, gasLimit: 100_000_000);

            var (_, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(0, result.StateGasUsed);
        }

        [Fact]
        public async Task Given_ValueTransfer_AtOsaka_ToFreshAddress_Then_NotCharged_NoCrossForkLeak()
        {
            var config = HardforkConfig.Osaka.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);
            var ctx = TransferCtx(executionState, FreshTargetAddress, value: 1000, gasLimit: 500_000);

            var (_, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(0, result.StateGasUsed);

            var targetBalance = await executionState.GetTotalBalanceAsync(FreshTargetAddress);
            Assert.Equal(new Nethereum.Util.EvmUInt256(1000), targetBalance);
        }
    }
}
