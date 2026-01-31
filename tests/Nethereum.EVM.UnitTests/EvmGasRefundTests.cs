using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Gas;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class EvmGasRefundTests
    {
        [Fact]
        public void AddRefund_ShouldAccumulateRefunds()
        {
            var program = new Program("60016001".HexToByteArray());

            program.AddRefund(1000);
            program.AddRefund(500);
            program.AddRefund(200);

            Assert.Equal(1700, program.RefundCounter);
        }

        [Fact]
        public void AddRefund_ShouldHandleNegativeRefunds()
        {
            var program = new Program("60016001".HexToByteArray());

            program.AddRefund(1000);
            program.AddRefund(-300);

            Assert.Equal(700, program.RefundCounter);
        }

        [Fact]
        public void GetEffectiveRefund_ShouldCapAtTwentyPercent()
        {
            var program = new Program("60016001".HexToByteArray());
            program.TotalGasUsed = 10000;
            program.RefundCounter = 5000;

            var effectiveRefund = program.GetEffectiveRefund();

            Assert.Equal(2000, effectiveRefund);
        }

        [Fact]
        public void GetEffectiveRefund_ShouldReturnFullRefundWhenBelowCap()
        {
            var program = new Program("60016001".HexToByteArray());
            program.TotalGasUsed = 10000;
            program.RefundCounter = 1000;

            var effectiveRefund = program.GetEffectiveRefund();

            Assert.Equal(1000, effectiveRefund);
        }

        [Fact]
        public void GetEffectiveRefund_ShouldReturnZeroWhenNoRefund()
        {
            var program = new Program("60016001".HexToByteArray());
            program.TotalGasUsed = 10000;
            program.RefundCounter = 0;

            var effectiveRefund = program.GetEffectiveRefund();

            Assert.Equal(0, effectiveRefund);
        }

        [Fact]
        public void GasConstants_ShouldMatchEIP3529Spec()
        {
            Assert.Equal(5, GasConstants.REFUND_QUOTIENT);
            Assert.Equal(4800, GasConstants.SSTORE_CLEARS_SCHEDULE);
            Assert.Equal(20000, GasConstants.SSTORE_SET);
            Assert.Equal(2900, GasConstants.SSTORE_RESET);
            Assert.Equal(100, GasConstants.SSTORE_NOOP);
        }

        [Fact]
        public void GasConstants_SStoreRefunds_ShouldBeCalculatedCorrectly()
        {
            Assert.Equal(GasConstants.SSTORE_SET - GasConstants.SSTORE_NOOP, GasConstants.SSTORE_SET_REFUND);
            Assert.Equal(GasConstants.SSTORE_RESET - GasConstants.SSTORE_NOOP, GasConstants.SSTORE_RESET_REFUND);

            Assert.Equal(19900, GasConstants.SSTORE_SET_REFUND);
            Assert.Equal(2800, GasConstants.SSTORE_RESET_REFUND);
        }

        [Fact]
        public void GasConstants_EIP2929ColdWarmAccess_ShouldMatchSpec()
        {
            Assert.Equal(2100, GasConstants.COLD_SLOAD_COST);
            Assert.Equal(2600, GasConstants.COLD_ACCOUNT_ACCESS_COST);
            Assert.Equal(100, GasConstants.WARM_STORAGE_READ_COST);
        }

        [Fact]
        public void GasConstants_BaseOpcodes_ShouldMatchYellowPaper()
        {
            Assert.Equal(0, GasConstants.G_ZERO);
            Assert.Equal(1, GasConstants.G_JUMPDEST);
            Assert.Equal(2, GasConstants.G_BASE);
            Assert.Equal(3, GasConstants.G_VERYLOW);
            Assert.Equal(5, GasConstants.G_LOW);
            Assert.Equal(8, GasConstants.G_MID);
            Assert.Equal(10, GasConstants.G_HIGH);
            Assert.Equal(20, GasConstants.G_BLOCKHASH);
        }

        [Fact]
        public void GasConstants_DynamicOpcodes_ShouldMatchSpec()
        {
            Assert.Equal(10, GasConstants.EXP_BASE);
            Assert.Equal(50, GasConstants.EXP_BYTE);
            Assert.Equal(30, GasConstants.KECCAK256_BASE);
            Assert.Equal(6, GasConstants.KECCAK256_PER_WORD);
            Assert.Equal(3, GasConstants.COPY_BASE);
            Assert.Equal(3, GasConstants.COPY_PER_WORD);
            Assert.Equal(3, GasConstants.MEMORY_BASE);
        }

        [Fact]
        public void GasConstants_LogOpcodes_ShouldMatchSpec()
        {
            Assert.Equal(375, GasConstants.LOG_BASE);
            Assert.Equal(375, GasConstants.LOG_PER_TOPIC);
            Assert.Equal(8, GasConstants.LOG_PER_BYTE);
        }

        [Fact]
        public void GasConstants_CreateOpcodes_ShouldMatchSpec()
        {
            Assert.Equal(32000, GasConstants.CREATE_BASE);
            Assert.Equal(6, GasConstants.CREATE2_HASH_PER_WORD);
        }

        [Fact]
        public void GasConstants_CallOpcodes_ShouldMatchSpec()
        {
            Assert.Equal(9000, GasConstants.CALL_VALUE_TRANSFER);
            Assert.Equal(25000, GasConstants.CALL_NEW_ACCOUNT);
        }

        [Fact]
        public void GasConstants_TransientStorage_ShouldMatchEIP1153()
        {
            Assert.Equal(100, GasConstants.TLOAD_COST);
            Assert.Equal(100, GasConstants.TSTORE_COST);
        }

        [Fact]
        public void GasConstants_TransactionIntrinsic_ShouldMatchSpec()
        {
            Assert.Equal(21000, GasConstants.TX_GAS);
            Assert.Equal(53000, GasConstants.TX_GAS_CONTRACT_CREATION);
            Assert.Equal(4, GasConstants.TX_DATA_ZERO_GAS);
            Assert.Equal(16, GasConstants.TX_DATA_NON_ZERO_GAS);
            Assert.Equal(2400, GasConstants.TX_ACCESS_LIST_ADDRESS_GAS);
            Assert.Equal(1900, GasConstants.TX_ACCESS_LIST_STORAGE_KEY_GAS);
        }

        [Fact]
        public void GasConstants_Precompiles_ShouldMatchSpec()
        {
            Assert.Equal(3000, GasConstants.ECRECOVER_GAS);
            Assert.Equal(60, GasConstants.SHA256_BASE_GAS);
            Assert.Equal(12, GasConstants.SHA256_PER_WORD_GAS);
            Assert.Equal(600, GasConstants.RIPEMD160_BASE_GAS);
            Assert.Equal(120, GasConstants.RIPEMD160_PER_WORD_GAS);
            Assert.Equal(15, GasConstants.IDENTITY_BASE_GAS);
            Assert.Equal(3, GasConstants.IDENTITY_PER_WORD_GAS);
        }

        [Fact]
        public void GasConstants_CallStipend_ShouldMatch()
        {
            Assert.Equal(2300, GasConstants.CALL_STIPEND);
        }

        [Fact]
        public void GasConstants_CreateDataGas_ShouldMatch()
        {
            Assert.Equal(200, GasConstants.CREATE_DATA_GAS);
            Assert.Equal(2, GasConstants.INIT_CODE_WORD_GAS);
        }

        [Fact]
        public void RefundQuotient_ShouldLimitMaxRefund()
        {
            for (int gasUsed = 1000; gasUsed <= 100000; gasUsed += 1000)
            {
                var maxRefund = gasUsed / GasConstants.REFUND_QUOTIENT;
                Assert.Equal(gasUsed / 5, maxRefund);
                Assert.True(maxRefund <= gasUsed * 0.2m);
            }
        }
    }

    public class SStoreRefundTests
    {
        private readonly EvmStorageMemoryExecution _storageExecution = new();
        private const string TestAddress = "0x1234567890123456789012345678901234567890";
        private const string CallerAddress = "0xABCDEF0123456789ABCDEF0123456789ABCDEF01";

        private static byte[] NonZeroValue => new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x42 };
        private static byte[] AnotherNonZeroValue => new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF };
        private static byte[] ZeroValue => ByteUtil.InitialiseEmptyByteArray(32);

        private (Program program, ExecutionStateService stateService) CreateProgramWithStorage(BigInteger key, byte[] initialValue)
        {
            var stateService = new ExecutionStateService(new MockNodeDataService());

            if (initialValue != null && !ByteUtil.IsZero(initialValue))
            {
                stateService.SaveToStorage(TestAddress, key, initialValue);
                var state = stateService.CreateOrGetAccountExecutionState(TestAddress);
                state.OriginalStorageValues[key] = initialValue;
            }

            var callInput = new CallInput
            {
                To = TestAddress,
                From = CallerAddress,
                Gas = new Nethereum.Hex.HexTypes.HexBigInteger(100000),
                Data = "0x"
            };

            var context = new ProgramContext(callInput, stateService);
            var bytecode = "6001600155".HexToByteArray();
            var program = new Program(bytecode, context);
            program.GasRemaining = 100000;

            return (program, stateService);
        }

        [Fact]
        public async Task SStore_NoOp_ShouldNotAddRefund()
        {
            var (program, stateService) = CreateProgramWithStorage(1, NonZeroValue);

            program.StackPush(NonZeroValue);
            program.StackPush(1);

            await _storageExecution.SStore(program);

            Assert.Equal(0, program.RefundCounter);
        }

        [Fact]
        public async Task SStore_FreshClearToZero_ShouldAddClearsScheduleRefund()
        {
            var (program, stateService) = CreateProgramWithStorage(1, NonZeroValue);

            program.StackPush(ZeroValue);
            program.StackPush(1);

            await _storageExecution.SStore(program);

            Assert.Equal(GasConstants.SSTORE_CLEARS_SCHEDULE, program.RefundCounter);
        }

        [Fact]
        public async Task SStore_FreshSetFromZero_ShouldNotAddRefund()
        {
            var (program, stateService) = CreateProgramWithStorage(1, null);

            program.StackPush(NonZeroValue);
            program.StackPush(1);

            await _storageExecution.SStore(program);

            Assert.Equal(0, program.RefundCounter);
        }

        [Fact]
        public async Task SStore_FreshResetNonZeroToNonZero_ShouldNotAddRefund()
        {
            var (program, stateService) = CreateProgramWithStorage(1, NonZeroValue);

            program.StackPush(AnotherNonZeroValue);
            program.StackPush(1);

            await _storageExecution.SStore(program);

            Assert.Equal(0, program.RefundCounter);
        }

        [Fact]
        public async Task SStore_DirtyClearToZero_ShouldAddClearsScheduleRefund()
        {
            var (program, stateService) = CreateProgramWithStorage(1, NonZeroValue);

            stateService.SaveToStorage(TestAddress, 1, AnotherNonZeroValue);

            program.StackPush(ZeroValue);
            program.StackPush(1);

            await _storageExecution.SStore(program);

            Assert.Equal(GasConstants.SSTORE_CLEARS_SCHEDULE, program.RefundCounter);
        }

        [Fact]
        public async Task SStore_RestoreFromZeroToPreviouslyCleared_ShouldRemoveClearsScheduleRefund()
        {
            var (program, stateService) = CreateProgramWithStorage(1, NonZeroValue);

            stateService.SaveToStorage(TestAddress, 1, ZeroValue);

            program.StackPush(AnotherNonZeroValue);
            program.StackPush(1);

            await _storageExecution.SStore(program);

            Assert.Equal(-GasConstants.SSTORE_CLEARS_SCHEDULE, program.RefundCounter);
        }

        [Fact]
        public async Task SStore_RestoreToOriginalZero_ShouldAddSetRefund()
        {
            var (program, stateService) = CreateProgramWithStorage(1, null);

            var state = stateService.CreateOrGetAccountExecutionState(TestAddress);
            state.OriginalStorageValues[1] = ZeroValue;

            stateService.SaveToStorage(TestAddress, 1, NonZeroValue);

            program.StackPush(ZeroValue);
            program.StackPush(1);

            await _storageExecution.SStore(program);

            Assert.Equal(GasConstants.SSTORE_SET_REFUND, program.RefundCounter);
        }

        [Fact]
        public async Task SStore_RestoreToOriginalNonZero_ShouldAddResetRefund()
        {
            var (program, stateService) = CreateProgramWithStorage(1, NonZeroValue);

            stateService.SaveToStorage(TestAddress, 1, AnotherNonZeroValue);

            program.StackPush(NonZeroValue);
            program.StackPush(1);

            await _storageExecution.SStore(program);

            Assert.Equal(GasConstants.SSTORE_RESET_REFUND, program.RefundCounter);
        }

        [Fact]
        public async Task SStore_ComplexScenario_ClearThenRestoreOriginal()
        {
            var (program, stateService) = CreateProgramWithStorage(1, NonZeroValue);

            stateService.SaveToStorage(TestAddress, 1, ZeroValue);

            program.StackPush(NonZeroValue);
            program.StackPush(1);

            await _storageExecution.SStore(program);

            Assert.Equal(
                -GasConstants.SSTORE_CLEARS_SCHEDULE + GasConstants.SSTORE_RESET_REFUND,
                program.RefundCounter);
        }

        [Fact]
        public async Task SStore_RefundValues_MatchEIP3529Spec()
        {
            Assert.Equal(4800, GasConstants.SSTORE_CLEARS_SCHEDULE);
            Assert.Equal(19900, GasConstants.SSTORE_SET_REFUND);
            Assert.Equal(2800, GasConstants.SSTORE_RESET_REFUND);

            var (program, stateService) = CreateProgramWithStorage(1, NonZeroValue);

            program.StackPush(ZeroValue);
            program.StackPush(1);

            await _storageExecution.SStore(program);

            Assert.Equal(4800, program.RefundCounter);
        }

        [Fact]
        public async Task SStore_EffectiveRefund_CappedAtTwentyPercent()
        {
            var (program, stateService) = CreateProgramWithStorage(1, NonZeroValue);

            program.StackPush(ZeroValue);
            program.StackPush(1);

            await _storageExecution.SStore(program);

            program.TotalGasUsed = 10000;

            var effectiveRefund = program.GetEffectiveRefund();

            Assert.Equal(10000 / GasConstants.REFUND_QUOTIENT, effectiveRefund);
            Assert.Equal(2000, effectiveRefund);
        }

        [Fact]
        public async Task SStore_MultipleOperations_AccumulatesRefunds()
        {
            var (program, stateService) = CreateProgramWithStorage(1, NonZeroValue);

            stateService.SaveToStorage(TestAddress, 2, AnotherNonZeroValue);
            var state = stateService.CreateOrGetAccountExecutionState(TestAddress);
            state.OriginalStorageValues[2] = AnotherNonZeroValue;

            program.StackPush(ZeroValue);
            program.StackPush(1);
            await _storageExecution.SStore(program);

            program.StackPush(ZeroValue);
            program.StackPush(2);
            await _storageExecution.SStore(program);

            Assert.Equal(2 * GasConstants.SSTORE_CLEARS_SCHEDULE, program.RefundCounter);
        }
    }

    public class SStoreSentryTests
    {
        private const string TestAddress = "0x1234567890123456789012345678901234567890";
        private const string CallerAddress = "0xABCDEF0123456789ABCDEF0123456789ABCDEF01";

        [Fact]
        public void GasConstants_SentryValue_ShouldMatch()
        {
            Assert.Equal(2300, GasConstants.SSTORE_SENTRY);
        }

        [Fact]
        public async Task SStore_WithEnforceGasSentry_ShouldThrowWhenGasTooLow()
        {
            var stateService = new ExecutionStateService(new MockNodeDataService());
            var callInput = new CallInput
            {
                To = TestAddress,
                From = CallerAddress,
                Gas = new Nethereum.Hex.HexTypes.HexBigInteger(100000),
                Data = "0x"
            };

            var context = new ProgramContext(callInput, stateService);
            context.EnforceGasSentry = true;

            var bytecode = "6001600155".HexToByteArray();
            var program = new Program(bytecode, context);
            program.GasRemaining = 2300;

            program.StackPush(new byte[] { 0x42 });
            program.StackPush(1);

            var storageExecution = new EvmStorageMemoryExecution();

            await Assert.ThrowsAsync<Exceptions.SStoreSentryException>(() => storageExecution.SStore(program));
        }

        [Fact]
        public async Task SStore_WithEnforceGasSentry_ShouldSucceedWhenGasSufficient()
        {
            var stateService = new ExecutionStateService(new MockNodeDataService());
            var callInput = new CallInput
            {
                To = TestAddress,
                From = CallerAddress,
                Gas = new Nethereum.Hex.HexTypes.HexBigInteger(100000),
                Data = "0x"
            };

            var context = new ProgramContext(callInput, stateService);
            context.EnforceGasSentry = true;

            var bytecode = "6001600155".HexToByteArray();
            var program = new Program(bytecode, context);
            program.GasRemaining = 2301;

            program.StackPush(new byte[] { 0x42 });
            program.StackPush(1);

            var storageExecution = new EvmStorageMemoryExecution();

            var exception = await Record.ExceptionAsync(() => storageExecution.SStore(program));
            Assert.Null(exception);
        }

        [Fact]
        public async Task SStore_WithoutEnforceGasSentry_ShouldSucceedWithLowGas()
        {
            var stateService = new ExecutionStateService(new MockNodeDataService());
            var callInput = new CallInput
            {
                To = TestAddress,
                From = CallerAddress,
                Gas = new Nethereum.Hex.HexTypes.HexBigInteger(100000),
                Data = "0x"
            };

            var context = new ProgramContext(callInput, stateService);
            context.EnforceGasSentry = false;

            var bytecode = "6001600155".HexToByteArray();
            var program = new Program(bytecode, context);
            program.GasRemaining = 100;

            program.StackPush(new byte[] { 0x42 });
            program.StackPush(1);

            var storageExecution = new EvmStorageMemoryExecution();

            var exception = await Record.ExceptionAsync(() => storageExecution.SStore(program));
            Assert.Null(exception);
        }
    }
}
