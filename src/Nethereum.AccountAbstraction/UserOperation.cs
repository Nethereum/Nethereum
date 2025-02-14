using Nethereum.Util;
using System.Numerics;


namespace Nethereum.AccountAbstraction
{
    public class UserOperation
    {
        public string Sender { get; set; } = AddressUtil.ZERO_ADDRESS;
        public BigInteger Nonce { get; set; } = 0;
        public byte[] InitCode { get; set; } = new byte[0];
        public byte[] CallData { get; set; } = new byte[0];
        public BigInteger CallGasLimit { get; set; } = 0;
        public BigInteger VerificationGasLimit { get; set; } = 15000; // default verification gas. will add create2 cost (3200+200*length) if initCode exists
        public BigInteger PreVerificationGas { get; set; } = 21000;
        public BigInteger MaxFeePerGas { get; set; } = 0;
        public BigInteger MaxPriorityFeePerGas { get; set; } = 1000000000;
        public string Paymaster { get; set; } = AddressUtil.ZERO_ADDRESS;
        public byte[] PaymasterData { get; set; }  = new byte[0];
        public byte[] Signature { get; set; } = new byte[0];
        public BigInteger PaymasterVerificationGasLimit { get; set; } = 300000;
        public BigInteger PaymasterPostOpGasLimit { get; set; } = 0;


    }
}
