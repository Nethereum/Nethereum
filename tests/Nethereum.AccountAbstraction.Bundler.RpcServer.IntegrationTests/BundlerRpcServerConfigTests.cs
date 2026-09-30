using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.RpcServer.Configuration;
using Nethereum.AccountAbstraction.Bundler.Validation;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.AccountAbstraction.Validation;
using Nethereum.Signer;
using Xunit;
using UserOperation = Nethereum.AccountAbstraction.UserOperation;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests
{
    public class BundlerRpcServerConfigTests
    {
        [Fact]
        public void ToBundlerConfig_PropagatesNonDefaultMaxVerificationGas()
        {
            var serverConfig = new BundlerRpcServerConfig
            {
                BeneficiaryAddress = "0x0000000000000000000000000000000000dEaD",
                SupportedEntryPoints = new[] { "0x0000000000000000000000000000000000dEaD" },
                MaxVerificationGas = 12_345_678
            };

            var bundlerConfig = serverConfig.ToBundlerConfig();

            Assert.Equal(12_345_678, bundlerConfig.MaxVerificationGas);
        }

        [Fact]
        public void MaxVerificationGas_DefaultIsAboveTheOldOneAndHalfMillionCeiling()
        {
            var serverConfig = new BundlerRpcServerConfig();

            Assert.True(serverConfig.MaxVerificationGas > 5_000_000,
                $"Default MaxVerificationGas ({serverConfig.MaxVerificationGas}) must exceed the " +
                "5,000,000 verificationGasLimit used by bundler-spec-tests EREP-020.");
        }

        [Fact]
        public void CreateDefault_MaxVerificationGas_PropagatesThroughToBundlerConfig()
        {
            var serverConfig = BundlerRpcServerConfig.CreateDefault(
                "0x0000000000000000000000000000000000dEaD",
                "0x0000000000000000000000000000000000bEEF",
                "http://localhost:8545",
                1);

            var bundlerConfig = serverConfig.ToBundlerConfig();

            Assert.Equal(serverConfig.MaxVerificationGas, bundlerConfig.MaxVerificationGas);
        }

        [Fact]
        public void CreateAppChainConfig_MaxVerificationGas_PropagatesThroughToBundlerConfig()
        {
            var serverConfig = BundlerRpcServerConfig.CreateAppChainConfig(
                "0x0000000000000000000000000000000000dEaD",
                "0x0000000000000000000000000000000000bEEF",
                "http://localhost:8545",
                1);

            var bundlerConfig = serverConfig.ToBundlerConfig();

            Assert.Equal(serverConfig.MaxVerificationGas, bundlerConfig.MaxVerificationGas);
        }
    }

    [Collection(BundlerRpcServerFixture.COLLECTION_NAME)]
    public class BundlerRpcServerConfigStructuralValidationTests
    {
        private const long CompliancePinnedVerificationGas = 5_000_000;
        private const long AboveRaisedCeilingVerificationGas = 10_000_001;

        private readonly BundlerRpcServerFixture _fixture;

        public BundlerRpcServerConfigStructuralValidationTests(BundlerRpcServerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task ValidateStructureAsync_ComplianceGas_RejectedWithOldOneAndHalfMillionDefault()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);
            var userOp = await BuildSignedUserOpAsync(accountAddress, accountKey, CompliancePinnedVerificationGas);

            var oldDefaultConfig = new BundlerConfig
            {
                SupportedEntryPoints = new[] { _fixture.EntryPointService.ContractAddress },
                BeneficiaryAddress = _fixture.BeneficiaryAddress,
                ChainId = _fixture.ChainId
            };

            var validator = new UserOpValidator(_fixture.Web3, oldDefaultConfig);
            var result = await validator.ValidateStructureAsync(userOp, _fixture.EntryPointService.ContractAddress);

            Assert.False(result.IsValid);
            Assert.Equal(UserOpValidationError.InsufficientVerificationGas, result.ErrorCode);
        }

        [Fact]
        public async Task ValidateStructureAsync_ComplianceGas_AcceptedWithRaisedRpcServerConfigDefault()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);
            var userOp = await BuildSignedUserOpAsync(accountAddress, accountKey, CompliancePinnedVerificationGas);

            var serverConfig = new BundlerRpcServerConfig
            {
                SupportedEntryPoints = new[] { _fixture.EntryPointService.ContractAddress },
                BeneficiaryAddress = _fixture.BeneficiaryAddress,
                ChainId = _fixture.ChainId
            };

            var raisedConfig = serverConfig.ToBundlerConfig();
            var validator = new UserOpValidator(_fixture.Web3, raisedConfig);
            var result = await validator.ValidateStructureAsync(userOp, _fixture.EntryPointService.ContractAddress);

            Assert.True(result.IsValid, result.Error);
        }

        [Fact]
        public async Task ValidateStructureAsync_AboveRaisedCeiling_StillRejected()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);
            var userOp = await BuildSignedUserOpAsync(accountAddress, accountKey, AboveRaisedCeilingVerificationGas);

            var serverConfig = new BundlerRpcServerConfig
            {
                SupportedEntryPoints = new[] { _fixture.EntryPointService.ContractAddress },
                BeneficiaryAddress = _fixture.BeneficiaryAddress,
                ChainId = _fixture.ChainId
            };

            var raisedConfig = serverConfig.ToBundlerConfig();
            var validator = new UserOpValidator(_fixture.Web3, raisedConfig);
            var result = await validator.ValidateStructureAsync(userOp, _fixture.EntryPointService.ContractAddress);

            Assert.False(result.IsValid);
            Assert.Equal(UserOpValidationError.InsufficientVerificationGas, result.ErrorCode);
        }

        private async Task<PackedUserOperation> BuildSignedUserOpAsync(
            string sender, EthECKey signerKey, BigInteger verificationGasLimit)
        {
            var userOp = new UserOperation
            {
                Sender = sender,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 100_000,
                VerificationGasLimit = verificationGasLimit,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 1_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            return await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, signerKey);
        }
    }
}
