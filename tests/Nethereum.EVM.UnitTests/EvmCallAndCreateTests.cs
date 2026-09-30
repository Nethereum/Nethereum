using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Precompiles;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class EvmCallAndCreateTests
    {
        private readonly EVMSimulator _vm = new EVMSimulator(DefaultHardforkConfigs.Cancun);

        #region RETURNDATASIZE and RETURNDATACOPY Tests

        [Fact]
        public async Task ReturnDataSize_BeforeAnyCall_ShouldBeZero()
        {
            var program = await ExecuteProgram("3D");
            Assert.Equal(0, (int)program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task ReturnDataSize_AfterCallToPrecompile_ShouldReturnSize()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callInput = CreateDefaultCallInput();
            var programContext = new ProgramContext(callInput, executionStateService, callInput.From);

            var bytecode = string.Concat(
                "6000",
                "6000",
                "6020",
                "6000",
                "6000",
                "7304",
                "61FFFF",
                "F1",
                "50",
                "3D"
            ).HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.ProgramResult.LastCallReturnData == null || program.ProgramResult.LastCallReturnData.Length == 32 || program.StackPeekAtU256(0) >= 0);
        }

        [Fact]
        public async Task ReturnDataCopy_ShouldCopyToMemory()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callInput = CreateDefaultCallInput();
            var programContext = new ProgramContext(callInput, executionStateService, callInput.From);

            var bytecode = string.Concat(
                "6000",
                "6000",
                "6020",
                "6000",
                "6000",
                "7304",
                "61FFFF",
                "F1",
                "50",
                "6020",
                "6000",
                "6000",
                "3E",
                "600051"
            ).HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
        }

        [Fact]
        public async Task StaticCall_ToIdentityPrecompile_ShouldSetReturnData()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callerAddress = "0x1111111111111111111111111111111111111111";
            executionStateService.SetInitialChainBalance(callerAddress, EvmUInt256BigIntegerExtensions.FromBigInteger(BigInteger.Parse("1000000000000000000")));

            var callInput = CreateDefaultCallInput(callerAddress);
            var programContext = new ProgramContext(callInput, executionStateService, callerAddress);

            var testData = "DEADBEEF00000000000000000000000000000000000000000000000000000000";

            var bytecodeBuilder = new System.Text.StringBuilder();

            bytecodeBuilder.Append("7F" + testData);
            bytecodeBuilder.Append("6000");
            bytecodeBuilder.Append("52");

            bytecodeBuilder.Append("6020");
            bytecodeBuilder.Append("6040");
            bytecodeBuilder.Append("6020");
            bytecodeBuilder.Append("6000");
            bytecodeBuilder.Append("6004");
            bytecodeBuilder.Append("61FFFF");
            bytecodeBuilder.Append("FA");

            bytecodeBuilder.Append("3D");

            bytecodeBuilder.Append("00");

            var bytecode = bytecodeBuilder.ToString().HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped, "Program should have stopped");
            Assert.True(program.ProgramResult.Exception == null,
                $"Program should not have exception but got: {program.ProgramResult.Exception?.Message}\n{program.ProgramResult.Exception?.StackTrace}");

            var stackItems = program.GetCurrentStackAsHex();
            Assert.True(stackItems.Count >= 2,
                $"Stack should have at least 2 items (success, returnDataSize) but has {stackItems.Count}");

            var returnDataSize = program.StackPeekAtU256(0);
            Assert.Equal(32, (int)returnDataSize);

            var success = program.StackPeekAtU256(1);
            Assert.Equal(1, (int)success);
        }

        [Fact]
        public async Task StaticCall_ToEcrecover_WithUserOpSignature_ShouldReturnRecoveredAddress()
        {

            var userOpHash = "0x25e6c2c52e4ea62ca1faea7124bf4ed924fe1662f3cc751694edaa24d7d98258".HexToByteArray();
            var signature = "0x974fca31f645caffd93b76b6631bcc04e030ef6c24b836e63867ec31d9befd2a60d628c60862788da369a209a18351896dcc1b205b018bdc666c7a5e575ddd181c".HexToByteArray();
            var expectedOwner = "0x6813Eb9362372EEF6200f3b1dbC3f819671cBA69";

            var prefixedHash = new Nethereum.Util.EthereumMessageHasher().HashPrefixedMessage(userOpHash);
            var expectedPrefixedHashHex = "0x7f28bfb6bbd565a8349ff72003de88a7f7e9aeff80f2491f2f27476a199bf26b";
            Assert.Equal(expectedPrefixedHashHex.ToLower(), prefixedHash.ToHex(true).ToLower());

            var r = signature.Take(32).ToArray();
            var s = signature.Skip(32).Take(32).ToArray();
            var v = signature[64];

            Assert.True(v == 27 || v == 28, $"V should be 27 or 28 but was {v}");

            var ecrecoverInput = new byte[128];
            Array.Copy(prefixedHash, 0, ecrecoverInput, 0, 32);
            ecrecoverInput[63] = v;
            Array.Copy(r, 0, ecrecoverInput, 64, 32);
            Array.Copy(s, 0, ecrecoverInput, 96, 32);

            var precompiles = new Nethereum.EVM.Execution.EvmPreCompiledContractsExecution();
            var precompileResult = precompiles.ExecutePreCompile("0x0000000000000000000000000000000000000001", ecrecoverInput);
            var recoveredFromPrecompile = precompileResult.ToHex();
            Assert.EndsWith(expectedOwner.ToLower().Substring(2), recoveredFromPrecompile.ToLower(),
                StringComparison.OrdinalIgnoreCase);

            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callerAddress = "0x1111111111111111111111111111111111111111";
            executionStateService.SetInitialChainBalance(callerAddress, EvmUInt256BigIntegerExtensions.FromBigInteger(BigInteger.Parse("1000000000000000000")));

            var callInput = CreateDefaultCallInput(callerAddress);
            var programContext = new ProgramContext(callInput, executionStateService, callerAddress);

            var inputHex = ecrecoverInput.ToHex(false);
            var bytecodeBuilder = new System.Text.StringBuilder();

            bytecodeBuilder.Append("7F" + inputHex.Substring(0, 64));
            bytecodeBuilder.Append("6000");
            bytecodeBuilder.Append("52");

            bytecodeBuilder.Append("7F" + inputHex.Substring(64, 64));
            bytecodeBuilder.Append("6020");
            bytecodeBuilder.Append("52");

            bytecodeBuilder.Append("7F" + inputHex.Substring(128, 64));
            bytecodeBuilder.Append("6040");
            bytecodeBuilder.Append("52");

            bytecodeBuilder.Append("7F" + inputHex.Substring(192, 64));
            bytecodeBuilder.Append("6060");
            bytecodeBuilder.Append("52");

            bytecodeBuilder.Append("6020");
            bytecodeBuilder.Append("618100");
            bytecodeBuilder.Append("6080");
            bytecodeBuilder.Append("6000");
            bytecodeBuilder.Append("6001");
            bytecodeBuilder.Append("61FFFF");
            bytecodeBuilder.Append("FA");

            bytecodeBuilder.Append("3D");

            bytecodeBuilder.Append("6020");
            bytecodeBuilder.Append("6000");
            bytecodeBuilder.Append("618120");
            bytecodeBuilder.Append("3E");

            bytecodeBuilder.Append("618120");
            bytecodeBuilder.Append("51");

            bytecodeBuilder.Append("00");

            var bytecode = bytecodeBuilder.ToString().HexToByteArray();
            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped, "Program should have stopped");
            Assert.True(program.ProgramResult.Exception == null,
                $"Exception: {program.ProgramResult.Exception?.Message}\n{program.ProgramResult.Exception?.StackTrace}");

            var stackItems = program.GetCurrentStackAsHex();
            Assert.True(stackItems.Count >= 3,
                $"Stack should have 3 items but has {stackItems.Count}");

            var recoveredAddrBytes = program.StackPeekAt(0);
            var returnDataSize = program.StackPeekAtU256(1);
            var success = program.StackPeekAtU256(2);

            Assert.Equal(1, (int)success);
            Assert.Equal(32, (int)returnDataSize);

            var recoveredAddrHex = recoveredAddrBytes.ToHex();
            Assert.EndsWith(expectedOwner.ToLower().Substring(2), recoveredAddrHex.ToLower(),
                StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task StaticCall_ToEcrecover_ShouldReturnRecoveredAddress()
        {

            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callerAddress = "0x1111111111111111111111111111111111111111";
            executionStateService.SetInitialChainBalance(callerAddress, EvmUInt256BigIntegerExtensions.FromBigInteger(BigInteger.Parse("1000000000000000000")));

            var callInput = CreateDefaultCallInput(callerAddress);
            var programContext = new ProgramContext(callInput, executionStateService, callerAddress);

            var privateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
            var expectedAddress = new EthECKey(privateKey).GetPublicAddress();
            var message = "Hello, Ethereum!";

            var signer = new EthereumMessageSigner();
            var signature = signer.EncodeUTF8AndSign(message, new EthECKey(privateKey));

            var messageHash = signer.HashPrefixedMessage(System.Text.Encoding.UTF8.GetBytes(message));
            var signatureBytes = signature.HexToByteArray();

            var r = signatureBytes.Take(32).ToArray();
            var s = signatureBytes.Skip(32).Take(32).ToArray();
            var v = signatureBytes.Skip(64).First();

            var ecrecoverInput = new byte[128];
            Array.Copy(messageHash, 0, ecrecoverInput, 0, 32);
            ecrecoverInput[63] = v;
            Array.Copy(r, 0, ecrecoverInput, 64, 32);
            Array.Copy(s, 0, ecrecoverInput, 96, 32);


            var inputHex = ecrecoverInput.ToHex(false);

            var bytecodeBuilder = new System.Text.StringBuilder();

            bytecodeBuilder.Append("7F" + inputHex.Substring(0, 64));
            bytecodeBuilder.Append("6000");
            bytecodeBuilder.Append("52");

            bytecodeBuilder.Append("7F" + inputHex.Substring(64, 64));
            bytecodeBuilder.Append("6020");
            bytecodeBuilder.Append("52");

            bytecodeBuilder.Append("7F" + inputHex.Substring(128, 64));
            bytecodeBuilder.Append("6040");
            bytecodeBuilder.Append("52");

            bytecodeBuilder.Append("7F" + inputHex.Substring(192, 64));
            bytecodeBuilder.Append("6060");
            bytecodeBuilder.Append("52");

            bytecodeBuilder.Append("6020");
            bytecodeBuilder.Append("618100");
            bytecodeBuilder.Append("6080");
            bytecodeBuilder.Append("6000");
            bytecodeBuilder.Append("6001");
            bytecodeBuilder.Append("61FFFF");
            bytecodeBuilder.Append("FA");

            bytecodeBuilder.Append("50");

            bytecodeBuilder.Append("3D");

            bytecodeBuilder.Append("6020");
            bytecodeBuilder.Append("6000");
            bytecodeBuilder.Append("618100");
            bytecodeBuilder.Append("3E");

            bytecodeBuilder.Append("618100");
            bytecodeBuilder.Append("51");

            bytecodeBuilder.Append("00");

            var bytecode = bytecodeBuilder.ToString().HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped, "Program should have stopped");
            Assert.False(program.ProgramResult.IsRevert, "Program should not revert");
            Assert.True(program.ProgramResult.Exception == null,
                $"Program should not have exception but got: {program.ProgramResult.Exception?.Message}");

            var stackItems = program.GetCurrentStackAsHex();
            Assert.True(stackItems.Count >= 2,
                $"Stack should have at least 2 items but has {stackItems.Count}. " +
                $"LastCallReturnData: {(program.ProgramResult.LastCallReturnData?.ToHex() ?? "null")}. " +
                $"Exception: {program.ProgramResult.Exception?.Message ?? "none"}");

            var recoveredAddressBytes = program.StackPeekAt(0);
            var returnDataSize = program.StackPeekAtU256(1);

            Assert.Equal(32, (int)returnDataSize);

            var recoveredAddressHex = recoveredAddressBytes.ToHex();
            var expectedAddressHex = expectedAddress.ToLower().RemoveHexPrefix();

            Assert.EndsWith(expectedAddressHex.Substring(2), recoveredAddressHex.ToLower(),
                StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region CREATE Tests

        [Fact]
        public async Task Create_SimpleContract_ShouldReturnAddress()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var deployerAddress = "0x1111111111111111111111111111111111111111";
            executionStateService.SetInitialChainBalance(deployerAddress, EvmUInt256BigIntegerExtensions.FromBigInteger(BigInteger.Parse("1000000000000000000")));

            var callInput = new CallInput
            {
                From = deployerAddress,
                To = deployerAddress,
                Data = "".ToHexUTF8(),
                Value = new HexBigInteger(0),
                Gas = new HexBigInteger(1000000)
            };

            var programContext = new ProgramContext(callInput, executionStateService, deployerAddress);

            var initCode = "6042600052602060006000F0";
            var initCodeHex = "60426000526001601FF3";
            var initCodeLength = initCodeHex.Length / 2;

            var bytecode = string.Concat(
                "69" + initCodeHex,
                "6000",
                "52",
                $"60{initCodeLength:X2}",
                "600A",
                "6000",
                "F0"
            ).HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
        }

        [Fact]
        public async Task Create_WithValue_ShouldTransferEther()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var deployerAddress = "0x1111111111111111111111111111111111111111";
            executionStateService.SetInitialChainBalance(deployerAddress, EvmUInt256BigIntegerExtensions.FromBigInteger(BigInteger.Parse("1000000000000000000")));

            var callInput = new CallInput
            {
                From = deployerAddress,
                To = deployerAddress,
                Data = "".ToHexUTF8(),
                Value = new HexBigInteger(0),
                Gas = new HexBigInteger(1000000)
            };

            var programContext = new ProgramContext(callInput, executionStateService, deployerAddress);

            var initCodeHex = "60426000526001601FF3";
            var initCodeLength = initCodeHex.Length / 2;

            var bytecode = string.Concat(
                "69" + initCodeHex,
                "6000",
                "52",
                $"60{initCodeLength:X2}",
                "600A",
                "6064",
                "F0"
            ).HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
        }

        [Fact]
        public async Task Create_EmptyInitCode_ShouldReturnAddressWithEmptyCode()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var deployerAddress = "0x1111111111111111111111111111111111111111";
            executionStateService.SetInitialChainBalance(deployerAddress, EvmUInt256BigIntegerExtensions.FromBigInteger(BigInteger.Parse("1000000000000000000")));

            var callInput = CreateDefaultCallInput(deployerAddress);
            var programContext = new ProgramContext(callInput, executionStateService, deployerAddress);

            var bytecode = "60006000600060006000F0".HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
        }

        #endregion

        #region CREATE2 Tests

        [Fact]
        public async Task Create2_WithSalt_ShouldReturnDeterministicAddress()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var deployerAddress = "0x1111111111111111111111111111111111111111";
            executionStateService.SetInitialChainBalance(deployerAddress, EvmUInt256BigIntegerExtensions.FromBigInteger(BigInteger.Parse("1000000000000000000")));

            var callInput = CreateDefaultCallInput(deployerAddress);
            var programContext = new ProgramContext(callInput, executionStateService, deployerAddress);

            var initCodeHex = "60426000526001601FF3";
            var initCodeLength = initCodeHex.Length / 2;
            var salt = "0000000000000000000000000000000000000000000000000000000000000001";

            var bytecode = string.Concat(
                "69" + initCodeHex,
                "6000",
                "52",
                "7F" + salt,
                $"60{initCodeLength:X2}",
                "600A",
                "6000",
                "F5"
            ).HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
        }

        [Fact]
        public async Task Create2_DifferentSalts_ShouldExecuteSuccessfully()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var deployerAddress = "0x1111111111111111111111111111111111111111";
            executionStateService.SetInitialChainBalance(deployerAddress, EvmUInt256BigIntegerExtensions.FromBigInteger(BigInteger.Parse("2000000000000000000")));

            var callInput = CreateDefaultCallInput(deployerAddress);
            var programContext = new ProgramContext(callInput, executionStateService, deployerAddress);

            var initCodeHex = "60426000526001601FF3";
            var initCodeLength = initCodeHex.Length / 2;

            var bytecode = string.Concat(
                "69" + initCodeHex,
                "6000",
                "52",
                "7F" + "0000000000000000000000000000000000000000000000000000000000000001",
                $"60{initCodeLength:X2}",
                "600A",
                "6000",
                "F5"
            ).HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 2000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
        }

        [Fact]
        public void Create2_AddressCalculation_ShouldMatchExpected()
        {
            var deployer = "0x0000000000000000000000000000000000000000".HexToByteArray();
            var salt = new byte[32];
            var initCodeHash = new Sha3Keccack().CalculateHash(new byte[0]);

            var preimage = new byte[1 + 20 + 32 + 32];
            preimage[0] = 0xff;
            Array.Copy(deployer, 0, preimage, 1, 20);
            Array.Copy(salt, 0, preimage, 21, 32);
            Array.Copy(initCodeHash, 0, preimage, 53, 32);

            var addressHash = new Sha3Keccack().CalculateHash(preimage);
            var address = new byte[20];
            Array.Copy(addressHash, 12, address, 0, 20);

            Assert.Equal(20, address.Length);
        }

        #endregion

        #region CALL Tests

        [Fact]
        public async Task Call_ToPrecompileIdentity_ShouldExecuteSuccessfully()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callerAddress = "0x1111111111111111111111111111111111111111";
            executionStateService.SetInitialChainBalance(callerAddress, EvmUInt256BigIntegerExtensions.FromBigInteger(BigInteger.Parse("1000000000000000000")));

            var callInput = CreateDefaultCallInput(callerAddress);
            var programContext = new ProgramContext(callInput, executionStateService, callerAddress);

            var bytecode = string.Concat(
                "7F" + "DEADBEEF00000000000000000000000000000000000000000000000000000000",
                "6000",
                "52",
                "6020",
                "6000",
                "6004",
                "6000",
                "6000",
                "7304",
                "61FFFF",
                "F1"
            ).HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
        }

        [Fact]
        public async Task Call_ToPrecompileSha256_ShouldExecuteSuccessfully()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callerAddress = "0x1111111111111111111111111111111111111111";
            executionStateService.SetInitialChainBalance(callerAddress, EvmUInt256BigIntegerExtensions.FromBigInteger(BigInteger.Parse("1000000000000000000")));

            var callInput = CreateDefaultCallInput(callerAddress);
            var programContext = new ProgramContext(callInput, executionStateService, callerAddress);

            var bytecode = string.Concat(
                "7F" + "68656c6c6f000000000000000000000000000000000000000000000000000000",
                "6000",
                "52",
                "6020",
                "6000",
                "6005",
                "6000",
                "6000",
                "7302",
                "61FFFF",
                "F1"
            ).HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
        }

        [Fact]
        public async Task Call_WithValueTransfer_ShouldTransferEther()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callerAddress = "0x1111111111111111111111111111111111111111";
            var targetAddress = "0x2222222222222222222222222222222222222222";

            executionStateService.SetInitialChainBalance(callerAddress, EvmUInt256BigIntegerExtensions.FromBigInteger(BigInteger.Parse("1000000000000000000")));
            executionStateService.SaveCode(targetAddress, "00".HexToByteArray());

            var callInput = CreateDefaultCallInput(callerAddress);
            var programContext = new ProgramContext(callInput, executionStateService, callerAddress);

            var bytecode = string.Concat(
                "6000",
                "6000",
                "6000",
                "6000",
                "6064",
                "73" + targetAddress.Substring(2),
                "61FFFF",
                "F1"
            ).HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
        }

        [Fact]
        public async Task StaticCall_ShouldNotAllowStateModification()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callerAddress = "0x1111111111111111111111111111111111111111";
            var targetAddress = "0x2222222222222222222222222222222222222222";

            executionStateService.SetInitialChainBalance(callerAddress, EvmUInt256BigIntegerExtensions.FromBigInteger(BigInteger.Parse("1000000000000000000")));
            executionStateService.SaveCode(targetAddress, "6042600055".HexToByteArray());

            var callInput = CreateDefaultCallInput(callerAddress);
            var programContext = new ProgramContext(callInput, executionStateService, callerAddress);

            var bytecode = string.Concat(
                "6000",
                "6000",
                "6000",
                "6000",
                "73" + targetAddress.Substring(2),
                "61FFFF",
                "FA"
            ).HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
        }

        [Fact]
        public async Task DelegateCall_ShouldExecuteInCallerContext()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callerAddress = "0x1111111111111111111111111111111111111111";
            var targetAddress = "0x2222222222222222222222222222222222222222";

            executionStateService.SetInitialChainBalance(callerAddress, EvmUInt256BigIntegerExtensions.FromBigInteger(BigInteger.Parse("1000000000000000000")));
            executionStateService.SaveCode(targetAddress, "6001600055".HexToByteArray());

            var callInput = CreateDefaultCallInput(callerAddress);
            var programContext = new ProgramContext(callInput, executionStateService, callerAddress);

            var bytecode = string.Concat(
                "6000",
                "6000",
                "6000",
                "6000",
                "73" + targetAddress.Substring(2),
                "61FFFF",
                "F4"
            ).HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
        }

        #endregion

        #region EXTCODEHASH Tests

        [Fact]
        public async Task ExtCodeHash_ForNonExistentAccount_ShouldReturnHash()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callerAddress = "0x1111111111111111111111111111111111111111";
            var targetAddress = "0x9999999999999999999999999999999999999999";

            var callInput = CreateDefaultCallInput(callerAddress);
            var programContext = new ProgramContext(callInput, executionStateService, callerAddress);

            var bytecode = string.Concat(
                "73" + targetAddress.Substring(2),
                "3F"
            ).HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
            var hash = program.StackPeek();
            Assert.Equal(32, hash.Length);
        }

        [Fact]
        public async Task ExtCodeHash_ForEmptyAccount_ShouldReturnEmptyCodeHash()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callerAddress = "0x1111111111111111111111111111111111111111";
            var targetAddress = "0x2222222222222222222222222222222222222222";

            executionStateService.SetInitialChainBalance(targetAddress, 1);
            executionStateService.SaveCode(targetAddress, new byte[0]);

            var callInput = CreateDefaultCallInput(callerAddress);
            var programContext = new ProgramContext(callInput, executionStateService, callerAddress);

            var bytecode = string.Concat(
                "73" + targetAddress.Substring(2),
                "3F"
            ).HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
        }

        [Fact]
        public async Task ExtCodeHash_ForContractAccount_ShouldReturnCodeHash()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callerAddress = "0x1111111111111111111111111111111111111111";
            var targetAddress = "0x2222222222222222222222222222222222222222";

            var contractCode = "6042600052602060006000F0".HexToByteArray();
            executionStateService.SaveCode(targetAddress, contractCode);

            var callInput = CreateDefaultCallInput(callerAddress);
            var programContext = new ProgramContext(callInput, executionStateService, callerAddress);

            var bytecode = string.Concat(
                "73" + targetAddress.Substring(2),
                "3F"
            ).HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
            var hash = program.StackPeek();
            Assert.Equal(32, hash.Length);
        }

        #endregion

        #region EXTCODESIZE Tests

        [Fact]
        public async Task ExtCodeSize_ForNonExistentAccount_ShouldReturnZero()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callerAddress = "0x1111111111111111111111111111111111111111";
            var targetAddress = "0x9999999999999999999999999999999999999999";

            var callInput = CreateDefaultCallInput(callerAddress);
            var programContext = new ProgramContext(callInput, executionStateService, callerAddress);

            var bytecode = string.Concat(
                "73" + targetAddress.Substring(2),
                "3B"
            ).HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
            var size = program.StackPeekAtU256(0);
            Assert.Equal(0, size);
        }

        [Fact]
        public async Task ExtCodeSize_ForContractAccount_ShouldReturnCodeLength()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callerAddress = "0x1111111111111111111111111111111111111111";
            var targetAddress = "0x2222222222222222222222222222222222222222";

            var contractCode = "6042600052602060006000F0".HexToByteArray();
            executionStateService.SaveCode(targetAddress, contractCode);

            var callInput = CreateDefaultCallInput(callerAddress);
            var programContext = new ProgramContext(callInput, executionStateService, callerAddress);

            var bytecode = string.Concat(
                "73" + targetAddress.Substring(2),
                "3B"
            ).HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
            var size = program.StackPeekAtU256(0);
            Assert.Equal(contractCode.Length, (int)size);
        }

        #endregion

        #region EXTCODECOPY Tests

        [Fact]
        public async Task ExtCodeCopy_ShouldCopyCodeToMemory()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callerAddress = "0x1111111111111111111111111111111111111111";
            var targetAddress = "0x2222222222222222222222222222222222222222";

            var contractCode = "60426000526020F3".HexToByteArray();
            executionStateService.SaveCode(targetAddress, contractCode);

            var callInput = CreateDefaultCallInput(callerAddress);
            var programContext = new ProgramContext(callInput, executionStateService, callerAddress);

            var codeLength = contractCode.Length;
            var bytecode = string.Concat(
                $"60{codeLength:X2}",
                "6000",
                "6000",
                "73" + targetAddress.Substring(2),
                "3C",
                "600051"
            ).HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task Revert_WithErrorData_ShouldReturnErrorData()
        {
            var program = await ExecuteProgram(
                "7F08C379A0000000000000000000000000000000000000000000000000000000006000526020600052604060006000FD",
                100);

            Assert.True(program.Stopped);
            Assert.True(program.ProgramResult.IsRevert);
            Assert.NotNull(program.ProgramResult.Result);
        }

        [Fact]
        public async Task Invalid_Opcode_ShouldStop()
        {
            var program = await ExecuteProgram("FE", 100);

            Assert.True(program.Stopped);
        }

        [Fact]
        public async Task StackUnderflow_ShouldStop()
        {
            var program = await ExecuteProgram("01", 100);

            Assert.True(program.Stopped);
        }

        [Fact]
        public void OutOfGas_ShouldThrowException()
        {
            var bytecode = "60016001".HexToByteArray();
            var program = new Program(bytecode);
            program.GasRemaining = 1;

            Assert.Throws<Nethereum.EVM.Exceptions.OutOfGasException>(() => program.UpdateGasUsed(5));
        }

        #endregion

        #region SELFDESTRUCT Tests

        [Fact]
        public async Task SelfDestruct_ShouldTransferBalance()
        {
            var nodeDataService = new MockNodeDataService();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var contractAddress = "0x1111111111111111111111111111111111111111";
            var beneficiaryAddress = "0x2222222222222222222222222222222222222222";

            executionStateService.SetInitialChainBalance(contractAddress, EvmUInt256BigIntegerExtensions.FromBigInteger(BigInteger.Parse("1000000000000000000")));

            var callInput = CreateDefaultCallInput(contractAddress);
            var programContext = new ProgramContext(callInput, executionStateService, contractAddress);

            var bytecode = string.Concat(
                "73" + beneficiaryAddress.Substring(2),
                "FF"
            ).HexToByteArray();

            var program = new Program(bytecode, programContext);
            program.GasRemaining = 1000000;

            await ExecuteProgramToEnd(program);

            Assert.True(program.Stopped);
        }

        #endregion

        #region Helper Methods

        private async Task<Program> ExecuteProgram(string hexBytecode, int maxSteps = 100)
        {
            var bytecode = hexBytecode.HexToByteArray();
            var program = new Program(bytecode);
            program.GasRemaining = 1000000;

            try
            {
                program = await _vm.ExecuteWithCallStackAsync(program, traceEnabled: false);
            }
            catch (Exception)
            {
                program.Stop();
            }

            return program;
        }

        private async Task ExecuteProgramToEnd(Program program, int maxSteps = 1000, bool throwOnError = false)
        {
            try
            {
                await _vm.ExecuteWithCallStackAsync(program, traceEnabled: false);
            }
            catch (Exception ex)
            {
                if (throwOnError)
                    throw;
                program.ProgramResult.Exception = ex;
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
                Gas = new HexBigInteger(1000000),
                ChainId = new HexBigInteger(1)
            };
        }

        #endregion
    }
}
