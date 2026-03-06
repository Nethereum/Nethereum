using System.Numerics;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests
{
    public static class UserOperationTestHelper
    {
        public static object CreateUserOpObject(PackedUserOperation userOp)
        {
            var (verificationGasLimit, callGasLimit) = UnpackAccountGasLimits(userOp.AccountGasLimits);
            var (maxPriorityFeePerGas, maxFeePerGas) = UnpackGasFees(userOp.GasFees);
            var (factory, factoryData) = UnpackInitCode(userOp.InitCode);

            return new
            {
                sender = userOp.Sender,
                nonce = "0x" + userOp.Nonce.ToString("x"),
                factory = factory,
                factoryData = factoryData,
                callData = userOp.CallData?.ToHex(true) ?? "0x",
                callGasLimit = "0x" + callGasLimit.ToString("x"),
                verificationGasLimit = "0x" + verificationGasLimit.ToString("x"),
                preVerificationGas = "0x" + userOp.PreVerificationGas.ToString("x"),
                maxFeePerGas = "0x" + maxFeePerGas.ToString("x"),
                maxPriorityFeePerGas = "0x" + maxPriorityFeePerGas.ToString("x"),
                paymaster = (string?)null,
                paymasterData = "0x",
                paymasterVerificationGasLimit = "0x0",
                paymasterPostOpGasLimit = "0x0",
                signature = userOp.Signature?.ToHex(true) ?? "0x"
            };
        }

        public static (BigInteger verificationGasLimit, BigInteger callGasLimit) UnpackAccountGasLimits(byte[]? data)
        {
            if (data == null || data.Length < 32)
                return (BigInteger.Zero, BigInteger.Zero);

            var verificationBytes = new byte[16];
            var callBytes = new byte[16];
            Array.Copy(data, 0, verificationBytes, 0, 16);
            Array.Copy(data, 16, callBytes, 0, 16);

            return (new BigInteger(verificationBytes, true, true), new BigInteger(callBytes, true, true));
        }

        public static (BigInteger maxPriorityFeePerGas, BigInteger maxFeePerGas) UnpackGasFees(byte[]? data)
        {
            if (data == null || data.Length < 32)
                return (BigInteger.Zero, BigInteger.Zero);

            var priorityBytes = new byte[16];
            var maxBytes = new byte[16];
            Array.Copy(data, 0, priorityBytes, 0, 16);
            Array.Copy(data, 16, maxBytes, 0, 16);

            return (new BigInteger(priorityBytes, true, true), new BigInteger(maxBytes, true, true));
        }

        public static (string? factory, string factoryData) UnpackInitCode(byte[]? data)
        {
            if (data == null || data.Length == 0)
                return (null, "0x");

            if (data.Length < 20)
                return (null, "0x");

            var factoryBytes = new byte[20];
            Array.Copy(data, 0, factoryBytes, 0, 20);
            var factory = factoryBytes.ToHex(true);

            if (data.Length > 20)
            {
                var dataBytes = new byte[data.Length - 20];
                Array.Copy(data, 20, dataBytes, 0, data.Length - 20);
                return (factory, dataBytes.ToHex(true));
            }

            return (factory, "0x");
        }
    }
}
