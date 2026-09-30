using System.Numerics;
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Contracts.Standards.Permit2
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
