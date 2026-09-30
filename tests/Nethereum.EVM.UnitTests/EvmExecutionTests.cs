using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Precompiles;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class EvmExecutionTests
    {
        private readonly EVMSimulator _vm = new EVMSimulator(DefaultHardforkConfigs.Cancun);

        #region Stack Operations

        [Theory]
        [InlineData("6001", 1)]
        [InlineData("6101FF", 0x01FF)]
        [InlineData("620ABCDE", 0x0ABCDE)]
        [InlineData("6000", 0)]
        [InlineData("60FF", 0xFF)]
        public async Task Push_ShouldPushCorrectValue(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode);
            Assert.Equal(expected, (int)program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task Pop_ShouldRemoveTopOfStack()
        {
            var program = await ExecuteProgram("6001600250");
            Assert.Equal(1, (int)program.StackPeekAtU256(0));
        }

        [Theory]
        [InlineData("60016002600380", 3, 3)]
        [InlineData("60016002600381", 3, 2)]
        [InlineData("60016002600382", 3, 1)]
        public async Task Dup_ShouldDuplicateCorrectStackItem(string bytecode, int steps, int expectedTop)
        {
            var program = await ExecuteProgram(bytecode, steps + 1);
            Assert.Equal(expectedTop, (int)program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task Swap1_ShouldSwapTopTwoItems()
        {
            var program = await ExecuteProgram("6001600290", 3);
            Assert.Equal(1, (int)program.StackPeekAtU256(0));
            Assert.Equal(2, (int)program.StackPeekAtU256(1));
        }

        #endregion

        #region Arithmetic Operations

        [Theory]
        [InlineData("6002600301", 5)]
        [InlineData("600A600501", 15)]
        [InlineData("6000600001", 0)]
        public async Task Add_ShouldAddCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (int)program.StackPeekAtU256(0));
        }

        [Theory]
        [InlineData("6002600503", 3)]
        [InlineData("6005600A03", 5)]
        [InlineData("6005600503", 0)]
        public async Task Sub_ShouldSubtractCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (int)program.StackPeekAtU256(0));
        }

        [Theory]
        [InlineData("6003600402", 12)]
        [InlineData("6000600502", 0)]
        [InlineData("6001600702", 7)]
        public async Task Mul_ShouldMultiplyCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (int)program.StackPeekAtU256(0));
        }

        [Theory]
        [InlineData("6002600604", 3)]
        [InlineData("6003600A04", 3)]
        [InlineData("6001600704", 7)]
        public async Task Div_ShouldDivideCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (int)program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task Div_ByZero_ShouldReturnZero()
        {
            var program = await ExecuteProgram("6000600604", 3);
            Assert.Equal(0, (int)program.StackPeekAtU256(0));
        }

        [Theory]
        [InlineData("6003600A06", 1)]
        [InlineData("6002600806", 0)]
        [InlineData("6007600506", 5)]
        public async Task Mod_ShouldModCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (int)program.StackPeekAtU256(0));
        }

        [Theory]
        [InlineData("600260030a", 9)]
        [InlineData("600360020a", 8)]
        [InlineData("600060050a", 1)]
        [InlineData("600160050a", 5)]
        public async Task Exp_ShouldExponentiateCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (int)program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task AddMod_ShouldCalculateCorrectly()
        {
            var program = await ExecuteProgram("6008600A600A08", 4);
            Assert.Equal(4, (int)program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task MulMod_ShouldCalculateCorrectly()
        {
            var program = await ExecuteProgram("6008600A600A09", 4);
            Assert.Equal(4, (int)program.StackPeekAtU256(0));
        }

        #endregion

        #region Comparison Operations

        [Theory]
        [InlineData("6009600A10", 0)]
        [InlineData("600A600910", 1)]
        [InlineData("6005600510", 0)]
        public async Task Lt_ShouldCompareCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (int)program.StackPeekAtU256(0));
        }

        [Theory]
        [InlineData("6009600A11", 1)]
        [InlineData("600A600911", 0)]
        [InlineData("6005600511", 0)]
        public async Task Gt_ShouldCompareCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (int)program.StackPeekAtU256(0));
        }

        [Theory]
        [InlineData("6005600514", 1)]
        [InlineData("6005600614", 0)]
        public async Task Eq_ShouldCompareCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (int)program.StackPeekAtU256(0));
        }

        [Theory]
        [InlineData("600015", 1)]
        [InlineData("600115", 0)]
        [InlineData("60FF15", 0)]
        public async Task IsZero_ShouldCheckCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 2);
            Assert.Equal(expected, (int)program.StackPeekAtU256(0));
        }

        #endregion

        #region Bitwise Operations

        [Theory]
        [InlineData("60FF60FF16", 0xFF)]
        [InlineData("60F060F016", 0xF0)]
        [InlineData("600F60F016", 0x00)]
        public async Task And_ShouldBitwiseAndCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (int)program.StackPeekAtU256(0));
        }

        [Theory]
        [InlineData("60F0600F17", 0xFF)]
        [InlineData("6000600017", 0x00)]
        public async Task Or_ShouldBitwiseOrCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (int)program.StackPeekAtU256(0));
        }

        [Theory]
        [InlineData("60FF60FF18", 0x00)]
        [InlineData("60F0600F18", 0xFF)]
        public async Task Xor_ShouldBitwiseXorCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (int)program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task Not_ShouldBitwiseNotCorrectly()
        {
            var program = await ExecuteProgram("600019", 2);
            var result = program.StackPeekAtU256(0);
            Assert.Equal(EvmUInt256.MaxValue, result);
        }

        [Theory]
        [InlineData("6001600a1b", 1024)]
        [InlineData("6001601f1b", 0x80000000)]
        [InlineData("600260011b", 4)]
        public async Task Shl_ShouldShiftLeftCorrectly(string bytecode, long expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (long)program.StackPeekAtU256(0));
        }

        [Theory]
        [InlineData("6100ff60081c", 0)]
        [InlineData("610400600a1c", 1)]
        [InlineData("6008600a1c", 0)]
        public async Task Shr_ShouldShiftRightCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (int)program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task Byte_ShouldExtractCorrectByte()
        {
            var program = await ExecuteProgram("65AABBCCDDEEFF601F1A", 3);
            Assert.Equal(0xFF, (int)program.StackPeekAtU256(0));
        }

        #endregion

        #region Memory Operations

        [Fact]
        public async Task MStore_MLoad_ShouldWorkCorrectly()
        {
            var program = await ExecuteProgram("6042600052600051", 5);
            Assert.Equal(0x42, (int)program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task MStore8_ShouldStoreSingleByte()
        {
            var program = await ExecuteProgram("60FF600053600051", 5);
            var result = program.StackPeekAtU256(0);
            Assert.Equal(EvmUInt256.FromHex("ff00000000000000000000000000000000000000000000000000000000000000"), result);
        }

        [Fact]
        public async Task MSize_ShouldReturnMemorySize()
        {
            var program = await ExecuteProgram("604260005259", 4);
            Assert.Equal(32, (int)program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task Memory_StoreAndLoad_MultipleLocations()
        {
            var program = await ExecuteProgram("60116000526022602052600051602051", 10);
            var val1 = program.StackPeekAtU256(0);
            var val2 = program.StackPeekAtU256(1);
            Assert.Equal(0x22, val1);
            Assert.Equal(0x11, val2);
        }

        #endregion

        #region Control Flow

        [Fact]
        public async Task Jump_ShouldJumpToDestination()
        {
            var program = await ExecuteProgram("600456FE5B6042", 4);
            Assert.Equal(0x42, (int)program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task JumpI_ShouldJumpWhenConditionTrue()
        {
            var program = await ExecuteProgram("6001600657005B6042", 5);
            Assert.Equal(0x42, (int)program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task JumpI_ShouldNotJumpWhenConditionFalse()
        {
            var program = await ExecuteProgram("6000600657601100", 4);
            Assert.Equal(0x11, (int)program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task Pc_ShouldReturnProgramCounter()
        {
            var program = await ExecuteProgram("60005058", 3);
            Assert.Equal(3, (int)program.StackPeekAtU256(0));
        }

        #endregion

        #region Keccak256

        [Fact]
        public async Task Keccak256_ShouldHashCorrectly()
        {
            var program = await ExecuteProgram("60016000536001600020", 6);
            var result = program.StackPeek().ToHex();
            Assert.Equal("5fe7f977e71dba2ea1a68e21057beebb9be2ac30c6410aa38d4f3fbe41dcffd2", result.ToLower());
        }

        [Fact]
        public async Task Keccak256_EmptyInput_ShouldHashCorrectly()
        {
            var program = await ExecuteProgram("6000600020", 3);
            var result = program.StackPeek().ToHex();
            Assert.Equal("c5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a470", result.ToLower());
        }

        #endregion

        #region Return and Revert

        [Fact]
        public async Task Return_ShouldReturnData()
        {
            var program = await ExecuteProgram("604260005260206000F3", 100);
            Assert.True(program.Stopped);
            Assert.NotNull(program.ProgramResult.Result);
            Assert.Equal(32, program.ProgramResult.Result.Length);
        }

        [Fact]
        public async Task Revert_ShouldSetRevertFlag()
        {
            var program = await ExecuteProgram("604260005260046000FD", 100);
            Assert.True(program.Stopped);
            Assert.True(program.ProgramResult.IsRevert);
        }

        [Fact]
        public async Task Stop_ShouldStopExecution()
        {
            var program = await ExecuteProgram("60420060FF", 100);
            Assert.True(program.Stopped);
            Assert.Equal(0x42, (int)program.StackPeekAtU256(0));
        }

        #endregion

        #region Context Operations

        [Fact]
        public async Task CallDataLoad_ShouldLoadCalldata()
        {
            var callData = "0000000000000000000000000000000000000000000000000000000000000042".HexToByteArray();
            var program = await ExecuteProgramWithContext("600035", 2, callData);
            Assert.Equal(0x42, (int)program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task CallDataSize_ShouldReturnSize()
        {
            var callData = "0102030405".HexToByteArray();
            var program = await ExecuteProgramWithContext("36", 1, callData);
            Assert.Equal(5, (int)program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task CallDataCopy_ShouldCopyToMemory()
        {
            var callData = "00000000000000000000000000000000000000000000000000000000000000FF".HexToByteArray();
            var program = await ExecuteProgramWithContext("60206000600037600051", 6, callData);
            Assert.Equal(0xFF, (int)program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task CodeSize_ShouldReturnSize()
        {
            var program = await ExecuteProgram("38", 1);
            Assert.Equal(1, (int)program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task CodeCopy_ShouldCopyToMemory()
        {
            var program = await ExecuteProgram("60016000600039600051", 6);
            var result = program.StackPeekAtU256(0);
            Assert.True(result > 0);
        }

        #endregion

        #region Full Contract Execution Scenarios

        [Fact]
        public async Task SimpleAddition_UsingStack()
        {
            var program = await ExecuteProgram("6005600701", 3);
            Assert.Equal(12, (int)program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task MultipleArithmeticOperations()
        {
            var program = await ExecuteProgram("6002600760050102", 5);
            Assert.Equal(24, (int)program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task ConditionalJump_NotTaken()
        {
            var program = await ExecuteProgram("6000600757601100", 4);
            Assert.Equal(0x11, (int)program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task NestedDupAndSwap()
        {
            var program = await ExecuteProgram("600160028090", 4);
            Assert.Equal(2, (int)program.StackPeekAtU256(0));
            Assert.Equal(2, (int)program.StackPeekAtU256(1));
        }

        #endregion

        #region Edge Cases

        [Fact]
        public async Task DivisionByZero_ShouldReturnZero()
        {
            var program = await ExecuteProgram("6000600A04", 3);
            Assert.Equal(0, (int)program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task ModByZero_ShouldReturnZero()
        {
            var program = await ExecuteProgram("6000600A06", 3);
            Assert.Equal(0, (int)program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task Overflow_ShouldWrap()
        {
            var maxUintMinus1 = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFE";
            var program = await ExecuteProgram($"60057F{maxUintMinus1}01", 3);
            var result = program.StackPeekAtU256(0);
            Assert.Equal(3, result);
        }

        [Fact]
        public async Task SignedDivision_NegativeNumbers()
        {
            var negEight = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF8";
            var program = await ExecuteProgram($"60027F{negEight}05", 3);
            var result = (EvmInt256)program.StackPeekAtU256(0);
            Assert.Equal(new EvmInt256(-4), result);
        }

        [Fact]
        public async Task SignedModulo_NegativeNumbers()
        {
            var negEight = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF8";
            var program = await ExecuteProgram($"60037F{negEight}07", 3);
            var result = (EvmInt256)program.StackPeekAtU256(0);
            Assert.Equal(new EvmInt256(-2), result);
        }

        #endregion

        #region Helper Methods

        private async Task<Program> ExecuteProgram(string hexBytecode, int maxSteps = 100)
        {
            var bytecode = hexBytecode.HexToByteArray();
            var program = new Program(bytecode);
            program.GasRemaining = 1000000;

            return await _vm.ExecuteWithCallStackAsync(program, traceEnabled: false);
        }

        private async Task<Program> ExecuteProgramWithContext(string hexBytecode, int maxSteps, byte[] callData)
        {
            var bytecode = hexBytecode.HexToByteArray();
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callInput = new CallInput
            {
                From = "0x1111111111111111111111111111111111111111",
                To = "0x2222222222222222222222222222222222222222",
                Data = callData.ToHex(),
                Value = new HexBigInteger(0),
                Gas = new HexBigInteger(1000000)
            };

            var programContext = new ProgramContext(
                callInput,
                executionStateService,
                "0x1111111111111111111111111111111111111111"
            );

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            return await _vm.ExecuteWithCallStackAsync(program, traceEnabled: false);
        }

        #endregion
    }

    public static class ByteArrayExtensions
    {
        public static byte[] Reverse(this byte[] array)
        {
            var result = new byte[array.Length];
            for (int i = 0; i < array.Length; i++)
            {
                result[i] = array[array.Length - 1 - i];
            }
            return result;
        }
    }
}
