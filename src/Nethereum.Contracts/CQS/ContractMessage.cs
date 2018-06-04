using System.Numerics;

namespace Nethereum.Contracts.CQS
{
    public class ContractMessage
    {
        public BigInteger AmountToSend { get; set; }
        public BigInteger? Gas { get; set; }
        public BigInteger? GasPrice { get; set; }
        public string FromAddress { get; set; }
        public BigInteger? Nonce { get; set; }
    }
}