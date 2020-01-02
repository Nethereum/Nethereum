using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using System.Numerics;

namespace Nethereum.GSN.DTOs
{
    [Function("acceptRelayedCall", typeof(AcceptRelayedCallOutput))]
    public class AcceptRelayedCallFunction : FunctionMessage
    {
        [Parameter("address", "relay", 1)]
        public string Relay { get; set; }

        [Parameter("address", "from", 2)]
        public string From { get; set; }

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

        [Parameter("bytes", "approvalData", 8)]
        public byte[] ApprovalData { get; set; }

        [Parameter("uint256", "maxPossibleCharge", 9)]
        public BigInteger MaxPossibleCharge { get; set; }
    }

    [FunctionOutput]
    public class AcceptRelayedCallOutput : IFunctionOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public BigInteger IsAllow { get; set; }

        [Parameter("bytes", "", 2)]
        public string Data { get; set; }
    }
}
