using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Uniswap.Core.Permit2.ContractDefinition
{
    public partial class PermitSingle : PermitSingleBase { }

    public class PermitSingleBase 
    {
        [Parameter("tuple", "details", 1)]
        public virtual PermitDetails Details { get; set; }
        [Parameter("address", "spender", 2)]
        public virtual string Spender { get; set; }
        [Parameter("uint256", "sigDeadline", 3)]
        public virtual BigInteger SigDeadline { get; set; }
    }
}
