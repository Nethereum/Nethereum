using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution.TransferLogs.Rules;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class Eip7708EthTransferLogTests
    {
        private const string SenderAddress = "0x1111111111111111111111111111111111111111";
        private const string RecipientAddress = "0x2222222222222222222222222222222222222222";
        private const string CallerContractAddress = "0x3333333333333333333333333333333333333333";
        private const string BeneficiaryAddress = "0x4444444444444444444444444444444444444444";
        private const string PreExistingContractAddress = "0x5555555555555555555555555555555555555555";
        private const string RevertingContractAddress = "0x6666666666666666666666666666666666666666";
        private const string DelegateTargetAddress = "0x7777777777777777777777777777777777777777";
        private const string CoinbaseAddress = "0x8888888888888888888888888888888888888888";

        private static byte[] SelfDestructToSelfInitCode() => new byte[]
        {
            (byte)Instruction.ADDRESS,
            (byte)Instruction.SELFDESTRUCT
        };

        private static byte[] SelfDestructToBeneficiaryCode(string beneficiaryAddress)
        {
            var code = new List<byte> { (byte)Instruction.PUSH20 };
            code.AddRange(beneficiaryAddress.HexToByteArray());
            code.Add((byte)Instruction.SELFDESTRUCT);
            return code.ToArray();
        }

        private static byte[] CallWithValueCode(string recipient, byte value)
        {
            var code = new List<byte>
            {
                (byte)Instruction.PUSH1, 0x00,
                (byte)Instruction.PUSH1, 0x00,
                (byte)Instruction.PUSH1, 0x00,
                (byte)Instruction.PUSH1, 0x00,
                (byte)Instruction.PUSH1, value
            };
            code.Add((byte)Instruction.PUSH20);
            code.AddRange(recipient.HexToByteArray());
            code.Add((byte)Instruction.PUSH2);
            code.Add(0x80);
            code.Add(0x00);
            code.Add((byte)Instruction.CALL);
            code.Add((byte)Instruction.POP);
            code.Add((byte)Instruction.STOP);
            return code.ToArray();
        }

        private static byte[] DelegateCallCode(string target)
        {
            var code = new List<byte>
            {
                (byte)Instruction.PUSH1, 0x00,
                (byte)Instruction.PUSH1, 0x00,
                (byte)Instruction.PUSH1, 0x00,
                (byte)Instruction.PUSH1, 0x00
            };
            code.Add((byte)Instruction.PUSH20);
            code.AddRange(target.HexToByteArray());
            code.Add((byte)Instruction.PUSH2);
            code.Add(0x80);
            code.Add(0x00);
            code.Add((byte)Instruction.DELEGATECALL);
            code.Add((byte)Instruction.POP);
            code.Add((byte)Instruction.STOP);
            return code.ToArray();
        }

        private static byte[] RevertingCode() => new byte[]
        {
            (byte)Instruction.PUSH1, 0x00,
            (byte)Instruction.PUSH1, 0x00,
            (byte)Instruction.REVERT
        };

        private static byte[] DeepCallWithValueCode(string target, byte value)
        {
            var code = new List<byte>
            {
                (byte)Instruction.PUSH1, 0x00,
                (byte)Instruction.PUSH1, 0x00,
                (byte)Instruction.PUSH1, 0x00,
                (byte)Instruction.PUSH1, 0x00,
                (byte)Instruction.PUSH1, value
            };
            code.Add((byte)Instruction.PUSH20);
            code.AddRange(target.HexToByteArray());
            code.Add((byte)Instruction.PUSH3);
            code.Add(0x0F);
            code.Add(0x42);
            code.Add(0x40);
            code.Add((byte)Instruction.CALL);
            code.Add((byte)Instruction.POP);
            code.Add((byte)Instruction.STOP);
            return code.ToArray();
        }

        private static byte[] DeepCallWithValueThenRevertCode(string target, byte value)
        {
            var code = new List<byte>(DeepCallWithValueCode(target, value));
            code.RemoveAt(code.Count - 1);
            code.Add((byte)Instruction.PUSH1);
            code.Add(0x00);
            code.Add((byte)Instruction.PUSH1);
            code.Add(0x00);
            code.Add((byte)Instruction.REVERT);
            return code.ToArray();
        }

        private static byte[] CallWithValueThenRevertCode(string target, byte value)
        {
            var code = new List<byte>(CallWithValueCode(target, value));
            code.RemoveAt(code.Count - 1);
            code.Add((byte)Instruction.PUSH1);
            code.Add(0x00);
            code.Add((byte)Instruction.PUSH1);
            code.Add(0x00);
            code.Add((byte)Instruction.REVERT);
            return code.ToArray();
        }

        private static byte[] TwoCallsWithValueCode(string firstTarget, string secondTarget, byte value)
        {
            var code = new List<byte>(CallWithValueCode(firstTarget, value));
            code.RemoveAt(code.Count - 1);
            code.AddRange(CallWithValueCode(secondTarget, value));
            return code.ToArray();
        }

        private static async Task<EIP7702TestNodeDataService> FundedNodeAsync(string address, BigInteger balance)
        {
            var node = new EIP7702TestNodeDataService();
            await node.SetBalanceAsync(address, balance);
            return node;
        }

        private static async Task<(TransactionExecutionContext ctx, TransactionExecutionResult result)> RunAsync(
            HardforkConfig config, TransactionExecutionContext ctx)
        {
            var executor = new TransactionExecutor(config);
            var result = await executor.ExecuteAsync(ctx);
            return (ctx, result);
        }

        private static TransactionExecutionContext CreationCtx(
            ExecutionStateService executionState, byte[] initCode, long value, long gasLimit = 2_000_000) =>
            new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = "",
                Data = initCode,
                IsContractCreation = true,
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

        private static TransactionExecutionContext CallCtx(
            ExecutionStateService executionState, string to, long value, long gasLimit = 2_000_000,
            string coinbase = SenderAddress, long gasPrice = 1, long baseFee = 0) =>
            new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = to,
                Data = null,
                IsContractCreation = false,
                GasLimit = gasLimit,
                Value = value,
                GasPrice = gasPrice,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = baseFee,
                ChainId = 1,
                Coinbase = coinbase,
                ExecutionState = executionState
            };

        private static List<FilterLog> TransferLogs(TransactionExecutionResult result) =>
            (result.Logs ?? new List<FilterLog>())
                .Where(l => l.Address.IsTheSameAddress(Eip7708EthTransferLogRule.SystemAddress))
                .ToList();

        private static void AssertTransfer(FilterLog log, string from, string to, long value)
        {
            Assert.Equal(3, log.Topics.Length);
            Assert.Equal(Eip7708EthTransferLogRule.TransferTopic, ((string)log.Topics[0]).ToLower());
            Assert.Equal(AddressTopic(from), ((string)log.Topics[1]).ToLower());
            Assert.Equal(AddressTopic(to), ((string)log.Topics[2]).ToLower());
            Assert.Equal(new EvmUInt256(value).ToBigEndian().ToHex(), log.Data.ToLower());
        }

        private static string AddressTopic(string address) =>
            address.HexToByteArray().PadTo32Bytes().ToHex();

        [Fact]
        public async Task Given_AmsterdamValueTransaction_When_RecipientHasNoCode_Then_TransferLogIsEmittedFromTheSystemAddress()
        {
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);
            var ctx = CallCtx(executionState, RecipientAddress, value: 1000);

            var (_, result) = await RunAsync(HardforkConfig.Amsterdam, ctx);

            Assert.True(result.Success, result.Error);
            var transfers = TransferLogs(result);
            var transfer = Assert.Single(transfers);
            AssertTransfer(transfer, SenderAddress, RecipientAddress, 1000);
        }

        [Fact]
        public async Task Given_AmsterdamValueTransaction_When_ValueIsZero_Then_NoTransferLogIsEmitted()
        {
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);
            var ctx = CallCtx(executionState, RecipientAddress, value: 0);

            var (_, result) = await RunAsync(HardforkConfig.Amsterdam, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Empty(TransferLogs(result));
        }

        [Fact]
        public async Task Given_AmsterdamCreationTransaction_When_InitCodeSweepsEndowmentToBeneficiary_Then_BothTheEndowmentAndTheSweepAreLogged()
        {
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);
            var ctx = CreationCtx(executionState, SelfDestructToBeneficiaryCode(BeneficiaryAddress), value: 1000);

            var (resultCtx, result) = await RunAsync(HardforkConfig.Amsterdam, ctx);

            Assert.True(result.Success, result.Error);
            var transfers = TransferLogs(result);
            Assert.Equal(2, transfers.Count);
            AssertTransfer(transfers[0], SenderAddress, resultCtx.ContractAddress, 1000);
            AssertTransfer(transfers[1], resultCtx.ContractAddress, BeneficiaryAddress, 1000);
        }

        [Fact]
        public async Task Given_AmsterdamCreationTransaction_When_SameTxContractSweepsToItself_Then_OnlyTheEndowmentIsLogged()
        {
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);
            var ctx = CreationCtx(executionState, SelfDestructToSelfInitCode(), value: 1000);

            var (resultCtx, result) = await RunAsync(HardforkConfig.Amsterdam, ctx);

            Assert.True(result.Success, result.Error);
            var transfer = Assert.Single(TransferLogs(result));
            AssertTransfer(transfer, SenderAddress, resultCtx.ContractAddress, 1000);
        }

        [Fact]
        public async Task Given_AmsterdamCall_When_ContractForwardsValueToAnAccount_Then_BothTheTransactionAndTheCallAreLogged()
        {
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(CallerContractAddress, CallWithValueCode(RecipientAddress, 0x07));
            await node.SetNonceAsync(CallerContractAddress, 1);
            var executionState = new ExecutionStateService(node);
            var ctx = CallCtx(executionState, CallerContractAddress, value: 1000);

            var (_, result) = await RunAsync(HardforkConfig.Amsterdam, ctx);

            Assert.True(result.Success, result.Error);
            var transfers = TransferLogs(result);
            Assert.Equal(2, transfers.Count);
            AssertTransfer(transfers[0], SenderAddress, CallerContractAddress, 1000);
            AssertTransfer(transfers[1], CallerContractAddress, RecipientAddress, 7);
        }

        [Fact]
        public async Task Given_AmsterdamCall_When_TheCalleeReverts_Then_OnlyTheTransactionsOwnTransferIsLogged()
        {
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(CallerContractAddress, CallWithValueCode(RevertingContractAddress, 0x07));
            await node.SetNonceAsync(CallerContractAddress, 1);
            await node.SetCodeAsync(RevertingContractAddress, RevertingCode());
            await node.SetNonceAsync(RevertingContractAddress, 1);
            var executionState = new ExecutionStateService(node);
            var ctx = CallCtx(executionState, CallerContractAddress, value: 1000);

            var (_, result) = await RunAsync(HardforkConfig.Amsterdam, ctx);

            Assert.True(result.Success, result.Error);
            var transfer = Assert.Single(TransferLogs(result));
            AssertTransfer(transfer, SenderAddress, CallerContractAddress, 1000);
        }

        [Fact]
        public async Task Given_AmsterdamCall_When_PreExistingContractSweepsToBeneficiary_Then_TheSweepIsLogged()
        {
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(PreExistingContractAddress, SelfDestructToBeneficiaryCode(BeneficiaryAddress));
            await node.SetNonceAsync(PreExistingContractAddress, 1);
            await node.SetBalanceAsync(PreExistingContractAddress, BigInteger.Parse("1000"));
            var executionState = new ExecutionStateService(node);
            var ctx = CallCtx(executionState, PreExistingContractAddress, value: 0);

            var (_, result) = await RunAsync(HardforkConfig.Amsterdam, ctx);

            Assert.True(result.Success, result.Error);
            var transfer = Assert.Single(TransferLogs(result));
            AssertTransfer(transfer, PreExistingContractAddress, BeneficiaryAddress, 1000);
        }

        [Fact]
        public async Task Given_AmsterdamDelegateCall_When_TheFrameRuns_Then_NoTransferLogIsEmitted()
        {
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(CallerContractAddress, DelegateCallCode(DelegateTargetAddress));
            await node.SetNonceAsync(CallerContractAddress, 1);
            await node.SetCodeAsync(DelegateTargetAddress, new byte[] { (byte)Instruction.STOP });
            await node.SetNonceAsync(DelegateTargetAddress, 1);
            var executionState = new ExecutionStateService(node);
            var ctx = CallCtx(executionState, CallerContractAddress, value: 1000);

            var (_, result) = await RunAsync(HardforkConfig.Amsterdam, ctx);

            Assert.True(result.Success, result.Error);
            var transfer = Assert.Single(TransferLogs(result));
            AssertTransfer(transfer, SenderAddress, CallerContractAddress, 1000);
        }

        [Fact]
        public async Task Given_AmsterdamCallWithValueToTheCoinbase_When_NoPriorityFee_Then_OnlyTheCallIsLoggedAndNotTheFee()
        {
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);
            var ctx = CallCtx(executionState, CoinbaseAddress, value: 1000,
                coinbase: CoinbaseAddress, gasPrice: 7, baseFee: 7);

            var (_, result) = await RunAsync(HardforkConfig.Amsterdam, ctx);

            Assert.True(result.Success, result.Error);
            var transfer = Assert.Single(TransferLogs(result));
            AssertTransfer(transfer, SenderAddress, CoinbaseAddress, 1000);
        }


        /// <summary>
        /// AMS-7708-08. EIP-7708 requires the move be "to a different account", and the
        /// guard is on the FRAME's caller and target, not on the transaction's sender
        /// and recipient - a distinct code path from the SELFDESTRUCT-to-self case
        /// already covered above. The transaction here carries no value, so the only
        /// transfer that could be logged is the contract's call to itself.
        /// </summary>
        [Fact]
        [Trait("Category", "EIP7708")]
        public async Task Given_AmsterdamCall_When_AContractCallsItselfWithValue_Then_NoTransferLogIsEmitted()
        {
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(CallerContractAddress, CallWithValueCode(CallerContractAddress, 0x07));
            await node.SetNonceAsync(CallerContractAddress, 1);
            await node.SetBalanceAsync(CallerContractAddress, 1000);
            var executionState = new ExecutionStateService(node);
            var ctx = CallCtx(executionState, CallerContractAddress, value: 0);

            var (_, result) = await RunAsync(HardforkConfig.Amsterdam, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Empty(TransferLogs(result));
        }

        [Fact]
        [Trait("Category", "EIP7708")]
        public async Task Given_AmsterdamCall_When_AContractCallsADifferentAddressWithTheSameValue_Then_ATransferLogIsEmitted()
        {
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(PreExistingContractAddress, EmitsOneLogCode());
            await node.SetNonceAsync(PreExistingContractAddress, 1);
            await node.SetCodeAsync(CallerContractAddress,
                CallWithValueCode(PreExistingContractAddress, 0x07));
            await node.SetNonceAsync(CallerContractAddress, 1);
            await node.SetBalanceAsync(CallerContractAddress, 1000);
            var executionState = new ExecutionStateService(node);
            var ctx = CallCtx(executionState, CallerContractAddress, value: 0);

            var (_, result) = await RunAsync(HardforkConfig.Amsterdam, ctx);

            Assert.True(result.Success, result.Error);
            var transfers = TransferLogs(result);
            Assert.Single(transfers);
            AssertTransfer(transfers[0], CallerContractAddress, PreExistingContractAddress, 7);
        }

        [Fact]
        [Trait("Category", "EIP7708")]
        public async Task Given_AmsterdamNestedCalls_When_TheMiddleFrameRevertsAfterPayingOnwards_Then_NeitherTransferSurvives()
        {
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(RevertingContractAddress,
                DeepCallWithValueThenRevertCode(RecipientAddress, 0x03));
            await node.SetNonceAsync(RevertingContractAddress, 1);
            await node.SetCodeAsync(CallerContractAddress,
                DeepCallWithValueCode(RevertingContractAddress, 0x07));
            await node.SetNonceAsync(CallerContractAddress, 1);
            await node.SetBalanceAsync(CallerContractAddress, BigInteger.Parse("1000"));
            var executionState = new ExecutionStateService(node);
            var ctx = CallCtx(executionState, CallerContractAddress, value: 0);

            var (_, result) = await RunAsync(HardforkConfig.Amsterdam, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Empty(TransferLogs(result));
        }

        [Fact]
        [Trait("Category", "EIP7708")]
        public async Task Given_AmsterdamNestedCalls_When_TheMiddleFrameCompletes_Then_BothTransfersAreLogged()
        {
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(RevertingContractAddress,
                DeepCallWithValueCode(RecipientAddress, 0x03));
            await node.SetNonceAsync(RevertingContractAddress, 1);
            await node.SetCodeAsync(CallerContractAddress,
                DeepCallWithValueCode(RevertingContractAddress, 0x07));
            await node.SetNonceAsync(CallerContractAddress, 1);
            await node.SetBalanceAsync(CallerContractAddress, BigInteger.Parse("1000"));
            var executionState = new ExecutionStateService(node);
            var ctx = CallCtx(executionState, CallerContractAddress, value: 0);

            var (_, result) = await RunAsync(HardforkConfig.Amsterdam, ctx);

            Assert.True(result.Success, result.Error);
            var transfers = TransferLogs(result);
            Assert.Equal(2, transfers.Count);
            AssertTransfer(transfers[0], CallerContractAddress, RevertingContractAddress, 7);
            AssertTransfer(transfers[1], RevertingContractAddress, RecipientAddress, 3);
        }

        [Fact]
        [Trait("Category", "EIP7708")]
        public async Task Given_AmsterdamCall_When_OneCallRevertsAndASiblingSucceeds_Then_OnlyTheSiblingsTransferIsLogged()
        {
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(RevertingContractAddress, RevertingCode());
            await node.SetNonceAsync(RevertingContractAddress, 1);
            await node.SetCodeAsync(CallerContractAddress,
                TwoCallsWithValueCode(RevertingContractAddress, RecipientAddress, 0x07));
            await node.SetNonceAsync(CallerContractAddress, 1);
            await node.SetBalanceAsync(CallerContractAddress, 1000);
            var executionState = new ExecutionStateService(node);
            var ctx = CallCtx(executionState, CallerContractAddress, value: 0);

            var (_, result) = await RunAsync(HardforkConfig.Amsterdam, ctx);

            Assert.True(result.Success, result.Error);
            var transfers = TransferLogs(result);
            Assert.Single(transfers);
            AssertTransfer(transfers[0], CallerContractAddress, RecipientAddress, 7);
        }

        [Fact]
        [Trait("Category", "EIP7708")]
        public async Task Given_AmsterdamSelfDestruct_When_TheBeneficiaryIsTheSystemAddress_Then_ItIsLoggedLikeAnyOtherTransfer()
        {
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(PreExistingContractAddress,
                SelfDestructToBeneficiaryCode(Eip7708EthTransferLogRule.SystemAddress));
            await node.SetNonceAsync(PreExistingContractAddress, 1);
            await node.SetBalanceAsync(PreExistingContractAddress, BigInteger.Parse("1000"));
            var executionState = new ExecutionStateService(node);
            var ctx = CallCtx(executionState, PreExistingContractAddress, value: 0);

            var (_, result) = await RunAsync(HardforkConfig.Amsterdam, ctx);

            Assert.True(result.Success, result.Error);
            var transfers = TransferLogs(result);
            Assert.Single(transfers);
            AssertTransfer(transfers[0], PreExistingContractAddress,
                Eip7708EthTransferLogRule.SystemAddress, 1000);
        }


        private static byte[] EmitsOneLogCode() => new byte[]
        {
            (byte)Instruction.PUSH1, 0x00,
            (byte)Instruction.PUSH1, 0x00,
            (byte)Instruction.LOG0,
            (byte)Instruction.STOP
        };

        private static byte[] LogsThenCallsWithValueThenLogsAgainCode(string target, byte value)
        {
            var log0 = new byte[]
            {
                (byte)Instruction.PUSH1, 0x00,
                (byte)Instruction.PUSH1, 0x00,
                (byte)Instruction.LOG0
            };
            var code = new List<byte>(log0);
            var call = new List<byte>(DeepCallWithValueCode(target, value));
            call.RemoveAt(call.Count - 1);
            code.AddRange(call);
            code.AddRange(log0);
            code.Add((byte)Instruction.STOP);
            return code.ToArray();
        }

        [Fact]
        [Trait("Category", "EIP7708")]
        public async Task Given_AmsterdamCall_When_BothFramesLogAroundTheTransfer_Then_TheTransferIsTheChildsFirstLog()
        {
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(RecipientAddress, EmitsOneLogCode());
            await node.SetNonceAsync(RecipientAddress, 1);
            await node.SetCodeAsync(CallerContractAddress,
                LogsThenCallsWithValueThenLogsAgainCode(RecipientAddress, 0x07));
            await node.SetNonceAsync(CallerContractAddress, 1);
            await node.SetBalanceAsync(CallerContractAddress, BigInteger.Parse("1000"));
            var executionState = new ExecutionStateService(node);
            var ctx = CallCtx(executionState, CallerContractAddress, value: 0);

            var (_, result) = await RunAsync(HardforkConfig.Amsterdam, ctx);

            Assert.True(result.Success, result.Error);
            var all = result.Logs ?? new List<FilterLog>();
            Assert.Equal(4, all.Count);

            var isTransfer = new bool[4];
            for (var i = 0; i < 4; i++)
                isTransfer[i] = all[i].Address.IsTheSameAddress(Eip7708EthTransferLogRule.SystemAddress);

            Assert.False(isTransfer[0], "index 0 must be the caller's log, emitted before the CALL");
            Assert.True(isTransfer[1],
                "index 1 must be the transfer: after the caller's log (so emission is not " +
                "batched) and before the callee's (so it is the child's FIRST log)");
            Assert.False(isTransfer[2], "index 2 must be the callee's own log");
            Assert.False(isTransfer[3], "index 3 must be the caller's log after the CALL returned");

            AssertTransfer(all[1], CallerContractAddress, RecipientAddress, 7);
        }

        [Theory]
        [Trait("Category", "EIP7708")]
        [InlineData("legacy", false)]
        [InlineData("eip1559", false)]
        [InlineData("blob", false)]
        [InlineData("legacy", true)]
        [InlineData("eip1559", true)]
        [InlineData("blob", true)]
        public async Task Given_AnAmsterdamValueTransaction_When_SentAsAnyTypeToAnyTargetShape_Then_TheSameTransferLogIsEmitted(
            string transactionShape, bool targetHasCode)
        {
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            if (targetHasCode)
            {
                await node.SetCodeAsync(RecipientAddress, EmitsOneLogCode());
                await node.SetNonceAsync(RecipientAddress, 1);
            }
            var executionState = new ExecutionStateService(node);
            var ctx = CallCtx(executionState, RecipientAddress, value: 1000);

            switch (transactionShape)
            {
                case "legacy":
                    break;
                case "eip1559":
                    ctx.IsEip1559 = true;
                    ctx.MaxFeePerGas = 1;
                    ctx.MaxPriorityFeePerGas = 0;
                    break;
                case "blob":
                    ctx.IsEip1559 = true;
                    ctx.MaxFeePerGas = 1;
                    ctx.MaxPriorityFeePerGas = 0;
                    ctx.IsType3Transaction = true;
                    ctx.BlobVersionedHashes = new List<string> { "0x01" + new string('0', 62) };
                    ctx.MaxFeePerBlobGas = 1;
                    break;
            }

            var (_, result) = await RunAsync(HardforkConfig.Amsterdam, ctx);

            Assert.True(result.Success, $"{transactionShape}/code={targetHasCode}: {result.Error}");
            var transfer = Assert.Single(TransferLogs(result));
            AssertTransfer(transfer, SenderAddress, RecipientAddress, 1000);
        }

        [Fact]
        public async Task Given_OsakaValueTransaction_When_RecipientHasNoCode_Then_NoTransferLogIsEmitted_ProvingTheForkGate()
        {
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);
            var ctx = CallCtx(executionState, RecipientAddress, value: 1000);

            var (_, result) = await RunAsync(HardforkConfig.Osaka, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Empty(TransferLogs(result));
        }

        [Fact]
        public async Task Given_OsakaCreationTransaction_When_InitCodeSweepsEndowmentToBeneficiary_Then_NoTransferLogIsEmitted_ProvingTheForkGate()
        {
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);
            var ctx = CreationCtx(executionState, SelfDestructToBeneficiaryCode(BeneficiaryAddress), value: 1000);

            var (_, result) = await RunAsync(HardforkConfig.Osaka, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Empty(TransferLogs(result));
        }
    }
}
