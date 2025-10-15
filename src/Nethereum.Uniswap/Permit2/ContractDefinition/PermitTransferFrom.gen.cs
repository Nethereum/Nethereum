using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Uniswap.Core.Permit2.ContractDefinition
{
    public partial class PermitTransferFrom : PermitTransferFromBase { }

    public class PermitTransferFromBase 
    {
        [Parameter("tuple", "permitted", 1)]
        public virtual TokenPermissions Permitted { get; set; }
        [Parameter("uint256", "nonce", 2)]
        public virtual BigInteger Nonce { get; set; }
        [Parameter("uint256", "deadline", 3)]
        public virtual BigInteger Deadline { get; set; }
    }
}
