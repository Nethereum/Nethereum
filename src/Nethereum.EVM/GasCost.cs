namespace Nethereum.EVM
{
    public class GasCost
    {

        /* backwards compatibility, remove eventually */
        public const int STEP = 1;
        public const int SSTORE = 300;
        /* backwards compatibility, remove eventually */

        public const int ZEROSTEP = 0;
        public const int QUICKSTEP = 2;
        public const int FASTESTSTEP = 3;
        public const int FASTSTEP = 5;
        public const int MIDSTEP = 8;
        public const int SLOWSTEP = 10;
        public const int EXTSTEP = 20;

        public const int GENESISGASLIMIT = 1000000;
        public const int MINGASLIMIT = 125000;

        public const int BALANCE = 20;
        public const int SHA3 = 30;
        public const int SHA3_WORD = 6;
        public const int SLOAD = 50;
        public const int STOP = 0;
        public const int SUICIDE = 0;
        public const int CLEAR_SSTORE = 5000;
        public const int SET_SSTORE = 20000;
        public const int RESET_SSTORE = 5000;
        public const int REFUND_SSTORE = 15000;
        public const int CREATE = 32000;

        public const int JUMPDEST = 1;
        public const int CREATE_DATA_BYTE = 5;
        public const int CALL = 40;
        public const int STIPEND_CALL = 2300;
        public const int VT_CALL = 9000; //value transfer call
        public const int NEW_ACCT_CALL = 25000; //new account call
        public const int MEMORY = 3;
        public const int SUICIDE_REFUND = 24000;
        public const int QUAD_COEFF_DIV = 512;
        public const int CREATE_DATA = 200;
        public const int TX_NO_ZERO_DATA = 68;
        public const int TX_ZERO_DATA = 4;
        public const int TRANSACTION = 21000;
        public const int TRANSACTION_CREATE_CONTRACT = 53000;
        public const int LOG_GAS = 375;
        public const int LOG_DATA_GAS = 8;
        public const int LOG_TOPIC_GAS = 375;
        public const int COPY_GAS = 3;
        public const int EXP_GAS = 10;
        public const int EXP_BYTE_GAS = 10;
        public const int IDENTITY = 15;
        public const int IDENTITY_WORD = 3;
        public const int RIPEMD160 = 600;
        public const int RIPEMD160_WORD = 120;
        public const int SHA256 = 60;
        public const int SHA256_WORD = 12;
        public const int EC_RECOVER = 3000;
    }
}