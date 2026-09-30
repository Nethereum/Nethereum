using Nethereum.AccountAbstraction.Structs;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests
{
    public static class UserOperationTestHelper
    {
        public static object CreateUserOpObject(PackedUserOperation userOp)
        {
            var (verificationGasLimit, callGasLimit) = userOp.UnpackAccountGasLimits();
            var (maxPriorityFeePerGas, maxFeePerGas) = userOp.UnpackGasFees();
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
