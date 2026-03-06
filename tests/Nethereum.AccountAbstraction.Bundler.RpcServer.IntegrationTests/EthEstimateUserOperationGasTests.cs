using System.Numerics;
using System.Text.Json;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.XUnitEthereumClients;
using Xunit;
using UserOperation = Nethereum.AccountAbstraction.UserOperation;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests
{
    [Collection(BundlerRpcServerFixture.COLLECTION_NAME)]
    public class EthEstimateUserOperationGasTests
    {
        private readonly BundlerRpcServerFixture _fixture;

        public EthEstimateUserOperationGasTests(BundlerRpcServerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task EstimateGas_ExistingAccount_ReturnsValidEstimates()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var userOpObject = new
            {
                sender = accountAddress,
                nonce = "0x0",
                initCode = "0x",
                callData = "0x",
                signature = "0x" + new string('0', 130)
            };

            var response = await _fixture.SendRpcRequestAsync(
                "eth_estimateUserOperationGas",
                userOpObject,
                _fixture.EntryPointService.ContractAddress);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);

            var result = response.Result.Value;
            Assert.True(result.TryGetProperty("callGasLimit", out var callGas));
            Assert.True(result.TryGetProperty("verificationGasLimit", out var verificationGas));
            Assert.True(result.TryGetProperty("preVerificationGas", out var preVerificationGas));

            var callGasValue = ParseHexBigInteger(callGas.GetString()!);
            var verificationGasValue = ParseHexBigInteger(verificationGas.GetString()!);
            var preVerificationGasValue = ParseHexBigInteger(preVerificationGas.GetString()!);

            Assert.True(callGasValue > 0, "callGasLimit should be > 0");
            Assert.True(verificationGasValue > 0, "verificationGasLimit should be > 0");
            Assert.True(preVerificationGasValue > 0, "preVerificationGas should be > 0");
        }

        [Fact]
        public async Task EstimateGas_WithCallData_ReturnsHigherCallGas()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 1m);

            var emptyCallOp = new
            {
                sender = accountAddress,
                nonce = "0x0",
                initCode = "0x",
                callData = "0x",
                signature = "0x" + new string('0', 130)
            };

            var emptyResponse = await _fixture.SendRpcRequestAsync(
                "eth_estimateUserOperationGas",
                emptyCallOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.Null(emptyResponse.Error);
            var emptyCallGas = ParseHexBigInteger(
                emptyResponse.Result!.Value.GetProperty("callGasLimit").GetString()!);

            var executeFunction = new ExecuteFunction
            {
                Target = "0x1111111111111111111111111111111111111111",
                Value = Nethereum.Web3.Web3.Convert.ToWei(0.001m),
                Data = Array.Empty<byte>()
            };

            var withCallDataOp = new
            {
                sender = accountAddress,
                nonce = "0x0",
                initCode = "0x",
                callData = executeFunction.GetCallData().ToHex(true),
                signature = "0x" + new string('0', 130)
            };

            var withCallResponse = await _fixture.SendRpcRequestAsync(
                "eth_estimateUserOperationGas",
                withCallDataOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.Null(withCallResponse.Error);
            var withCallGas = ParseHexBigInteger(
                withCallResponse.Result!.Value.GetProperty("callGasLimit").GetString()!);

            Assert.True(withCallGas >= emptyCallGas,
                $"CallGas with call data ({withCallGas}) should be >= empty ({emptyCallGas})");
        }

        [Fact]
        public async Task EstimateGas_NewAccountWithInitCode_IncludesDeploymentCost()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var newAccountKey = new EthECKey(TestAccounts.Account3PrivateKey);
            var newOwnerAddress = newAccountKey.GetPublicAddress();

            var predictedAddress = await _fixture.GetAccountAddressAsync(newOwnerAddress, salt);

            await _fixture.FundAccountAsync(predictedAddress, 0.5m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(newOwnerAddress, salt);

            var newAccountOp = new
            {
                sender = predictedAddress,
                nonce = "0x0",
                initCode = initCode.ToHex(true),
                callData = "0x",
                signature = "0x" + new string('0', 130)
            };

            var response = await _fixture.SendRpcRequestAsync(
                "eth_estimateUserOperationGas",
                newAccountOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);

            var result = response.Result.Value;
            var verificationGas = ParseHexBigInteger(
                result.GetProperty("verificationGasLimit").GetString()!);

            Assert.True(verificationGas > 100_000,
                $"VerificationGas for new account ({verificationGas}) should be > 100000 to cover deployment");
        }

        [Fact]
        public async Task EstimateGas_InvalidSender_ReturnsError()
        {
            var invalidOp = new
            {
                sender = "0x0000000000000000000000000000000000000000",
                nonce = "0x0",
                initCode = "0x",
                callData = "0x",
                signature = "0x" + new string('0', 130)
            };

            var response = await _fixture.SendRpcRequestAsync(
                "eth_estimateUserOperationGas",
                invalidOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.NotNull(response.Error);
        }

        [Fact]
        public async Task EstimateGas_ConsistentResults_ForSameInput()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var userOpObject = new
            {
                sender = accountAddress,
                nonce = "0x0",
                initCode = "0x",
                callData = "0x",
                signature = "0x" + new string('0', 130)
            };

            var response1 = await _fixture.SendRpcRequestAsync(
                "eth_estimateUserOperationGas",
                userOpObject,
                _fixture.EntryPointService.ContractAddress);

            var response2 = await _fixture.SendRpcRequestAsync(
                "eth_estimateUserOperationGas",
                userOpObject,
                _fixture.EntryPointService.ContractAddress);

            Assert.Null(response1.Error);
            Assert.Null(response2.Error);

            var callGas1 = response1.Result!.Value.GetProperty("callGasLimit").GetString();
            var callGas2 = response2.Result!.Value.GetProperty("callGasLimit").GetString();

            Assert.Equal(callGas1, callGas2);
        }

        private static BigInteger ParseHexBigInteger(string hex)
        {
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hex = hex.Substring(2);
            return BigInteger.Parse("0" + hex, System.Globalization.NumberStyles.HexNumber);
        }
    }
}
