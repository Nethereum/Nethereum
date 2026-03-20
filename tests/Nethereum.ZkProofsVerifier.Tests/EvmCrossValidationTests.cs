using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.ZkProofsVerifier.Circom;
using Xunit;
using BigIntegerBouncyCastle = Org.BouncyCastle.Math.BigInteger;

namespace Nethereum.ZkProofsVerifier.Tests
{
    public class MockNodeDataServiceLocal : INodeDataService
    {
        public Task<BigInteger> GetBalanceAsync(byte[] address) => Task.FromResult(BigInteger.Zero);
        public Task<BigInteger> GetBalanceAsync(string address) => Task.FromResult(BigInteger.Zero);
        public Task<byte[]> GetCodeAsync(byte[] address) => Task.FromResult(Array.Empty<byte>());
        public Task<byte[]> GetCodeAsync(string address) => Task.FromResult(Array.Empty<byte>());
        public Task<byte[]> GetBlockHashAsync(BigInteger blockNumber) => Task.FromResult(new byte[32]);
        public Task<byte[]> GetStorageAtAsync(byte[] address, BigInteger position) => Task.FromResult(Array.Empty<byte>());
        public Task<byte[]> GetStorageAtAsync(string address, BigInteger position) => Task.FromResult(Array.Empty<byte>());
        public Task<BigInteger> GetTransactionCount(byte[] address) => Task.FromResult(BigInteger.Zero);
        public Task<BigInteger> GetTransactionCount(string address) => Task.FromResult(BigInteger.Zero);
    }

    public class EvmCrossValidationTests
    {
        private static string GetTestDataPath(string filename) =>
            Path.Combine(AppContext.BaseDirectory, "TestData", filename);

        private static byte[] BigIntegerTo32Bytes(BigIntegerBouncyCastle value)
        {
            var bytes = value.ToByteArrayUnsigned();
            if (bytes.Length >= 32) return bytes;
            var padded = new byte[32];
            Array.Copy(bytes, 0, padded, 32 - bytes.Length, bytes.Length);
            return padded;
        }

        private static byte[] EncodeVerifyProofCalldata(
            string proofJson, string publicJson)
        {
            var proof = SnarkjsProofParser.Parse(proofJson);
            var publicInputs = SnarkjsPublicInputParser.Parse(publicJson);

            var proofA = proof.A.Normalize();
            var proofC = proof.C.Normalize();

            var proofBAffine = proof.B.MakeAffine();
            var bxImag = proofBAffine.X.A;
            var bxReal = proofBAffine.X.B;
            var byImag = proofBAffine.Y.A;
            var byReal = proofBAffine.Y.B;

            var selectorBytes = new Sha3Keccack().CalculateHashAsBytes(
                "verifyProof(uint256[2],uint256[2][2],uint256[2],uint256[3])");

            int totalSize = 4 + (2 + 4 + 2 + 3) * 32;
            var calldata = new byte[totalSize];

            Array.Copy(selectorBytes, 0, calldata, 0, 4);
            int offset = 4;

            void WriteWord(byte[] word)
            {
                Array.Copy(word, 0, calldata, offset, 32);
                offset += 32;
            }

            void WriteBigInt(BigIntegerBouncyCastle val) => WriteWord(BigIntegerTo32Bytes(val));

            WriteBigInt(proofA.AffineXCoord.ToBigInteger());
            WriteBigInt(proofA.AffineYCoord.ToBigInteger());

            WriteBigInt(new BigIntegerBouncyCastle(bxImag.ToString()));
            WriteBigInt(new BigIntegerBouncyCastle(bxReal.ToString()));
            WriteBigInt(new BigIntegerBouncyCastle(byImag.ToString()));
            WriteBigInt(new BigIntegerBouncyCastle(byReal.ToString()));

            WriteBigInt(proofC.AffineXCoord.ToBigInteger());
            WriteBigInt(proofC.AffineYCoord.ToBigInteger());

            foreach (var input in publicInputs)
            {
                WriteBigInt(input);
            }

            return calldata;
        }

        private async Task<byte[]> DeployContract(ExecutionStateService state, string deployer, string contractAddress, byte[] initCode)
        {
            var deployInput = new CallInput
            {
                From = deployer,
                To = contractAddress,
                Data = initCode.ToHex(true),
                Value = new HexBigInteger(0),
                Gas = new HexBigInteger(30000000),
                ChainId = new HexBigInteger(1)
            };

            var ctx = new ProgramContext(deployInput, state, deployer);
            var program = new Program(initCode, ctx);
            program.GasRemaining = 30000000;

            var vm = new EVMSimulator();
            await vm.ExecuteWithCallStackAsync(program, 0, 0, false);

            return program.ProgramResult.Result;
        }

        private async Task<bool> ExecuteVerifierContract(byte[] initCode, byte[] calldata)
        {
            var nodeDataService = new MockNodeDataServiceLocal();
            var executionStateService = new ExecutionStateService(nodeDataService);

            var callerAddress = "0x1111111111111111111111111111111111111111";
            var contractAddress = "0x2222222222222222222222222222222222222222";

            var runtimeCode = await DeployContract(executionStateService, callerAddress, contractAddress, initCode);
            if (runtimeCode == null || runtimeCode.Length == 0)
                throw new Exception("Contract deployment failed — no runtime bytecode returned");

            executionStateService.SaveCode(contractAddress, runtimeCode);

            var callInput = new CallInput
            {
                From = callerAddress,
                To = contractAddress,
                Data = calldata.ToHex(true),
                Value = new HexBigInteger(0),
                Gas = new HexBigInteger(30000000),
                ChainId = new HexBigInteger(1)
            };

            var programContext = new ProgramContext(callInput, executionStateService, callerAddress);
            var program = new Program(runtimeCode, programContext);
            program.GasRemaining = 30000000;

            var vm = new EVMSimulator();
            await vm.ExecuteWithCallStackAsync(program, 0, 0, false);

            var result = program.ProgramResult.Result;
            if (result == null || result.Length == 0)
                return false;

            return result.Length >= 32 && result[result.Length - 1] == 1;
        }

        [Fact]
        [Trait("Category", "ZK-EvmCrossValidation")]
        public async Task ValidProof_NativeAndEvm_BothReturnTrue()
        {
            var proofJson = File.ReadAllText(GetTestDataPath("proof.json"));
            var vkJson = File.ReadAllText(GetTestDataPath("verification_key.json"));
            var publicJson = File.ReadAllText(GetTestDataPath("public.json"));

            var nativeResult = CircomGroth16Adapter.Verify(proofJson, vkJson, publicJson);
            Assert.True(nativeResult.IsValid, "Native verifier should accept valid proof: " + nativeResult.Error);

            var bytecodeHex = File.ReadAllText(GetTestDataPath("verifier_sol_Groth16Verifier.bin")).Trim();
            var bytecode = bytecodeHex.HexToByteArray();
            var calldata = EncodeVerifyProofCalldata(proofJson, publicJson);

            var evmResult = await ExecuteVerifierContract(bytecode, calldata);
            Assert.True(evmResult, "EVM Solidity verifier should accept valid proof");
        }

        [Fact]
        [Trait("Category", "ZK-EvmCrossValidation")]
        public async Task TamperedInput_NativeAndEvm_BothReturnFalse()
        {
            var proofJson = File.ReadAllText(GetTestDataPath("proof.json"));
            var vkJson = File.ReadAllText(GetTestDataPath("verification_key.json"));

            var tamperedPublicJson = "[\"34\", \"3\", \"11\"]";

            var nativeResult = CircomGroth16Adapter.Verify(proofJson, vkJson, tamperedPublicJson);
            Assert.False(nativeResult.IsValid, "Native verifier should reject tampered input");

            var bytecodeHex = File.ReadAllText(GetTestDataPath("verifier_sol_Groth16Verifier.bin")).Trim();
            var bytecode = bytecodeHex.HexToByteArray();
            var calldata = EncodeVerifyProofCalldata(proofJson, tamperedPublicJson);

            var evmResult = await ExecuteVerifierContract(bytecode, calldata);
            Assert.False(evmResult, "EVM Solidity verifier should reject tampered input");
        }
    }
}
