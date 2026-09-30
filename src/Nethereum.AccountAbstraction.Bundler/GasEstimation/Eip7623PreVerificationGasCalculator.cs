using System.Numerics;
using Nethereum.ABI;
using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.AccountAbstraction.Contracts.Interfaces.IAccountExecute.ContractDefinition;
using Nethereum.AccountAbstraction.GasEstimation;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.AccountAbstraction.Bundler.GasEstimation
{
    public static class Eip7623PreVerificationGasCalculator
    {
        private const long TransactionGasStipend = 21000;
        private const long FixedGasOverhead = 9830;
        private const long PerUserOpGasOverhead = 7260;
        private const long ExecuteUserOpGasOverhead = 1610;
        private const decimal PerUserOpWordGasOverhead = 9.5m;
        private const decimal ExecuteUserOpPerWordGasOverhead = 8.2m;

        private const long StandardTokenGasCost = 4;
        private const long FloorPerTokenGasCost = 10;
        private const long ExpectedBundleSize = 1;

        public const long MaxVerificationGasUsed = 500_000;

        private static readonly byte[] PackedUserOpTypeHash = Sha3Keccack.Current.CalculateHashAsBytes(
            new Eip712TypedDataEncoder().GetEncodedType(
                "PackedUserOperation", typeof(Nethereum.AccountAbstraction.PackedUserOperationForHash)));

        public static BigInteger CalculateMinRequired(PackedUserOperation packedOp, BigInteger verificationGasUsed)
        {
            var encoded = new ABIEncode().GetABIParamsEncoded(ToCalldataCostEncoding(packedOp));

            var standardComponent = UserOperationGasEstimator.CalculateCalldataCost(encoded);
            var floorComponent = standardComponent * FloorPerTokenGasCost / StandardTokenGasCost;

            var (_, callGasLimit) = packedOp.UnpackAccountGasLimits();
            var (_, paymasterPostOpGasLimit) = packedOp.UnpackPaymasterGasLimits();

            var callData = packedOp.CallData ?? Array.Empty<byte>();

            decimal perUserOpOverhead = PerUserOpGasOverhead;
            decimal callDataOverhead;

            if (IsExecuteUserOpCallData(callData))
            {
                var wordsLength = (decimal)(encoded.Length + 31) / 32;
                perUserOpOverhead += ExecuteUserOpGasOverhead;
                callDataOverhead = ExecuteUserOpPerWordGasOverhead * wordsLength;
            }
            else
            {
                var callDataWords = (callData.Length + 31) / 32;
                callDataOverhead = PerUserOpWordGasOverhead * callDataWords;
            }

            var userOpSpecificOverhead = perUserOpOverhead + callDataOverhead;
            var userOpShareOfBundleCost = (decimal)FixedGasOverhead / ExpectedBundleSize;
            var userOpShareOfStipend = (decimal)TransactionGasStipend / ExpectedBundleSize;

            var calculatedGasUsed = callGasLimit / 10 + paymasterPostOpGasLimit / 10 + verificationGasUsed;

            var executionGasCost = userOpShareOfBundleCost + userOpSpecificOverhead + (decimal)calculatedGasUsed;
            var standard = (decimal)standardComponent + executionGasCost;
            var floor = (decimal)floorComponent;

            var total = Math.Round(userOpShareOfStipend + Math.Max(standard, floor), MidpointRounding.AwayFromZero);
            var minRequired = new BigInteger(total) - calculatedGasUsed;

            return BigInteger.Max(minRequired, BigInteger.Zero);
        }

        private static bool IsExecuteUserOpCallData(byte[] callData)
        {
            if (callData.Length < 4) return false;

            var executeUserOpAbi = ABITypedRegistry.GetFunctionABI<ExecuteUserOpFunction>();
            return new FunctionCallDecoder().IsDataForFunction(executeUserOpAbi, callData.ToHex(true));
        }

        private static Eip7623CalldataCostEncoding ToCalldataCostEncoding(PackedUserOperation packedOp)
        {
            return new Eip7623CalldataCostEncoding
            {
                TypeHash = PackedUserOpTypeHash,
                Sender = packedOp.Sender,
                Nonce = packedOp.Nonce,
                InitCode = packedOp.InitCode ?? Array.Empty<byte>(),
                CallData = packedOp.CallData ?? Array.Empty<byte>(),
                AccountGasLimits = packedOp.AccountGasLimits ?? new byte[32],
                PreVerificationGas = packedOp.PreVerificationGas,
                GasFees = packedOp.GasFees ?? new byte[32],
                PaymasterAndData = packedOp.PaymasterAndData ?? Array.Empty<byte>(),
                Signature = packedOp.Signature ?? Array.Empty<byte>()
            };
        }

        private class Eip7623CalldataCostEncoding
        {
            [Parameter("bytes32", "typeHash", 1)]
            public byte[] TypeHash { get; set; } = Array.Empty<byte>();
            [Parameter("address", "sender", 2)]
            public string Sender { get; set; } = string.Empty;
            [Parameter("uint256", "nonce", 3)]
            public BigInteger Nonce { get; set; }
            [Parameter("bytes", "initCode", 4)]
            public byte[] InitCode { get; set; } = Array.Empty<byte>();
            [Parameter("bytes", "callData", 5)]
            public byte[] CallData { get; set; } = Array.Empty<byte>();
            [Parameter("bytes32", "accountGasLimits", 6)]
            public byte[] AccountGasLimits { get; set; } = Array.Empty<byte>();
            [Parameter("uint256", "preVerificationGas", 7)]
            public BigInteger PreVerificationGas { get; set; }
            [Parameter("bytes32", "gasFees", 8)]
            public byte[] GasFees { get; set; } = Array.Empty<byte>();
            [Parameter("bytes", "paymasterAndData", 9)]
            public byte[] PaymasterAndData { get; set; } = Array.Empty<byte>();
            [Parameter("bytes", "signature", 10)]
            public byte[] Signature { get; set; } = Array.Empty<byte>();
        }
    }
}
