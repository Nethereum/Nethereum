using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.UnitTests;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    /// <summary>
    /// The Amsterdam calldata floor as <see cref="TransactionExecutor"/>
    /// actually wires it at settlement. <see cref="IntrinsicGasRulesTests"/>
    /// calls <c>IntrinsicGasRules</c> directly and therefore cannot see which
    /// anchor the executor asks for; that is the gap these two tests close.
    ///
    /// <para>EIP-2780 §Interactions with other EIPs: <i>"With this EIP active, that base
    /// term is replaced by the transaction's decomposed base — <c>TX_BASE_COST</c> plus the
    /// applicable recipient and value primitives (<c>COLD_ACCOUNT_ACCESS</c> and
    /// <c>TX_VALUE_COST</c> for a value transfer to a different account, or
    /// <c>CREATE_ACCESS</c> for contract creation)."</i></para>
    ///
    /// <para>The two shapes consequently disagree by
    /// <c>CREATE_ACCESS - (COLD_ACCOUNT_ACCESS + TX_VALUE_COST)</c> = 3,000 gas
    /// on byte-identical calldata, and the pair below pins each half against its
    /// own hand-derived total. A settlement that re-derives the anchor blind to
    /// contract creation reads the call shape's answer for both, which the
    /// creation test alone catches and the call test alone would ratify.</para>
    ///
    /// <para>Matrix AMS-2780-05. Checked against EELS
    /// forks/amsterdam/transactions.py <c>calculate_intrinsic_cost</c> and
    /// forks/amsterdam/vm/gas.py <c>settle_transaction_gas</c>.</para>
    /// </summary>
    public class IntrinsicGasFinalisationTests
    {
        private const string SenderAddress = "0x1111111111111111111111111111111111111111";
        private const string AliveRecipientAddress = "0x2222222222222222222222222222222222222222";

        private const int CalldataBytes = 4096;

        private const long GasLimit = 500_000;
        private const long EndowmentWei = 1000;

        private static async Task<(TransactionExecutionContext ctx, TransactionExecutionResult result)> RunAsync(
            string to, bool isContractCreation)
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(
                Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());

            var node = new EIP7702TestNodeDataService();
            await node.SetBalanceAsync(SenderAddress, BigInteger.Parse("1000000000000000000"));
            await node.SetBalanceAsync(AliveRecipientAddress, 1);

            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = to,
                Data = new byte[CalldataBytes],
                IsContractCreation = isContractCreation,
                GasLimit = GasLimit,
                Value = EndowmentWei,
                GasPrice = 1,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 0,
                ChainId = 1,
                Coinbase = SenderAddress,
                ExecutionState = new ExecutionStateService(node)
            };

            var result = await new TransactionExecutor(config).ExecuteAsync(ctx);
            return (ctx, result);
        }

        [Fact]
        public async Task Given_ValueBearingContractCreation_When_FloorBindsAtFinalisation_Then_AnchorsOnCreateAccess_NotColdAccessOrValueCost()
        {
            var (ctx, result) = await RunAsync(to: "", isContractCreation: true);

            Assert.True(result.Success, result.Error);
            Assert.Equal(286_144L, ctx.FloorGas);
            Assert.Equal(286_144L, result.GasUsed);
            Assert.Equal(286_144L, result.ExecutionGasUsed);
            Assert.Equal(183_600L, result.StateGasUsed);
        }

        [Fact]
        public async Task Given_ValueBearingCallToAliveRecipient_When_FloorBindsAtFinalisation_Then_AnchorsOnColdAccessAndValueCost_NotCreateAccess()
        {
            var (ctx, result) = await RunAsync(to: AliveRecipientAddress, isContractCreation: false);

            Assert.True(result.Success, result.Error);
            Assert.Equal(283_144L, ctx.FloorGas);
            Assert.Equal(283_144L, result.GasUsed);
            Assert.Equal(283_144L, result.ExecutionGasUsed);
            Assert.Equal(0L, result.StateGasUsed);
        }
    }
}
