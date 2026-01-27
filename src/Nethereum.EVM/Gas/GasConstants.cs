namespace Nethereum.EVM.Gas
{
    public static class GasConstants
    {
        // EIP-2929: Cold/Warm access costs (Berlin)
        public const int COLD_SLOAD_COST = 2100;
        public const int COLD_ACCOUNT_ACCESS_COST = 2600;
        public const int WARM_STORAGE_READ_COST = 100;

        // EIP-2200/EIP-2929: SSTORE costs (post-Berlin)
        public const int SSTORE_SET = 20000;
        public const int SSTORE_RESET = 2900;  // 5000 - COLD_SLOAD_COST per EIP-2929
        public const int SSTORE_NOOP = 100;    // WARM_STORAGE_READ_COST per EIP-2929

        // EIP-3529: Gas refunds (London)
        public const int SSTORE_CLEARS_SCHEDULE = 4800;
        public const int REFUND_QUOTIENT = 5;  // max refund = gas_used / 5 (20%)

        // EIP-2200/EIP-3529: Refunds for reverting to original
        public const int SSTORE_SET_REFUND = SSTORE_SET - SSTORE_NOOP;    // 19900
        public const int SSTORE_RESET_REFUND = SSTORE_RESET - SSTORE_NOOP; // 2800

        // EIP-2200: SSTORE gas sentry (minimum gas for re-entrancy protection)
        public const int SSTORE_SENTRY = 2300;

        // Yellow Paper Appendix G: Base opcode costs
        public const int G_ZERO = 0;
        public const int G_JUMPDEST = 1;
        public const int G_BASE = 2;
        public const int G_VERYLOW = 3;
        public const int G_LOW = 5;
        public const int G_MID = 8;
        public const int G_HIGH = 10;
        public const int G_BLOCKHASH = 20;

        // EXP gas costs (EIP-158: Spurious Dragon)
        public const int EXP_BASE = 10;
        public const int EXP_BYTE = 50;

        // Memory operations
        public const int COPY_BASE = 3;
        public const int COPY_PER_WORD = 3;
        public const int MEMORY_BASE = 3;
        public const int QUAD_COEFF_DIV = 512;  // Memory expansion quadratic coefficient divisor

        // KECCAK256 (SHA3)
        public const int KECCAK256_BASE = 30;
        public const int KECCAK256_PER_WORD = 6;

        // LOG operations
        public const int LOG_BASE = 375;
        public const int LOG_PER_TOPIC = 375;
        public const int LOG_PER_BYTE = 8;

        // CREATE operations
        public const int CREATE_BASE = 32000;
        public const int CREATE2_HASH_PER_WORD = 6;  // Same as KECCAK256_PER_WORD
        public const int CREATE_DATA_GAS = 200;      // Per byte of deployed code
        public const int INIT_CODE_WORD_GAS = 2;     // EIP-3860: Per word of initcode

        // CALL operations (Yellow Paper Appendix G)
        public const int G_CALL = 700;                // Pre-Berlin base cost (EIP-2929 replaced with warm/cold access)
        public const int CALL_VALUE_TRANSFER = 9000;
        public const int CALL_NEW_ACCOUNT = 25000;
        public const int CALL_STIPEND = 2300;        // Gas given to called contract with value

        // EIP-150: 63/64 gas retention rule for subcalls
        public const int GAS_DIVISOR = 64;

        // SELFDESTRUCT (EIP-150)
        public const int SELFDESTRUCT_COST = 5000;

        // Maximum call depth (EVM limitation from go-ethereum)
        public const int MAX_CALL_DEPTH = 1024;

        // EIP-170: Maximum contract code size (Spurious Dragon)
        public const int MAX_CODE_SIZE = 24576;

        // EIP-3860: Maximum initcode size (Shanghai)
        public const int MAX_INITCODE_SIZE = 49152;  // 2 * MAX_CODE_SIZE

        // Overflow protection: Return impossibly high gas cost for operations that would overflow
        public const long OVERFLOW_GAS_COST = 1_000_000_000_000_000_000L;  // 10^18

        // Transient storage (EIP-1153: Cancun)
        public const int TLOAD_COST = 100;
        public const int TSTORE_COST = 100;

        // Transaction intrinsic gas
        public const int TX_GAS = 21000;
        public const int TX_GAS_CONTRACT_CREATION = 53000;
        public const int TX_DATA_ZERO_GAS = 4;
        public const int TX_DATA_NON_ZERO_GAS = 16;  // EIP-2028
        public const int TX_ACCESS_LIST_ADDRESS_GAS = 2400;
        public const int TX_ACCESS_LIST_STORAGE_KEY_GAS = 1900;

        // Precompile costs
        public const int ECRECOVER_GAS = 3000;
        public const int SHA256_BASE_GAS = 60;
        public const int SHA256_PER_WORD_GAS = 12;
        public const int RIPEMD160_BASE_GAS = 600;
        public const int RIPEMD160_PER_WORD_GAS = 120;
        public const int IDENTITY_BASE_GAS = 15;
        public const int IDENTITY_PER_WORD_GAS = 3;
    }
}

