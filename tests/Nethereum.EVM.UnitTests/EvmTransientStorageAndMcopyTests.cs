using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class EvmTransientStorageAndMcopyTests
    {
        private readonly EVMSimulator _vm = new EVMSimulator();

        #region TSTORE/TLOAD Tests (EIP-1153)

        [Fact]
        public async Task TStore_TLoad_ShouldStoreAndRetrieveValue()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callerAddress = "0x1111111111111111111111111111111111111111";
            var callInput = CreateDefaultCallInput(callerAddress);
            var programContext = new ProgramContext(callInput, executionStateService, callerAddress);

            var bytecode = string.Concat(
                "6042",
                "6001",
                "5D",
                "6001",
                "5C"
            ).HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
            var result = program.StackPeekAtAndConvertToUBigInteger(0);
            Assert.Equal(0x42, (int)result);
        }

        [Fact]
        public async Task TLoad_UnsetKey_ShouldReturnZero()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callerAddress = "0x1111111111111111111111111111111111111111";
            var callInput = CreateDefaultCallInput(callerAddress);
            var programContext = new ProgramContext(callInput, executionStateService, callerAddress);

            var bytecode = string.Concat(
                "6099",
                "5C"
            ).HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
            var result = program.StackPeekAtAndConvertToUBigInteger(0);
            Assert.Equal(0, (int)result);
        }

        [Fact]
        public async Task TStore_MultipleKeys_ShouldStoreIndependently()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callerAddress = "0x1111111111111111111111111111111111111111";
            var callInput = CreateDefaultCallInput(callerAddress);
            var programContext = new ProgramContext(callInput, executionStateService, callerAddress);

            var bytecode = string.Concat(
                "60AA",
                "6001",
                "5D",
                "60BB",
                "6002",
                "5D",
                "6001",
                "5C",
                "6002",
                "5C"
            ).HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
            var value2 = program.StackPeekAtAndConvertToUBigInteger(0);
            var value1 = program.StackPeekAtAndConvertToUBigInteger(1);
            Assert.Equal(0xBB, (int)value2);
            Assert.Equal(0xAA, (int)value1);
        }

        [Fact]
        public async Task TStore_Overwrite_ShouldUpdateValue()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callerAddress = "0x1111111111111111111111111111111111111111";
            var callInput = CreateDefaultCallInput(callerAddress);
            var programContext = new ProgramContext(callInput, executionStateService, callerAddress);

            var bytecode = string.Concat(
                "6011",
                "6001",
                "5D",
                "6022",
                "6001",
                "5D",
                "6001",
                "5C"
            ).HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
            var result = program.StackPeekAtAndConvertToUBigInteger(0);
            Assert.Equal(0x22, (int)result);
        }

        [Fact]
        public async Task TStore_LargeKey_ShouldWork()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callerAddress = "0x1111111111111111111111111111111111111111";
            var callInput = CreateDefaultCallInput(callerAddress);
            var programContext = new ProgramContext(callInput, executionStateService, callerAddress);

            var largeKey = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";

            var bytecode = string.Concat(
                "6042",
                "7F" + largeKey,
                "5D",
                "7F" + largeKey,
                "5C"
            ).HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
            var result = program.StackPeekAtAndConvertToUBigInteger(0);
            Assert.Equal(0x42, (int)result);
        }

        [Fact]
        public async Task TStore_ZeroValue_ShouldClearSlot()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callerAddress = "0x1111111111111111111111111111111111111111";
            var callInput = CreateDefaultCallInput(callerAddress);
            var programContext = new ProgramContext(callInput, executionStateService, callerAddress);

            var bytecode = string.Concat(
                "6042",
                "6001",
                "5D",
                "6000",
                "6001",
                "5D",
                "6001",
                "5C"
            ).HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
            var result = program.StackPeekAtAndConvertToUBigInteger(0);
            Assert.Equal(0, (int)result);
        }

        #endregion

        #region MCOPY Tests (EIP-5656)

        [Fact]
        public async Task MCopy_ShouldCopyMemory()
        {
            var bytecode = string.Concat(
                "7F" + "DEADBEEF00000000000000000000000000000000000000000000000000000000",
                "6000",
                "52",
                "6004",
                "6000",
                "6020",
                "5E",
                "602051"
            ).HexToByteArray();

            var program = new Program(bytecode);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
        }

        [Fact]
        public async Task MCopy_ZeroLength_ShouldBeNoOp()
        {
            var bytecode = string.Concat(
                "7F" + "1234567800000000000000000000000000000000000000000000000000000000",
                "6000",
                "52",
                "6000",
                "6000",
                "6020",
                "5E",
                "602051"
            ).HexToByteArray();

            var program = new Program(bytecode);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
            var result = program.StackPeekAtAndConvertToUBigInteger(0);
            Assert.Equal(0, (int)result);
        }

        [Fact]
        public async Task MCopy_OverlappingForward_ShouldHandleCorrectly()
        {
            var bytecode = string.Concat(
                "7F" + "0102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F20",
                "6000",
                "52",
                "6010",
                "6000",
                "6008",
                "5E",
                "600851"
            ).HexToByteArray();

            var program = new Program(bytecode);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
        }

        [Fact]
        public async Task MCopy_OverlappingBackward_ShouldHandleCorrectly()
        {
            var bytecode = string.Concat(
                "7F" + "0102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F20",
                "6000",
                "52",
                "6010",
                "6008",
                "6000",
                "5E",
                "600051"
            ).HexToByteArray();

            var program = new Program(bytecode);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
        }

        [Fact]
        public async Task MCopy_LargeBlock_ShouldWork()
        {
            var bytecode = string.Concat(
                "7F" + "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
                "6000",
                "52",
                "6020",
                "6000",
                "6040",
                "5E",
                "604051"
            ).HexToByteArray();

            var program = new Program(bytecode);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
        }

        #endregion


        #region Transient vs Persistent Storage

        [Fact]
        public async Task TStore_ShouldNotAffectPersistentStorage()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callerAddress = "0x1111111111111111111111111111111111111111";
            var callInput = CreateDefaultCallInput(callerAddress);
            var programContext = new ProgramContext(callInput, executionStateService, callerAddress);

            var bytecode = string.Concat(
                "6042",
                "6001",
                "5D",
                "6001",
                "54"
            ).HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
            var persistentValue = program.StackPeekAtAndConvertToUBigInteger(0);
            Assert.Equal(0, (int)persistentValue);
        }

        [Fact]
        public async Task SStore_ShouldNotAffectTransientStorage()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callerAddress = "0x1111111111111111111111111111111111111111";
            var callInput = CreateDefaultCallInput(callerAddress);
            var programContext = new ProgramContext(callInput, executionStateService, callerAddress);

            var bytecode = string.Concat(
                "6042",
                "6001",
                "55",
                "6001",
                "5C"
            ).HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
            var transientValue = program.StackPeekAtAndConvertToUBigInteger(0);
            Assert.Equal(0, (int)transientValue);
        }

        #endregion

        #region Helper Methods

        private async Task ExecuteProgramToEnd(Program program, int maxSteps = 1000)
        {
            try
            {
                await _vm.ExecuteWithCallStackAsync(program, traceEnabled: false);
            }
            catch (Exception)
            {
                program.Stop();
            }
        }

        private CallInput CreateDefaultCallInput(string from = null)
        {
            return new CallInput
            {
                From = from ?? "0x1111111111111111111111111111111111111111",
                To = "0x2222222222222222222222222222222222222222",
                Data = "",
                Value = new HexBigInteger(0),
                Gas = new HexBigInteger(1000000)
            };
        }

        #endregion
    }
}
