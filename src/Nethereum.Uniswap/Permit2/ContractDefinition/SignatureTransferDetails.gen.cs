using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Uniswap.Permit2.ContractDefinition
{
    public partial class SignatureTransferDetails : SignatureTransferDetailsBase { }

    public class SignatureTransferDetailsBase 
    {
        [Parameter("address", "to", 1)]
        public virtual string To { get; set; }
        [Parameter("uint256", "requestedAmount", 2)]
        public virtual BigInteger RequestedAmount { get; set; }
    }
}
