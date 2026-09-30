namespace Nethereum.EVM.Gas
{
    public static class GasConstants
    {
        public const int COLD_SLOAD_COST = 2100;
        public const int COLD_ACCOUNT_ACCESS_COST = 2600;
        public const int WARM_STORAGE_READ_COST = 100;

        public const int SSTORE_SET = 20000;
        public const int SSTORE_RESET = 2900;
        public const int SSTORE_RESET_PRE_BERLIN = 5000;
        public const int SSTORE_NOOP = 100;

        public const int SSTORE_CLEARS_SCHEDULE = 4800;
        public const int REFUND_QUOTIENT = 5;

        public const int SSTORE_SET_REFUND = SSTORE_SET - SSTORE_NOOP;
        public const int SSTORE_RESET_REFUND = SSTORE_RESET - SSTORE_NOOP;

        // EIP-2200: "If gasleft is less than or equal to gas stipend, fail the current
        // call frame with 'out of gas' exception." The same quantity as the EIP-150 call
        public const int SSTORE_GAS_STIPEND = CALL_STIPEND;

        public const int G_ZERO = 0;
        public const int G_JUMPDEST = 1;
        public const int G_BASE = 2;
        public const int G_VERYLOW = 3;
        public const int G_LOW = 5;
        public const int G_MID = 8;
        public const int G_HIGH = 10;
        public const int G_BLOCKHASH = 20;

        public const int EXP_BASE = 10;
        public const int EXP_BYTE = 50;

        public const int COPY_BASE = 3;
        public const int COPY_PER_WORD = 3;
        public const int MEMORY_BASE = 3;
        public const int QUAD_COEFF_DIV = 512;

        public const int KECCAK256_BASE = 30;
        public const int KECCAK256_PER_WORD = 6;

        public const int LOG_BASE = 375;
        public const int LOG_PER_TOPIC = 375;
        public const int LOG_PER_BYTE = 8;

        public const int CREATE_BASE = 32000;
        public const int CREATE2_HASH_PER_WORD = 6;
        public const int CREATE_DATA_GAS = 200;
        public const int INIT_CODE_WORD_GAS = 2;

        public const int G_CALL = 700;
        public const int CALL_VALUE_TRANSFER = 9000;
        public const int CALL_NEW_ACCOUNT = 25000;
        public const int CALL_STIPEND = 2300;

        public const int GAS_DIVISOR = 64;

        public const int SELFDESTRUCT_COST = 5000;

        public const int MAX_CALL_DEPTH = 1024;

        public const int MAX_CODE_SIZE = 24576;

        public const int MAX_INITCODE_SIZE = 49152;

        public const int EIP7954_MAX_CODE_SIZE = 65536;

        public const int EIP7954_MAX_INITCODE_SIZE = 2 * EIP7954_MAX_CODE_SIZE;

        public const long EIP7976_FLOOR_PER_TOKEN_GAS = 16;

        public const long EIP7976_FLOOR_TOKENS_PER_BYTE = 4;

        public const long OVERFLOW_GAS_COST = 1_000_000_000_000_000_000L;

        public const int TLOAD_COST = 100;
        public const int TSTORE_COST = 100;

        public const int TX_GAS = 21000;
        public const int TX_GAS_CONTRACT_CREATION = 53000;
        public const int TX_DATA_ZERO_GAS = 4;
        public const int TX_DATA_NON_ZERO_GAS = 16;
        public const int TX_ACCESS_LIST_ADDRESS_GAS = 2400;
        public const int TX_ACCESS_LIST_STORAGE_KEY_GAS = 1900;

        public const int TX_FLOOR_PER_TOKEN = 10;
        public const int TX_TOKENS_PER_NON_ZERO_BYTE = 4;

        public const int ECRECOVER_GAS = 3000;
        public const int SHA256_BASE_GAS = 60;
        public const int SHA256_PER_WORD_GAS = 12;
        public const int RIPEMD160_BASE_GAS = 600;
        public const int RIPEMD160_PER_WORD_GAS = 120;
        public const int IDENTITY_BASE_GAS = 15;
        public const int IDENTITY_PER_WORD_GAS = 3;
        public const int KZG_POINT_EVALUATION_GAS = 50000;

        public const int BLS12_G1ADD_GAS = 375;
        public const int BLS12_G1MSM_BASE_GAS = 12000;
        public const int BLS12_G2ADD_GAS = 600;
        public const int BLS12_G2MSM_BASE_GAS = 22500;
        public const int BLS12_PAIRING_BASE_GAS = 37700;
        public const int BLS12_PAIRING_PER_PAIR_GAS = 32600;
        public const int BLS12_MAP_FP_TO_G1_GAS = 5500;
        public const int BLS12_MAP_FP2_TO_G2_GAS = 23800;

        public const int P256VERIFY_GAS = 6900;

        public const long EIP2780_TX_BASE_COST = 12000;
        public const long EIP2780_TX_VALUE_COST = 6000;

        public const long EIP8038_COLD_ACCOUNT_ACCESS = 3000;
        public const long EIP8038_ACCOUNT_WRITE = 9000;
        public const long EIP8038_STORAGE_WRITE = 10000;
        public const long EIP8038_CALL_VALUE_TRANSFER = EIP8038_ACCOUNT_WRITE + CALL_STIPEND;
        public const long EIP8038_CREATE_ACCESS = EIP8038_ACCOUNT_WRITE + EIP8038_COLD_ACCOUNT_ACCESS;
        public const long EIP8038_REFUND_STORAGE_CLEAR = (EIP8038_STORAGE_WRITE + COLD_SLOAD_COST) * 4800 / 5000;

        public const long EIP8037_COST_PER_STATE_BYTE = 1530;
        public const long EIP8037_LARGEST_CODE_DEPOSIT_ANY_FORK_PERMITS =
            (long)EIP7954_MAX_CODE_SIZE * EIP8037_COST_PER_STATE_BYTE;

        public const long BLOCK_EXECUTION_GAS_HEADROOM = 30_000_000;

        public static long BlockGasLimitLargeEnoughToDeployAt(bool stateGasActive) =>
            stateGasActive
                ? BLOCK_EXECUTION_GAS_HEADROOM + EIP8037_LARGEST_CODE_DEPOSIT_ANY_FORK_PERMITS
                : BLOCK_EXECUTION_GAS_HEADROOM;

        public const long EIP8037_STORAGE_SET_STATE_GAS = 64 * EIP8037_COST_PER_STATE_BYTE;
        public const long EIP8037_NEW_ACCOUNT_STATE_GAS = 120 * EIP8037_COST_PER_STATE_BYTE;
        public const long EIP8037_AUTH_BASE_STATE_GAS = 23 * EIP8037_COST_PER_STATE_BYTE;

        public const long EIP8037_TX_MAX_GAS_LIMIT = 16_777_216;

        public const long EIP8037_SYSTEM_MAX_SSTORES_PER_CALL = 16;

        public const long SYSTEM_CALL_EXECUTION_GAS = 30_000_000;

        public const long EIP7928_ITEM_COST = 2000;

        public const long EIP8037_CODE_HASH_PER_WORD = KECCAK256_PER_WORD;
    }
}

