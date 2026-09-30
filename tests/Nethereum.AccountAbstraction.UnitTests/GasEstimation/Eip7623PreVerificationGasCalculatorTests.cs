using System;
using System.Linq;
using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.GasEstimation;
using Nethereum.AccountAbstraction.Structs;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.GasEstimation
{
    public class Eip7623PreVerificationGasCalculatorTests
    {
        private static PackedUserOperation MinimalOp(byte[] callData)
        {
            return new PackedUserOperation
            {
                Sender = "0x0000000000000000000000000000000000000000",
                Nonce = BigInteger.Zero,
                InitCode = Array.Empty<byte>(),
                CallData = callData,
                AccountGasLimits = new byte[32],
                PreVerificationGas = BigInteger.Zero,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = Array.Empty<byte>()
            };
        }

        [Fact]
        public void CalculateMinRequired_NormalCalldata_StandardCostDominates()
        {
            var op = MinimalOp(new byte[] { 0x01 });

            var minRequired = Eip7623PreVerificationGasCalculator.CalculateMinRequired(op, verificationGasUsed: 0);

            Assert.Equal(new BigInteger(40512), minRequired);
        }

        [Fact]
        public void CalculateMinRequired_HugeCalldata_FloorDominates()
        {
            var op = MinimalOp(Enumerable.Repeat((byte)0xff, 1000).ToArray());

            var minRequired = Eip7623PreVerificationGasCalculator.CalculateMinRequired(op, verificationGasUsed: 0);

            Assert.Equal(new BigInteger(66950), minRequired);
        }

        [Fact]
        public void CalculateMinRequired_HugeCalldata_RequiresMoreThanNormalCalldata()
        {
            var normalOp = MinimalOp(new byte[] { 0x01 });
            var hugeOp = MinimalOp(Enumerable.Repeat((byte)0xff, 1000).ToArray());

            var normalMinRequired = Eip7623PreVerificationGasCalculator.CalculateMinRequired(normalOp, verificationGasUsed: 0);
            var hugeMinRequired = Eip7623PreVerificationGasCalculator.CalculateMinRequired(hugeOp, verificationGasUsed: 0);

            Assert.True(hugeMinRequired > normalMinRequired);
        }

        [Fact]
        public void CalculateMinRequired_ExecuteUserOpCallData_UsesFullPackedOpWordLength()
        {
            var op = MinimalOp(new byte[] { 0x8d, 0xd7, 0x71, 0x2f });

            var minRequired = Eip7623PreVerificationGasCalculator.CalculateMinRequired(op, verificationGasUsed: 0);

            Assert.Equal(new BigInteger(42279), minRequired);
        }

        [Fact]
        public void MaxVerificationGasUsed_MatchesReferenceBundlerConstant()
        {
            Assert.Equal(500_000, Eip7623PreVerificationGasCalculator.MaxVerificationGasUsed);
        }
    }
}
