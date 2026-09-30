using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

namespace Nethereum.ABI.EIP712.Permit2
{
    [Struct("TokenPermissions")]
    public partial class TokenPermissions  {

        [Parameter("address", "token", 1)]
        public virtual string Token { get; set; }
        [Parameter("uint256", "amount", 2)]
        public virtual BigInteger Amount { get; set; }
    }
}
