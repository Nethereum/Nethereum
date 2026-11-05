using System.Numerics;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Bundler.Validation
{
    public static class UserOpAdmissionBounds
    {
        private const int AbiWord = 32;
        private const int AbiHeadWords = 9;
        private const int PaymasterPrefixLength = 52;

        public static string? Check(PackedUserOperation userOp, BigInteger maxGas, int maxSize)
        {
            var sizeViolation = CheckSize(AbiEncodedSize(userOp), maxSize);
            if (sizeViolation != null)
            {
                return sizeViolation;
            }

            var (verificationGas, callGas) = userOp.UnpackAccountGasLimits();
            var (paymasterVerificationGas, paymasterPostOpGas) = userOp.UnpackPaymasterGasLimits();

            var limits = new (string Name, BigInteger Value)[]
            {
                ("callGasLimit", callGas),
                ("verificationGasLimit", verificationGas),
                ("paymasterVerificationGasLimit", paymasterVerificationGas),
                ("paymasterPostOpGasLimit", paymasterPostOpGas),
                ("preVerificationGas", userOp.PreVerificationGas)
            };

            foreach (var (name, value) in limits)
            {
                if (value > maxGas)
                {
                    return $"{name} {value} exceeds the maximum bundle gas {maxGas}";
                }
            }

            var total = userOp.GetTotalGas();
            if (total > maxGas)
            {
                return $"UserOperation total gas {total} exceeds the maximum bundle gas {maxGas}";
            }

            return null;
        }

        public static string? Check(UserOperation userOp, int maxSize)
        {
            var paymasterAndDataLength = string.IsNullOrEmpty(userOp.Paymaster)
                ? 0
                : PaymasterPrefixLength + (userOp.PaymasterData?.Length ?? 0);

            return CheckSize(
                AbiEncodedSize(
                    userOp.InitCode?.Length ?? 0,
                    userOp.CallData?.Length ?? 0,
                    paymasterAndDataLength,
                    userOp.Signature?.Length ?? 0),
                maxSize);
        }

        public static int AbiEncodedSize(PackedUserOperation userOp) =>
            AbiEncodedSize(
                userOp.InitCode?.Length ?? 0,
                userOp.CallData?.Length ?? 0,
                userOp.PaymasterAndData?.Length ?? 0,
                userOp.Signature?.Length ?? 0);

        private static string? CheckSize(int size, int maxSize) =>
            size > maxSize
                ? $"UserOperation is {size} bytes ABI-encoded, above the {maxSize} byte limit"
                : null;

        private static int AbiEncodedSize(int initCode, int callData, int paymasterAndData, int signature)
        {
            long size = AbiHeadWords * AbiWord;
            size += DynamicBytesSize(initCode);
            size += DynamicBytesSize(callData);
            size += DynamicBytesSize(paymasterAndData);
            size += DynamicBytesSize(signature);
            return size > int.MaxValue ? int.MaxValue : (int)size;
        }

        private static long DynamicBytesSize(int length) =>
            AbiWord + ((long)length + AbiWord - 1) / AbiWord * AbiWord;
    }
}
