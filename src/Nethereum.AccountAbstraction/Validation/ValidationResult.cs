using System.Numerics;

namespace Nethereum.AccountAbstraction.Validation
{
    /// <summary>
    /// Result of user operation validation.
    /// </summary>
    public class UserOpValidationResult
    {
        /// <summary>
        /// Whether the validation passed.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Error message if validation failed.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Error code for programmatic handling.
        /// </summary>
        public UserOpValidationError ErrorCode { get; set; } = UserOpValidationError.None;

        /// <summary>
        /// The validation data returned by the account (packed format).
        /// </summary>
        public BigInteger ValidationData { get; set; }

        /// <summary>
        /// The validation data returned by the paymaster (packed format), if any.
        /// </summary>
        public BigInteger PaymasterValidationData { get; set; }

        /// <summary>
        /// Parsed timestamp after which the signature is valid.
        /// </summary>
        public ulong ValidAfter { get; set; }

        /// <summary>
        /// Parsed timestamp until which the signature is valid.
        /// </summary>
        public ulong ValidUntil { get; set; }

        /// <summary>
        /// Aggregator address if signature aggregation is used.
        /// </summary>
        public string? Aggregator { get; set; }

        /// <summary>
        /// Estimated pre-verification gas.
        /// </summary>
        public BigInteger PreVerificationGas { get; set; }

        /// <summary>
        /// Estimated verification gas limit.
        /// </summary>
        public BigInteger VerificationGasLimit { get; set; }

        /// <summary>
        /// Estimated call gas limit.
        /// </summary>
        public BigInteger CallGasLimit { get; set; }

        /// <summary>
        /// Creates a successful validation result.
        /// </summary>
        public static UserOpValidationResult Success() => new() { IsValid = true };

        /// <summary>
        /// Creates a failed validation result.
        /// </summary>
        public static UserOpValidationResult Failure(string error, UserOpValidationError code = UserOpValidationError.Unknown)
            => new() { IsValid = false, Error = error, ErrorCode = code };
    }

    /// <summary>
    /// Validation error codes following ERC-4337 spec.
    /// </summary>
    public enum UserOpValidationError
    {
        None = 0,
        Unknown = -1,

        // Sender errors
        InvalidSender = 10,
        SenderNotDeployed = 11,
        InitCodeFailed = 12,
        InitCodeNotDeployed = 13,

        // Signature errors
        InvalidSignature = 20,
        SignatureValidationFailed = 21,
        ExpiredSignature = 22,
        NotYetValid = 23,

        // Nonce errors
        InvalidNonce = 30,

        // Gas errors
        InsufficientPrefund = 40,
        InsufficientVerificationGas = 41,
        InsufficientCallGas = 42,
        GasValuesOverflow = 43,
        MaxFeePerGasTooLow = 44,
        MaxPriorityFeePerGasTooLow = 45,

        // Paymaster errors
        PaymasterNotDeployed = 50,
        PaymasterDepositTooLow = 51,
        PaymasterValidationFailed = 52,
        PaymasterPostOpFailed = 53,

        // Execution errors
        ExecutionReverted = 60,
        CallFailed = 61,

        // Storage access errors (4337 rules)
        InvalidStorageAccess = 70,
        InvalidOpcodeAccess = 71,
        OutOfGas = 72,

        // Aggregator errors
        AggregatorValidationFailed = 80,
        InvalidAggregator = 81,

        // Bundle errors
        BundleFull = 90,
        DuplicateUserOp = 91,
        ReplacementUnderpriced = 92
    }

    /// <summary>
    /// Helper methods for parsing ERC-4337 validation data.
    /// </summary>
    public static class ValidationDataHelper
    {
        /// <summary>
        /// Signature validation failed constant.
        /// </summary>
        public const uint SIG_VALIDATION_FAILED = 1;

        /// <summary>
        /// Packs validation data into the expected format.
        /// </summary>
        /// <param name="sigFailed">Whether signature validation failed</param>
        /// <param name="validUntil">Timestamp until which the signature is valid (0 = infinite)</param>
        /// <param name="validAfter">Timestamp after which the signature is valid (0 = immediately)</param>
        /// <param name="aggregator">Aggregator address (0 = none)</param>
        /// <returns>Packed validation data as BigInteger</returns>
        public static BigInteger Pack(bool sigFailed, ulong validUntil, ulong validAfter, string? aggregator = null)
        {
            // Layout: (sigFailed: 1 bit) | (validUntil: 48 bits) | (validAfter: 48 bits) | (aggregator: 160 bits)
            // But the actual layout in 4337 v0.7 is:
            // validationData = address(aggregator) | uint48(validUntil) << 160 | uint48(validAfter) << 208
            // sigFailed is encoded as aggregator == address(1)

            BigInteger result = BigInteger.Zero;

            if (!string.IsNullOrEmpty(aggregator) && aggregator != "0x0000000000000000000000000000000000000000")
            {
                result = BigInteger.Parse(aggregator.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber);
            }
            else if (sigFailed)
            {
                result = BigInteger.One; // address(1) means sig failed
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

        /// <summary>
        /// Parses packed validation data.
        /// </summary>
        public static (bool SigFailed, ulong ValidUntil, ulong ValidAfter, string? Aggregator) Parse(BigInteger validationData)
        {
            if (validationData == BigInteger.Zero)
            {
                return (false, 0, 0, null);
            }

            // Extract aggregator (lower 160 bits)
            var aggregatorMask = (BigInteger.One << 160) - 1;
            var aggregatorValue = validationData & aggregatorMask;

            bool sigFailed = aggregatorValue == BigInteger.One;
            string? aggregator = null;

            if (aggregatorValue > 1)
            {
                aggregator = "0x" + aggregatorValue.ToString("x40");
            }

            // Extract validUntil (bits 160-207)
            var validUntil = (ulong)((validationData >> 160) & 0xFFFFFFFFFFFF);

            // Extract validAfter (bits 208-255)
            var validAfter = (ulong)((validationData >> 208) & 0xFFFFFFFFFFFF);

            return (sigFailed, validUntil, validAfter, aggregator);
        }

        /// <summary>
        /// Checks if the validation data indicates the signature is currently valid.
        /// </summary>
        public static bool IsValidNow(BigInteger validationData)
        {
            var (sigFailed, validUntil, validAfter, _) = Parse(validationData);

            if (sigFailed) return false;

            var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (validAfter > 0 && now < validAfter) return false;
            if (validUntil > 0 && now > validUntil) return false;

            return true;
        }

        /// <summary>
        /// Merges account and paymaster validation data.
        /// </summary>
        public static BigInteger Merge(BigInteger accountValidation, BigInteger paymasterValidation)
        {
            var (accSigFailed, accValidUntil, accValidAfter, accAggregator) = Parse(accountValidation);
            var (pmSigFailed, pmValidUntil, pmValidAfter, _) = Parse(paymasterValidation);

            // Take the most restrictive values
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
