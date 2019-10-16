using System.Numerics;

namespace Nethereum.GSN.Models
{
    public class RelayRequest
    {
        public string EncodedFunction { get; set; }
        public byte[] Signature { get; set; }
        public byte[] ApprovalData { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public BigInteger GasPrice { get; set; }
        public BigInteger GasLimit { get; set; }
        public BigInteger RelayFee { get; set; }
        public BigInteger RecipientNonce { get; set; }
        public BigInteger RelayMaxNonce { get; set; }
        public string RelayHubAddress { get; set; }
        public string UserAgent { get; set; }
    }
}
