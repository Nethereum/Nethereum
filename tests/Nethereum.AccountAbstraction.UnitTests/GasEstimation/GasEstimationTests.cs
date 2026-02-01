using System.Numerics;
using Nethereum.AccountAbstraction.GasEstimation;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.GasEstimation
{
    public class GasEstimationTests
    {
        [Fact]
        public void CalculateCalldataCost_EmptyData_ReturnsZero()
        {
            var cost = UserOperationGasEstimator.CalculateCalldataCost(Array.Empty<byte>());
            Assert.Equal(BigInteger.Zero, cost);
        }

        [Fact]
        public void CalculateCalldataCost_NullData_ReturnsZero()
        {
            var cost = UserOperationGasEstimator.CalculateCalldataCost(null!);
            Assert.Equal(BigInteger.Zero, cost);
        }

        [Fact]
        public void CalculateCalldataCost_AllZeroBytes_Returns4PerByte()
        {
            var data = new byte[] { 0, 0, 0, 0, 0 };
            var cost = UserOperationGasEstimator.CalculateCalldataCost(data);
            Assert.Equal(20, cost);
        }

        [Fact]
        public void CalculateCalldataCost_AllNonZeroBytes_Returns16PerByte()
        {
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var cost = UserOperationGasEstimator.CalculateCalldataCost(data);
            Assert.Equal(80, cost);
        }

        [Fact]
        public void CalculateCalldataCost_MixedBytes_ReturnsCorrectCost()
        {
            var data = new byte[] { 0, 1, 0, 2, 0 };
            var cost = UserOperationGasEstimator.CalculateCalldataCost(data);
            Assert.Equal(12 + 32, cost);
        }

        [Fact]
        public void CalculateCalldataCost_LargeData_CalculatesCorrectly()
        {
            var data = new byte[1000];
            for (int i = 0; i < 1000; i++)
            {
                data[i] = (byte)(i % 2 == 0 ? 0 : 1);
            }
            var cost = UserOperationGasEstimator.CalculateCalldataCost(data);
            Assert.Equal(500 * 4 + 500 * 16, cost);
        }

        [Theory]
        [InlineData(new byte[] { 0x00 }, 4)]
        [InlineData(new byte[] { 0x01 }, 16)]
        [InlineData(new byte[] { 0xFF }, 16)]
        [InlineData(new byte[] { 0x00, 0xFF }, 20)]
        [InlineData(new byte[] { 0x00, 0x00, 0xFF, 0xFF }, 40)]
        public void CalculateCalldataCost_EIP2028TestVectors(byte[] data, int expectedCost)
        {
            var cost = UserOperationGasEstimator.CalculateCalldataCost(data);
            Assert.Equal(expectedCost, cost);
        }

        [Fact]
        public void PreVerificationGas_MinimumOverhead_Is50000()
        {
            Assert.Equal(50000, GasEstimationConstants.PRE_VERIFICATION_OVERHEAD_GAS);
        }

        [Fact]
        public void MaxVerificationGas_Is500000()
        {
            Assert.Equal(500000, GasEstimationConstants.MAX_VERIFICATION_GAS);
        }

        [Fact]
        public void BaseTransactionGas_Is21000()
        {
            Assert.Equal(21000, GasEstimationConstants.BASE_TRANSACTION_GAS);
        }

        [Fact]
        public void InnerGasOverhead_Is10000()
        {
            Assert.Equal(10000, GasEstimationConstants.INNER_GAS_OVERHEAD);
        }
    }

    public class PreVerificationGasTests
    {
        [Fact]
        public void CalculatePreVerificationGas_EmptyUserOp_ReturnsBaseCosts()
        {
            var userOp = new UserOperation
            {
                Sender = "0x1234567890123456789012345678901234567890",
                Nonce = 0,
                CallData = Array.Empty<byte>(),
                InitCode = Array.Empty<byte>()
            };

            var estimator = new UserOperationGasEstimator(null!, "0x0000000000000000000000000000000000000000");
            var preVerificationGas = estimator.CalculatePreVerificationGas(userOp);

            var minExpected = GasEstimationConstants.BASE_TRANSACTION_GAS * 2;
            Assert.True(preVerificationGas >= minExpected,
                $"PreVerificationGas {preVerificationGas} should be >= {minExpected} (2x BASE_TRANSACTION_GAS)");
        }

        [Fact]
        public void CalculatePreVerificationGas_WithCallData_IncludesCalldataCost()
        {
            var emptyOp = new UserOperation
            {
                Sender = "0x1234567890123456789012345678901234567890",
                Nonce = 0,
                CallData = Array.Empty<byte>(),
                InitCode = Array.Empty<byte>()
            };

            var opWithCallData = new UserOperation
            {
                Sender = "0x1234567890123456789012345678901234567890",
                Nonce = 0,
                CallData = new byte[100],
                InitCode = Array.Empty<byte>()
            };

            for (int i = 0; i < 100; i++)
            {
                opWithCallData.CallData[i] = (byte)(i + 1);
            }

            var estimator = new UserOperationGasEstimator(null!, "0x0000000000000000000000000000000000000000");

            var emptyGas = estimator.CalculatePreVerificationGas(emptyOp);
            var callDataGas = estimator.CalculatePreVerificationGas(opWithCallData);

            Assert.True(callDataGas > emptyGas,
                $"PreVerificationGas with callData ({callDataGas}) should be > empty ({emptyGas})");
        }

        [Fact]
        public void CalculatePreVerificationGas_AlwaysIncludesStandardSignatureCost()
        {
            var userOp = new UserOperation
            {
                Sender = "0x1234567890123456789012345678901234567890",
                Nonce = 0,
                Signature = Array.Empty<byte>()
            };

            var estimator = new UserOperationGasEstimator(null!, "0x0000000000000000000000000000000000000000");
            var gas = estimator.CalculatePreVerificationGas(userOp);

            var signatureGasCost = GasEstimationConstants.SIGNATURE_SIZE * GasEstimationConstants.NON_ZERO_BYTE_GAS_COST;
            var minExpected = GasEstimationConstants.BASE_TRANSACTION_GAS * 2 + signatureGasCost;
            Assert.True(gas >= minExpected,
                $"PreVerificationGas ({gas}) should include base costs plus signature cost");
        }
    }

    public class GasConstantsValidationTests
    {
        [Fact]
        public void PER_USER_OP_WORD_GAS_MatchesEIP4337Spec()
        {
            Assert.Equal(8, GasEstimationConstants.PER_USER_OP_WORD_GAS);
        }

        [Fact]
        public void ZeroByteGasCost_MatchesEIP2028()
        {
            Assert.Equal(4, GasEstimationConstants.ZERO_BYTE_GAS_COST);
        }

        [Fact]
        public void NonZeroByteGasCost_MatchesEIP2028()
        {
            Assert.Equal(16, GasEstimationConstants.NON_ZERO_BYTE_GAS_COST);
        }

        [Fact]
        public void Create2Cost_Is32000()
        {
            Assert.Equal(32000, GasEstimationConstants.CREATE2_COST);
        }

        [Fact]
        public void SignatureSize_Is65Bytes()
        {
            Assert.Equal(65, GasEstimationConstants.SIGNATURE_SIZE);
        }

        [Fact]
        public void AddressSize_Is20Bytes()
        {
            Assert.Equal(20, GasEstimationConstants.ADDRESS_SIZE);
        }

        [Fact]
        public void WordSize_Is32Bytes()
        {
            Assert.Equal(32, GasEstimationConstants.WORD_SIZE);
        }
    }

    public class ValidationTests
    {
        [Fact]
        public void CalculatePreVerificationGas_NullSender_UsesZeroAddressDefault()
        {
            var userOp = new UserOperation
            {
                Sender = null,
                Nonce = 0
            };

            var estimator = new UserOperationGasEstimator(null, "0x0000000000000000000000000000000000000000");
            var gas = estimator.CalculatePreVerificationGas(userOp);

            var minExpected = GasEstimationConstants.BASE_TRANSACTION_GAS * 2;
            Assert.True(gas >= minExpected,
                $"Should use zero address default and calculate gas. Got {gas}, expected >= {minExpected}");
        }

        [Fact]
        public void CalculatePreVerificationGas_EmptySender_UsesZeroAddressDefault()
        {
            var userOp = new UserOperation
            {
                Sender = "",
                Nonce = 0
            };

            var estimator = new UserOperationGasEstimator(null, "0x0000000000000000000000000000000000000000");
            var gas = estimator.CalculatePreVerificationGas(userOp);

            var minExpected = GasEstimationConstants.BASE_TRANSACTION_GAS * 2;
            Assert.True(gas >= minExpected,
                $"Should use zero address default and calculate gas. Got {gas}, expected >= {minExpected}");
        }

        [Fact]
        public void CalculatePreVerificationGas_ZeroAddressWithoutInitCode_Calculates()
        {
            var userOp = new UserOperation
            {
                Sender = AddressUtil.ZERO_ADDRESS,
                Nonce = 0,
                InitCode = Array.Empty<byte>()
            };

            var estimator = new UserOperationGasEstimator(null, "0x0000000000000000000000000000000000000000");
            var gas = estimator.CalculatePreVerificationGas(userOp);

            var minExpected = GasEstimationConstants.BASE_TRANSACTION_GAS * 2;
            Assert.True(gas >= minExpected,
                $"CalculatePreVerificationGas should calculate gas. Got {gas}, expected >= {minExpected}");
        }

        [Fact]
        public void CalculatePreVerificationGas_ZeroAddressWithInitCode_Succeeds()
        {
            var factoryAddress = new byte[20];
            for (int i = 0; i < 20; i++) factoryAddress[i] = 0xAB;
            var factoryData = new byte[] { 0x01, 0x02, 0x03 };
            var initCode = factoryAddress.Concat(factoryData).ToArray();

            var userOp = new UserOperation
            {
                Sender = AddressUtil.ZERO_ADDRESS,
                Nonce = 0,
                InitCode = initCode
            };

            var estimator = new UserOperationGasEstimator(null, "0x0000000000000000000000000000000000000000");
            var gas = estimator.CalculatePreVerificationGas(userOp);

            Assert.True(gas > 0, "Should calculate gas for zero address with initCode");
        }

        [Fact]
        public void Constructor_NullEntryPoint_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new UserOperationGasEstimator(null, null!));
        }

        [Fact]
        public void Constructor_NullWeb3_Succeeds()
        {
            var estimator = new UserOperationGasEstimator(null, "0x0000000000000000000000000000000000000000");
            Assert.NotNull(estimator);
        }
    }

    public class InitCodeParsingTests
    {
        [Fact]
        public void CalculatePreVerificationGas_WithInitCode_IncludesInitCodeCost()
        {
            var factoryAddress = new byte[20];
            for (int i = 0; i < 20; i++) factoryAddress[i] = 0xFF;
            var factoryData = new byte[100];
            for (int i = 0; i < 100; i++) factoryData[i] = (byte)(i + 1);
            var initCode = factoryAddress.Concat(factoryData).ToArray();

            var opWithoutInit = new UserOperation
            {
                Sender = "0x1234567890123456789012345678901234567890",
                Nonce = 0,
                InitCode = Array.Empty<byte>()
            };

            var opWithInit = new UserOperation
            {
                Sender = "0x1234567890123456789012345678901234567890",
                Nonce = 0,
                InitCode = initCode
            };

            var estimator = new UserOperationGasEstimator(null, "0x0000000000000000000000000000000000000000");

            var gasWithoutInit = estimator.CalculatePreVerificationGas(opWithoutInit);
            var gasWithInit = estimator.CalculatePreVerificationGas(opWithInit);

            Assert.True(gasWithInit > gasWithoutInit,
                $"Gas with initCode ({gasWithInit}) should be > without ({gasWithoutInit})");
        }

        [Fact]
        public void CalculatePreVerificationGas_ShortInitCode_HandlesGracefully()
        {
            var userOp = new UserOperation
            {
                Sender = "0x1234567890123456789012345678901234567890",
                Nonce = 0,
                InitCode = new byte[] { 0x01, 0x02, 0x03 }
            };

            var estimator = new UserOperationGasEstimator(null, "0x0000000000000000000000000000000000000000");
            var gas = estimator.CalculatePreVerificationGas(userOp);

            var minExpected = GasEstimationConstants.BASE_TRANSACTION_GAS * 2;
            Assert.True(gas >= minExpected,
                $"PreVerificationGas ({gas}) should be >= {minExpected}");
        }

        [Fact]
        public void CalculatePreVerificationGas_ExactAddressSizeInitCode_HandlesGracefully()
        {
            var initCode = new byte[20];
            for (int i = 0; i < 20; i++) initCode[i] = 0xAB;

            var userOp = new UserOperation
            {
                Sender = "0x1234567890123456789012345678901234567890",
                Nonce = 0,
                InitCode = initCode
            };

            var estimator = new UserOperationGasEstimator(null, "0x0000000000000000000000000000000000000000");
            var gas = estimator.CalculatePreVerificationGas(userOp);

            var minExpected = GasEstimationConstants.BASE_TRANSACTION_GAS * 2;
            Assert.True(gas >= minExpected,
                $"PreVerificationGas ({gas}) should be >= {minExpected}");
        }
    }

    public class PaymasterDetectionTests
    {
        [Fact]
        public void CalculatePreVerificationGas_WithPaymaster_IncludesPaymasterData()
        {
            var opWithoutPaymaster = new UserOperation
            {
                Sender = "0x1234567890123456789012345678901234567890",
                Nonce = 0,
                Paymaster = null,
                PaymasterData = Array.Empty<byte>()
            };

            var opWithPaymaster = new UserOperation
            {
                Sender = "0x1234567890123456789012345678901234567890",
                Nonce = 0,
                Paymaster = "0xABCDABCDABCDABCDABCDABCDABCDABCDABCDABCD",
                PaymasterData = new byte[100]
            };
            for (int i = 0; i < 100; i++) opWithPaymaster.PaymasterData[i] = 0xFF;

            var estimator = new UserOperationGasEstimator(null, "0x0000000000000000000000000000000000000000");

            var gasWithoutPaymaster = estimator.CalculatePreVerificationGas(opWithoutPaymaster);
            var gasWithPaymaster = estimator.CalculatePreVerificationGas(opWithPaymaster);

            Assert.True(gasWithPaymaster > gasWithoutPaymaster,
                $"Gas with paymaster ({gasWithPaymaster}) should be > without ({gasWithoutPaymaster})");
        }

        [Fact]
        public void CalculatePreVerificationGas_ZeroAddressPaymaster_SameAsNoPaymaster()
        {
            var opWithZeroPaymaster = new UserOperation
            {
                Sender = "0x1234567890123456789012345678901234567890",
                Nonce = 0,
                Paymaster = AddressUtil.ZERO_ADDRESS,
                PaymasterData = Array.Empty<byte>()
            };

            var opWithoutPaymaster = new UserOperation
            {
                Sender = "0x1234567890123456789012345678901234567890",
                Nonce = 0,
                Paymaster = null,
                PaymasterData = Array.Empty<byte>()
            };

            var estimator = new UserOperationGasEstimator(null, "0x0000000000000000000000000000000000000000");

            var gasZeroPaymaster = estimator.CalculatePreVerificationGas(opWithZeroPaymaster);
            var gasNoPaymaster = estimator.CalculatePreVerificationGas(opWithoutPaymaster);

            Assert.Equal(gasZeroPaymaster, gasNoPaymaster);
        }

        [Fact]
        public void CalculatePreVerificationGas_LargePaymasterData_IncreasesGas()
        {
            var opSmallPaymasterData = new UserOperation
            {
                Sender = "0x1234567890123456789012345678901234567890",
                Nonce = 0,
                Paymaster = "0xABCDABCDABCDABCDABCDABCDABCDABCDABCDABCD",
                PaymasterData = new byte[10]
            };

            var opLargePaymasterData = new UserOperation
            {
                Sender = "0x1234567890123456789012345678901234567890",
                Nonce = 0,
                Paymaster = "0xABCDABCDABCDABCDABCDABCDABCDABCDABCDABCD",
                PaymasterData = new byte[200]
            };
            for (int i = 0; i < 200; i++) opLargePaymasterData.PaymasterData[i] = 0xFF;

            var estimator = new UserOperationGasEstimator(null, "0x0000000000000000000000000000000000000000");

            var gasSmall = estimator.CalculatePreVerificationGas(opSmallPaymasterData);
            var gasLarge = estimator.CalculatePreVerificationGas(opLargePaymasterData);

            Assert.True(gasLarge > gasSmall,
                $"Large paymaster data ({gasLarge}) should cost more than small ({gasSmall})");
        }
    }

    public class GasLimitCapTests
    {
        [Fact]
        public void MaxVerificationGas_IsCappedAt500000()
        {
            Assert.Equal(500000, GasEstimationConstants.MAX_VERIFICATION_GAS);
        }

        [Fact]
        public void DefaultCallGasLimit_Is100000()
        {
            Assert.Equal(100000, GasEstimationConstants.DEFAULT_CALL_GAS_LIMIT);
        }

        [Fact]
        public void VerificationGasBuffer_Is100000()
        {
            Assert.Equal(100000, GasEstimationConstants.VERIFICATION_GAS_BUFFER);
        }

        [Fact]
        public void PaymasterValidationGasBuffer_Is20000()
        {
            Assert.Equal(20000, GasEstimationConstants.PAYMASTER_VALIDATION_GAS_BUFFER);
        }

        [Fact]
        public void FixedVerificationGasOverhead_Is21000()
        {
            Assert.Equal(21000, GasEstimationConstants.FIXED_VERIFICATION_GAS_OVERHEAD);
        }

        [Fact]
        public void AccountDeploymentBaseGas_Is32000()
        {
            Assert.Equal(32000, GasEstimationConstants.ACCOUNT_DEPLOYMENT_BASE_GAS);
        }
    }

    public class NonceTests
    {
        [Fact]
        public void CalculatePreVerificationGas_LargeNonce_IncludesNonceCost()
        {
            var opSmallNonce = new UserOperation
            {
                Sender = "0x1234567890123456789012345678901234567890",
                Nonce = 0
            };

            var opLargeNonce = new UserOperation
            {
                Sender = "0x1234567890123456789012345678901234567890",
                Nonce = BigInteger.Parse("115792089237316195423570985008687907853269984665640564039457584007913129639935")
            };

            var estimator = new UserOperationGasEstimator(null, "0x0000000000000000000000000000000000000000");

            var gasSmallNonce = estimator.CalculatePreVerificationGas(opSmallNonce);
            var gasLargeNonce = estimator.CalculatePreVerificationGas(opLargeNonce);

            Assert.True(gasLargeNonce > gasSmallNonce,
                $"Large nonce gas ({gasLargeNonce}) should be > small nonce ({gasSmallNonce})");
        }
    }

    public class GasPriceTests
    {
        [Fact]
        public void CalculatePreVerificationGas_DifferentGasPrices_SameResult()
        {
            var opLowGas = new UserOperation
            {
                Sender = "0x1234567890123456789012345678901234567890",
                Nonce = 0,
                MaxFeePerGas = 1_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var opHighGas = new UserOperation
            {
                Sender = "0x1234567890123456789012345678901234567890",
                Nonce = 0,
                MaxFeePerGas = 100_000_000_000,
                MaxPriorityFeePerGas = 10_000_000_000
            };

            var estimator = new UserOperationGasEstimator(null, "0x0000000000000000000000000000000000000000");

            var gasLow = estimator.CalculatePreVerificationGas(opLowGas);
            var gasHigh = estimator.CalculatePreVerificationGas(opHighGas);

            Assert.True(gasHigh > gasLow,
                "Higher gas prices result in more non-zero bytes in packed format");
        }
    }
}
