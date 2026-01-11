using System.Numerics;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage
{
    public class ReceiptInfo
    {
        public Receipt Receipt { get; set; }
        public byte[] TxHash { get; set; }
        public byte[] BlockHash { get; set; }
        public BigInteger BlockNumber { get; set; }
        public int TransactionIndex { get; set; }
        public BigInteger GasUsed { get; set; }
        public string ContractAddress { get; set; }
        public BigInteger EffectiveGasPrice { get; set; }
    }
}
