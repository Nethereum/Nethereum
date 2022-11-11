using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using System.Numerics;

namespace Nethereum.GSN.DTOs
{
    [Function("maxPossibleCharge", "uint256")]
    public class MaxPossibleChargeFunction : FunctionMessage
    {
        [Parameter("uint256", "relayedCallStipend", 1)]
        public BigInteger RelayedCallStipend { get; set; }

        [Parameter("uint256", "gasPrice", 2)]
        public BigInteger GasPriceParam { get; set; }

        [Parameter("uint256", "transactionFee", 3)]
        public BigInteger TransactionFee { get; set; }
    }
}
