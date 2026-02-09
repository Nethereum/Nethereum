using System.Numerics;

namespace Nethereum.AccountAbstraction.GasEstimation
{
    public static class GasEstimationConstants
    {
        public const int ZERO_BYTE_GAS_COST = 4;
        public const int NON_ZERO_BYTE_GAS_COST = 16;
        public const int PRE_VERIFICATION_OVERHEAD_GAS = 50000;
        public const int BASE_TRANSACTION_GAS = 21000;
        public const int VERIFICATION_GAS_BUFFER = 100000;
        public const int PAYMASTER_VALIDATION_GAS_BUFFER = 20000;
        public const int MAX_VERIFICATION_GAS = 500000;
        public const int DEFAULT_CALL_GAS_LIMIT = 100000;
        public const int INNER_GAS_OVERHEAD = 10000;
        public const int PER_USER_OP_WORD_GAS = 8;
        public const int PER_USER_OP_OVERHEAD = 18300;
        public const int SIGNATURE_SIZE = 65;
        public const int ADDRESS_SIZE = 20;
        public const int WORD_SIZE = 32;
        public static readonly BigInteger FIXED_VERIFICATION_GAS_OVERHEAD = 21000;
        public static readonly BigInteger ACCOUNT_DEPLOYMENT_BASE_GAS = 32000;
        public static readonly BigInteger CREATE2_COST = 32000;

        // HandleOps-based estimation constants
        public const int HANDLE_OPS_FIXED_OVERHEAD = 30000;
        public const int VERIFICATION_GAS_BUFFER_PERCENT = 20;
        public const int CALL_GAS_BUFFER_PERCENT = 20;
        public const long MAX_SIMULATION_GAS = 10_000_000;

        // Default fallback values when estimation fails
        public const int DEFAULT_VERIFICATION_GAS_FALLBACK = 150000;
        public const int DEFAULT_PAYMASTER_VERIFICATION_GAS_FALLBACK = 100000;
        public const int DEFAULT_PAYMASTER_POST_OP_GAS_FALLBACK = 50000;
    }
}
