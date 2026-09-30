using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Execution.Opcodes;
using Nethereum.EVM.Execution.Storage;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Hardforks;
using Nethereum.EVM.UnitTests;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class Eip8038StateAccessRepricingTests
    {
        private const string ContractAddress = "0x1234567890123456789012345678901234567890";
        private const string CallerAddress = "0xABCDEF0123456789ABCDEF0123456789ABCDEF01";
        private const string OtherAddress = "0x9999999999999999999999999999999999999999";

        private static (Program program, ExecutionStateService stateService) CreateProgram(long gasRemaining = 1_000_000)
        {
            var stateService = new ExecutionStateService(new MockNodeDataService());
            var callInput = new CallInput
            {
                To = ContractAddress,
                From = CallerAddress,
                Gas = new Nethereum.Hex.HexTypes.HexBigInteger(gasRemaining),
                Data = "0x"
            };

            var context = new ProgramContext(callInput, stateService);
            var program = new Program("00".HexToByteArray(), context);
            program.GasRemaining = gasRemaining;
            return (program, stateService);
        }


        [Fact]
        public async Task Given_ColdAccountAccess_AtAmsterdam_Then_Charges3000_NotBerlin2600()
        {
            var (program, _) = CreateProgram();
            program.StackPush(OtherAddress.HexToByteArray());

            var amsterdamCost = await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.BALANCE, program);

            Assert.Equal(GasConstants.EIP8038_COLD_ACCOUNT_ACCESS, amsterdamCost);
            Assert.Equal(3000, amsterdamCost);
        }

        [Fact]
        public async Task Given_WarmAccountAccess_AtAmsterdam_Then_ChargesUnchanged100()
        {
            var (program, stateService) = CreateProgram();
            stateService.MarkAddressAsWarm(OtherAddress);
            program.StackPush(OtherAddress.HexToByteArray());

            var amsterdamCost = await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.BALANCE, program);

            Assert.Equal(GasConstants.WARM_STORAGE_READ_COST, amsterdamCost);
        }

        [Fact]
        public async Task Given_ColdAccountAccess_AtOsaka_Then_StaysAt2600_ProvingNoCrossForkLeak()
        {
            var (program, _) = CreateProgram();
            program.StackPush(OtherAddress.HexToByteArray());

            var osakaCost = await OpcodeHandlerSets.Osaka.GetGasCostAsync(Instruction.BALANCE, program);

            Assert.Equal(GasConstants.COLD_ACCOUNT_ACCESS_COST, osakaCost);
            Assert.Equal(2600, osakaCost);
        }

        [Fact]
        public async Task Given_ColdAccountAccess_AtAmsterdam_Then_CallFamilyAlsoRepriced()
        {
            var (program, _) = CreateProgram();
            program.StackPush(0);
            program.StackPush(0);
            program.StackPush(0);
            program.StackPush(0);
            program.StackPush(0);
            program.StackPush(OtherAddress.HexToByteArray());
            program.StackPush(21000);

            var amsterdamCost = await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.CALL, program);

            Assert.Equal(GasConstants.EIP8038_COLD_ACCOUNT_ACCESS, amsterdamCost);
        }


        [Fact]
        public async Task Given_SelfDestructColdAccess_AtAmsterdam_Then_Charges3000_NotBerlin2600()
        {
            var (program, _) = CreateProgram();
            program.StackPush(OtherAddress.HexToByteArray());

            var amsterdamCost = await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.SELFDESTRUCT, program);

            Assert.Equal(GasConstants.SELFDESTRUCT_COST + GasConstants.EIP8038_COLD_ACCOUNT_ACCESS, amsterdamCost);
            Assert.Equal(8000, amsterdamCost);
        }

        [Fact]
        public async Task Given_SelfDestructColdAccess_AtOsaka_Then_StaysAt2600_ProvingNoCrossForkLeak()
        {
            var (program, _) = CreateProgram();
            program.StackPush(OtherAddress.HexToByteArray());

            var osakaCost = await OpcodeHandlerSets.Osaka.GetGasCostAsync(Instruction.SELFDESTRUCT, program);

            Assert.Equal(GasConstants.SELFDESTRUCT_COST + GasConstants.COLD_ACCOUNT_ACCESS_COST, osakaCost);
            Assert.Equal(7600, osakaCost);
        }

        [Fact]
        public async Task Given_SelfDestructWarmAccess_AtAmsterdam_Then_ChargesUnchangedBase()
        {
            var (program, stateService) = CreateProgram();
            stateService.MarkAddressAsWarm(OtherAddress);
            program.StackPush(OtherAddress.HexToByteArray());

            var amsterdamCost = await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.SELFDESTRUCT, program);

            Assert.Equal(GasConstants.SELFDESTRUCT_COST, amsterdamCost);
            Assert.Equal(5000, amsterdamCost);
        }


        [Fact]
        public async Task Given_ExtCodeSize_AtAmsterdam_Then_ChargesSecondRead()
        {
            var (program, _) = CreateProgram();
            program.StackPush(OtherAddress.HexToByteArray());

            var cost = await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.EXTCODESIZE, program);

            Assert.Equal(GasConstants.EIP8038_COLD_ACCOUNT_ACCESS + GasConstants.WARM_STORAGE_READ_COST, cost);
            Assert.Equal(3100, cost);
        }

        [Fact]
        public async Task Given_ExtCodeOnEmptyAccount_When_Charged_Then_SecondReadSurchargeStillApplies()
        {
            var (program, stateService) = CreateProgram();
            program.StackPush(OtherAddress.HexToByteArray());

            var extCodeSizeCost = await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.EXTCODESIZE, program);

            Assert.Equal(3100, extCodeSizeCost);

            var (program2, _) = CreateProgram();
            program2.StackPush(0);
            program2.StackPush(0);
            program2.StackPush(0);
            program2.StackPush(OtherAddress.HexToByteArray());

            var extCodeCopyCost = await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.EXTCODECOPY, program2);

            Assert.Equal(3100, extCodeCopyCost);
        }

        [Fact]
        public async Task Given_ExtCodeHash_AtAmsterdam_Then_NoReadSurcharge()
        {
            var (program, _) = CreateProgram();
            program.StackPush(OtherAddress.HexToByteArray());

            var cost = await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.EXTCODEHASH, program);

            Assert.Equal(GasConstants.EIP8038_COLD_ACCOUNT_ACCESS, cost);
            Assert.Equal(3000, cost);
        }


        [Fact]
        public async Task Given_Create_AtAmsterdam_Then_UsesDerivedCreateAccess_NotBerlinCreateBase()
        {
            var (program, _) = CreateProgram();
            program.StackPush(0);
            program.StackPush(0);
            program.StackPush(0);

            var amsterdamCost = await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.CREATE, program);
            var osakaCost = await OpcodeHandlerSets.Osaka.GetGasCostAsync(Instruction.CREATE, program);

            Assert.Equal(12000, amsterdamCost);
            Assert.Equal(GasConstants.EIP8038_CREATE_ACCESS, amsterdamCost);
            Assert.Equal(32000, osakaCost);
            Assert.Equal(GasConstants.CREATE_BASE, osakaCost);
        }

        [Fact]
        public async Task Given_Create2_AtAmsterdam_Then_UsesDerivedCreateAccess()
        {
            var (program, _) = CreateProgram();
            program.StackPush(0);
            program.StackPush(0);
            program.StackPush(0);
            program.StackPush(0);

            var amsterdamCost = await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.CREATE2, program);

            Assert.Equal(12000, amsterdamCost);
        }


        [Fact]
        public async Task Given_ColdNoValueChangeSstore_AtAmsterdam_Then_Charges2100_NotBerlin2200()
        {
            var (program, stateService) = CreateProgram();
            var key = (EvmUInt256)1;
            var value = new byte[32];
            value[31] = 0x42;
            stateService.SaveToStorage(ContractAddress, key, value);
            var state = stateService.CreateOrGetAccountExecutionState(ContractAddress);
            state.OriginalStorageValues[key] = value;

            program.StackPush(value);
            program.StackPush(1);

            var amsterdamCost = await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.SSTORE, program);

            Assert.Equal(GasConstants.COLD_SLOAD_COST, amsterdamCost);
            Assert.Equal(2100, amsterdamCost);
        }

        [Fact]
        public async Task Given_WarmFirstTimeWriteSstore_AtAmsterdam_Then_ChargesWarmAccessPlusStorageWrite()
        {
            var (program, stateService) = CreateProgram();
            var key = (EvmUInt256)1;
            var original = new byte[32];
            original[31] = 0x42;
            stateService.SaveToStorage(ContractAddress, key, original);
            var state = stateService.CreateOrGetAccountExecutionState(ContractAddress);
            state.OriginalStorageValues[key] = original;
            state.MarkStorageKeyAsWarm(key);

            var newValue = new byte[32];
            newValue[31] = 0xFF;
            program.StackPush(newValue);
            program.StackPush(1);

            var cost = await OpcodeHandlerSets.Amsterdam.GetGasCostAsync(Instruction.SSTORE, program);

            Assert.Equal(GasConstants.WARM_STORAGE_READ_COST + GasConstants.EIP8038_STORAGE_WRITE, cost);
            Assert.Equal(10100, cost);
        }


        [Fact]
        public void Given_AmsterdamConfig_Then_EnforceSstoreGasStipendStaysEip2200Active()
        {
            var config = HardforkConfigFromSpec.Build(AmsterdamSpec.Instance);

            Assert.True(config.EnforceSstoreGasStipend);
        }

        [Fact]
        public void Given_ColdStorageAccessUnchangedAtAmsterdam_Then_NeverExceedsCallStipendPlusOne()
        {
            Assert.True(GasConstants.COLD_SLOAD_COST < GasConstants.CALL_STIPEND + 1);
        }


        private static ProgramContext CreateRefundContext(ExecutionStateService stateService)
        {
            var callInput = new CallInput
            {
                To = ContractAddress,
                From = CallerAddress,
                Gas = new Nethereum.Hex.HexTypes.HexBigInteger(100000),
                Data = "0x"
            };
            var context = new ProgramContext(callInput, stateService)
            {
                SstoreRefundRule = Eip8038SstoreRefundRule.Instance,
                SstoreClearsSchedule = GasConstants.EIP8038_REFUND_STORAGE_CLEAR,
                SstoreSetRefund = GasConstants.EIP8038_STORAGE_WRITE,
                SstoreResetRefund = GasConstants.EIP8038_STORAGE_WRITE,
            };
            return context;
        }

        private static byte[] NonZero(byte b) { var v = new byte[32]; v[31] = b; return v; }
        private static byte[] Zero => ByteUtil.InitialiseEmptyByteArray(32);

        [Fact]
        public async Task Given_SstoreClearsSlot_Then_AddsRefundStorageClear()
        {
            var stateService = new ExecutionStateService(new MockNodeDataService());
            var key = (EvmUInt256)1;
            var original = NonZero(0x42);
            stateService.SaveToStorage(ContractAddress, key, original);
            var state = stateService.CreateOrGetAccountExecutionState(ContractAddress);
            state.OriginalStorageValues[key] = original;

            var context = CreateRefundContext(stateService);
            var program = new Program("00".HexToByteArray(), context);
            program.StackPush(Zero);
            program.StackPush(1);

            await new EvmStorageMemoryExecution().SStore(program);

            Assert.Equal(GasConstants.EIP8038_REFUND_STORAGE_CLEAR, program.RefundCounter);
            Assert.Equal(11616, program.RefundCounter);
        }

        [Fact]
        public async Task Given_SstoreReversesAnEarlierClear_Then_SubtractsRefundStorageClear()
        {
            var stateService = new ExecutionStateService(new MockNodeDataService());
            var key = (EvmUInt256)1;
            var original = NonZero(0x42);
            stateService.SaveToStorage(ContractAddress, key, original);
            var state = stateService.CreateOrGetAccountExecutionState(ContractAddress);
            state.OriginalStorageValues[key] = original;
            stateService.SaveToStorage(ContractAddress, key, Zero);

            var context = CreateRefundContext(stateService);
            var program = new Program("00".HexToByteArray(), context);
            program.StackPush(NonZero(0xFF));
            program.StackPush(1);

            await new EvmStorageMemoryExecution().SStore(program);

            Assert.Equal(-GasConstants.EIP8038_REFUND_STORAGE_CLEAR, program.RefundCounter);
        }

        [Fact]
        public async Task Given_SstoreRestoresOriginalZero_Then_AddsFlatStorageWriteRefund()
        {
            var stateService = new ExecutionStateService(new MockNodeDataService());
            var key = (EvmUInt256)1;
            var state = stateService.CreateOrGetAccountExecutionState(ContractAddress);
            state.OriginalStorageValues[key] = Zero;
            stateService.SaveToStorage(ContractAddress, key, NonZero(0x42));

            var context = CreateRefundContext(stateService);
            var program = new Program("00".HexToByteArray(), context);
            program.StackPush(Zero);
            program.StackPush(1);

            await new EvmStorageMemoryExecution().SStore(program);

            Assert.Equal(GasConstants.EIP8038_STORAGE_WRITE, program.RefundCounter);
            Assert.Equal(10000, program.RefundCounter);
        }

        [Fact]
        public async Task Given_SstoreRestoresOriginalNonZero_Then_AddsFlatStorageWriteRefund_NotResetSplit()
        {
            var stateService = new ExecutionStateService(new MockNodeDataService());
            var key = (EvmUInt256)1;
            var original = NonZero(0x42);
            stateService.SaveToStorage(ContractAddress, key, original);
            var state = stateService.CreateOrGetAccountExecutionState(ContractAddress);
            state.OriginalStorageValues[key] = original;
            stateService.SaveToStorage(ContractAddress, key, NonZero(0xFF));

            var context = CreateRefundContext(stateService);
            var program = new Program("00".HexToByteArray(), context);
            program.StackPush(original);
            program.StackPush(1);

            await new EvmStorageMemoryExecution().SStore(program);

            Assert.Equal(GasConstants.EIP8038_STORAGE_WRITE, program.RefundCounter);
            Assert.Equal(10000, program.RefundCounter);
        }

        [Fact]
        public async Task Given_SstoreClearsThenRestoresNonZeroOriginal_Then_BothReversalAndRestoreFire()
        {
            var stateService = new ExecutionStateService(new MockNodeDataService());
            var key = (EvmUInt256)1;
            var original = NonZero(0x42);
            stateService.SaveToStorage(ContractAddress, key, original);
            var state = stateService.CreateOrGetAccountExecutionState(ContractAddress);
            state.OriginalStorageValues[key] = original;
            stateService.SaveToStorage(ContractAddress, key, Zero);

            var context = CreateRefundContext(stateService);
            var program = new Program("00".HexToByteArray(), context);
            program.StackPush(original);
            program.StackPush(1);

            await new EvmStorageMemoryExecution().SStore(program);

            Assert.Equal(-GasConstants.EIP8038_REFUND_STORAGE_CLEAR + GasConstants.EIP8038_STORAGE_WRITE, program.RefundCounter);
            Assert.Equal(-1616, program.RefundCounter);
        }
    }
}
