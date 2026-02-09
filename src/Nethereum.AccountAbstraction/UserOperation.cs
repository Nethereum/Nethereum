using Nethereum.Util;
using System.Numerics;


namespace Nethereum.AccountAbstraction
{
    public class UserOperation
    {
        public static readonly string DEFAULT_SENDER = AddressUtil.ZERO_ADDRESS;
        public static readonly BigInteger? DEFAULT_NONCE = 0;
        public static readonly byte[] DEFAULT_INIT_CODE = Array.Empty<byte>();
        public static readonly byte[] DEFAULT_CALL_DATA = Array.Empty<byte>();
        public static readonly BigInteger DEFAULT_CALL_GAS_LIMIT = 0;
        public static readonly BigInteger? DEFAULT_VERIFICATION_GAS_LIMIT = 150000;
        public static readonly BigInteger DEFAULT_PRE_VERIFICATION_GAS = 21000;
        public static readonly BigInteger DEFAULT_MAX_FEE_PER_GAS = 0;
        public static readonly BigInteger DEFAULT_MAX_PRIORITY_FEE_PER_GAS = 1000000000;
        public static readonly string DEFAULT_PAYMASTER = AddressUtil.ZERO_ADDRESS;
        public static readonly byte[] DEFAULT_PAYMASTER_DATA = Array.Empty<byte>();
        public static readonly byte[] DEFAULT_SIGNATURE = Array.Empty<byte>();
        public static readonly BigInteger DEFAULT_PAYMASTER_VERIFICATION_GAS_LIMIT = 300000; // default verification gas. will add create2 cost (3200+200*length) if initCode exists
        public static readonly BigInteger DEFAULT_PAYMASTER_POST_OP_GAS_LIMIT = 0;

        public string Sender { get; set; }
        public BigInteger? Nonce { get; set; }
        public byte[] InitCode { get; set; } = DEFAULT_INIT_CODE;
        public byte[] CallData { get; set; } = DEFAULT_CALL_DATA;
        public BigInteger? CallGasLimit { get; set; }
        public BigInteger? VerificationGasLimit { get; set; }
        public BigInteger? PreVerificationGas { get; set; }
        public BigInteger? MaxFeePerGas { get; set; }
        public BigInteger? MaxPriorityFeePerGas { get; set; }
        public string Paymaster { get; set; }
        public byte[] PaymasterData { get; set; }
        public byte[] Signature { get; set; }
        public BigInteger? PaymasterVerificationGasLimit { get; set; }
        public BigInteger? PaymasterPostOpGasLimit { get; set; }


        public virtual void SetNullValuesToDefaultValues()
        {
            Sender ??= DEFAULT_SENDER;
            Nonce ??= DEFAULT_NONCE;
            InitCode ??= DEFAULT_INIT_CODE;
            CallData ??= DEFAULT_CALL_DATA;
            CallGasLimit ??= DEFAULT_CALL_GAS_LIMIT;
            VerificationGasLimit ??= DEFAULT_VERIFICATION_GAS_LIMIT;
            PreVerificationGas ??= DEFAULT_PRE_VERIFICATION_GAS;
            MaxFeePerGas ??= DEFAULT_MAX_FEE_PER_GAS;
            MaxPriorityFeePerGas ??= DEFAULT_MAX_PRIORITY_FEE_PER_GAS;
            Paymaster ??= DEFAULT_PAYMASTER;
            PaymasterData ??= DEFAULT_PAYMASTER_DATA;
            Signature ??= DEFAULT_SIGNATURE;
            PaymasterVerificationGasLimit ??= DEFAULT_PAYMASTER_VERIFICATION_GAS_LIMIT;
            PaymasterPostOpGasLimit ??= DEFAULT_PAYMASTER_POST_OP_GAS_LIMIT;
        }

    }
}
