using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.Validation;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.AccountAbstraction.Validation;
using Nethereum.Contracts;
using System.Numerics;
using Xunit;
using ExecuteFunction = Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition.ExecuteFunction;

namespace Nethereum.AccountAbstraction.IntegrationTests.Bundler
{
    [Collection(BundlerTestFixture.BUNDLER_COLLECTION)]
    public class UserOpValidatorTests
    {
        private readonly BundlerTestFixture _fixture;

        public UserOpValidatorTests(BundlerTestFixture fixture)
        {
            _fixture = fixture;
        }

        private static PackedUserOperation CreateValidPackedUserOp(
            string sender,
            byte[]? signature = null)
        {
            var accountGasLimits = new byte[32];
            var verificationGas = BitConverter.GetBytes((long)100_000);
            var callGas = BitConverter.GetBytes((long)100_000);
            Array.Reverse(verificationGas);
            Array.Reverse(callGas);
            Array.Copy(verificationGas, 0, accountGasLimits, 8, 8);
            Array.Copy(callGas, 0, accountGasLimits, 24, 8);

            var gasFees = new byte[32];
            var priorityFee = BitConverter.GetBytes((long)1_000_000_000);
            var maxFee = BitConverter.GetBytes((long)2_000_000_000);
            Array.Reverse(priorityFee);
            Array.Reverse(maxFee);
            Array.Copy(priorityFee, 0, gasFees, 8, 8);
            Array.Copy(maxFee, 0, gasFees, 24, 8);

            return new PackedUserOperation
            {
                Sender = sender,
                Nonce = BigInteger.Zero,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = accountGasLimits,
                PreVerificationGas = 100_000,
                GasFees = gasFees,
                PaymasterAndData = Array.Empty<byte>(),
                Signature = signature ?? new byte[65]
            };
        }

        [Fact]
        public async Task ValidateStructure_WithValidOp_ReturnsSuccess()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, 0);

            var validator = new UserOpValidator(_fixture.Web3, _fixture.BundlerConfig);
            var userOp = CreateValidPackedUserOp(accountAddress);
            userOp.Nonce = nonce;

            var result = await validator.ValidateStructureAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.True(result.IsValid, result.Error);
            Assert.Null(result.Error);
        }

        [Fact]
        public async Task ValidateStructure_WithUnsupportedEntryPoint_ReturnsFailure()
        {
            var validator = new UserOpValidator(_fixture.Web3, _fixture.BundlerConfig);
            var userOp = CreateValidPackedUserOp("0x1234567890123456789012345678901234567890");

            var result = await validator.ValidateStructureAsync(
                userOp,
                "0x0000000000000000000000000000000000000001");

            Assert.False(result.IsValid);
            Assert.Contains("Unsupported EntryPoint", result.Error);
        }

        [Fact]
        public async Task ValidateStructure_WithInvalidSender_ReturnsFailure()
        {
            var validator = new UserOpValidator(_fixture.Web3, _fixture.BundlerConfig);
            var userOp = CreateValidPackedUserOp("");

            var result = await validator.ValidateStructureAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.False(result.IsValid);
            Assert.Equal(UserOpValidationError.InvalidSender, result.ErrorCode);
        }

        [Fact]
        public async Task ValidateStructure_WithEmptySignature_ReturnsSuccess()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, 0);

            var validator = new UserOpValidator(_fixture.Web3, _fixture.BundlerConfig);
            var userOp = CreateValidPackedUserOp(accountAddress, signature: Array.Empty<byte>());
            userOp.Nonce = nonce;

            var result = await validator.ValidateStructureAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.True(result.IsValid, result.Error);
            Assert.NotEqual(UserOpValidationError.InvalidSignature, result.ErrorCode);
        }

        [Fact]
        public async Task Validate_WithEmptySignature_ReachesRealValidation_NotRejectedStructurally()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);
            var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, 0);

            var userOp = CreateValidPackedUserOp(accountAddress, signature: Array.Empty<byte>());
            userOp.Nonce = nonce;

            var validator = new UserOpValidator(_fixture.Web3, CreateSimulationConfig());

            var result = await validator.ValidateAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.False(result.IsValid, "an empty signature must still be rejected - but by real validation, not structurally");
            Assert.NotEqual(UserOpValidationError.InvalidSignature, result.ErrorCode);
            Assert.DoesNotContain("Signature required", result.Error);
        }

        [Fact]
        public async Task ValidateStructure_WithInvalidAccountGasLimits_ReturnsFailure()
        {
            var validator = new UserOpValidator(_fixture.Web3, _fixture.BundlerConfig);
            var userOp = CreateValidPackedUserOp("0x1234567890123456789012345678901234567890");
            userOp.AccountGasLimits = new byte[16];

            var result = await validator.ValidateStructureAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.False(result.IsValid);
            Assert.Equal(UserOpValidationError.GasValuesOverflow, result.ErrorCode);
        }

        [Fact]
        public async Task ValidateStructure_WithInvalidGasFees_ReturnsFailure()
        {
            var validator = new UserOpValidator(_fixture.Web3, _fixture.BundlerConfig);
            var userOp = CreateValidPackedUserOp("0x1234567890123456789012345678901234567890");
            userOp.GasFees = new byte[16];

            var result = await validator.ValidateStructureAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.False(result.IsValid);
            Assert.Equal(UserOpValidationError.GasValuesOverflow, result.ErrorCode);
        }

        [Fact]
        public async Task ValidateStructure_WithTooLowPriorityFee_ReturnsFailure()
        {
            var config = new BundlerConfig
            {
                SupportedEntryPoints = new[] { _fixture.EntryPointService.ContractAddress },
                BeneficiaryAddress = _fixture.BeneficiaryAddress,
                MinPriorityFeePerGas = 10_000_000_000,
                AutoBundleIntervalMs = 0
            };

            var validator = new UserOpValidator(_fixture.Web3, config);
            var userOp = CreateValidPackedUserOp("0x1234567890123456789012345678901234567890");

            var result = await validator.ValidateStructureAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.False(result.IsValid);
            Assert.Equal(UserOpValidationError.MaxPriorityFeePerGasTooLow, result.ErrorCode);
        }

        [Fact]
        public async Task ValidateStructure_WithMaxFeeLessThanPriorityFee_ReturnsFailure()
        {
            var validator = new UserOpValidator(_fixture.Web3, _fixture.BundlerConfig);
            var userOp = CreateValidPackedUserOp("0x1234567890123456789012345678901234567890");

            var gasFees = new byte[32];
            var priorityFee = BitConverter.GetBytes((long)5_000_000_000);
            var maxFee = BitConverter.GetBytes((long)1_000_000_000);
            Array.Reverse(priorityFee);
            Array.Reverse(maxFee);
            Array.Copy(priorityFee, 0, gasFees, 8, 8);
            Array.Copy(maxFee, 0, gasFees, 24, 8);
            userOp.GasFees = gasFees;

            var result = await validator.ValidateStructureAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.False(result.IsValid);
            Assert.Equal(UserOpValidationError.MaxFeePerGasTooLow, result.ErrorCode);
        }

        [Fact]
        public async Task ValidateStructure_WithStrictMode_ValidatesInitCode()
        {
            var config = new BundlerConfig
            {
                SupportedEntryPoints = new[] { _fixture.EntryPointService.ContractAddress },
                BeneficiaryAddress = _fixture.BeneficiaryAddress,
                StrictValidation = true,
                AutoBundleIntervalMs = 0
            };

            var validator = new UserOpValidator(_fixture.Web3, config);
            var userOp = CreateValidPackedUserOp("0x1234567890123456789012345678901234567890");
            userOp.InitCode = new byte[10];

            var result = await validator.ValidateStructureAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.False(result.IsValid);
            Assert.Equal(UserOpValidationError.InitCodeFailed, result.ErrorCode);
        }

        [Fact]
        public async Task ValidateStructure_WithStrictMode_ValidatesPaymasterAndData()
        {
            var config = new BundlerConfig
            {
                SupportedEntryPoints = new[] { _fixture.EntryPointService.ContractAddress },
                BeneficiaryAddress = _fixture.BeneficiaryAddress,
                StrictValidation = true,
                AutoBundleIntervalMs = 0
            };

            var validator = new UserOpValidator(_fixture.Web3, config);
            var userOp = CreateValidPackedUserOp("0x1234567890123456789012345678901234567890");
            userOp.PaymasterAndData = new byte[10];

            var result = await validator.ValidateStructureAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.False(result.IsValid);
            Assert.Equal(UserOpValidationError.PaymasterNotDeployed, result.ErrorCode);
        }

        [Fact]
        public async Task ValidateStructure_ReturnsGasLimits()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, 0);

            var validator = new UserOpValidator(_fixture.Web3, _fixture.BundlerConfig);
            var userOp = CreateValidPackedUserOp(accountAddress);
            userOp.Nonce = nonce;

            var result = await validator.ValidateStructureAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.True(result.IsValid, result.Error);
            Assert.True(result.VerificationGasLimit > 0);
            Assert.True(result.CallGasLimit > 0);
            Assert.True(result.PreVerificationGas > 0);
        }

        [Fact]
        public async Task Validate_WithSignedOperation_ReturnsSuccess()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = executeFunction.GetCallData(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 100_000,
                    PreVerificationGas = 100_000
                },
                accountKey);

            var validator = new UserOpValidator(_fixture.Web3, _fixture.BundlerConfig);

            var result = await validator.ValidateAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.True(result.IsValid);
        }

        #region Real validation simulation (EntryPointSimulations via eth_call state override)

        private BundlerConfig CreateSimulationConfig() => new()
        {
            SupportedEntryPoints = _fixture.BundlerConfig.SupportedEntryPoints,
            BeneficiaryAddress = _fixture.BundlerConfig.BeneficiaryAddress,
            MinPriorityFeePerGas = 0,
            AutoBundleIntervalMs = 0,
            StrictValidation = false,
            SimulateValidation = true,
            UnsafeMode = true,
            ChainId = _fixture.BundlerConfig.ChainId
        };

        [Fact]
        public async Task SimulateValidation_WrongSignerSignature_RejectsWithAA24()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);
            var wrongKey = Nethereum.Signer.EthECKey.GenerateKey();

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = executeFunction.GetCallData(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 200_000,
                    PreVerificationGas = 100_000,
                    MaxFeePerGas = 2_000_000_000,
                    MaxPriorityFeePerGas = 1_000_000_000
                },
                wrongKey);

            var validator = new UserOpValidator(_fixture.Web3, CreateSimulationConfig());

            var result = await validator.SimulateValidationAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.False(result.IsValid, "a structurally valid signature from the WRONG signer must be rejected");
            Assert.Contains("AA24", result.Error);
            Assert.Equal(UserOpValidationError.InvalidSignature, result.ErrorCode);
        }

        [Fact]
        public async Task SendUserOperation_WrongSignerSignature_ThrowsInvalidSignatureCode()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);
            var wrongKey = Nethereum.Signer.EthECKey.GenerateKey();

            var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = Array.Empty<byte>(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 200_000,
                    PreVerificationGas = 100_000,
                    MaxFeePerGas = 2_000_000_000,
                    MaxPriorityFeePerGas = 1_000_000_000
                },
                wrongKey);

            using var bundler = _fixture.CreateNewBundlerService(CreateSimulationConfig());

            var ex = await Assert.ThrowsAsync<BundlerRpcException>(() =>
                bundler.SendUserOperationAsync(userOp, _fixture.EntryPointService.ContractAddress));

            Assert.Equal(BundlerErrorCodes.InvalidSignature, ex.Code);
        }

        [Fact]
        public async Task SimulateValidation_CorrectlySignedOp_SucceedsWithOpenTimeRange()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = executeFunction.GetCallData(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 200_000,
                    PreVerificationGas = 100_000,
                    MaxFeePerGas = 2_000_000_000,
                    MaxPriorityFeePerGas = 1_000_000_000
                },
                accountKey);

            var validator = new UserOpValidator(_fixture.Web3, CreateSimulationConfig());

            var result = await validator.SimulateValidationAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.True(result.IsValid, result.Error);
            Assert.Equal(0UL, result.ValidAfter);
            Assert.Equal(0UL, result.ValidUntil);
            Assert.Null(result.Aggregator);
        }

        [Fact]
        public async Task SimulateValidation_CounterfactualSenderWithInitCode_RunsDeploymentInSimulation()
        {
            var accountKey = Nethereum.Signer.EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            var salt = (ulong)Random.Shared.NextInt64();

            var accountAddress = await _fixture.GetAccountAddressAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 1m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);

            var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    Nonce = 0,
                    InitCode = initCode,
                    CallData = Array.Empty<byte>(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 500_000,
                    PreVerificationGas = 100_000,
                    MaxFeePerGas = 2_000_000_000,
                    MaxPriorityFeePerGas = 1_000_000_000
                },
                accountKey);

            var validator = new UserOpValidator(_fixture.Web3, CreateSimulationConfig());

            var result = await validator.SimulateValidationAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.True(result.IsValid,
                $"counterfactual deployment must run inside the simulation, got: {result.Error}");
        }

        [Fact]
        public async Task SimulateValidation_UnfundedCounterfactualSender_RejectsWithAA21()
        {
            var accountKey = Nethereum.Signer.EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            var salt = (ulong)Random.Shared.NextInt64();

            var accountAddress = await _fixture.GetAccountAddressAsync(ownerAddress, salt);
            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);

            var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    Nonce = 0,
                    InitCode = initCode,
                    CallData = Array.Empty<byte>(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 500_000,
                    PreVerificationGas = 100_000,
                    MaxFeePerGas = 2_000_000_000,
                    MaxPriorityFeePerGas = 1_000_000_000
                },
                accountKey);

            var validator = new UserOpValidator(_fixture.Web3, CreateSimulationConfig());

            var result = await validator.SimulateValidationAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.False(result.IsValid, "an op whose sender cannot pay the prefund must be rejected");
            Assert.Contains("AA21", result.Error);
        }

        #endregion

        [Fact]
        public async Task EstimateGas_WithValidOp_ReturnsEstimates()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = Array.Empty<byte>(),
                MaxFeePerGas = 1_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var validator = new UserOpValidator(_fixture.Web3, _fixture.BundlerConfig);

            var result = await validator.EstimateGasAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.True(result.IsValid);
            Assert.True(result.VerificationGasLimit >= 0);
            Assert.True(result.CallGasLimit >= 0);
        }
    }
}
