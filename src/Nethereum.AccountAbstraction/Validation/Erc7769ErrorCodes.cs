namespace Nethereum.AccountAbstraction.Validation
{
    public static class Erc7769ErrorCodes
    {
        public const int ParseError = -32700;
        public const int InvalidRequest = -32600;
        public const int MethodNotFound = -32601;
        public const int InvalidFields = -32602;
        public const int InternalError = -32603;

        public const int SimulateValidation = -32500;

        public const int SimulatePaymasterValidation = -32501;

        public const int OpcodeValidation = -32502;

        public const int NotInTimeRange = -32503;

        public const int Reputation = -32504;

        public const int InsufficientStake = -32505;

        public const int UnsupportedSignatureAggregator = -32506;

        public const int InvalidSignature = -32507;

        public const int PaymasterDepositTooLow = -32508;

        public const int UserOperationReverted = -32521;
    }
}
