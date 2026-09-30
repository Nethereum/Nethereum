using System.Numerics;

namespace Nethereum.AccountAbstraction.Validation
{
    public class UserOpValidationResult
    {
        public bool IsValid { get; set; }

        public string? Error { get; set; }

        public UserOpValidationError ErrorCode { get; set; } = UserOpValidationError.None;

        public BigInteger ValidationData { get; set; }

        public BigInteger PaymasterValidationData { get; set; }

        public ulong ValidAfter { get; set; }

        public ulong ValidUntil { get; set; }

        public string? Aggregator { get; set; }

        public BigInteger PreVerificationGas { get; set; }

        public BigInteger VerificationGasLimit { get; set; }

        public BigInteger CallGasLimit { get; set; }

        public static UserOpValidationResult Success() => new() { IsValid = true };

        public static UserOpValidationResult Failure(string error, UserOpValidationError code = UserOpValidationError.Unknown)
            => new() { IsValid = false, Error = error, ErrorCode = code };
    }

    public enum UserOpValidationError
    {
        None = 0,
        Unknown = -1,

        InvalidSender = 10,
        SenderNotDeployed = 11,
        InitCodeFailed = 12,
        InitCodeNotDeployed = 13,

        InvalidAuthorisation = 15,

        InvalidSignature = 20,
        SignatureValidationFailed = 21,
        ExpiredSignature = 22,
        NotYetValid = 23,

        InvalidNonce = 30,

        InsufficientPrefund = 40,
        InsufficientVerificationGas = 41,
        InsufficientCallGas = 42,
        GasValuesOverflow = 43,
        MaxFeePerGasTooLow = 44,
        MaxPriorityFeePerGasTooLow = 45,

        PaymasterNotDeployed = 50,
        PaymasterDepositTooLow = 51,
        PaymasterValidationFailed = 52,
        PaymasterPostOpFailed = 53,

        ExecutionReverted = 60,
        CallFailed = 61,

        InvalidStorageAccess = 70,
        InvalidOpcodeAccess = 71,
        OutOfGas = 72,

        AggregatorValidationFailed = 80,
        InvalidAggregator = 81,

        BundleFull = 90,
        DuplicateUserOp = 91,
        ReplacementUnderpriced = 92
    }

    public static class ValidationDataCodec
    {
        public const uint SIG_VALIDATION_FAILED = 1;

        public static BigInteger Pack(bool sigFailed, ulong validUntil, ulong validAfter, string? aggregator = null)
        {

            BigInteger result = BigInteger.Zero;

            if (!string.IsNullOrEmpty(aggregator) && aggregator != "0x0000000000000000000000000000000000000000")
            {
                result = BigInteger.Parse(aggregator.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber);
            }
            else if (sigFailed)
            {
                result = BigInteger.One;
            }

            if (validUntil > 0)
            {
                result |= new BigInteger(validUntil) << 160;
            }

            if (validAfter > 0)
            {
                result |= new BigInteger(validAfter) << 208;
            }

            return result;
        }

        public static (bool SigFailed, ulong ValidUntil, ulong ValidAfter, string? Aggregator) Parse(BigInteger validationData)
        {
            if (validationData == BigInteger.Zero)
            {
                return (false, 0, 0, null);
            }

            var aggregatorMask = (BigInteger.One << 160) - 1;
            var aggregatorValue = validationData & aggregatorMask;

            bool sigFailed = aggregatorValue == BigInteger.One;
            string? aggregator = null;

            if (aggregatorValue > 1)
            {
                aggregator = "0x" + aggregatorValue.ToString("x40");
            }

            var validUntil = (ulong)((validationData >> 160) & 0xFFFFFFFFFFFF);

            var validAfter = (ulong)((validationData >> 208) & 0xFFFFFFFFFFFF);

            return (sigFailed, validUntil, validAfter, aggregator);
        }

        public static bool IsValidNow(BigInteger validationData)
        {
            var (sigFailed, validUntil, validAfter, _) = Parse(validationData);

            if (sigFailed) return false;

            var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (validAfter > 0 && now < validAfter) return false;
            if (validUntil > 0 && now > validUntil) return false;

            return true;
        }

        public static BigInteger Merge(BigInteger accountValidation, BigInteger paymasterValidation)
        {
            var (accSigFailed, accValidUntil, accValidAfter, accAggregator) = Parse(accountValidation);
            var (pmSigFailed, pmValidUntil, pmValidAfter, _) = Parse(paymasterValidation);

            var sigFailed = accSigFailed || pmSigFailed;

            ulong validUntil = 0;
            if (accValidUntil > 0 && pmValidUntil > 0)
                validUntil = Math.Min(accValidUntil, pmValidUntil);
            else if (accValidUntil > 0)
                validUntil = accValidUntil;
            else if (pmValidUntil > 0)
                validUntil = pmValidUntil;

            ulong validAfter = Math.Max(accValidAfter, pmValidAfter);

            return Pack(sigFailed, validUntil, validAfter, accAggregator);
        }
    }
}
