using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    /// <summary>
    /// EIP-7778 splits two numbers that were previously one. The SENDER is billed the
    /// post-refund amount, exactly as before; the BLOCK counts the pre-refund amount.
    /// EIP-7778 §Gas Accounting Changes, verbatim: <i>"Block gas accounting
    /// becomes: <c>block.gas_used += max(tx_gas_used, calldata_floor_gas_cost)</c>"</i>,
    /// where <c>tx_gas_used</c> is the total before refunds are applied.
    ///
    /// <para>The point of the change is that refunds must no longer buy block space: a
    /// transaction that clears storage used the block's capacity to do the work,
    /// whatever it later gets back. Before this, one refunding transaction let more
    /// work into a block than its gas limit allowed.</para>
    ///
    /// <para>Which makes the discriminating observation a GAP rather than a value:
    /// after a refund the two figures must DIFFER, and differ by exactly the refund.
    /// A settlement that computed block accounting from the post-refund total would
    /// report them equal, and would still produce a perfectly plausible receipt.</para>
    ///
    /// <para>Matrix AMS-7778-02. Settlement site
    /// <c>TransactionExecutor.RecordSettledGas</c>, checked against EELS
    /// forks/amsterdam/fork.py.</para>
    /// </summary>
    public class Eip7778RefundsExcludedFromBlockAccountingTests
    {
        private const string SenderAddress = "0x1111111111111111111111111111111111111111";
        private const string ContractAddress = "0x2222222222222222222222222222222222222222";

        private const long RefundStorageClear = 11_616;

        private static readonly byte[] BurnsEnoughGasThatTheRefundCeilingDoesNotBind =
            { 0x60, 0x00, 0x62, 0x02, 0x00, 0x00, 0x52 };

        private static readonly byte[] ClearsSlotZero = Prefixed(0x60, 0x00, 0x60, 0x00, 0x55, 0x00);

        private static readonly byte[] OverwritesSlotZero = Prefixed(0x60, 0x07, 0x60, 0x00, 0x55, 0x00);

        private static byte[] Prefixed(params byte[] tail)
        {
            var code = new byte[BurnsEnoughGasThatTheRefundCeilingDoesNotBind.Length + tail.Length];
            BurnsEnoughGasThatTheRefundCeilingDoesNotBind.CopyTo(code, 0);
            tail.CopyTo(code, BurnsEnoughGasThatTheRefundCeilingDoesNotBind.Length);
            return code;
        }

        private static byte[] NonZeroWord(byte b)
        {
            var word = new byte[32];
            word[31] = b;
            return word;
        }

        private static async Task<TransactionExecutionResult> RunAsync(byte[] contractCode)
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(
                Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());

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

            return await new TransactionExecutor(config).ExecuteAsync(ctx);
        }

        [Fact]
        public async Task Given_ATransactionThatClearsStorage_When_Settled_Then_TheBlockCountsThePreRefundGasWhileTheSenderIsBilledLess()
        {
            var result = await RunAsync(ClearsSlotZero);

            Assert.True(result.Success, result.Error);

            Assert.Equal(RefundStorageClear, result.ExecutionGasUsed - result.GasUsed);
        }

        [Fact]
        public async Task Given_ATransactionThatEarnsNoRefund_When_Settled_Then_TheBlockFigureAndTheSenderBillAgree()
        {
            var result = await RunAsync(OverwritesSlotZero);

            Assert.True(result.Success, result.Error);
            Assert.Equal(result.GasUsed, result.ExecutionGasUsed);
        }

        [Fact]
        public async Task Given_ATransactionThatClearsStorage_When_Settled_Then_TheSenderStillPaysLessThanOneThatDoesNot()
        {
            var cleared = await RunAsync(ClearsSlotZero);
            var overwritten = await RunAsync(OverwritesSlotZero);

            Assert.True(cleared.Success, cleared.Error);
            Assert.True(overwritten.Success, overwritten.Error);
            Assert.True(cleared.GasUsed < overwritten.GasUsed,
                $"clearing billed {cleared.GasUsed}, overwriting billed {overwritten.GasUsed}");
        }

        [Fact]
        public async Task Given_ATransactionThatClearsStorage_When_Settled_Then_TheReceiptFigureIsThePostRefundBill()
        {
            var result = await RunAsync(ClearsSlotZero);

            Assert.True(result.Success, result.Error);
            Assert.Equal(result.GasUsed, result.EffectiveGasUsed);
        }
    }
}
