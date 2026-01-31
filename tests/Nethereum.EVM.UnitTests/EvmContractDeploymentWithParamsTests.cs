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
    public class EvmContractDeploymentWithParamsTests
    {
        private readonly EVMSimulator _vm = new EVMSimulator();

        [Fact]
        public async Task ShouldReadConstructorParameterFromEndOfInitCode()
        {
            // This test verifies that constructor parameters appended to init code
            // can be correctly read using CODESIZE and CODECOPY
            //
            // CODECOPY stack order: [size, offset, destOffset] with destOffset on top
            // We need: destOffset=0, offset=CODESIZE-32, size=32
            //
            // Bytecode design:
            // 38       CODESIZE      -> [CS]
            // 6020     PUSH1 32      -> [CS, 32]
            // 80       DUP1          -> [CS, 32, 32]
            // 91       SWAP2         -> [32, 32, CS]
            // 03       SUB           -> [32, CS-32] (SUB: top - second = CS - 32)
            // 6000     PUSH1 0       -> [32, CS-32, 0]
            // 39       CODECOPY      -> copies 32 bytes from offset CS-32 to memory[0]
            // 6000     PUSH1 0       -> [0]
            // 51       MLOAD         -> [value_from_memory]
            // 6000     PUSH1 0       -> [value, 0]
            // 55       SSTORE        -> stores value at slot 0
            // 6000     PUSH1 0       -> [0] (STOP opcode for runtime)
            // 6000     PUSH1 0       -> [0, 0]
            // 53       MSTORE8       -> store byte 0x00 at memory[0]
            // 6001     PUSH1 1       -> [1]
            // 6000     PUSH1 0       -> [1, 0]
            // F3       RETURN        -> return 1 byte from memory[0]

            var initCodeHex = "386020809103600039600051600055600060005360016000F3";
            var initCode = initCodeHex.HexToByteArray();

            // Constructor parameter: uint256 value = 0x1234
            // Create 32-byte big-endian representation
            var paramPadded = new byte[32];
            paramPadded[30] = 0x12;
            paramPadded[31] = 0x34;

            // Combine initCode + constructorParam
            var fullInitCode = new byte[initCode.Length + paramPadded.Length];
            Array.Copy(initCode, 0, fullInitCode, 0, initCode.Length);
            Array.Copy(paramPadded, 0, fullInitCode, initCode.Length, paramPadded.Length);

            // Setup execution context
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var deployerAddress = "0x1111111111111111111111111111111111111111";
            var contractAddress = "0x2222222222222222222222222222222222222222";

            var callInput = new CallInput
            {
                From = deployerAddress,
                To = contractAddress,
                Data = fullInitCode.ToHex(true),
                Value = new HexBigInteger(0),
                Gas = new HexBigInteger(1000000),
                ChainId = new HexBigInteger(1)
            };

            var programContext = new ProgramContext(
                callInput,
                executionStateService,
                deployerAddress
            );

            var program = new Program(fullInitCode, programContext);
            program.GasRemaining = 1000000;

            // Execute
            await _vm.ExecuteWithCallStackAsync(program, traceEnabled: false);

            // Verify execution succeeded and returned runtime code
            Assert.False(program.ProgramResult.IsRevert,
                $"Execution reverted: {program.ProgramResult.GetRevertMessage()}");
            Assert.NotNull(program.ProgramResult.Result);
            Assert.True(program.ProgramResult.Result.Length > 0, "No runtime code returned");

            // Verify storage slot 0 was set to the constructor parameter
            var storageValue = await programContext.GetFromStorageAsync(BigInteger.Zero);
            Assert.NotNull(storageValue);

            var storedValue = new BigInteger(storageValue, isUnsigned: true, isBigEndian: true);
            Assert.Equal(new BigInteger(0x1234), storedValue);
        }

        [Fact]
        public async Task CodeSize_ShouldReturnFullInitCodeLengthIncludingParams()
        {
            // Test that CODESIZE returns the full length of init code including constructor params
            // Bytecode: CODESIZE, PUSH 0, MSTORE, PUSH 32, PUSH 0, RETURN
            // 38 6000 52 6020 6000 F3

            var initCodeHex = "3860005260206000F3";
            var initCode = initCodeHex.HexToByteArray();

            // Add 32 bytes of "constructor params"
            var constructorParams = new byte[32];
            constructorParams[31] = 0x42;

            var fullInitCode = new byte[initCode.Length + constructorParams.Length];
            Array.Copy(initCode, 0, fullInitCode, 0, initCode.Length);
            Array.Copy(constructorParams, 0, fullInitCode, initCode.Length, constructorParams.Length);

            // Need context for EVMSimulator
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callInput = new CallInput
            {
                From = "0x1111111111111111111111111111111111111111",
                To = "0x2222222222222222222222222222222222222222",
                Value = new HexBigInteger(0),
                Gas = new HexBigInteger(100000),
                ChainId = new HexBigInteger(1)
            };

            var programContext = new ProgramContext(
                callInput,
                executionStateService,
                "0x1111111111111111111111111111111111111111"
            );

            var program = new Program(fullInitCode, programContext);
            program.GasRemaining = 100000;

            await _vm.ExecuteWithCallStackAsync(program, traceEnabled: false);

            Assert.False(program.ProgramResult.IsRevert);
            Assert.NotNull(program.ProgramResult.Result);
            Assert.Equal(32, program.ProgramResult.Result.Length);

            // The return data should contain CODESIZE = initCode.Length + constructorParams.Length
            var returnedCodeSize = new BigInteger(program.ProgramResult.Result, isUnsigned: true, isBigEndian: true);
            Assert.Equal(fullInitCode.Length, (int)returnedCodeSize);
        }

        [Fact]
        public async Task CodeCopy_ShouldCopyConstructorParamsFromEndOfInitCode()
        {
            // Test that CODECOPY can access constructor params at the end of init code
            // CODECOPY stack: [size, offset, destOffset] with destOffset on top
            //
            // Bytecode:
            // 38       CODESIZE      -> [CS]
            // 6020     PUSH1 32      -> [CS, 32]
            // 80       DUP1          -> [CS, 32, 32]
            // 91       SWAP2         -> [32, 32, CS]
            // 03       SUB           -> [32, CS-32]
            // 6000     PUSH1 0       -> [32, CS-32, 0]
            // 39       CODECOPY      -> copies param to memory[0]
            // 6020     PUSH1 32      -> [32]
            // 6000     PUSH1 0       -> [32, 0]
            // F3       RETURN        -> return 32 bytes from memory[0]

            var initCode = "38602080910360003960206000F3".HexToByteArray();

            // Constructor param: 0xDEADBEEF (left-padded to 32 bytes)
            var constructorParams = new byte[32];
            constructorParams[28] = 0xDE;
            constructorParams[29] = 0xAD;
            constructorParams[30] = 0xBE;
            constructorParams[31] = 0xEF;

            var fullInitCode = new byte[initCode.Length + constructorParams.Length];
            Array.Copy(initCode, 0, fullInitCode, 0, initCode.Length);
            Array.Copy(constructorParams, 0, fullInitCode, initCode.Length, constructorParams.Length);

            // Need context for EVMSimulator
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callInput = new CallInput
            {
                From = "0x1111111111111111111111111111111111111111",
                To = "0x2222222222222222222222222222222222222222",
                Value = new HexBigInteger(0),
                Gas = new HexBigInteger(100000),
                ChainId = new HexBigInteger(1)
            };

            var programContext = new ProgramContext(
                callInput,
                executionStateService,
                "0x1111111111111111111111111111111111111111"
            );

            var program = new Program(fullInitCode, programContext);
            program.GasRemaining = 100000;

            await _vm.ExecuteWithCallStackAsync(program, traceEnabled: false);

            Assert.False(program.ProgramResult.IsRevert,
                $"Execution reverted: {program.ProgramResult.GetRevertMessage()}");
            Assert.NotNull(program.ProgramResult.Result);
            Assert.Equal(32, program.ProgramResult.Result.Length);

            // Verify the returned data matches the constructor params
            Assert.Equal(constructorParams, program.ProgramResult.Result);
        }

        [Fact]
        public async Task ShouldDeployContractWithMultipleConstructorParams()
        {
            // Test deployment with two uint256 constructor parameters
            // This simulates: constructor(uint256 a, uint256 b) { slot0 = a; slot1 = b; }
            //
            // Bytecode:
            // Copy 64 bytes (two params) from end of code to memory[0]
            // Store memory[0:32] to slot 0
            // Store memory[32:64] to slot 1
            // Return minimal runtime code
            //
            // 38       CODESIZE       -> [CS]
            // 6040     PUSH1 64       -> [CS, 64]
            // 80       DUP1           -> [CS, 64, 64]
            // 91       SWAP2          -> [64, 64, CS]
            // 03       SUB            -> [64, CS-64]
            // 6000     PUSH1 0        -> [64, CS-64, 0]
            // 39       CODECOPY       -> copies 64 bytes to memory[0]
            // 6000     PUSH1 0        -> [0]
            // 51       MLOAD          -> [param1]
            // 6000     PUSH1 0        -> [param1, 0]
            // 55       SSTORE         -> stores param1 at slot 0
            // 6020     PUSH1 32       -> [32]
            // 51       MLOAD          -> [param2]
            // 6001     PUSH1 1        -> [param2, 1]
            // 55       SSTORE         -> stores param2 at slot 1
            // 6000     PUSH1 0        -> [0]
            // 6040     PUSH1 64       -> [0, 64]
            // 53       MSTORE8        -> store 0 at memory[64]
            // 6001     PUSH1 1        -> [1]
            // 6040     PUSH1 64       -> [1, 64]
            // F3       RETURN         -> return 1 byte from memory[64]

            var initCodeHex = "386040809103600039600051600055602051600155600060405360016040F3";
            var initCode = initCodeHex.HexToByteArray();

            // Two constructor params
            var param1 = new byte[32];
            param1[31] = 0x11; // value = 0x11

            var param2 = new byte[32];
            param2[31] = 0x22; // value = 0x22

            var fullInitCode = new byte[initCode.Length + 64];
            Array.Copy(initCode, 0, fullInitCode, 0, initCode.Length);
            Array.Copy(param1, 0, fullInitCode, initCode.Length, 32);
            Array.Copy(param2, 0, fullInitCode, initCode.Length + 32, 32);

            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var deployerAddress = "0x1111111111111111111111111111111111111111";
            var contractAddress = "0x2222222222222222222222222222222222222222";

            var callInput = new CallInput
            {
                From = deployerAddress,
                To = contractAddress,
                Data = fullInitCode.ToHex(true),
                Value = new HexBigInteger(0),
                Gas = new HexBigInteger(1000000),
                ChainId = new HexBigInteger(1)
            };

            var programContext = new ProgramContext(
                callInput,
                executionStateService,
                deployerAddress
            );

            var program = new Program(fullInitCode, programContext);
            program.GasRemaining = 1000000;

            await _vm.ExecuteWithCallStackAsync(program, traceEnabled: false);

            Assert.False(program.ProgramResult.IsRevert,
                $"Execution reverted: {program.ProgramResult.GetRevertMessage()}");

            // Verify slot 0 = 0x11
            var slot0Value = await programContext.GetFromStorageAsync(BigInteger.Zero);
            Assert.NotNull(slot0Value);
            Assert.Equal(new BigInteger(0x11), new BigInteger(slot0Value, isUnsigned: true, isBigEndian: true));

            // Verify slot 1 = 0x22
            var slot1Value = await programContext.GetFromStorageAsync(BigInteger.One);
            Assert.NotNull(slot1Value);
            Assert.Equal(new BigInteger(0x22), new BigInteger(slot1Value, isUnsigned: true, isBigEndian: true));
        }
    }
}
