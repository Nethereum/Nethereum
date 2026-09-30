using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    /// <summary>
    /// AMS-8037-06 of <c>docs/internal/glamsterdam-traceability-matrix.md</c>:
    /// an out-of-gas halt in a state-charging opcode must never leave the
    /// reservoir LARGER than the level the frame entered with.
    ///
    /// <para>
    /// EIP-8037 §Transaction-level gas accounting (reservoir model):
    /// <i>"If the operation is unsuccessful before entering the call frame
    /// (e.g., due to insufficient balance or due to the stack depth), or if
    /// the child frame reverts or halts exceptionally, the charged
    /// state-gas is refilled in LIFO order and <c>evm_state_gas_used</c>
    /// decreases by the same amount."</i> LIFO is what bounds the refill:
    /// a charge that spilled into execution gas must be repaid to execution
    /// gas, so a frame whose reservoir was exhausted hands back exactly the
    /// reservoir it was granted and not a penny more.
    /// </para>
    ///
    /// <para>
    /// Every case below runs the transaction with a gas limit BELOW
    /// <c>EIP8037_TX_MAX_GAS_LIMIT</c>, so the reservoir is zero and every
    /// state charge spills into execution gas. That is the shape in which
    /// a refill sent to the wrong pool is visible: it turns a zero
    /// reservoir into a positive one, and the transaction settles with
    /// NEGATIVE state gas — the condition
    /// <c>TransactionExecutor</c>'s <c>Math.Max(0L, ctx.StateGasUsed)</c>
    /// clamp exists to absorb, which is why the assertions read
    /// <c>ctx</c> rather than the clamped <c>result.StateGasUsed</c>.
    /// </para>
    ///
    /// <para>
    /// Each opcode is stated twice: the halting case, and the twin that
    /// completes and proves the charge is genuinely made — without it a
    /// build where the opcode charges nothing at all would satisfy every
    /// non-inflation assertion here.
    /// </para>
    /// </summary>
    public class Eip8037ReservoirInflationTests
    {
        private const string SenderAddress = "0x1111111111111111111111111111111111111111";
        private const string CallerAddress = "0x2222222222222222222222222222222222222222";
        private const string ChildAddress = "0x3333333333333333333333333333333333333333";
        private const string FreshRecipientAddress = "0x7777777777777777777777777777777777777777";
        private const string DeadBeneficiaryAddress = "0x8888888888888888888888888888888888888888";

        private const long TransactionGasLimit = 10_000_000;

        private const long AmpleChildStipend = 1_000_000;

        private static readonly long SelfDestructExecutionCost =
            GasConstants.SELFDESTRUCT_COST
            + GasConstants.EIP8038_COLD_ACCOUNT_ACCESS
            + GasConstants.EIP8038_ACCOUNT_WRITE;

        private static readonly long SelfDestructStipendOneGasShort =
            PushCost + SelfDestructExecutionCost + GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS - 1;

        private const long PushCost = 3;

        private static byte[] Concat(params byte[][] parts)
        {
            var bytes = new List<byte>();
            foreach (var part in parts) bytes.AddRange(part);
            return bytes.ToArray();
        }

        private static byte[] Push(long value)
        {
            var hex = value.ToString("x");
            if (hex.Length % 2 != 0) hex = "0" + hex;
            var operand = hex.HexToByteArray();
            var code = new List<byte> { (byte)(0x60 + operand.Length - 1) };
            code.AddRange(operand);
            return code.ToArray();
        }

        private static byte[] Push20(string address)
        {
            var code = new List<byte> { 0x73 };
            code.AddRange(address.HexToByteArray());
            return code.ToArray();
        }

        private static byte[] CallWithStipend(string target, long stipend) => Concat(
            Push(0), Push(0), Push(0), Push(0), Push(0),
            Push20(target),
            Push(stipend),
            new byte[] { 0xF1, 0x50, 0x00 });

        private static readonly byte[] MemoryExpansionBeyondAnyStipend = Concat(
            Push(0), Push(0x1E0000), new byte[] { 0x52 });

        private static readonly byte[] Stop = new byte[] { 0x00 };

        private static readonly byte[] SstoreFirstTimeSet =
            Concat(Push(5), Push(1), new byte[] { 0x55 });

        private static readonly byte[] ValueCallCreatingTheRecipient = Concat(
            Push(0), Push(0), Push(0), Push(0),
            Push(1),
            Push20(FreshRecipientAddress),
            Push(0),
            new byte[] { 0xF1, 0x50 });

        private static readonly byte[] CreateEmptyContract =
            Concat(Push(0), Push(0), Push(0), new byte[] { 0xF0, 0x50 });

        private static readonly byte[] SelfDestructToDeadBeneficiary =
            Concat(Push20(DeadBeneficiaryAddress), new byte[] { 0xFF });

        private static async Task<(TransactionExecutionContext ctx, TransactionExecutionResult result)> RunCallerAsync(
            byte[] childCode, long childStipend, BigInteger childBalance)
        {
            var node = new EIP7702TestNodeDataService();
            await node.SetBalanceAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(CallerAddress, CallWithStipend(ChildAddress, childStipend));
            await node.SetCodeAsync(ChildAddress, childCode);
            await node.SetBalanceAsync(ChildAddress, childBalance);

            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = CallerAddress,
                Data = null,
                IsContractCreation = false,
                GasLimit = TransactionGasLimit,
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

            var executor = new TransactionExecutor(
                HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase()));
            var result = await executor.ExecuteAsync(ctx);
            return (ctx, result);
        }

        private static void AssertNoReservoirInflation(TransactionExecutionContext ctx, TransactionExecutionResult result)
        {
            Assert.True(result.Success, result.Error);
            Assert.Equal(0, ctx.StateGasReservoir);
            Assert.True(ctx.StateGas.ReservoirRemaining <= ctx.StateGasReservoir,
                $"the halted frame handed back a reservoir of {ctx.StateGas.ReservoirRemaining} against the {ctx.StateGasReservoir} it was granted — a state-gas refill reached the reservoir instead of repaying the execution gas it spilled from");
            Assert.Equal(0, ctx.StateGasUsed);
        }

        private static void AssertChargeIsConsumed(
            TransactionExecutionContext ctx, TransactionExecutionResult result, long expectedStateGas)
        {
            Assert.True(result.Success, result.Error);
            Assert.Equal(expectedStateGas, ctx.StateGasUsed);
            Assert.Equal(expectedStateGas, ctx.StateGas.SpilledIntoExecution);
        }

        [Fact]
        public async Task Given_SstoreChargedStateGasBeforeTheFrameRanOutOfGas_When_Settled_Then_NoReservoirInflation()
        {
            var (ctx, result) = await RunCallerAsync(
                Concat(SstoreFirstTimeSet, MemoryExpansionBeyondAnyStipend),
                AmpleChildStipend, childBalance: 0);

            AssertNoReservoirInflation(ctx, result);
        }

        [Fact]
        public async Task Given_SstoreInAFrameWithGasToSpare_When_Settled_Then_TheStorageSetChargeIsConsumed()
        {
            var (ctx, result) = await RunCallerAsync(
                Concat(SstoreFirstTimeSet, Stop),
                AmpleChildStipend, childBalance: 0);

            AssertChargeIsConsumed(ctx, result, GasConstants.EIP8037_STORAGE_SET_STATE_GAS);
        }

        [Fact]
        public async Task Given_CallChargedStateGasBeforeTheFrameRanOutOfGas_When_Settled_Then_NoReservoirInflation()
        {
            var (ctx, result) = await RunCallerAsync(
                Concat(ValueCallCreatingTheRecipient, MemoryExpansionBeyondAnyStipend),
                AmpleChildStipend, childBalance: 1);

            AssertNoReservoirInflation(ctx, result);
        }

        [Fact]
        public async Task Given_CallInAFrameWithGasToSpare_When_Settled_Then_TheNewAccountChargeIsConsumed()
        {
            var (ctx, result) = await RunCallerAsync(
                Concat(ValueCallCreatingTheRecipient, Stop),
                AmpleChildStipend, childBalance: 1);

            AssertChargeIsConsumed(ctx, result, GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS);
        }

        [Fact]
        public async Task Given_SelfdestructRanOutOfGasOnItsOwnCharge_When_Settled_Then_NoReservoirInflation()
        {
            var (ctx, result) = await RunCallerAsync(
                SelfDestructToDeadBeneficiary,
                SelfDestructStipendOneGasShort, childBalance: 1);

            AssertNoReservoirInflation(ctx, result);
        }

        [Fact]
        public async Task Given_SelfdestructInAFrameWithGasToSpare_When_Settled_Then_TheNewAccountChargeIsConsumed()
        {
            var (ctx, result) = await RunCallerAsync(
                SelfDestructToDeadBeneficiary,
                AmpleChildStipend, childBalance: 1);

            AssertChargeIsConsumed(ctx, result, GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS);
        }

        [Fact]
        public async Task Given_CreateChargedStateGasBeforeTheFrameRanOutOfGas_When_Settled_Then_NoReservoirInflation()
        {
            var (ctx, result) = await RunCallerAsync(
                Concat(CreateEmptyContract, MemoryExpansionBeyondAnyStipend),
                AmpleChildStipend, childBalance: 0);

            AssertNoReservoirInflation(ctx, result);
        }

        [Fact]
        public async Task Given_CreateInAFrameWithGasToSpare_When_Settled_Then_TheNewAccountChargeIsConsumed()
        {
            var (ctx, result) = await RunCallerAsync(
                Concat(CreateEmptyContract, Stop),
                AmpleChildStipend, childBalance: 0);

            AssertChargeIsConsumed(ctx, result, GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS);
        }
    }
}
