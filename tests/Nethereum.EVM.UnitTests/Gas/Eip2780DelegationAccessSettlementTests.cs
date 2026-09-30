using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas;
using Nethereum.EVM.UnitTests;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class Eip2780DelegationAccessSettlementTests
    {
        private const string SenderAddress = "0x1111111111111111111111111111111111111111";
        private const string RecipientAddress = "0x3333333333333333333333333333333333333333";
        private const string CoinbaseAddress = "0x5555555555555555555555555555555555555555";

        private static byte[] DelegationCode(string delegate_)
        {
            var code = new byte[23];
            code[0] = 0xef; code[1] = 0x01; code[2] = 0x00;
            delegate_.HexToByteArray().CopyTo(code, 3);
            return code;
        }

        private static async Task<EIP7702TestNodeDataService> NodeAsync()
        {
            var node = new EIP7702TestNodeDataService();
            await node.SetBalanceAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetNonceAsync(RecipientAddress, 1);
            return node;
        }

        private static TransactionExecutionContext TransferCtx(ExecutionStateService executionState) => new TransactionExecutionContext
        {
            Sender = SenderAddress,
            To = RecipientAddress,
            Data = null,
            IsContractCreation = false,
            GasLimit = 100_000,
            Value = 1,
            GasPrice = 1,
            Nonce = 0,
            BlockNumber = 1,
            Timestamp = 1704067200,
            BaseFee = 0,
            ChainId = 1,
            Coinbase = CoinbaseAddress,
            ExecutionState = executionState
        };

        private static async Task<(TransactionExecutionContext ctx, TransactionExecutionResult result)> RunAsync(
            HardforkConfig config, TransactionExecutionContext ctx)
        {
            var executor = new TransactionExecutor(config);
            var result = await executor.ExecuteAsync(ctx);
            return (ctx, result);
        }

        [Fact]
        public async Task Given_ARecipientWithAWarmDelegationTarget_When_APlainValueTransferIsSettled_Then_TheDelegationAccessChargeIsBilled()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());

            var delegatedNode = await NodeAsync();
            await delegatedNode.SetCodeAsync(RecipientAddress, DelegationCode(SenderAddress));
            var (delegatedCtx, delegatedResult) = await RunAsync(config, TransferCtx(new ExecutionStateService(delegatedNode)));

            var plainNode = await NodeAsync();
            var (_, plainResult) = await RunAsync(config, TransferCtx(new ExecutionStateService(plainNode)));

            Assert.True(delegatedResult.Success, delegatedResult.Error);
            Assert.True(plainResult.Success, plainResult.Error);

            Assert.Equal(GasConstants.WARM_STORAGE_READ_COST, delegatedCtx.PreDispatchExecutionGasCharged);
            Assert.Equal(GasConstants.WARM_STORAGE_READ_COST, delegatedResult.GasUsed - plainResult.GasUsed);
        }

        [Fact]
        public async Task Given_ARecipientWithAWarmDelegationTarget_When_APlainValueTransferIsSettled_Then_TheCoinbaseIsPaidTheSameCharge()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());

            var delegatedNode = await NodeAsync();
            await delegatedNode.SetCodeAsync(RecipientAddress, DelegationCode(SenderAddress));
            var delegatedState = new ExecutionStateService(delegatedNode);
            var (_, delegatedResult) = await RunAsync(config, TransferCtx(delegatedState));
            var delegatedCoinbaseBalance = await delegatedState.GetTotalBalanceAsync(CoinbaseAddress);

            var plainNode = await NodeAsync();
            var plainState = new ExecutionStateService(plainNode);
            var (_, plainResult) = await RunAsync(config, TransferCtx(plainState));
            var plainCoinbaseBalance = await plainState.GetTotalBalanceAsync(CoinbaseAddress);

            Assert.True(delegatedResult.Success, delegatedResult.Error);
            Assert.True(plainResult.Success, plainResult.Error);

            var coinbaseDelta = delegatedCoinbaseBalance.ToBigInteger() - plainCoinbaseBalance.ToBigInteger();
            Assert.Equal(new BigInteger(GasConstants.WARM_STORAGE_READ_COST), coinbaseDelta);

            var gasDelta = delegatedResult.GasUsed - plainResult.GasUsed;
            Assert.Equal(gasDelta, (long)coinbaseDelta);
        }

        [Fact]
        public async Task Given_APlainValueTransferWithNoDelegation_When_Settled_Then_GasUsedIsUnchanged()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());

            var node = await NodeAsync();
            var (ctx, result) = await RunAsync(config, TransferCtx(new ExecutionStateService(node)));

            Assert.True(result.Success, result.Error);
            Assert.Equal(0, ctx.PreDispatchExecutionGasCharged);
            Assert.Equal(0, ctx.StateGas.SpilledIntoExecution);
            Assert.Equal(ctx.IntrinsicExecutionGas, result.GasUsed);
        }

        // that runs is the designator itself. EIP-7702 §Set Code
        // 0xef0100 || address". EIP-3541 reserves that leading byte:
        // "the 0xEF byte ... is not a valid opcode", so the frame halts

        private const string StopContractAddress = "0x7777777777777777777777777777777777777777";

        private static async Task<(TransactionExecutionContext ctx, TransactionExecutionResult result)> RunDelegatedRecipientAsync(
            string delegateAddress)
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await NodeAsync();
            await node.SetCodeAsync(RecipientAddress, DelegationCode(delegateAddress));
            await node.SetCodeAsync(StopContractAddress, new byte[] { 0x00 });

            return await RunAsync(config, TransferCtx(new ExecutionStateService(node)));
        }

        [Fact]
        public async Task Given_ARecipientDelegatedToItself_When_Dispatched_Then_TheDesignatorsLeadingEfByteHaltsTheFrame()
        {
            var (ctx, result) = await RunDelegatedRecipientAsync(RecipientAddress);

            Assert.Equal(RecipientAddress, ctx.DelegateAddress);
            Assert.False(result.Success, "a self-delegated recipient dispatches into 0xef0100 || itself, which is not executable");
            Assert.Equal(ctx.GasLimit.ToLongSafe(), result.GasUsed);
        }

        [Fact]
        public async Task Given_ARecipientDelegatedToAnExecutableContract_When_Dispatched_Then_ItRunsAndKeepsItsRemainingGas()
        {
            var (ctx, result) = await RunDelegatedRecipientAsync(StopContractAddress);

            Assert.Equal(StopContractAddress, ctx.DelegateAddress);
            Assert.True(result.Success, result.Error);
            Assert.True(result.GasUsed < ctx.GasLimit.ToLongSafe(),
                "an executable delegate must leave gas over — a total forfeit here would mean the halt is not caused by the self-reference");
        }
    }
}
