using Nethereum.AccountAbstraction.Validation;

namespace Nethereum.AccountAbstraction.Bundler
{
    public static class BundlerErrorCodes
    {
        public const int ParseError = Erc7769ErrorCodes.ParseError;
        public const int InvalidRequest = Erc7769ErrorCodes.InvalidRequest;
        public const int MethodNotFound = Erc7769ErrorCodes.MethodNotFound;
        public const int InvalidFields = Erc7769ErrorCodes.InvalidFields;
        public const int InternalError = Erc7769ErrorCodes.InternalError;

        public const int SimulateValidation = Erc7769ErrorCodes.SimulateValidation;

        public const int SimulatePaymasterValidation = Erc7769ErrorCodes.SimulatePaymasterValidation;

        public const int OpcodeValidation = Erc7769ErrorCodes.OpcodeValidation;

        public const int NotInTimeRange = Erc7769ErrorCodes.NotInTimeRange;

        public const int Reputation = Erc7769ErrorCodes.Reputation;

        public const int InsufficientStake = Erc7769ErrorCodes.InsufficientStake;

        public const int UnsupportedSignatureAggregator = Erc7769ErrorCodes.UnsupportedSignatureAggregator;

        public const int InvalidSignature = Erc7769ErrorCodes.InvalidSignature;

        public const int PaymasterDepositTooLow = Erc7769ErrorCodes.PaymasterDepositTooLow;

        public const int UserOperationReverted = Erc7769ErrorCodes.UserOperationReverted;
    }
}
