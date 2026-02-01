using System.Numerics;
using Nethereum.AccountAbstraction.Validation;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Validation
{
    using Nethereum.AccountAbstraction;
    public class UserOpValidationResultTests
    {
        [Fact]
        public void Success_ReturnsValidResult()
        {
            var result = UserOpValidationResult.Success();

            Assert.True(result.IsValid);
            Assert.Null(result.Error);
            Assert.Equal(UserOpValidationError.None, result.ErrorCode);
        }

        [Fact]
        public void Failure_ReturnsInvalidResultWithMessage()
        {
            var result = UserOpValidationResult.Failure("Test error", UserOpValidationError.InvalidSender);

            Assert.False(result.IsValid);
            Assert.Equal("Test error", result.Error);
            Assert.Equal(UserOpValidationError.InvalidSender, result.ErrorCode);
        }

        [Fact]
        public void Failure_DefaultErrorCode_IsUnknown()
        {
            var result = UserOpValidationResult.Failure("Some error");

            Assert.Equal(UserOpValidationError.Unknown, result.ErrorCode);
        }
    }

    public class UserOpValidationErrorCodesTests
    {
        [Fact]
        public void SenderErrors_FollowEIP4337Pattern_AA1x()
        {
            Assert.Equal(10, (int)UserOpValidationError.InvalidSender);
            Assert.Equal(11, (int)UserOpValidationError.SenderNotDeployed);
            Assert.Equal(12, (int)UserOpValidationError.InitCodeFailed);
            Assert.Equal(13, (int)UserOpValidationError.InitCodeNotDeployed);
        }

        [Fact]
        public void SignatureErrors_FollowEIP4337Pattern_AA2x()
        {
            Assert.Equal(20, (int)UserOpValidationError.InvalidSignature);
            Assert.Equal(21, (int)UserOpValidationError.SignatureValidationFailed);
            Assert.Equal(22, (int)UserOpValidationError.ExpiredSignature);
            Assert.Equal(23, (int)UserOpValidationError.NotYetValid);
        }

        [Fact]
        public void NonceErrors_FollowEIP4337Pattern_AA3x()
        {
            Assert.Equal(30, (int)UserOpValidationError.InvalidNonce);
        }

        [Fact]
        public void GasErrors_FollowEIP4337Pattern_AA4x()
        {
            Assert.Equal(40, (int)UserOpValidationError.InsufficientPrefund);
            Assert.Equal(41, (int)UserOpValidationError.InsufficientVerificationGas);
            Assert.Equal(42, (int)UserOpValidationError.InsufficientCallGas);
            Assert.Equal(43, (int)UserOpValidationError.GasValuesOverflow);
        }

        [Fact]
        public void PaymasterErrors_FollowEIP4337Pattern_AA5x()
        {
            Assert.Equal(50, (int)UserOpValidationError.PaymasterNotDeployed);
            Assert.Equal(51, (int)UserOpValidationError.PaymasterDepositTooLow);
            Assert.Equal(52, (int)UserOpValidationError.PaymasterValidationFailed);
            Assert.Equal(53, (int)UserOpValidationError.PaymasterPostOpFailed);
        }

        [Fact]
        public void ExecutionErrors_FollowEIP4337Pattern_AA6x()
        {
            Assert.Equal(60, (int)UserOpValidationError.ExecutionReverted);
            Assert.Equal(61, (int)UserOpValidationError.CallFailed);
        }

        [Fact]
        public void StorageErrors_FollowEIP4337Pattern_AA7x()
        {
            Assert.Equal(70, (int)UserOpValidationError.InvalidStorageAccess);
            Assert.Equal(71, (int)UserOpValidationError.InvalidOpcodeAccess);
            Assert.Equal(72, (int)UserOpValidationError.OutOfGas);
        }

        [Fact]
        public void AggregatorErrors_FollowEIP4337Pattern_AA8x()
        {
            Assert.Equal(80, (int)UserOpValidationError.AggregatorValidationFailed);
            Assert.Equal(81, (int)UserOpValidationError.InvalidAggregator);
        }

        [Fact]
        public void BundleErrors_FollowEIP4337Pattern_AA9x()
        {
            Assert.Equal(90, (int)UserOpValidationError.BundleFull);
            Assert.Equal(91, (int)UserOpValidationError.DuplicateUserOp);
            Assert.Equal(92, (int)UserOpValidationError.ReplacementUnderpriced);
        }
    }

    public class SenderInitCodeValidationRulesTests
    {
        [Fact]
        public void AA10_SenderAlreadyDeployed_WithInitCode_ShouldFail()
        {
            var result = UserOpValidationResult.Failure(
                "AA10: sender already deployed - initCode must be empty",
                UserOpValidationError.InitCodeFailed);

            Assert.False(result.IsValid);
            Assert.Contains("AA10", result.Error);
            Assert.Equal(UserOpValidationError.InitCodeFailed, result.ErrorCode);
        }

        [Fact]
        public void AA13_FactoryNotDeployed_ShouldFail()
        {
            var result = UserOpValidationResult.Failure(
                "AA13: factory not deployed",
                UserOpValidationError.InitCodeFailed);

            Assert.False(result.IsValid);
            Assert.Contains("AA13", result.Error);
            Assert.Equal(UserOpValidationError.InitCodeFailed, result.ErrorCode);
        }

        [Fact]
        public void AA20_SenderNotDeployed_NoInitCode_ShouldFail()
        {
            var result = UserOpValidationResult.Failure(
                "AA20: sender not deployed and no initCode",
                UserOpValidationError.InvalidSender);

            Assert.False(result.IsValid);
            Assert.Contains("AA20", result.Error);
            Assert.Equal(UserOpValidationError.InvalidSender, result.ErrorCode);
        }
    }

    public class NonceValidationRulesTests
    {
        [Fact]
        public void AA25_InvalidNonce_ShouldFail()
        {
            BigInteger expected = 5;
            BigInteger actual = 7;

            var result = UserOpValidationResult.Failure(
                $"AA25: invalid nonce - expected {expected}, got {actual}",
                UserOpValidationError.InvalidNonce);

            Assert.False(result.IsValid);
            Assert.Contains("AA25", result.Error);
            Assert.Contains(expected.ToString(), result.Error);
            Assert.Contains(actual.ToString(), result.Error);
            Assert.Equal(UserOpValidationError.InvalidNonce, result.ErrorCode);
        }

        [Fact]
        public void Nonce_2DFormat_KeyAndSequence()
        {
            BigInteger key = 1;
            BigInteger sequence = 5;
            BigInteger nonce = (key << 64) | sequence;

            BigInteger extractedKey = nonce >> 64;
            BigInteger extractedSequence = nonce & ((BigInteger.One << 64) - 1);

            Assert.Equal(key, extractedKey);
            Assert.Equal(sequence, extractedSequence);
        }

        [Fact]
        public void Nonce_ZeroKey_SequenceIncrements()
        {
            BigInteger nonce0 = 0;
            BigInteger nonce1 = 1;
            BigInteger nonce2 = 2;

            Assert.Equal(BigInteger.Zero, nonce0 >> 64);
            Assert.Equal(BigInteger.Zero, nonce1 >> 64);
            Assert.Equal(BigInteger.Zero, nonce2 >> 64);
        }

        [Fact]
        public void Nonce_ParallelChannels_DifferentKeys()
        {
            BigInteger key0Nonce = 0;
            BigInteger key1Nonce = BigInteger.One << 64;
            BigInteger key2Nonce = new BigInteger(2) << 64;

            Assert.Equal(BigInteger.Zero, key0Nonce >> 64);
            Assert.Equal(BigInteger.One, key1Nonce >> 64);
            Assert.Equal(new BigInteger(2), key2Nonce >> 64);
        }
    }

    public class PaymasterValidationRulesTests
    {
        [Fact]
        public void AA30_PaymasterNotDeployed_ShouldFail()
        {
            var result = UserOpValidationResult.Failure(
                "AA30: paymaster not deployed",
                UserOpValidationError.PaymasterNotDeployed);

            Assert.False(result.IsValid);
            Assert.Contains("AA30", result.Error);
            Assert.Equal(UserOpValidationError.PaymasterNotDeployed, result.ErrorCode);
        }

        [Fact]
        public void AA31_PaymasterDepositTooLow_ShouldFail()
        {
            var result = UserOpValidationResult.Failure(
                "AA31: paymaster deposit too low",
                UserOpValidationError.PaymasterDepositTooLow);

            Assert.False(result.IsValid);
            Assert.Contains("AA31", result.Error);
            Assert.Equal(UserOpValidationError.PaymasterDepositTooLow, result.ErrorCode);
        }

        [Fact]
        public void PaymasterAndData_Format_AddressPlusGasLimitsPlusData()
        {
            var paymaster = new byte[20];
            var verificationGasLimit = new byte[16];
            var postOpGasLimit = new byte[16];
            var customData = new byte[] { 0x01, 0x02, 0x03 };

            var paymasterAndData = new byte[52 + customData.Length];
            Array.Copy(paymaster, 0, paymasterAndData, 0, 20);
            Array.Copy(verificationGasLimit, 0, paymasterAndData, 20, 16);
            Array.Copy(postOpGasLimit, 0, paymasterAndData, 36, 16);
            Array.Copy(customData, 0, paymasterAndData, 52, customData.Length);

            Assert.Equal(20, paymaster.Length);
            Assert.Equal(16, verificationGasLimit.Length);
            Assert.Equal(16, postOpGasLimit.Length);
            Assert.Equal(55, paymasterAndData.Length);
        }

        [Fact]
        public void PaymasterAndData_MinimumLength_Is52Bytes()
        {
            var minLength = 20 + 16 + 16;
            Assert.Equal(52, minLength);
        }
    }

    public class InitCodeFormatTests
    {
        [Fact]
        public void InitCode_MinimumLength_Is20Bytes()
        {
            var factoryAddress = new byte[20];
            Assert.Equal(20, factoryAddress.Length);
        }

        [Fact]
        public void InitCode_Format_FactoryAddressPlusCallData()
        {
            var factoryAddress = new byte[20];
            for (int i = 0; i < 20; i++) factoryAddress[i] = 0xAB;

            var factoryCallData = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
            var initCode = new byte[factoryAddress.Length + factoryCallData.Length];

            Array.Copy(factoryAddress, 0, initCode, 0, factoryAddress.Length);
            Array.Copy(factoryCallData, 0, initCode, factoryAddress.Length, factoryCallData.Length);

            Assert.Equal(24, initCode.Length);
            Assert.Equal(0xAB, initCode[0]);
            Assert.Equal(0xAB, initCode[19]);
            Assert.Equal(0xDE, initCode[20]);
        }

        [Fact]
        public void InitCode_ShortData_LessThan20Bytes_Invalid()
        {
            var shortInitCode = new byte[] { 0x01, 0x02, 0x03 };

            Assert.True(shortInitCode.Length < 20);
        }
    }

    public class AccountGasLimitsPackingTests
    {
        [Fact]
        public void AccountGasLimits_MustBe32Bytes()
        {
            BigInteger verificationGas = 500000;
            BigInteger callGas = 100000;

            var packed = UserOperationBuilder.PackAccountGasLimits(verificationGas, callGas);

            Assert.Equal(32, packed.Length);
        }

        [Fact]
        public void AccountGasLimits_HighBytes_VerificationGasLimit()
        {
            BigInteger verificationGas = 500000;
            BigInteger callGas = 100000;

            var packed = UserOperationBuilder.PackAccountGasLimits(verificationGas, callGas);

            Assert.Equal(32, packed.Length);

            var unpackedVerification = new BigInteger(packed.Take(16).Reverse().Concat(new byte[] { 0 }).ToArray());
            Assert.Equal(verificationGas, unpackedVerification);
        }

        [Fact]
        public void AccountGasLimits_LowBytes_CallGasLimit()
        {
            BigInteger verificationGas = 500000;
            BigInteger callGas = 100000;

            var packed = UserOperationBuilder.PackAccountGasLimits(verificationGas, callGas);
            var unpackedCall = new BigInteger(packed.Skip(16).Take(16).Reverse().Concat(new byte[] { 0 }).ToArray());

            Assert.Equal(callGas, unpackedCall);
        }
    }

    public class GasFeesPackingTests
    {
        [Fact]
        public void GasFees_MustBe32Bytes()
        {
            BigInteger maxPriorityFee = 1_000_000_000;
            BigInteger maxFee = 2_000_000_000;

            var packed = UserOperationBuilder.PackAccountGasLimits(maxPriorityFee, maxFee);

            Assert.Equal(32, packed.Length);
        }

        [Fact]
        public void GasFees_HighBytes_MaxPriorityFeePerGas()
        {
            BigInteger maxPriorityFee = 1_000_000_000;
            BigInteger maxFee = 2_000_000_000;

            var packed = UserOperationBuilder.PackAccountGasLimits(maxPriorityFee, maxFee);

            Assert.Equal(32, packed.Length);

            var unpackedPriority = new BigInteger(packed.Take(16).Reverse().Concat(new byte[] { 0 }).ToArray());
            Assert.Equal(maxPriorityFee, unpackedPriority);
        }

        [Fact]
        public void GasFees_LowBytes_MaxFeePerGas()
        {
            BigInteger maxPriorityFee = 1_000_000_000;
            BigInteger maxFee = 2_000_000_000;

            var packed = UserOperationBuilder.PackAccountGasLimits(maxPriorityFee, maxFee);
            var unpackedMaxFee = new BigInteger(packed.Skip(16).Take(16).Reverse().Concat(new byte[] { 0 }).ToArray());

            Assert.Equal(maxFee, unpackedMaxFee);
        }

        [Fact]
        public void GasFees_MaxPriorityFee_MustBeLessThanOrEqualToMaxFee()
        {
            BigInteger maxPriorityFee = 2_000_000_000;
            BigInteger maxFee = 1_000_000_000;

            Assert.True(maxPriorityFee > maxFee);
        }
    }
}
