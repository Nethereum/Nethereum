using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    /// <summary>
    /// EIP-3529 §Specification, verbatim: <i>"the maximum gas refunded is changed to
    /// <c>gas_used // MAX_REFUND_QUOTIENT</c>"</i>, with <i>"<c>MAX_REFUND_QUOTIENT</c>
    /// … 5"</i>.
    ///
    /// <para>The ceiling is a SETTLEMENT rule, not a frame rule. Refunds accrue uncapped
    /// into the program's refund counter as storage is cleared; the clip happens once, at
    /// <c>TransactionExecutor.ApplyGasRefunds</c>, against the divisor the resolved fork
    /// carries — 2 before London, 5 after. Anything that clips inside a frame, or against a
    /// hard-coded 5, is asserting neither the site nor the fork-dependence.</para>
    ///
    /// <para>Which makes the discriminating observation the pair below: the same clearing
    /// SSTORE, earning the same refund, settled once where the ceiling binds and once where
    /// it does not. A settlement that never clipped would pass the second alone; one that
    /// always clipped would pass the first alone.</para>
    /// </summary>
    public class Eip3529RefundCeilingTests
    {
        private const string SenderAddress = "0x1111111111111111111111111111111111111111";
        private const string ContractAddress = "0x2222222222222222222222222222222222222222";

        private const long RefundStorageClear = GasConstants.EIP8038_REFUND_STORAGE_CLEAR;

        private static readonly byte[] ClearsSlotZero = { 0x60, 0x00, 0x60, 0x00, 0x55, 0x00 };

        private static readonly byte[] ClearsSlotZeroAfterBurningGas =
            { 0x60, 0x00, 0x62, 0x02, 0x00, 0x00, 0x52, 0x60, 0x00, 0x60, 0x00, 0x55, 0x00 };

        private static byte[] NonZeroWord(byte b)
        {
            var word = new byte[32];
            word[31] = b;
            return word;
        }

        private static HardforkConfig AmsterdamConfig() =>
            HardforkConfig.Amsterdam.WithPrecompiles(
                Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());

        private static async Task<TransactionExecutionResult> RunAsync(byte[] contractCode)
        {
            var node = new EIP7702TestNodeDataService();
            await node.SetBalanceAsync(SenderAddress, BigInteger.Parse("1000000000000000000"));
            await node.SetCodeAsync(ContractAddress, contractCode);
            await node.SetBalanceAsync(ContractAddress, 1);
            await node.SetStorageAsync(ContractAddress, 0, NonZeroWord(0x2a));

            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = ContractAddress,
                Data = new byte[0],
                IsContractCreation = false,
                GasLimit = 1_000_000,
                Value = 0,
                GasPrice = 1,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 0,
                ChainId = 1,
                Coinbase = SenderAddress,
                ExecutionState = new ExecutionStateService(node)
            };

            return await new TransactionExecutor(AmsterdamConfig()).ExecuteAsync(ctx);
        }

        private static long PreRefundTotal(TransactionExecutionResult result) =>
            result.GasUsed + result.GasRefund;

        [Fact]
        public async Task Given_ARefundExceedingOneFifthOfTheGasUsed_When_Settled_Then_OnlyTheCeilingIsCredited()
        {
            var result = await RunAsync(ClearsSlotZero);

            Assert.True(result.Success, result.Error);
            Assert.True(result.GasRefund < RefundStorageClear,
                $"the ceiling must bind for this transaction, but the whole {RefundStorageClear} refund was credited");
            Assert.Equal(PreRefundTotal(result) / AmsterdamConfig().RefundQuotient, result.GasRefund);
        }

        [Fact]
        public async Task Given_ARefundBelowOneFifthOfTheGasUsed_When_Settled_Then_TheWholeRefundIsCredited()
        {
            var result = await RunAsync(ClearsSlotZeroAfterBurningGas);

            Assert.True(result.Success, result.Error);
            Assert.True(RefundStorageClear < PreRefundTotal(result) / AmsterdamConfig().RefundQuotient,
                "the ceiling must NOT bind for this transaction, or the twin proves nothing");
            Assert.Equal(RefundStorageClear, result.GasRefund);
        }
    }
}
