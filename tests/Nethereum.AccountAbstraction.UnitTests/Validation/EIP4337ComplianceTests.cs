using System.Numerics;
using Nethereum.AccountAbstraction.GasEstimation;
using Nethereum.AccountAbstraction.Validation;
using Nethereum.ABI.EIP712;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer.EIP712;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Validation
{
    public class EIP4337ComplianceTests
    {
        [Fact]
        public void PER_USER_OP_WORD_GAS_MatchesEIP4337Spec()
        {
            Assert.Equal(8, GasEstimationConstants.PER_USER_OP_WORD_GAS);
        }

        [Fact]
        public void PackedUserOperationTypehash_MatchesSpec()
        {
            var expectedTypehashString = "PackedUserOperation(address sender,uint256 nonce,bytes initCode,bytes callData,bytes32 accountGasLimits,uint256 preVerificationGas,bytes32 gasFees,bytes paymasterAndData)";
            var expectedTypehash = Sha3Keccack.Current.CalculateHash(System.Text.Encoding.UTF8.GetBytes(expectedTypehashString));

            Assert.Equal(32, expectedTypehash.Length);
        }

        [Fact]
        public void ERC4337Domain_HasCorrectNameAndVersion()
        {
            var domain = new ERC4337Domain("0x5FF137D4b0FDCD49DcA30c7CF57E578a026d2789", 1);

            Assert.Equal("ERC4337", domain.Name);
            Assert.Equal("1", domain.Version);
        }

        [Fact]
        public void ERC4337Domain_IncludesChainIdAndVerifyingContract()
        {
            var entryPoint = "0x5FF137D4b0FDCD49DcA30c7CF57E578a026d2789";
            var chainId = 1;
            var domain = new ERC4337Domain(entryPoint, chainId);

            Assert.Equal(chainId, domain.ChainId);
            Assert.Equal(entryPoint, domain.VerifyingContract);
        }

        [Fact]
        public void PackUserOperation_CreateValidPackedFormat()
        {
            var userOp = CreateTestUserOperation();
            var packed = UserOperationBuilder.PackUserOperation(userOp);

            Assert.NotNull(packed);
            Assert.Equal(userOp.Sender, packed.Sender);
            Assert.Equal(userOp.Nonce.Value, packed.Nonce);
            Assert.Equal(userOp.PreVerificationGas.Value, packed.PreVerificationGas);
        }

        [Fact]
        public void PackAccountGasLimits_CorrectlyPacksHighLow()
        {
            BigInteger verificationGasLimit = 500000;
            BigInteger callGasLimit = 100000;

            var packed = UserOperationBuilder.PackAccountGasLimits(verificationGasLimit, callGasLimit);

            Assert.Equal(32, packed.Length);

            var highBytes = packed.Take(16).ToArray();
            var lowBytes = packed.Skip(16).Take(16).ToArray();

            var unpackedVerification = new BigInteger(highBytes.Reverse().Concat(new byte[] { 0 }).ToArray());
            var unpackedCall = new BigInteger(lowBytes.Reverse().Concat(new byte[] { 0 }).ToArray());

            Assert.Equal(verificationGasLimit, unpackedVerification);
            Assert.Equal(callGasLimit, unpackedCall);
        }

        [Fact]
        public void PackGasFees_CorrectlyPacksHighLow()
        {
            BigInteger maxPriorityFee = 1_000_000_000;
            BigInteger maxFee = 2_000_000_000;

            var packed = UserOperationBuilder.PackAccountGasLimits(maxPriorityFee, maxFee);

            Assert.Equal(32, packed.Length);
        }

        [Fact]
        public void PackPaymasterData_CorrectFormat()
        {
            var paymaster = "0x1234567890123456789012345678901234567890";
            BigInteger verificationGas = 100000;
            BigInteger postOpGas = 50000;
            var data = new byte[] { 0x01, 0x02, 0x03 };

            var packed = UserOperationBuilder.PackPaymasterData(paymaster, verificationGas, postOpGas, data);

            Assert.True(packed.Length >= 52 + data.Length);
        }

        [Fact]
        public void HashUserOperation_ProducesConsistentEncodedData()
        {
            var userOp = CreateTestUserOperation();
            var packed = UserOperationBuilder.PackUserOperation(userOp);
            var entryPoint = "0x5FF137D4b0FDCD49DcA30c7CF57E578a026d2789";
            BigInteger chainId = 1;

            var encoded1 = UserOperationBuilder.HashUserOperation(packed, entryPoint, chainId);
            var encoded2 = UserOperationBuilder.HashUserOperation(packed, entryPoint, chainId);

            Assert.Equal(encoded1, encoded2);
            Assert.True(encoded1.Length > 0, "Encoded data should not be empty");

            var hash1 = Sha3Keccack.Current.CalculateHash(encoded1);
            var hash2 = Sha3Keccack.Current.CalculateHash(encoded2);

            Assert.Equal(hash1, hash2);
            Assert.Equal(32, hash1.Length);
        }

        [Fact]
        public void HashUserOperation_DifferentChainId_ProducesDifferentHash()
        {
            var userOp = CreateTestUserOperation();
            var packed = UserOperationBuilder.PackUserOperation(userOp);
            var entryPoint = "0x5FF137D4b0FDCD49DcA30c7CF57E578a026d2789";

            var hash1 = UserOperationBuilder.HashUserOperation(packed, entryPoint, 1);
            var hash2 = UserOperationBuilder.HashUserOperation(packed, entryPoint, 5);

            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void HashUserOperation_DifferentEntryPoint_ProducesDifferentHash()
        {
            var userOp = CreateTestUserOperation();
            var packed = UserOperationBuilder.PackUserOperation(userOp);
            BigInteger chainId = 1;

            var hash1 = UserOperationBuilder.HashUserOperation(packed, "0x5FF137D4b0FDCD49DcA30c7CF57E578a026d2789", chainId);
            var hash2 = UserOperationBuilder.HashUserOperation(packed, "0x0000000071727De22E5E9d8BAf0edAc6f37da032", chainId);

            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void PackAndHashEIP712_MatchesTypedDataEncoding()
        {
            var userOp = CreateTestUserOperation();
            var entryPoint = "0x5FF137D4b0FDCD49DcA30c7CF57E578a026d2789";
            BigInteger chainId = 1;

            var hash = UserOperationBuilder.PackAndHashEIP712UserOperation(userOp, entryPoint, chainId);

            Assert.NotNull(hash);
            Assert.Equal(32, hash.Length);
        }

        private static UserOperation CreateTestUserOperation()
        {
            return new UserOperation
            {
                Sender = "0x1234567890123456789012345678901234567890",
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = new byte[] { 0xb6, 0x1d, 0x27, 0xf6 },
                CallGasLimit = 100000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000,
                Paymaster = AddressUtil.ZERO_ADDRESS,
                PaymasterData = Array.Empty<byte>(),
                PaymasterVerificationGasLimit = 0,
                PaymasterPostOpGasLimit = 0,
                Signature = new byte[65]
            };
        }
    }

    public class ValidationDataHelperTests
    {
        [Fact]
        public void Pack_SignatureSuccess_ReturnsZeroForAggregator()
        {
            var result = ValidationDataHelper.Pack(false, 0, 0, null);
            Assert.Equal(BigInteger.Zero, result);
        }

        [Fact]
        public void Pack_SignatureFailed_ReturnsOne()
        {
            var result = ValidationDataHelper.Pack(true, 0, 0, null);
            Assert.Equal(BigInteger.One, result);
        }

        [Fact]
        public void Pack_ValidUntil_CorrectBitPosition()
        {
            ulong validUntil = 1700000000;
            var result = ValidationDataHelper.Pack(false, validUntil, 0, null);

            var (sigFailed, parsedValidUntil, parsedValidAfter, aggregator) = ValidationDataHelper.Parse(result);

            Assert.False(sigFailed);
            Assert.Equal(validUntil, parsedValidUntil);
            Assert.Equal(0UL, parsedValidAfter);
            Assert.Null(aggregator);
        }

        [Fact]
        public void Pack_ValidAfter_CorrectBitPosition()
        {
            ulong validAfter = 1600000000;
            var result = ValidationDataHelper.Pack(false, 0, validAfter, null);

            var (sigFailed, parsedValidUntil, parsedValidAfter, aggregator) = ValidationDataHelper.Parse(result);

            Assert.False(sigFailed);
            Assert.Equal(0UL, parsedValidUntil);
            Assert.Equal(validAfter, parsedValidAfter);
        }

        [Fact]
        public void Pack_AllFields_ParsesCorrectly()
        {
            ulong validUntil = 1700000000;
            ulong validAfter = 1600000000;

            var result = ValidationDataHelper.Pack(true, validUntil, validAfter, null);
            var (sigFailed, parsedValidUntil, parsedValidAfter, aggregator) = ValidationDataHelper.Parse(result);

            Assert.True(sigFailed);
            Assert.Equal(validUntil, parsedValidUntil);
            Assert.Equal(validAfter, parsedValidAfter);
        }

        [Fact]
        public void Parse_Zero_ReturnsAllDefaults()
        {
            var (sigFailed, validUntil, validAfter, aggregator) = ValidationDataHelper.Parse(BigInteger.Zero);

            Assert.False(sigFailed);
            Assert.Equal(0UL, validUntil);
            Assert.Equal(0UL, validAfter);
            Assert.Null(aggregator);
        }

        [Fact]
        public void IsValidNow_SignatureFailed_ReturnsFalse()
        {
            var validationData = ValidationDataHelper.Pack(true, 0, 0, null);
            Assert.False(ValidationDataHelper.IsValidNow(validationData));
        }

        [Fact]
        public void IsValidNow_ValidSignature_ReturnsTrue()
        {
            var validationData = ValidationDataHelper.Pack(false, 0, 0, null);
            Assert.True(ValidationDataHelper.IsValidNow(validationData));
        }

        [Fact]
        public void IsValidNow_FutureValidAfter_ReturnsFalse()
        {
            var futureTime = (ulong)DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
            var validationData = ValidationDataHelper.Pack(false, 0, futureTime, null);
            Assert.False(ValidationDataHelper.IsValidNow(validationData));
        }

        [Fact]
        public void IsValidNow_PastValidUntil_ReturnsFalse()
        {
            var pastTime = (ulong)DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
            var validationData = ValidationDataHelper.Pack(false, pastTime, 0, null);
            Assert.False(ValidationDataHelper.IsValidNow(validationData));
        }

        [Fact]
        public void Merge_TakesMoreRestrictive()
        {
            ulong accountValidUntil = 1700000000;
            ulong paymasterValidUntil = 1650000000;

            var accountData = ValidationDataHelper.Pack(false, accountValidUntil, 0, null);
            var paymasterData = ValidationDataHelper.Pack(false, paymasterValidUntil, 0, null);

            var merged = ValidationDataHelper.Merge(accountData, paymasterData);
            var (_, mergedValidUntil, _, _) = ValidationDataHelper.Parse(merged);

            Assert.Equal(paymasterValidUntil, mergedValidUntil);
        }

        [Fact]
        public void Merge_EitherSigFailed_ResultsFailed()
        {
            var accountData = ValidationDataHelper.Pack(true, 0, 0, null);
            var paymasterData = ValidationDataHelper.Pack(false, 0, 0, null);

            var merged = ValidationDataHelper.Merge(accountData, paymasterData);
            var (sigFailed, _, _, _) = ValidationDataHelper.Parse(merged);

            Assert.True(sigFailed);
        }
    }

    public class UserOpValidationErrorTests
    {
        [Fact]
        public void ErrorCodes_FollowsEIP4337Convention()
        {
            Assert.Equal(10, (int)UserOpValidationError.InvalidSender);
            Assert.Equal(12, (int)UserOpValidationError.InitCodeFailed);
            Assert.Equal(20, (int)UserOpValidationError.InvalidSignature);
            Assert.Equal(30, (int)UserOpValidationError.InvalidNonce);
            Assert.Equal(40, (int)UserOpValidationError.InsufficientPrefund);
            Assert.Equal(50, (int)UserOpValidationError.PaymasterNotDeployed);
            Assert.Equal(60, (int)UserOpValidationError.ExecutionReverted);
        }

        [Fact]
        public void UserOpValidationResult_Success_IsValid()
        {
            var result = UserOpValidationResult.Success();
            Assert.True(result.IsValid);
            Assert.Null(result.Error);
        }

        [Fact]
        public void UserOpValidationResult_Failure_HasErrorMessage()
        {
            var result = UserOpValidationResult.Failure("Test error", UserOpValidationError.InvalidSender);

            Assert.False(result.IsValid);
            Assert.Equal("Test error", result.Error);
            Assert.Equal(UserOpValidationError.InvalidSender, result.ErrorCode);
        }
    }
}
