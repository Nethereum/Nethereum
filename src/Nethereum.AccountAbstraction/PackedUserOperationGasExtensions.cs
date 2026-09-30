using System.Numerics;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction
{
    public static class PackedUserOperationGasExtensions
    {
        public static (BigInteger VerificationGasLimit, BigInteger CallGasLimit) UnpackAccountGasLimits(
            this PackedUserOperation userOp)
        {
            return UnpackHalves(userOp.AccountGasLimits);
        }

        public static (BigInteger MaxPriorityFeePerGas, BigInteger MaxFeePerGas) UnpackGasFees(
            this PackedUserOperation userOp)
        {
            return UnpackHalves(userOp.GasFees);
        }

        public static (BigInteger PaymasterVerificationGasLimit, BigInteger PaymasterPostOpGasLimit) UnpackPaymasterGasLimits(
            this PackedUserOperation userOp)
        {
            var paymasterAndData = userOp.PaymasterAndData;
            if (paymasterAndData == null || paymasterAndData.Length < 52)
            {
                return (BigInteger.Zero, BigInteger.Zero);
            }

            var verification = new BigInteger(
                paymasterAndData.Skip(20).Take(16).Reverse().ToArray(), isUnsigned: true);
            var postOp = new BigInteger(
                paymasterAndData.Skip(36).Take(16).Reverse().ToArray(), isUnsigned: true);

            return (verification, postOp);
        }

        public static BigInteger GetTotalGas(this PackedUserOperation userOp)
        {
            var (verificationGas, callGas) = userOp.UnpackAccountGasLimits();
            var (paymasterVerificationGas, paymasterPostOpGas) = userOp.UnpackPaymasterGasLimits();

            return verificationGas + callGas + userOp.PreVerificationGas +
                   paymasterVerificationGas + paymasterPostOpGas;
        }

        private static (BigInteger High, BigInteger Low) UnpackHalves(byte[]? packed)
        {
            if (packed == null || packed.Length < 32)
            {
                return (BigInteger.Zero, BigInteger.Zero);
            }

            var high = new BigInteger(packed.Take(16).Reverse().ToArray(), isUnsigned: true);
            var low = new BigInteger(packed.Skip(16).Take(16).Reverse().ToArray(), isUnsigned: true);

            return (high, low);
        }
    }
}
