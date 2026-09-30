using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Precompiles;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class EvmContractDeploymentWithParamsTests
    {
        private readonly EVMSimulator _vm = new EVMSimulator(DefaultHardforkConfigs.Cancun);

        [Fact]
        public async Task ShouldReadConstructorParameterFromEndOfInitCode()
        {

            var initCodeHex = "386020809103600039600051600055600060005360016000F3";
            var initCode = initCodeHex.HexToByteArray();

            var paramPadded = new byte[32];
            paramPadded[30] = 0x12;
            paramPadded[31] = 0x34;

            var fullInitCode = new byte[initCode.Length + paramPadded.Length];
            Array.Copy(initCode, 0, fullInitCode, 0, initCode.Length);
            Array.Copy(paramPadded, 0, fullInitCode, initCode.Length, paramPadded.Length);

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
            Assert.NotNull(program.ProgramResult.Result);
            Assert.True(program.ProgramResult.Result.Length > 0, "No runtime code returned");

            var storageValue = await programContext.GetFromStorageAsync(EvmUInt256.Zero);
            Assert.NotNull(storageValue);

            var storedValue = new BigInteger(storageValue, isUnsigned: true, isBigEndian: true);
            Assert.Equal(new BigInteger(0x1234), storedValue);
        }

        [Fact]
        public async Task CodeSize_ShouldReturnFullInitCodeLengthIncludingParams()
        {

            var initCodeHex = "3860005260206000F3";
            var initCode = initCodeHex.HexToByteArray();

            var constructorParams = new byte[32];
            constructorParams[31] = 0x42;

            var fullInitCode = new byte[initCode.Length + constructorParams.Length];
            Array.Copy(initCode, 0, fullInitCode, 0, initCode.Length);
            Array.Copy(constructorParams, 0, fullInitCode, initCode.Length, constructorParams.Length);

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

            var returnedCodeSize = new BigInteger(program.ProgramResult.Result, isUnsigned: true, isBigEndian: true);
            Assert.Equal(fullInitCode.Length, (int)returnedCodeSize);
        }

        [Fact]
        public async Task CodeCopy_ShouldCopyConstructorParamsFromEndOfInitCode()
        {

            var initCode = "38602080910360003960206000F3".HexToByteArray();

            var constructorParams = new byte[32];
            constructorParams[28] = 0xDE;
            constructorParams[29] = 0xAD;
            constructorParams[30] = 0xBE;
            constructorParams[31] = 0xEF;

            var fullInitCode = new byte[initCode.Length + constructorParams.Length];
            Array.Copy(initCode, 0, fullInitCode, 0, initCode.Length);
            Array.Copy(constructorParams, 0, fullInitCode, initCode.Length, constructorParams.Length);

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

            Assert.Equal(constructorParams, program.ProgramResult.Result);
        }

        [Fact]
        public async Task ShouldDeployContractWithMultipleConstructorParams()
        {

            var initCodeHex = "386040809103600039600051600055602051600155600060405360016040F3";
            var initCode = initCodeHex.HexToByteArray();

            var param1 = new byte[32];
            param1[31] = 0x11;

            var param2 = new byte[32];
            param2[31] = 0x22;

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

            var slot0Value = await programContext.GetFromStorageAsync(EvmUInt256.Zero);
            Assert.NotNull(slot0Value);
            Assert.Equal(new BigInteger(0x11), new BigInteger(slot0Value, isUnsigned: true, isBigEndian: true));

            var slot1Value = await programContext.GetFromStorageAsync(EvmUInt256.One);
            Assert.NotNull(slot1Value);
            Assert.Equal(new BigInteger(0x22), new BigInteger(slot1Value, isUnsigned: true, isBigEndian: true));
        }
    }
}
