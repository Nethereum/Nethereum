using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.Validation;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.AccountAbstraction.Validation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Mappers;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E
{
    /// <summary>
    /// Slice 2b: EIP-7702 authorization pre-checks and the AA20 bypass in UserOpValidator.
    /// These reproduce the eth-infinitism spec-test scenarios against the real validator:
    ///   test_send_nonsender_eip_7702_drop_userop  -> -32602 InvalidFields with "sender"
    ///   test_send_wrongchain_eip_7702_drop_userop -> -32602 InvalidFields with "chainid"
    ///   test_send_bad_eip_7702_drop_userop        -> a wrong-nonce auth is NOT pre-checked
    ///                                                (it must reach simulation, not -32602)
    ///   test_send_post_eip_7702_tx[0]             -> chainId 0 is universal (passes the check)
    /// The end-to-end -32500 / full-simulation inclusion is slice 3; here we assert the
    /// validation pre-checks, which is what flips the drop-userop spec tests.
    /// </summary>
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "EIP7702-4337")]
    public class Eip7702ValidatorPreCheckTests
    {
        private readonly DevChainBundlerFixture _fixture;
        private readonly ITestOutputHelper _output;

        public Eip7702ValidatorPreCheckTests(DevChainBundlerFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task Authorization_SignedByDifferentKey_IsRejected_WithSenderMessage()
        {
            var (senderKey, senderAddress) = _fixture.GenerateNewAccount();
            var (otherKey, _) = _fixture.GenerateNewAccount();
            var delegateAddress = "0x1111111111111111111111111111111111111111";

            var userOp = CreateValidPackedUserOp(senderAddress);
            var auth = _fixture.SignAuthorization(otherKey, delegateAddress, 0).ToRPCAuthorisation();

            var result = await CreateValidator().ValidateAsync(
                userOp, _fixture.EntryPointService.ContractAddress, EmptySet(), auth);

            Assert.False(result.IsValid);
            Assert.Equal(UserOpValidationError.InvalidAuthorisation, result.ErrorCode);
            Assert.Equal(BundlerErrorCodes.InvalidFields, BundlerRpcException.ToErrorCode(result.ErrorCode));
            Assert.Contains("sender", result.Error, StringComparison.OrdinalIgnoreCase);
            _output.WriteLine(result.Error);
        }

        [Fact]
        public async Task Authorization_WithWrongChainId_IsRejected_WithChainIdMessage()
        {
            var (senderKey, senderAddress) = _fixture.GenerateNewAccount();
            var delegateAddress = "0x2222222222222222222222222222222222222222";

            var userOp = CreateValidPackedUserOp(senderAddress);
            var auth = _fixture.SignAuthorization(senderKey, delegateAddress, 0, chainId: 999999)
                .ToRPCAuthorisation();

            var result = await CreateValidator().ValidateAsync(
                userOp, _fixture.EntryPointService.ContractAddress, EmptySet(), auth);

            Assert.False(result.IsValid);
            Assert.Equal(UserOpValidationError.InvalidAuthorisation, result.ErrorCode);
            Assert.Equal(BundlerErrorCodes.InvalidFields, BundlerRpcException.ToErrorCode(result.ErrorCode));
            Assert.Contains("chainid", result.Error, StringComparison.OrdinalIgnoreCase);
            _output.WriteLine(result.Error);
        }

        [Fact]
        public async Task Authorization_WithChainIdZero_PassesChainIdCheck()
        {
            var (senderKey, senderAddress) = _fixture.GenerateNewAccount();
            var delegateAddress = "0x3333333333333333333333333333333333333333";

            var userOp = CreateValidPackedUserOp(senderAddress);
            var auth = _fixture.SignAuthorization(senderKey, delegateAddress, 0, chainId: 0)
                .ToRPCAuthorisation();

            var result = await CreateValidator().ValidateAsync(
                userOp, _fixture.EntryPointService.ContractAddress, EmptySet(), auth);

            Assert.True(result.IsValid, result.Error);
        }

        [Fact]
        public async Task Authorization_WithWrongNonce_IsNotPreChecked_ReachesFurtherValidation()
        {
            var (senderKey, senderAddress) = _fixture.GenerateNewAccount();
            var delegateAddress = "0x4444444444444444444444444444444444444444";

            var userOp = CreateValidPackedUserOp(senderAddress);
            var auth = _fixture.SignAuthorization(senderKey, delegateAddress, nonce: 5)
                .ToRPCAuthorisation();

            var result = await CreateValidator().ValidateAsync(
                userOp, _fixture.EntryPointService.ContractAddress, EmptySet(), auth);

            Assert.NotEqual(UserOpValidationError.InvalidAuthorisation, result.ErrorCode);
            Assert.True(result.IsValid, result.Error);
        }

        [Fact]
        public async Task NotYetDelegatedSender_WithAuthorization_BypassesAA20()
        {
            var (senderKey, senderAddress) = _fixture.GenerateNewAccount();
            var delegateAddress = "0x5555555555555555555555555555555555555555";

            var userOp = CreateValidPackedUserOp(senderAddress);

            var withoutAuth = await CreateValidator().ValidateStructureAsync(
                userOp, _fixture.EntryPointService.ContractAddress);
            Assert.False(withoutAuth.IsValid);
            Assert.Equal(UserOpValidationError.InvalidSender, withoutAuth.ErrorCode);
            Assert.Contains("AA20", withoutAuth.Error);

            var auth = _fixture.SignAuthorization(senderKey, delegateAddress, 0).ToRPCAuthorisation();
            var withAuth = await CreateValidator().ValidateStructureAsync(
                userOp, _fixture.EntryPointService.ContractAddress, auth);
            Assert.True(withAuth.IsValid, withAuth.Error);
        }

        [Fact]
        public async Task NonEip7702Path_Unchanged_NoAuth_StillAA20()
        {
            var (_, senderAddress) = _fixture.GenerateNewAccount();
            var userOp = CreateValidPackedUserOp(senderAddress);

            var result = await CreateValidator().ValidateAsync(
                userOp, _fixture.EntryPointService.ContractAddress, EmptySet());

            Assert.False(result.IsValid);
            Assert.Equal(UserOpValidationError.InvalidSender, result.ErrorCode);
            Assert.Contains("AA20", result.Error);
        }


        [Fact]
        public async Task Eip7702InitMarker_WithFactoryData_IsAccepted_UnderStrictValidation()
        {
            var (senderKey, senderAddress) = _fixture.GenerateNewAccount();
            var delegateAddress = "0x6666666666666666666666666666666666666666";

            var factoryData = "0x0c55699c00000000000000000000000000000000000000000000000000000000000012fd";
            var userOp = CreateSentinelPackedUserOp(senderAddress, factoryData.HexToByteArray());
            var auth = _fixture.SignAuthorization(senderKey, delegateAddress, 0).ToRPCAuthorisation();

            var result = await CreateStrictValidator().ValidateStructureAsync(
                userOp, _fixture.EntryPointService.ContractAddress, auth);

            Assert.True(result.IsValid, result.Error);
        }

        [Fact]
        public async Task Eip7702InitMarker_NoFactoryData_IsAccepted_UnderStrictValidation()
        {
            var (senderKey, senderAddress) = _fixture.GenerateNewAccount();
            var delegateAddress = "0x7777777777777777777777777777777777777777";

            var userOp = CreateSentinelPackedUserOp(senderAddress, Array.Empty<byte>());
            var auth = _fixture.SignAuthorization(senderKey, delegateAddress, 0).ToRPCAuthorisation();

            var result = await CreateStrictValidator().ValidateStructureAsync(
                userOp, _fixture.EntryPointService.ContractAddress, auth);

            Assert.True(result.IsValid, result.Error);
        }

        [Fact]
        public async Task Eip7702InitMarker_WithoutAuthorization_IsRejected_AsFactoryNotDeployed()
        {
            var (_, senderAddress) = _fixture.GenerateNewAccount();
            var factoryData = "0x0c55699c00000000000000000000000000000000000000000000000000000000000012fd";
            var userOp = CreateSentinelPackedUserOp(senderAddress, factoryData.HexToByteArray());

            var result = await CreateValidator().ValidateStructureAsync(
                userOp, _fixture.EntryPointService.ContractAddress);

            Assert.False(result.IsValid);
            Assert.Contains("AA13", result.Error);
        }

        [Fact]
        public async Task ShortInitCode_NotSentinel_WithAuthorization_IsStillRejected_AsTooShort()
        {
            var (senderKey, senderAddress) = _fixture.GenerateNewAccount();
            var delegateAddress = "0x8888888888888888888888888888888888888888";

            var userOp = CreateSentinelPackedUserOp(senderAddress, Array.Empty<byte>());
            userOp.InitCode = new byte[] { 0x01, 0x02, 0x03 };
            var auth = _fixture.SignAuthorization(senderKey, delegateAddress, 0).ToRPCAuthorisation();

            var result = await CreateStrictValidator().ValidateStructureAsync(
                userOp, _fixture.EntryPointService.ContractAddress, auth);

            Assert.False(result.IsValid);
            Assert.Contains("too short", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetFactoryInfoAsync_ForEip7702Sentinel_ReturnsNull_RealFactoryUnchanged()
        {
            var stakingInfo = new StakingInfoService(_fixture.Web3, new BundlerConfig
            {
                SupportedEntryPoints = new[] { _fixture.EntryPointService.ContractAddress },
                ChainId = DevChainBundlerFixture.CHAIN_ID
            });
            var entryPoint = _fixture.EntryPointService.ContractAddress;

            var factoryData = "0x0c55699c00000000000000000000000000000000000000000000000000000000000012fd".HexToByteArray();
            var sentinelInitCode = ByteUtil.Merge(
                AAEIP7702Utils.INITCODE_EIP7702_MARKER, factoryData);
            var realFactoryInitCode = ByteUtil.Merge(
                _fixture.AccountFactoryService.ContractAddress.HexToByteArray(), factoryData);

            Assert.Null(await stakingInfo.GetFactoryInfoAsync(sentinelInitCode, entryPoint));
            Assert.NotNull(await stakingInfo.GetFactoryInfoAsync(realFactoryInitCode, entryPoint));
        }

        [Fact]
        public async Task Eip7702InitMarker_WithFactoryData_UnderErc7562Validation_NotRejectedByFactoryPath()
        {
            var (senderKey, senderAddress) = _fixture.GenerateNewAccount();
            var delegateAddress = "0x9999999999999999999999999999999999999999";

            var factoryData = "0x0c55699c00000000000000000000000000000000000000000000000000000000000012fd";
            var userOp = CreateSentinelPackedUserOp(senderAddress, factoryData.HexToByteArray());
            var auth = _fixture.SignAuthorization(senderKey, delegateAddress, 0).ToRPCAuthorisation();

            var result = await CreateErc7562Validator().ValidateAsync(
                userOp, _fixture.EntryPointService.ContractAddress, EmptySet(), auth);

            var error = result.Error ?? string.Empty;
            Assert.DoesNotContain("OP-041", error);
            Assert.DoesNotContain("factory", error, StringComparison.OrdinalIgnoreCase);
            _output.WriteLine($"IsValid={result.IsValid}, Error={error}");
        }

        private UserOpValidator CreateErc7562Validator()
        {
            var config = new BundlerConfig
            {
                SupportedEntryPoints = new[] { _fixture.EntryPointService.ContractAddress },
                BeneficiaryAddress = _fixture.BundlerAccount.Address,
                MaxBundleSize = 10,
                MaxMempoolSize = 100,
                AutoBundleIntervalMs = 0,
                StrictValidation = true,
                SimulateValidation = false,
                EnableERC7562Validation = true,
                UnsafeMode = true,
                MaxVerificationGas = 10_000_000,
                ChainId = DevChainBundlerFixture.CHAIN_ID
            };
            return new UserOpValidator(_fixture.Web3, config);
        }

        private UserOpValidator CreateStrictValidator()
        {
            var config = new BundlerConfig
            {
                SupportedEntryPoints = new[] { _fixture.EntryPointService.ContractAddress },
                BeneficiaryAddress = _fixture.BundlerAccount.Address,
                MaxBundleSize = 10,
                MaxMempoolSize = 100,
                AutoBundleIntervalMs = 0,
                StrictValidation = true,
                SimulateValidation = false,
                UnsafeMode = true,
                ChainId = DevChainBundlerFixture.CHAIN_ID
            };
            return new UserOpValidator(_fixture.Web3, config);
        }

        private static PackedUserOperation CreateSentinelPackedUserOp(string sender, byte[] factoryData)
        {
            var userOp = CreateValidPackedUserOp(sender);
            userOp.InitCode = ByteUtil.Merge(
                AAEIP7702Utils.INITCODE_EIP7702_MARKER,
                factoryData ?? Array.Empty<byte>());
            return userOp;
        }

        private UserOpValidator CreateValidator()
        {
            var config = new BundlerConfig
            {
                SupportedEntryPoints = new[] { _fixture.EntryPointService.ContractAddress },
                BeneficiaryAddress = _fixture.BundlerAccount.Address,
                MaxBundleSize = 10,
                MaxMempoolSize = 100,
                AutoBundleIntervalMs = 0,
                StrictValidation = false,
                SimulateValidation = false,
                UnsafeMode = true,
                ChainId = DevChainBundlerFixture.CHAIN_ID
            };
            return new UserOpValidator(_fixture.Web3, config);
        }

        private static ISet<string> EmptySet() =>
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static PackedUserOperation CreateValidPackedUserOp(string sender)
        {
            var accountGasLimits = new byte[32];
            PackBigEndian(accountGasLimits, 8, 100_000);
            PackBigEndian(accountGasLimits, 24, 100_000);

            var gasFees = new byte[32];
            PackBigEndian(gasFees, 8, 1_000_000_000);
            PackBigEndian(gasFees, 24, 2_000_000_000);

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
                Signature = new byte[65]
            };
        }

        private static void PackBigEndian(byte[] target, int offset, long value)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            Array.Copy(bytes, 0, target, offset, 8);
        }
    }
}
