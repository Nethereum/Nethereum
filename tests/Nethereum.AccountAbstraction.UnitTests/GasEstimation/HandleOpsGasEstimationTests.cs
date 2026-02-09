using System.Numerics;
using Nethereum.AccountAbstraction.GasEstimation;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.GasEstimation
{
    public class EstimationCall
    {
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public BigInteger Value { get; set; }
        public long GasLimit { get; set; }
    }

    public class MockEvmGasEstimator : IEvmGasEstimator
    {
        private readonly BigInteger _gasToReturn;
        private readonly bool _shouldSucceed;
        private readonly string _errorMessage;

        public int CallCount { get; private set; }
        public string LastFrom { get; private set; } = "";
        public string LastTo { get; private set; } = "";
        public byte[] LastData { get; private set; } = Array.Empty<byte>();
        public BigInteger LastValue { get; private set; }
        public long LastGasLimit { get; private set; }
        public List<EstimationCall> AllCalls { get; } = new();

        public MockEvmGasEstimator(BigInteger gasToReturn, bool shouldSucceed = true, string errorMessage = "")
        {
            _gasToReturn = gasToReturn;
            _shouldSucceed = shouldSucceed;
            _errorMessage = errorMessage;
        }

        public Task<EvmEstimationResult> EstimateGasAsync(string from, string to, byte[] data, BigInteger value, long gasLimit)
        {
            CallCount++;
            LastFrom = from;
            LastTo = to;
            LastData = data;
            LastValue = value;
            LastGasLimit = gasLimit;

            AllCalls.Add(new EstimationCall
            {
                From = from,
                To = to,
                Data = data,
                Value = value,
                GasLimit = gasLimit
            });

            return Task.FromResult(new EvmEstimationResult
            {
                Success = _shouldSucceed,
                GasUsed = _gasToReturn,
                Error = _errorMessage
            });
        }
    }

    public class HandleOpsGasEstimationTests
    {
        private const string TestEntryPoint = "0x5FF137D4b0FDCD49DcA30c7CF57E578a026d2789";
        private const string TestSender = "0x1234567890123456789012345678901234567890";
        private const string TestBundler = "0xBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

        [Fact]
        public async Task EstimateGasAsync_WithEvmEstimator_CallsEstimatorCorrectly()
        {
            var mockEstimator = new MockEvmGasEstimator(150000, shouldSucceed: true);
            var estimator = new UserOperationGasEstimator(mockEstimator, TestEntryPoint, TestBundler);

            var userOp = CreateTestUserOperation();
            var result = await estimator.EstimateGasAsync(userOp);

            Assert.True(mockEstimator.CallCount >= 1, "EVM estimator should be called at least once");
        }

        [Fact]
        public async Task EstimateGasAsync_HandleOpsCall_UsesBundlerAndEntryPoint()
        {
            var mockEstimator = new MockEvmGasEstimator(150000, shouldSucceed: true);
            var estimator = new UserOperationGasEstimator(mockEstimator, TestEntryPoint, TestBundler);

            var userOp = CreateTestUserOperation();
            await estimator.EstimateGasAsync(userOp);

            var handleOpsCall = mockEstimator.AllCalls.FirstOrDefault(c => c.To == TestEntryPoint);
            Assert.NotNull(handleOpsCall);
            Assert.Equal(TestBundler, handleOpsCall.From);
        }

        [Fact]
        public async Task EstimateGasAsync_CallGasEstimation_UsesEntryPointAndSender()
        {
            var mockEstimator = new MockEvmGasEstimator(150000, shouldSucceed: true);
            var estimator = new UserOperationGasEstimator(mockEstimator, TestEntryPoint, TestBundler);

            var userOp = CreateTestUserOperation();
            await estimator.EstimateGasAsync(userOp);

            var callGasCall = mockEstimator.AllCalls.FirstOrDefault(c => c.To == TestSender);
            Assert.NotNull(callGasCall);
            Assert.Equal(TestEntryPoint, callGasCall.From);
        }

        [Fact]
        public async Task EstimateGasAsync_WithEvmEstimator_ReturnsVerificationGasLimit()
        {
            var mockEstimator = new MockEvmGasEstimator(180000, shouldSucceed: true);
            var estimator = new UserOperationGasEstimator(mockEstimator, TestEntryPoint);

            var userOp = CreateTestUserOperation();
            var result = await estimator.EstimateGasAsync(userOp);

            Assert.True(result.VerificationGasLimit >= GasEstimationConstants.VERIFICATION_GAS_BUFFER,
                $"VerificationGasLimit ({result.VerificationGasLimit}) should be >= VERIFICATION_GAS_BUFFER ({GasEstimationConstants.VERIFICATION_GAS_BUFFER})");
        }

        [Fact]
        public async Task EstimateGasAsync_EvmEstimatorFails_FallsBackToLegacy()
        {
            var mockEstimator = new MockEvmGasEstimator(0, shouldSucceed: false, errorMessage: "Simulation failed");
            var estimator = new UserOperationGasEstimator(mockEstimator, TestEntryPoint);

            var userOp = CreateTestUserOperation();
            var result = await estimator.EstimateGasAsync(userOp);

            Assert.True(result.VerificationGasLimit > 0,
                "Should return valid gas even when EVM estimation fails");
        }

        [Fact]
        public async Task EstimateGasAsync_WithPaymaster_SetsPaymasterGasLimits()
        {
            var mockEstimator = new MockEvmGasEstimator(200000, shouldSucceed: true);
            var estimator = new UserOperationGasEstimator(mockEstimator, TestEntryPoint);

            var userOp = CreateTestUserOperation();
            userOp.Paymaster = "0xAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
            userOp.PaymasterData = new byte[] { 0x01, 0x02, 0x03 };

            var result = await estimator.EstimateGasAsync(userOp);

            Assert.Equal(GasEstimationConstants.DEFAULT_PAYMASTER_VERIFICATION_GAS_FALLBACK,
                result.PaymasterVerificationGasLimit);
            Assert.Equal(GasEstimationConstants.DEFAULT_PAYMASTER_POST_OP_GAS_FALLBACK,
                result.PaymasterPostOpGasLimit);
        }

        [Fact]
        public async Task EstimateGasAsync_AlwaysCalculatesPreVerificationGas()
        {
            var mockEstimator = new MockEvmGasEstimator(150000, shouldSucceed: true);
            var estimator = new UserOperationGasEstimator(mockEstimator, TestEntryPoint);

            var userOp = CreateTestUserOperation();
            var result = await estimator.EstimateGasAsync(userOp);

            Assert.True(result.PreVerificationGas >= GasEstimationConstants.BASE_TRANSACTION_GAS * 2,
                $"PreVerificationGas ({result.PreVerificationGas}) should be >= {GasEstimationConstants.BASE_TRANSACTION_GAS * 2}");
        }

        [Fact]
        public async Task EstimateCallGasAsync_WithCallData_UsesEvmEstimator()
        {
            var mockEstimator = new MockEvmGasEstimator(50000, shouldSucceed: true);
            var estimator = new UserOperationGasEstimator(mockEstimator, TestEntryPoint);

            var userOp = CreateTestUserOperation();
            userOp.CallData = new byte[100];
            for (int i = 0; i < 100; i++) userOp.CallData[i] = 0xFF;

            var callGas = await estimator.EstimateCallGasAsync(userOp);

            var expectedMinGas = 50000 + GasEstimationConstants.INNER_GAS_OVERHEAD;
            expectedMinGas = (expectedMinGas * (100 + GasEstimationConstants.CALL_GAS_BUFFER_PERCENT)) / 100;

            Assert.True(callGas >= expectedMinGas || callGas >= GasEstimationConstants.DEFAULT_CALL_GAS_LIMIT,
                $"CallGas ({callGas}) should include overhead and buffer");
        }

        [Fact]
        public async Task EstimateCallGasAsync_NoCallData_ReturnsDefault()
        {
            var mockEstimator = new MockEvmGasEstimator(50000, shouldSucceed: true);
            var estimator = new UserOperationGasEstimator(mockEstimator, TestEntryPoint);

            var userOp = CreateTestUserOperation();
            userOp.CallData = Array.Empty<byte>();

            var callGas = await estimator.EstimateCallGasAsync(userOp);

            Assert.Equal(GasEstimationConstants.DEFAULT_CALL_GAS_LIMIT, callGas);
        }

        [Fact]
        public async Task EstimateCallGasAsync_EstimationFails_ReturnsDefault()
        {
            var mockEstimator = new MockEvmGasEstimator(0, shouldSucceed: false, errorMessage: "Out of gas");
            var estimator = new UserOperationGasEstimator(mockEstimator, TestEntryPoint);

            var userOp = CreateTestUserOperation();
            userOp.CallData = new byte[] { 0x01, 0x02, 0x03, 0x04 };

            var callGas = await estimator.EstimateCallGasAsync(userOp);

            Assert.Equal(GasEstimationConstants.DEFAULT_CALL_GAS_LIMIT, callGas);
        }

        [Fact]
        public async Task EstimateGasAsync_VerificationGas_IsCappedAtMax()
        {
            var mockEstimator = new MockEvmGasEstimator(1_000_000, shouldSucceed: true);
            var estimator = new UserOperationGasEstimator(mockEstimator, TestEntryPoint);

            var userOp = CreateTestUserOperation();
            var result = await estimator.EstimateGasAsync(userOp);

            Assert.True(result.VerificationGasLimit <= GasEstimationConstants.MAX_VERIFICATION_GAS,
                $"VerificationGasLimit ({result.VerificationGasLimit}) should be <= MAX ({GasEstimationConstants.MAX_VERIFICATION_GAS})");
        }

        [Fact]
        public async Task EstimateGasAsync_VerificationGas_HasMinimumBuffer()
        {
            var mockEstimator = new MockEvmGasEstimator(35000, shouldSucceed: true);
            var estimator = new UserOperationGasEstimator(mockEstimator, TestEntryPoint);

            var userOp = CreateTestUserOperation();
            var result = await estimator.EstimateGasAsync(userOp);

            Assert.True(result.VerificationGasLimit >= GasEstimationConstants.VERIFICATION_GAS_BUFFER,
                $"VerificationGasLimit ({result.VerificationGasLimit}) should be >= minimum buffer ({GasEstimationConstants.VERIFICATION_GAS_BUFFER})");
        }

        [Fact]
        public async Task EstimateGasAsync_WithInitCode_EstimatesDeploymentGas()
        {
            var mockEstimator = new MockEvmGasEstimator(100000, shouldSucceed: true);
            var estimator = new UserOperationGasEstimator(mockEstimator, TestEntryPoint);

            var factoryAddress = new byte[20];
            for (int i = 0; i < 20; i++) factoryAddress[i] = 0xAB;
            var factoryData = new byte[] { 0x01, 0x02, 0x03, 0x04 };

            var userOp = CreateTestUserOperation();
            userOp.InitCode = factoryAddress.Concat(factoryData).ToArray();

            var result = await estimator.EstimateGasAsync(userOp);

            Assert.True(result.VerificationGasLimit > 0,
                "Should estimate verification gas for account deployment");
        }

        [Fact]
        public void Constructor_WithEvmEstimator_DoesNotRequireWeb3()
        {
            var mockEstimator = new MockEvmGasEstimator(100000);
            var estimator = new UserOperationGasEstimator(mockEstimator, TestEntryPoint);
            Assert.NotNull(estimator);
        }

        [Fact]
        public void Constructor_WithBothWeb3AndEvmEstimator_Succeeds()
        {
            var mockEstimator = new MockEvmGasEstimator(100000);
            var estimator = new UserOperationGasEstimator(null!, mockEstimator, TestEntryPoint, TestBundler);
            Assert.NotNull(estimator);
        }

        [Fact]
        public async Task EstimateGasAsync_InvalidSender_ThrowsArgumentException()
        {
            var mockEstimator = new MockEvmGasEstimator(100000);
            var estimator = new UserOperationGasEstimator(mockEstimator, TestEntryPoint);

            var userOp = new UserOperation
            {
                Sender = "",
                Nonce = 0
            };

            await Assert.ThrowsAsync<ArgumentException>(() => estimator.EstimateGasAsync(userOp));
        }

        [Fact]
        public async Task EstimateGasAsync_ZeroAddressWithoutInitCode_ThrowsArgumentException()
        {
            var mockEstimator = new MockEvmGasEstimator(100000);
            var estimator = new UserOperationGasEstimator(mockEstimator, TestEntryPoint);

            var userOp = new UserOperation
            {
                Sender = AddressUtil.ZERO_ADDRESS,
                Nonce = 0,
                InitCode = Array.Empty<byte>()
            };

            await Assert.ThrowsAsync<ArgumentException>(() => estimator.EstimateGasAsync(userOp));
        }

        [Fact]
        public async Task EstimateGasAsync_ZeroAddressWithInitCode_Succeeds()
        {
            var mockEstimator = new MockEvmGasEstimator(150000, shouldSucceed: true);
            var estimator = new UserOperationGasEstimator(mockEstimator, TestEntryPoint);

            var factoryAddress = new byte[20];
            for (int i = 0; i < 20; i++) factoryAddress[i] = 0xAB;
            var factoryData = new byte[] { 0x01, 0x02, 0x03, 0x04 };

            var userOp = new UserOperation
            {
                Sender = AddressUtil.ZERO_ADDRESS,
                Nonce = 0,
                InitCode = factoryAddress.Concat(factoryData).ToArray()
            };

            var result = await estimator.EstimateGasAsync(userOp);

            Assert.True(result.VerificationGasLimit > 0,
                "Should estimate gas for zero address with initCode");
        }

        private static UserOperation CreateTestUserOperation()
        {
            return new UserOperation
            {
                Sender = TestSender,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = new byte[] { 0x01, 0x02, 0x03 },
                MaxFeePerGas = 1_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };
        }
    }

    public class EvmEstimationResultTests
    {
        [Fact]
        public void EvmEstimationResult_DefaultValues()
        {
            var result = new EvmEstimationResult();

            Assert.False(result.Success);
            Assert.Equal(BigInteger.Zero, result.GasUsed);
            Assert.Null(result.Error);
        }

        [Fact]
        public void EvmEstimationResult_SuccessfulEstimation()
        {
            var result = new EvmEstimationResult
            {
                Success = true,
                GasUsed = 150000,
                Error = null
            };

            Assert.True(result.Success);
            Assert.Equal(150000, result.GasUsed);
            Assert.Null(result.Error);
        }

        [Fact]
        public void EvmEstimationResult_FailedEstimation()
        {
            var result = new EvmEstimationResult
            {
                Success = false,
                GasUsed = 0,
                Error = "Out of gas"
            };

            Assert.False(result.Success);
            Assert.Equal(0, result.GasUsed);
            Assert.Equal("Out of gas", result.Error);
        }
    }

    public class HandleOpsEstimationResultTests
    {
        [Fact]
        public void HandleOpsEstimationResult_DefaultValues()
        {
            var result = new HandleOpsEstimationResult();

            Assert.False(result.Success);
            Assert.Equal(BigInteger.Zero, result.VerificationGasLimit);
        }

        [Fact]
        public void HandleOpsEstimationResult_SuccessfulEstimation()
        {
            var result = new HandleOpsEstimationResult
            {
                Success = true,
                VerificationGasLimit = 200000
            };

            Assert.True(result.Success);
            Assert.Equal(200000, result.VerificationGasLimit);
        }
    }

    public class UserOperationGasEstimateResultTests
    {
        [Fact]
        public void GasEstimateResult_DefaultValues()
        {
            var result = new UserOperationGasEstimateResult();

            Assert.Equal(BigInteger.Zero, result.PreVerificationGas);
            Assert.Equal(BigInteger.Zero, result.VerificationGasLimit);
            Assert.Equal(BigInteger.Zero, result.CallGasLimit);
            Assert.Equal(BigInteger.Zero, result.MaxFeePerGas);
            Assert.Equal(BigInteger.Zero, result.MaxPriorityFeePerGas);
            Assert.Equal(BigInteger.Zero, result.PaymasterVerificationGasLimit);
            Assert.Equal(BigInteger.Zero, result.PaymasterPostOpGasLimit);
        }

        [Fact]
        public void GasEstimateResult_AllFieldsSet()
        {
            var result = new UserOperationGasEstimateResult
            {
                PreVerificationGas = 50000,
                VerificationGasLimit = 150000,
                CallGasLimit = 100000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000,
                PaymasterVerificationGasLimit = 100000,
                PaymasterPostOpGasLimit = 50000
            };

            Assert.Equal(50000, result.PreVerificationGas);
            Assert.Equal(150000, result.VerificationGasLimit);
            Assert.Equal(100000, result.CallGasLimit);
            Assert.Equal(2_000_000_000, result.MaxFeePerGas);
            Assert.Equal(1_000_000_000, result.MaxPriorityFeePerGas);
            Assert.Equal(100000, result.PaymasterVerificationGasLimit);
            Assert.Equal(50000, result.PaymasterPostOpGasLimit);
        }
    }

    public class GasBufferApplicationTests
    {
        [Fact]
        public void HandleOpsFixedOverhead_Is30000()
        {
            Assert.Equal(30000, GasEstimationConstants.HANDLE_OPS_FIXED_OVERHEAD);
        }

        [Fact]
        public void VerificationGasBufferPercent_Is20()
        {
            Assert.Equal(20, GasEstimationConstants.VERIFICATION_GAS_BUFFER_PERCENT);
        }

        [Fact]
        public void CallGasBufferPercent_Is20()
        {
            Assert.Equal(20, GasEstimationConstants.CALL_GAS_BUFFER_PERCENT);
        }

        [Fact]
        public void MaxSimulationGas_Is10Million()
        {
            Assert.Equal(10_000_000, GasEstimationConstants.MAX_SIMULATION_GAS);
        }

        [Fact]
        public void DefaultVerificationGasFallback_Is150000()
        {
            Assert.Equal(150000, GasEstimationConstants.DEFAULT_VERIFICATION_GAS_FALLBACK);
        }

        [Fact]
        public void DefaultPaymasterVerificationGasFallback_Is100000()
        {
            Assert.Equal(100000, GasEstimationConstants.DEFAULT_PAYMASTER_VERIFICATION_GAS_FALLBACK);
        }

        [Fact]
        public void DefaultPaymasterPostOpGasFallback_Is50000()
        {
            Assert.Equal(50000, GasEstimationConstants.DEFAULT_PAYMASTER_POST_OP_GAS_FALLBACK);
        }
    }

    public class GasEstimationEdgeCasesTests
    {
        private const string TestEntryPoint = "0x5FF137D4b0FDCD49DcA30c7CF57E578a026d2789";
        private const string TestSender = "0x1234567890123456789012345678901234567890";

        [Fact]
        public async Task EstimateGasAsync_VeryLargeCallData_HandlesGracefully()
        {
            var mockEstimator = new MockEvmGasEstimator(500000, shouldSucceed: true);
            var estimator = new UserOperationGasEstimator(mockEstimator, TestEntryPoint);

            var userOp = new UserOperation
            {
                Sender = TestSender,
                Nonce = 0,
                CallData = new byte[10000]
            };
            for (int i = 0; i < 10000; i++) userOp.CallData[i] = (byte)(i % 256);

            var result = await estimator.EstimateGasAsync(userOp);

            Assert.True(result.PreVerificationGas > GasEstimationConstants.BASE_TRANSACTION_GAS * 2,
                "Large calldata should increase PreVerificationGas significantly");
        }

        [Fact]
        public async Task EstimateGasAsync_VeryLargeInitCode_HandlesGracefully()
        {
            var mockEstimator = new MockEvmGasEstimator(300000, shouldSucceed: true);
            var estimator = new UserOperationGasEstimator(mockEstimator, TestEntryPoint);

            var factoryAddress = new byte[20];
            for (int i = 0; i < 20; i++) factoryAddress[i] = 0xAB;
            var factoryData = new byte[5000];
            for (int i = 0; i < 5000; i++) factoryData[i] = (byte)(i % 256);

            var userOp = new UserOperation
            {
                Sender = TestSender,
                Nonce = 0,
                InitCode = factoryAddress.Concat(factoryData).ToArray()
            };

            var result = await estimator.EstimateGasAsync(userOp);

            Assert.True(result.PreVerificationGas > GasEstimationConstants.BASE_TRANSACTION_GAS * 2,
                "Large initCode should increase PreVerificationGas significantly");
        }

        [Fact]
        public async Task EstimateGasAsync_MaxNonce_HandlesGracefully()
        {
            var mockEstimator = new MockEvmGasEstimator(150000, shouldSucceed: true);
            var estimator = new UserOperationGasEstimator(mockEstimator, TestEntryPoint);

            var userOp = new UserOperation
            {
                Sender = TestSender,
                Nonce = BigInteger.Parse("115792089237316195423570985008687907853269984665640564039457584007913129639935"),
                CallData = new byte[] { 0x01 }
            };

            var result = await estimator.EstimateGasAsync(userOp);

            Assert.True(result.VerificationGasLimit > 0, "Should handle max nonce value");
            Assert.True(result.PreVerificationGas > 0, "Should calculate preVerificationGas with max nonce");
        }

        [Fact]
        public async Task EstimateGasAsync_ZeroNonce_Succeeds()
        {
            var mockEstimator = new MockEvmGasEstimator(150000, shouldSucceed: true);
            var estimator = new UserOperationGasEstimator(mockEstimator, TestEntryPoint);

            var userOp = new UserOperation
            {
                Sender = TestSender,
                Nonce = 0,
                CallData = new byte[] { 0x01 }
            };

            var result = await estimator.EstimateGasAsync(userOp);

            Assert.True(result.VerificationGasLimit > 0, "Should handle zero nonce");
        }

        [Fact]
        public async Task EstimateGasAsync_NullNonce_UsesDefault()
        {
            var mockEstimator = new MockEvmGasEstimator(150000, shouldSucceed: true);
            var estimator = new UserOperationGasEstimator(mockEstimator, TestEntryPoint);

            var userOp = new UserOperation
            {
                Sender = TestSender,
                Nonce = null,
                CallData = new byte[] { 0x01 }
            };

            var result = await estimator.EstimateGasAsync(userOp);

            Assert.True(result.VerificationGasLimit > 0, "Should handle null nonce with default");
        }
    }

    public class MultipleEstimationsTests
    {
        private const string TestEntryPoint = "0x5FF137D4b0FDCD49DcA30c7CF57E578a026d2789";
        private const string TestSender = "0x1234567890123456789012345678901234567890";

        [Fact]
        public async Task EstimateGasAsync_MultipleCalls_AreIndependent()
        {
            var mockEstimator = new MockEvmGasEstimator(150000, shouldSucceed: true);
            var estimator = new UserOperationGasEstimator(mockEstimator, TestEntryPoint);

            var userOp1 = new UserOperation
            {
                Sender = TestSender,
                Nonce = 0,
                CallData = new byte[] { 0x01 }
            };

            var userOp2 = new UserOperation
            {
                Sender = TestSender,
                Nonce = 1,
                CallData = new byte[100]
            };
            for (int i = 0; i < 100; i++) userOp2.CallData[i] = 0xFF;

            var result1 = await estimator.EstimateGasAsync(userOp1);
            var result2 = await estimator.EstimateGasAsync(userOp2);

            Assert.True(result2.PreVerificationGas > result1.PreVerificationGas,
                "Larger calldata should result in higher PreVerificationGas");
        }
    }
}
