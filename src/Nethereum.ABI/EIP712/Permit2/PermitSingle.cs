using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

namespace Nethereum.ABI.EIP712.Permit2
{
    [Struct("PermitSingle")]
    public partial class PermitSingle {

        [Parameter("tuple", "details", 1, "PermitDetails")]
        public PermitDetails Details { get; set; }

        [Parameter("address", "spender", 2)]
        public virtual string Spender { get; set; }
        [Parameter("uint256", "sigDeadline", 3)]
        public virtual BigInteger SigDeadline { get; set; }

    }
}
