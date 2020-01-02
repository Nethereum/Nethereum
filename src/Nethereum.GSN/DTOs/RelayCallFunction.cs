using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using System.Numerics;

namespace Nethereum.GSN.DTOs
{
    [Function("relayCall")]
    public class RelayCallFunction : FunctionMessage
    {
        [Parameter("address", "from", 1)]
        public string From { get; set; }

        [Parameter("address", "to", 2)]
        public string To { get; set; }

        [Parameter("bytes", "encodedFunction", 3)]
        public byte[] EncodedFunction { get; set; }

        [Parameter("uint256", "transactionFee", 4)]
        public BigInteger TransactionFee { get; set; }

        [Parameter("uint256", "gasPrice", 5)]
        public BigInteger GasPriceParam { get; set; }

        [Parameter("uint256", "gasLimit", 6)]
        public BigInteger GasLimit { get; set; }

        [Parameter("uint256", "nonce", 7)]
        public BigInteger NonceParam { get; set; }

        [Parameter("bytes", "signature", 8)]
        public byte[] Signature { get; set; }

        [Parameter("bytes", "approvalData", 9)]
        public byte[] ApprovalData { get; set; }
    }
}
