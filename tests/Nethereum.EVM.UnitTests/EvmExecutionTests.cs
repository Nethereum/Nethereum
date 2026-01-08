using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class EvmExecutionTests
    {
        private readonly EVMSimulator _vm = new EVMSimulator();

        #region Stack Operations

        [Theory]
        [InlineData("6001", 1)] // PUSH1 0x01
        [InlineData("6101FF", 0x01FF)] // PUSH2 0x01FF
        [InlineData("620ABCDE", 0x0ABCDE)] // PUSH3
        [InlineData("6000", 0)] // PUSH1 0x00
        [InlineData("60FF", 0xFF)] // PUSH1 0xFF
        public async Task Push_ShouldPushCorrectValue(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode);
            Assert.Equal(expected, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Fact]
        public async Task Pop_ShouldRemoveTopOfStack()
        {
            // PUSH1 0x01, PUSH1 0x02, POP -> stack should have 0x01
            var program = await ExecuteProgram("6001600250");
            Assert.Equal(1, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Theory]
        [InlineData("60016002600380", 3, 3)] // DUP1: duplicates top (3), result top = 3
        [InlineData("60016002600381", 3, 2)] // DUP2: duplicates 2nd from top (2), result top = 2
        [InlineData("60016002600382", 3, 1)] // DUP3: duplicates 3rd from top (1), result top = 1
        public async Task Dup_ShouldDuplicateCorrectStackItem(string bytecode, int steps, int expectedTop)
        {
            var program = await ExecuteProgram(bytecode, steps + 1);
            Assert.Equal(expectedTop, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Fact]
        public async Task Swap1_ShouldSwapTopTwoItems()
        {
            // PUSH1 0x01, PUSH1 0x02, SWAP1 -> [1, 2]
            var program = await ExecuteProgram("6001600290", 3);
            Assert.Equal(1, (int)program.StackPeekAtAndConvertToUBigInteger(0));
            Assert.Equal(2, (int)program.StackPeekAtAndConvertToUBigInteger(1));
        }

        #endregion

        #region Arithmetic Operations

        [Theory]
        [InlineData("6002600301", 5)]   // 3 + 2 = 5
        [InlineData("600A600501", 15)]  // 5 + 10 = 15
        [InlineData("6000600001", 0)]   // 0 + 0 = 0
        public async Task Add_ShouldAddCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Theory]
        [InlineData("6002600503", 3)]   // 5 - 2 = 3
        [InlineData("6005600A03", 5)]   // 10 - 5 = 5
        [InlineData("6005600503", 0)]   // 5 - 5 = 0
        public async Task Sub_ShouldSubtractCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Theory]
        [InlineData("6003600402", 12)]  // 4 * 3 = 12
        [InlineData("6000600502", 0)]   // 5 * 0 = 0
        [InlineData("6001600702", 7)]   // 7 * 1 = 7
        public async Task Mul_ShouldMultiplyCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Theory]
        [InlineData("6002600604", 3)]   // 6 / 2 = 3
        [InlineData("6003600A04", 3)]   // 10 / 3 = 3 (integer division)
        [InlineData("6001600704", 7)]   // 7 / 1 = 7
        public async Task Div_ShouldDivideCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Fact]
        public async Task Div_ByZero_ShouldReturnZero()
        {
            // 6 / 0 = 0 (EVM behavior)
            var program = await ExecuteProgram("6000600604", 3);
            Assert.Equal(0, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Theory]
        [InlineData("6003600A06", 1)]   // 10 % 3 = 1
        [InlineData("6002600806", 0)]   // 8 % 2 = 0
        [InlineData("6007600506", 5)]   // 5 % 7 = 5
        public async Task Mod_ShouldModCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Theory]
        [InlineData("600260030a", 9)]    // 3^2 = 9
        [InlineData("600360020a", 8)]    // 2^3 = 8
        [InlineData("600060050a", 1)]    // 5^0 = 1
        [InlineData("600160050a", 5)]    // 5^1 = 5
        public async Task Exp_ShouldExponentiateCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Fact]
        public async Task AddMod_ShouldCalculateCorrectly()
        {
            // (10 + 10) % 8 = 4
            var program = await ExecuteProgram("6008600A600A08", 4);
            Assert.Equal(4, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Fact]
        public async Task MulMod_ShouldCalculateCorrectly()
        {
            // (10 * 10) % 8 = 4
            var program = await ExecuteProgram("6008600A600A09", 4);
            Assert.Equal(4, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        #endregion

        #region Comparison Operations

        [Theory]
        [InlineData("6009600A10", 0)]   // PUSH 9, PUSH 10, LT: pops 10 then 9, computes 10 < 9 = 0
        [InlineData("600A600910", 1)]   // PUSH 10, PUSH 9, LT: pops 9 then 10, computes 9 < 10 = 1
        [InlineData("6005600510", 0)]   // 5 < 5 = false
        public async Task Lt_ShouldCompareCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Theory]
        [InlineData("6009600A11", 1)]   // GT: a > b where a=10, b=9, so 10 > 9 = 1
        [InlineData("600A600911", 0)]   // GT: a > b where a=9, b=10, so 9 > 10 = 0
        [InlineData("6005600511", 0)]   // GT: 5 > 5 = 0
        public async Task Gt_ShouldCompareCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Theory]
        [InlineData("6005600514", 1)]   // 5 == 5
        [InlineData("6005600614", 0)]   // 6 == 5 = false
        public async Task Eq_ShouldCompareCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Theory]
        [InlineData("600015", 1)]   // IsZero(0) = true
        [InlineData("600115", 0)]   // IsZero(1) = false
        [InlineData("60FF15", 0)]   // IsZero(255) = false
        public async Task IsZero_ShouldCheckCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 2);
            Assert.Equal(expected, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        #endregion

        #region Bitwise Operations

        [Theory]
        [InlineData("60FF60FF16", 0xFF)]   // 0xFF & 0xFF = 0xFF
        [InlineData("60F060F016", 0xF0)]   // 0xF0 & 0xF0 = 0xF0
        [InlineData("600F60F016", 0x00)]   // 0xF0 & 0x0F = 0x00
        public async Task And_ShouldBitwiseAndCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Theory]
        [InlineData("60F0600F17", 0xFF)]   // 0x0F | 0xF0 = 0xFF
        [InlineData("6000600017", 0x00)]   // 0x00 | 0x00 = 0x00
        public async Task Or_ShouldBitwiseOrCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Theory]
        [InlineData("60FF60FF18", 0x00)]   // 0xFF ^ 0xFF = 0x00
        [InlineData("60F0600F18", 0xFF)]   // 0x0F ^ 0xF0 = 0xFF
        public async Task Xor_ShouldBitwiseXorCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Fact]
        public async Task Not_ShouldBitwiseNotCorrectly()
        {
            // NOT 0 = all 1s (max uint256)
            var program = await ExecuteProgram("600019", 2);
            var result = program.StackPeekAtAndConvertToUBigInteger(0);
            Assert.Equal(BigInteger.Pow(2, 256) - 1, result);
        }

        [Theory]
        [InlineData("6001600a1b", 1024)]   // 1 << 10 = 1024
        [InlineData("6001601f1b", 0x80000000)]   // 1 << 31
        [InlineData("600260011b", 4)]   // 2 << 1 = 4
        public async Task Shl_ShouldShiftLeftCorrectly(string bytecode, long expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (long)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Theory]
        [InlineData("6100ff60081c", 0)]      // 0xFF >> 8 = 0
        [InlineData("610400600a1c", 1)]      // 1024 >> 10 = 1
        [InlineData("6008600a1c", 0)]        // 10 >> 8 = 0
        public async Task Shr_ShouldShiftRightCorrectly(string bytecode, int expected)
        {
            var program = await ExecuteProgram(bytecode, 3);
            Assert.Equal(expected, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Fact]
        public async Task Byte_ShouldExtractCorrectByte()
        {
            // PUSH6 0xAABBCCDDEEFF, PUSH1 0x1F, BYTE -> extract rightmost byte (0xFF)
            var program = await ExecuteProgram("65AABBCCDDEEFF601F1A", 3);
            Assert.Equal(0xFF, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        #endregion

        #region Memory Operations

        [Fact]
        public async Task MStore_MLoad_ShouldWorkCorrectly()
        {
            // PUSH1 0x42, PUSH1 0x00, MSTORE, PUSH1 0x00, MLOAD
            var program = await ExecuteProgram("6042600052600051", 5);
            Assert.Equal(0x42, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Fact]
        public async Task MStore8_ShouldStoreSingleByte()
        {
            // PUSH1 0xFF, PUSH1 0x00, MSTORE8, PUSH1 0x00, MLOAD
            var program = await ExecuteProgram("60FF600053600051", 5);
            var result = program.StackPeekAtAndConvertToUBigInteger(0);
            // Should have 0xFF at position 0, rest zeros
            Assert.Equal(BigInteger.Parse("115339776388732929035197660848497720713218148788040405586178452820382218977280"), result);
        }

        [Fact]
        public async Task MSize_ShouldReturnMemorySize()
        {
            // PUSH1 0x42, PUSH1 0x00, MSTORE, MSIZE -> should be 32 (memory expanded to 32-byte words)
            var program = await ExecuteProgram("604260005259", 4);
            Assert.Equal(32, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Fact]
        public async Task Memory_StoreAndLoad_MultipleLocations()
        {
            // Store 0x11 at offset 0, 0x22 at offset 32, load from both
            // PUSH1 0x11, PUSH1 0x00, MSTORE, PUSH1 0x22, PUSH1 0x20, MSTORE, PUSH1 0x00, MLOAD, PUSH1 0x20, MLOAD
            var program = await ExecuteProgram("60116000526022602052600051602051", 10);
            // Top of stack should be 0x22 (last loaded)
            var val1 = program.StackPeekAtAndConvertToUBigInteger(0);
            var val2 = program.StackPeekAtAndConvertToUBigInteger(1);
            Assert.Equal(0x22, val1);
            Assert.Equal(0x11, val2);
        }

        #endregion

        #region Control Flow

        [Fact]
        public async Task Jump_ShouldJumpToDestination()
        {
            // PUSH1 0x04, JUMP, INVALID, JUMPDEST, PUSH1 0x42
            // Jump to position 4 (JUMPDEST), then push 0x42
            var program = await ExecuteProgram("600456FE5B6042", 4);
            Assert.Equal(0x42, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Fact]
        public async Task JumpI_ShouldJumpWhenConditionTrue()
        {
            // PUSH1 0x01, PUSH1 0x06, JUMPI, STOP, JUMPDEST, PUSH1 0x42
            // Bytecode positions: PUSH1(0-1), PUSH1(2-3), JUMPI(4), STOP(5), JUMPDEST(6), PUSH1(7-8)
            // Condition is 1 (true), jump to position 6 (JUMPDEST), then push 0x42
            // Steps: PUSH(1), PUSH(2), JUMPI(3), JUMPDEST(4), PUSH(5)
            var program = await ExecuteProgram("6001600657005B6042", 5);
            Assert.Equal(0x42, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Fact]
        public async Task JumpI_ShouldNotJumpWhenConditionFalse()
        {
            // PUSH1 0x00, PUSH1 0x06, JUMPI, PUSH1 0x11, STOP, JUMPDEST, PUSH1 0x42
            // Condition is 0 (false), don't jump, push 0x11
            var program = await ExecuteProgram("6000600657601100", 4);
            Assert.Equal(0x11, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Fact]
        public async Task Pc_ShouldReturnProgramCounter()
        {
            // PUSH1 0x00, POP, PC -> PC should be 3
            var program = await ExecuteProgram("60005058", 3);
            Assert.Equal(3, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        #endregion

        #region Keccak256

        [Fact]
        public async Task Keccak256_ShouldHashCorrectly()
        {
            // Store 0x01 at memory[0], hash 1 byte from position 0
            // PUSH1 0x01, PUSH1 0x00, MSTORE8, PUSH1 0x01, PUSH1 0x00, SHA3
            var program = await ExecuteProgram("60016000536001600020", 6);
            var result = program.StackPeek().ToHex();
            // keccak256(0x01) = 0x5fe7f977e71dba2ea1a68e21057beebb9be2ac30c6410aa38d4f3fbe41dcffd2
            Assert.Equal("5fe7f977e71dba2ea1a68e21057beebb9be2ac30c6410aa38d4f3fbe41dcffd2", result.ToLower());
        }

        [Fact]
        public async Task Keccak256_EmptyInput_ShouldHashCorrectly()
        {
            // Hash 0 bytes -> keccak256("")
            var program = await ExecuteProgram("6000600020", 3);
            var result = program.StackPeek().ToHex();
            // keccak256("") = 0xc5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a470
            Assert.Equal("c5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a470", result.ToLower());
        }

        #endregion

        #region Return and Revert

        [Fact]
        public async Task Return_ShouldReturnData()
        {
            // Store 0x42 at memory[0], return 32 bytes from position 0
            // PUSH1 0x42, PUSH1 0x00, MSTORE, PUSH1 0x20, PUSH1 0x00, RETURN (0xF3)
            var program = await ExecuteProgram("604260005260206000F3", 100);
            Assert.True(program.Stopped);
            Assert.NotNull(program.ProgramResult.Result);
            Assert.Equal(32, program.ProgramResult.Result.Length);
        }

        [Fact]
        public async Task Revert_ShouldSetRevertFlag()
        {
            // Store 0x42 at memory[0], revert with 4 bytes from position 0
            // PUSH1 0x42, PUSH1 0x00, MSTORE, PUSH1 0x04, PUSH1 0x00, REVERT (0xFD)
            var program = await ExecuteProgram("604260005260046000FD", 100);
            Assert.True(program.Stopped);
            Assert.True(program.ProgramResult.IsRevert);
        }

        [Fact]
        public async Task Stop_ShouldStopExecution()
        {
            // PUSH1 0x42, STOP, PUSH1 0xFF (should not execute)
            var program = await ExecuteProgram("60420060FF", 100);
            Assert.True(program.Stopped);
            Assert.Equal(0x42, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        #endregion

        #region Context Operations

        [Fact]
        public async Task CallDataLoad_ShouldLoadCalldata()
        {
            var callData = "0000000000000000000000000000000000000000000000000000000000000042".HexToByteArray();
            var program = await ExecuteProgramWithContext("600035", 2, callData);
            Assert.Equal(0x42, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Fact]
        public async Task CallDataSize_ShouldReturnSize()
        {
            var callData = "0102030405".HexToByteArray();
            var program = await ExecuteProgramWithContext("36", 1, callData);
            Assert.Equal(5, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Fact]
        public async Task CallDataCopy_ShouldCopyToMemory()
        {
            var callData = "00000000000000000000000000000000000000000000000000000000000000FF".HexToByteArray();
            // Copy 32 bytes from calldata[0] to memory[0], then load
            // PUSH1 0x20, PUSH1 0x00, PUSH1 0x00, CALLDATACOPY, PUSH1 0x00, MLOAD
            var program = await ExecuteProgramWithContext("60206000600037600051", 6, callData);
            Assert.Equal(0xFF, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Fact]
        public async Task CodeSize_ShouldReturnSize()
        {
            // CODESIZE for bytecode "38" (1 byte)
            var program = await ExecuteProgram("38", 1);
            Assert.Equal(1, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Fact]
        public async Task CodeCopy_ShouldCopyToMemory()
        {
            // Bytecode: PUSH1 0x01, PUSH1 0x00, PUSH1 0x00, CODECOPY, PUSH1 0x00, MLOAD
            // Copy 1 byte of code to memory[0]
            var program = await ExecuteProgram("60016000600039600051", 6);
            // First byte is 0x60 (PUSH1 opcode)
            var result = program.StackPeekAtAndConvertToUBigInteger(0);
            Assert.True(result > 0);
        }

        #endregion

        #region Full Contract Execution Scenarios

        [Fact]
        public async Task SimpleAddition_UsingStack()
        {
            // Simple addition using stack: push two values, add them
            // PUSH1 0x05, PUSH1 0x07, ADD
            var program = await ExecuteProgram("6005600701", 3);
            Assert.Equal(12, (int)program.StackPeekAtAndConvertToUBigInteger(0)); // 5 + 7 = 12
        }

        [Fact]
        public async Task MultipleArithmeticOperations()
        {
            // (5 + 7) * 2 = 24
            // PUSH1 0x02, PUSH1 0x07, PUSH1 0x05, ADD, MUL
            var program = await ExecuteProgram("6002600760050102", 5);
            Assert.Equal(24, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Fact]
        public async Task ConditionalJump_NotTaken()
        {
            // If 0, don't jump - continue to push 0x11
            // PUSH1 0x00, PUSH1 0x07, JUMPI, PUSH1 0x11, STOP, JUMPDEST, PUSH1 0x22
            var program = await ExecuteProgram("6000600757601100", 4);
            Assert.Equal(0x11, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Fact]
        public async Task NestedDupAndSwap()
        {
            // Test simple stack manipulation: PUSH 1, PUSH 2, DUP1, SWAP1
            // Stack after pushes: [1, 2] with 2 on top
            // After DUP1: [1, 2, 2] with 2 on top (duplicated top)
            // After SWAP1: [1, 2, 2] with 2 on top (swap identical values)
            var program = await ExecuteProgram("600160028090", 4);
            Assert.Equal(2, (int)program.StackPeekAtAndConvertToUBigInteger(0));
            Assert.Equal(2, (int)program.StackPeekAtAndConvertToUBigInteger(1));
        }

        #endregion

        #region Edge Cases

        [Fact]
        public async Task DivisionByZero_ShouldReturnZero()
        {
            // 10 / 0 = 0 in EVM
            var program = await ExecuteProgram("6000600A04", 3);
            Assert.Equal(0, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Fact]
        public async Task ModByZero_ShouldReturnZero()
        {
            // 10 % 0 = 0 in EVM
            var program = await ExecuteProgram("6000600A06", 3);
            Assert.Equal(0, (int)program.StackPeekAtAndConvertToUBigInteger(0));
        }

        [Fact]
        public async Task Overflow_ShouldWrap()
        {
            // Test that operations wrap correctly (no exception)
            // MAX_UINT - 1 + 5 = 3 (wraps)
            var maxUintMinus1 = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFE";
            var program = await ExecuteProgram($"60057F{maxUintMinus1}01", 3);
            // Result should be 3 (overflow wrap)
            var result = program.StackPeekAtAndConvertToUBigInteger(0);
            Assert.Equal(3, result);
        }

        [Fact]
        public async Task SignedDivision_NegativeNumbers()
        {
            // Test signed division: -8 / 2 = -4
            // -8 in two's complement 256-bit
            var negEight = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF8";
            var program = await ExecuteProgram($"60027F{negEight}05", 3);
            var result = program.StackPeekAtAndConvertToBigInteger(0);
            Assert.Equal(-4, result);
        }

        [Fact]
        public async Task SignedModulo_NegativeNumbers()
        {
            // Test signed modulo: -8 % 3 = -2
            var negEight = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF8";
            var program = await ExecuteProgram($"60037F{negEight}07", 3);
            var result = program.StackPeekAtAndConvertToBigInteger(0);
            Assert.Equal(-2, result);
        }

        #endregion

        #region Helper Methods

        private async Task<Program> ExecuteProgram(string hexBytecode, int maxSteps = 100)
        {
            var bytecode = hexBytecode.HexToByteArray();
            var program = new Program(bytecode);
            program.GasRemaining = 1000000;

            for (int i = 0; i < maxSteps && !program.Stopped; i++)
            {
                await _vm.StepAsync(program, i);
            }

            return program;
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

            for (int i = 0; i < maxSteps && !program.Stopped; i++)
            {
                await _vm.StepAsync(program, i);
            }

            return program;
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
