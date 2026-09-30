using Nethereum.AccountAbstraction.Validation;

namespace Nethereum.AccountAbstraction.Bundler
{
    public class BundlerRpcException : Exception
    {
        public int Code { get; }

        public object? ErrorData { get; }

        public BundlerRpcException(int code, string message, object? errorData = null)
            : base(message)
        {
            Code = code;
            ErrorData = errorData;
        }

        public static BundlerRpcException FromValidationResult(UserOpValidationResult result)
        {
            return new BundlerRpcException(ToErrorCode(result.ErrorCode), result.Error ?? "Validation failed");
        }

        public static int ToErrorCode(UserOpValidationError error)
        {
            switch (error)
            {
                case UserOpValidationError.InvalidSignature:
                case UserOpValidationError.SignatureValidationFailed:
                    return BundlerErrorCodes.InvalidSignature;

                case UserOpValidationError.ExpiredSignature:
                case UserOpValidationError.NotYetValid:
                    return BundlerErrorCodes.NotInTimeRange;

                case UserOpValidationError.PaymasterNotDeployed:
                case UserOpValidationError.PaymasterValidationFailed:
                case UserOpValidationError.PaymasterPostOpFailed:
                    return BundlerErrorCodes.SimulatePaymasterValidation;

                case UserOpValidationError.PaymasterDepositTooLow:
                    return BundlerErrorCodes.PaymasterDepositTooLow;

                case UserOpValidationError.InvalidStorageAccess:
                case UserOpValidationError.InvalidOpcodeAccess:
                    return BundlerErrorCodes.OpcodeValidation;

                case UserOpValidationError.InvalidAggregator:
                case UserOpValidationError.AggregatorValidationFailed:
                    return BundlerErrorCodes.UnsupportedSignatureAggregator;

                case UserOpValidationError.GasValuesOverflow:
                case UserOpValidationError.MaxFeePerGasTooLow:
                case UserOpValidationError.MaxPriorityFeePerGasTooLow:
                case UserOpValidationError.DuplicateUserOp:
                case UserOpValidationError.ReplacementUnderpriced:
                case UserOpValidationError.InvalidAuthorisation:
                    return BundlerErrorCodes.InvalidFields;

                default:
                    return BundlerErrorCodes.SimulateValidation;
            }
        }
    }
}
