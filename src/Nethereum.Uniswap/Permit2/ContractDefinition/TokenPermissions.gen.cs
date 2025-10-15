using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Uniswap.Core.Permit2.ContractDefinition
{
    public partial class TokenPermissions : TokenPermissionsBase { }

    public class TokenPermissionsBase 
    {
        [Parameter("address", "token", 1)]
        public virtual string Token { get; set; }
        [Parameter("uint256", "amount", 2)]
        public virtual BigInteger Amount { get; set; }
    }
}
